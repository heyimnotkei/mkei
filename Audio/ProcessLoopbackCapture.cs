using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MKei
{
    /// <summary>
    /// Captures just the render (output) audio belonging to a single process,
    /// using the documented Windows "process loopback" capture API:
    /// ActivateAudioInterfaceAsync + AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK.
    ///
    /// This is a fully documented, stable Win32 API (unlike the reverse-engineered
    /// per-app "persisted default endpoint" API) available since Windows 10 build
    /// 20348 (2004/20H2) and unchanged since. See:
    /// https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-activateaudiointerfaceasync
    /// https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/
    ///
    /// NOTE: this targets x64 (PropVariantBlob layout below assumes 8-byte
    /// pointers). If you build AnyCPU/x86, the PROPVARIANT layout needs
    /// adjusting (16 bytes total instead of 24, no 4-byte pad before the
    /// pointer field).
    /// </summary>
    public sealed class ProcessLoopbackCapture : IDisposable
    {
        /// <summary>Raised on a background thread with raw 16-bit mono PCM samples.</summary>
        public event Action<byte[], int>? DataAvailable;

        /// <summary>
        /// Raised on a background thread if activation or capture fails. Start()
        /// returns before activation completes (it's inherently async), so this
        /// is the only way to find out a route didn't actually come up.
        /// </summary>
        public event Action<Exception>? Failed;

        /// <summary>Raised roughly once a second: (totalFramesCaptured, totalFramesWithNonZeroAudio).</summary>
        public event Action<long, long>? Diagnostics;

        /// <summary>Raised once activation succeeds and capture is actually running.</summary>
        public event Action? Started;

        // The process-loopback audio client does not support GetMixFormat /
        // IsFormatSupported (returns E_NOTIMPL) - the format must be supplied
        // up front. This is documented behavior, not a bug in this code:
        // https://learn.microsoft.com/en-us/answers/questions/1125409/
        private const int SampleRate = 44100;
        private const short BitsPerSample = 16;
        private const short Channels = 2; // capture is always stereo; we downmix to mono ourselves

        private const string VirtualAudioDeviceProcessLoopback = @"VAD\Process_Loopback";
        private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const uint AUDCLNT_SHAREMODE_SHARED = 0;
        private const uint AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK = 1;
        private const uint PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE = 0;
        private const ushort VT_BLOB = 65;

        private static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

        private Thread? _captureThread;
        private volatile bool _stopRequested;
        private IAudioClient? _audioClient;
        private IAudioCaptureClient? _captureClient;
        private IntPtr _formatPtr = IntPtr.Zero;

        public uint ProcessId { get; }

        public ProcessLoopbackCapture(uint processId)
        {
            ProcessId = processId;
        }

        public void Start()
        {
            if (_captureThread != null)
                throw new InvalidOperationException("Already started.");

            // Activation + the read loop both happen on a dedicated MTA thread:
            // ActivateAudioInterfaceAsync's callback arrives on a worker thread
            // in the COM multi-threaded apartment, so we host everything there
            // rather than fight the WinForms STA thread for it.
            _stopRequested = false;
            _captureThread = new Thread(CaptureThreadProc) { IsBackground = true };
            _captureThread.SetApartmentState(ApartmentState.MTA);
            _captureThread.Start();
        }

        public void Stop()
        {
            _stopRequested = true;
            _captureThread?.Join(2000);
            _captureThread = null;
        }

        private void CaptureThreadProc()
        {
            try
            {
                ActivateAndCapture();
            }
            catch (Exception ex)
            {
                Failed?.Invoke(ex);
            }
            finally
            {
                if (_captureClient != null) Marshal.ReleaseComObject(_captureClient);
                if (_audioClient != null) Marshal.ReleaseComObject(_audioClient);
                if (_formatPtr != IntPtr.Zero) Marshal.FreeHGlobal(_formatPtr);
                _captureClient = null;
                _audioClient = null;
                _formatPtr = IntPtr.Zero;
            }
        }

        private void ActivateAndCapture()
        {
            // --- Build AUDIOCLIENT_ACTIVATION_PARAMS wrapped in a PROPVARIANT (VT_BLOB) ---
            var activationParams = new AUDIOCLIENT_ACTIVATION_PARAMS
            {
                ActivationType = AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
                ProcessLoopbackParams = new AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
                {
                    TargetProcessId = ProcessId,
                    ProcessLoopbackMode = PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE
                }
            };

            IntPtr paramsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>());
            IntPtr propVariantPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariantBlob>());
            try
            {
                Marshal.StructureToPtr(activationParams, paramsPtr, false);

                var propVariant = new PropVariantBlob
                {
                    vt = VT_BLOB,
                    cbSize = (uint)Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>(),
                    pBlobData = paramsPtr
                };
                Marshal.StructureToPtr(propVariant, propVariantPtr, false);

                using var completion = new ActivationCompletionHandler();

                int hr = ActivateAudioInterfaceAsync(
                    VirtualAudioDeviceProcessLoopback,
                    IID_IAudioClient,
                    propVariantPtr,
                    completion,
                    out IActivateAudioInterfaceAsyncOperation asyncOp);
                Marshal.ThrowExceptionForHR(hr);

                object activatedInterface = completion.WaitForResult();
                _audioClient = (IAudioClient)activatedInterface;
                Marshal.ReleaseComObject(asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(paramsPtr);
                Marshal.FreeHGlobal(propVariantPtr);
            }

            // --- Build a plain WAVEFORMATEX (PCM) describing the hard-required format ---
            _formatPtr = Marshal.AllocHGlobal(18); // sizeof(WAVEFORMATEX)
            short blockAlign = (short)(Channels * (BitsPerSample / 8));
            Marshal.WriteInt16(_formatPtr, 0, 1); // WAVE_FORMAT_PCM
            Marshal.WriteInt16(_formatPtr, 2, Channels);
            Marshal.WriteInt32(_formatPtr, 4, SampleRate);
            Marshal.WriteInt32(_formatPtr, 8, SampleRate * blockAlign);
            Marshal.WriteInt16(_formatPtr, 12, blockAlign);
            Marshal.WriteInt16(_formatPtr, 14, BitsPerSample);
            Marshal.WriteInt16(_formatPtr, 16, 0); // cbSize

            _audioClient!.Initialize(
                AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK,
                0, 0,
                _formatPtr,
                IntPtr.Zero);

            object captureService;
            _audioClient.GetService(IID_IAudioCaptureClient, out captureService);
            _captureClient = (IAudioCaptureClient)captureService;

            _audioClient.Start();
            Started?.Invoke();

            int bytesPerFrame = Channels * (BitsPerSample / 8);

            while (!_stopRequested)
            {
                _captureClient.GetNextPacketSize(out uint packetFrames);
                if (packetFrames == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                _captureClient.GetBuffer(out IntPtr dataBuffer, out uint framesAvailable, out uint flags, out _, out _);

                bool silent = (flags & 0x1) != 0; // AUDCLNT_BUFFERFLAGS_SILENT
                var stereoBytes = new byte[framesAvailable * bytesPerFrame];
                if (!silent && dataBuffer != IntPtr.Zero)
                    Marshal.Copy(dataBuffer, stereoBytes, 0, stereoBytes.Length);
                // else leave as zeroed silence

                _captureClient.ReleaseBuffer(framesAvailable);

                _totalFramesCaptured += framesAvailable;
                if (!silent && ContainsNonZero(stereoBytes)) _totalAudibleFrames += framesAvailable;
                if ((DateTime.UtcNow - _lastDiagnosticReport) > TimeSpan.FromSeconds(1))
                {
                    _lastDiagnosticReport = DateTime.UtcNow;
                    Diagnostics?.Invoke(_totalFramesCaptured, _totalAudibleFrames);
                }

                byte[] mono = DownmixStereoToMono16(stereoBytes);
                DataAvailable?.Invoke(mono, mono.Length);
            }

            _audioClient.Stop();
        }

        private long _totalFramesCaptured;
        private long _totalAudibleFrames;
        private DateTime _lastDiagnosticReport = DateTime.MinValue;

        private static bool ContainsNonZero(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                if (data[i] != 0) return true;
            return false;
        }

        private static byte[] DownmixStereoToMono16(byte[] stereo16)
        {
            int frameCount = stereo16.Length / 4; // 2 ch * 2 bytes
            var mono = new byte[frameCount * 2];
            for (int i = 0; i < frameCount; i++)
            {
                short left = BitConverter.ToInt16(stereo16, i * 4);
                short right = BitConverter.ToInt16(stereo16, i * 4 + 2);
                short avg = (short)(((int)left + right) / 2);
                byte[] b = BitConverter.GetBytes(avg);
                mono[i * 2] = b[0];
                mono[i * 2 + 1] = b[1];
            }
            return mono;
        }

        public void Dispose() => Stop();

        // ---------------- P/Invoke + COM plumbing ----------------

        [DllImport("Mmdevapi.dll", PreserveSig = true)]
        private static extern int ActivateAudioInterfaceAsync(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            IntPtr activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);

        [ComImport]
        [Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceCompletionHandler
        {
            void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
        }

        [ComImport]
        [Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceAsyncOperation
        {
            void GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
        }

        // Vtable order for IAudioClient is fixed since Windows Vista and is
        // used identically across every WASAPI sample; unlike IPolicyConfig
        // this one is fully documented and stable:
        // https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nn-audioclient-iaudioclient
        [ComImport]
        [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            void Initialize(uint shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr pFormat, IntPtr audioSessionGuid);
            void GetBufferSize(out uint bufferFrameCount);
            void GetStreamLatency(out long latency);
            void GetCurrentPadding(out uint currentPadding);
            void IsFormatSupported(uint shareMode, IntPtr pFormat, out IntPtr closestMatch);
            void GetMixFormat(out IntPtr deviceFormat);
            void GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);
            void Start();
            void Stop();
            void Reset();
            void SetEventHandle(IntPtr eventHandle);
            void GetService([MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport]
        [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            void GetBuffer(out IntPtr dataBuffer, out uint numFramesToRead, out uint flags, out ulong devicePosition, out ulong qpcPosition);
            void ReleaseBuffer(uint numFramesRead);
            void GetNextPacketSize(out uint numFramesInNextPacket);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
        {
            public uint TargetProcessId;
            public uint ProcessLoopbackMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AUDIOCLIENT_ACTIVATION_PARAMS
        {
            public uint ActivationType;
            public AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS ProcessLoopbackParams;
        }

        // PROPVARIANT restricted to the VT_BLOB case, x64 layout only
        // (8-byte header + 4-byte cbSize + 4-byte pad + 8-byte pointer = 24 bytes).
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariantBlob
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public uint cbSize;
            [FieldOffset(16)] public IntPtr pBlobData;
        }

        /// <summary>
        /// Implements IActivateAudioInterfaceCompletionHandler and blocks the
        /// calling thread until Windows invokes the callback.
        /// </summary>
        [ComVisible(true)]
        private sealed class ActivationCompletionHandler : IActivateAudioInterfaceCompletionHandler, IDisposable
        {
            private readonly ManualResetEventSlim _completed = new(false);
            private object? _result;
            private Exception? _error;

            public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
            {
                try
                {
                    activateOperation.GetActivateResult(out int hr, out object activatedInterface);
                    if (hr != 0)
                        _error = Marshal.GetExceptionForHR(hr);
                    else
                        _result = activatedInterface;
                }
                catch (Exception ex)
                {
                    _error = ex;
                }
                finally
                {
                    _completed.Set();
                }
            }

            public object WaitForResult()
            {
                if (!_completed.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out waiting for process loopback activation.");
                if (_error != null)
                    throw _error;
                return _result!;
            }

            public void Dispose() => _completed.Dispose();
        }
    }
}
