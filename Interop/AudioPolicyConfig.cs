using System;
using System.Runtime.InteropServices;

namespace MKei
{
    /// <summary>
    /// Sets a specific process's *persisted default audio endpoint* - the exact
    /// mechanism behind Windows' own Settings &gt; Volume Mixer per-app "Output
    /// device" / "Input device" dropdowns. This actually moves where the app
    /// renders, permanently, until changed again - no capture, no muting, no
    /// remixing needed.
    ///
    /// This is an undocumented WinRT interface
    /// ("Windows.Media.Internal.AudioPolicyConfig"), not the classic reverse-
    /// engineered IPolicyConfig COM interface (which is a different, unrelated
    /// interface that only supports *system-wide* default device changes and
    /// does not have this method at all - that mismatch is what crashed the
    /// original version of this file).
    ///
    /// The exact vtable below is sourced from the SoundSwitch project's commit
    /// history (https://github.com/Belphemur/SoundSwitch), specifically the
    /// commits that added Windows 11 support and then had to fix it after it
    /// shipped broken (b2e1de5, 40f5ba1, ed6b92a, 7324b15) - i.e. this is a
    /// battle-tested definition, not a first attempt. Check that project's
    /// license before redistributing this file commercially.
    ///
    /// LIMITATION: only the Windows 11 (build 21390+) vtable is implemented.
    /// Per-app device routing in Windows' own UI is itself a Windows 11 22H2+
    /// feature, so this should cover you, but on older Windows 10 builds
    /// IsSupported will be false and SetAppDevice will throw
    /// PlatformNotSupportedException rather than silently do the wrong thing
    /// (Windows 10 uses a different, shorter vtable not included here).
    /// </summary>
    public static class AudioPolicyConfig
    {
        private const int Windows11Build = 21390;

        private static readonly Guid IID_IAudioPolicyConfigFactoryWin11 =
            new Guid("ab3d4648-e242-459f-b02f-541c70306324");

        public static bool IsSupported => Environment.OSVersion.Version.Build >= Windows11Build;

        private const string DEVINTERFACE_AUDIO_RENDER = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        private const string DEVINTERFACE_AUDIO_CAPTURE = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";
        private const string MMDEVAPI_TOKEN = @"\\?\SWD#MMDEVAPI#";

        /// <summary>
        /// The WinRT API doesn't accept the plain MMDevice.ID string (e.g.
        /// "{0.0.0.00000000}.{guid}") - it needs the full device interface
        /// path, the same format Windows' own device stack uses internally.
        /// Passing the raw ID is exactly what produces E_INVALIDARG (0x80070057).
        /// </summary>
        private static string WrapDeviceId(string rawDeviceId, EDataFlow flow) =>
            $"{MMDEVAPI_TOKEN}{rawDeviceId}{(flow == EDataFlow.eRender ? DEVINTERFACE_AUDIO_RENDER : DEVINTERFACE_AUDIO_CAPTURE)}";

        private static string UnwrapDeviceId(string wrappedDeviceId)
        {
            string id = wrappedDeviceId;
            if (id.StartsWith(MMDEVAPI_TOKEN)) id = id.Substring(MMDEVAPI_TOKEN.Length);
            if (id.EndsWith(DEVINTERFACE_AUDIO_RENDER)) id = id.Remove(id.Length - DEVINTERFACE_AUDIO_RENDER.Length);
            else if (id.EndsWith(DEVINTERFACE_AUDIO_CAPTURE)) id = id.Remove(id.Length - DEVINTERFACE_AUDIO_CAPTURE.Length);
            return id;
        }

        /// <summary>
        /// Clears this process's persisted device override so it goes back to
        /// dynamically following whatever the system default is - matching
        /// "Default" in Windows' own per-app Volume Mixer dropdown.
        ///
        /// Passing a null HSTRING (not merely an empty string - an actual null
        /// pointer) is the documented WinRT convention for "reset to default"
        /// in this API family (see AcousticEchoCancellationConfiguration.
        /// SetEchoCancellationRenderEndpoint: "Passing in a null string...
        /// results in the loopback endpoint being reset to its default value").
        /// Explicitly setting the device to whatever today's default happens to
        /// be (the previous approach) pins it there permanently instead - it
        /// stops following future default-device changes, which is the bug
        /// you hit.
        /// </summary>
        public static void ClearAppDevice(uint processId)
        {
            RequireSupported();

            var factory = GetFactory();
            try
            {
                Marshal.ThrowExceptionForHR(
                    factory.SetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, ERole.eConsole, IntPtr.Zero));
                Marshal.ThrowExceptionForHR(
                    factory.SetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, ERole.eMultimedia, IntPtr.Zero));
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }

        /// <summary>Redirects <paramref name="processId"/>'s audio output to <paramref name="deviceId"/> (an MMDevice.ID string), permanently, until changed again.</summary>
        public static void SetAppDevice(uint processId, string deviceId)
        {
            RequireSupported();

            string wrappedDeviceId = WrapDeviceId(deviceId, EDataFlow.eRender);

            var factory = GetFactory();
            try
            {
                IntPtr deviceIdHString = IntPtr.Zero;
                try
                {
                    Marshal.ThrowExceptionForHR(WindowsCreateString(wrappedDeviceId, wrappedDeviceId.Length, out deviceIdHString));

                    // Set both roles Windows might query for this process.
                    Marshal.ThrowExceptionForHR(
                        factory.SetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, ERole.eConsole, deviceIdHString));
                    Marshal.ThrowExceptionForHR(
                        factory.SetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, ERole.eMultimedia, deviceIdHString));
                }
                finally
                {
                    if (deviceIdHString != IntPtr.Zero) WindowsDeleteString(deviceIdHString);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }

        /// <summary>Reads back the process's currently persisted device for the given role, or null if none is set / not supported.</summary>
        public static string? GetAppDevice(uint processId, ERole role = ERole.eMultimedia)
        {
            if (!IsSupported) return null;

            var factory = GetFactory();
            try
            {
                int hr = factory.GetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, role, out IntPtr deviceIdHString);
                if (hr != 0 || deviceIdHString == IntPtr.Zero)
                    return null;

                try
                {
                    IntPtr rawBuffer = WindowsGetStringRawBuffer(deviceIdHString, out uint length);
                    string? wrapped = rawBuffer == IntPtr.Zero ? null : Marshal.PtrToStringUni(rawBuffer, (int)length);
                    return wrapped == null ? null : UnwrapDeviceId(wrapped);
                }
                finally
                {
                    WindowsDeleteString(deviceIdHString);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }

        private static void RequireSupported()
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException(
                    "Per-app default device redirection needs Windows 11 (build 21390+); " +
                    $"this machine is on build {Environment.OSVersion.Version.Build}.");
        }

        private static IAudioPolicyConfigFactoryWin11 GetFactory()
        {
            const string runtimeClassName = "Windows.Media.Internal.AudioPolicyConfig";
            IntPtr classIdHString = IntPtr.Zero;
            try
            {
                Marshal.ThrowExceptionForHR(
                    WindowsCreateString(runtimeClassName, runtimeClassName.Length, out classIdHString));

                Marshal.ThrowExceptionForHR(
                    RoGetActivationFactory(classIdHString, IID_IAudioPolicyConfigFactoryWin11, out IntPtr factoryPtr));

                return (IAudioPolicyConfigFactoryWin11)
                    Marshal.GetTypedObjectForIUnknown(factoryPtr, typeof(IAudioPolicyConfigFactoryWin11));
            }
            finally
            {
                if (classIdHString != IntPtr.Zero) WindowsDeleteString(classIdHString);
            }
        }

        // ---------------- P/Invoke + COM plumbing ----------------

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId, [MarshalAs(UnmanagedType.LPStruct)] Guid iid, out IntPtr factory);

        public enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
        public enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        // Windows 11 (build 21390+) vtable for the
        // "Windows.Media.Internal.AudioPolicyConfig" WinRT factory. Windows 11
        // added a batch of volume-group/chat-application methods ahead of the
        // three we actually use; all of them must be declared (even though
        // unused) purely to keep the vtable slot positions correct - this is
        // exactly the class of bug that crashed the original version of this
        // file, so every slot here is taken directly from SoundSwitch's source
        // rather than guessed.
        [ComImport]
        [Guid("ab3d4648-e242-459f-b02f-541c70306324")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioPolicyConfigFactoryWin11
        {
            void GetIids(out int iidCount, out IntPtr iids);
            void GetRuntimeClassName(out IntPtr className);
            void GetTrustLevel(out int trustLevel);

            int __incomplete__add_CtxVolumeChange();
            int __incomplete__remove_CtxVolumeChanged();
            int __incomplete__add_RingerVibrateStateChanged();
            int __incomplete__remove_RingerVibrateStateChange();
            int __incomplete__SetVolumeGroupGainForId();
            int __incomplete__GetVolumeGroupGainForId();
            int __incomplete__GetActiveVolumeGroupForEndpointId();
            int __incomplete__GetVolumeGroupsForEndpoint();
            int __incomplete__GetCurrentVolumeContext();
            int __incomplete__SetVolumeGroupMuteForId();
            int __incomplete__GetVolumeGroupMuteForId();
            int __incomplete__SetRingerVibrateState();
            int __incomplete__GetRingerVibrateState();
            int __incomplete__SetPreferredChatApplication();
            int __incomplete__ResetPreferredChatApplication();
            int __incomplete__GetPreferredChatApplication();
            int __incomplete__GetCurrentChatApplications();
            int __incomplete__add_ChatContextChanged();
            int __incomplete__remove_ChatContextChanged();

            [PreserveSig]
            int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, IntPtr deviceId);

            [PreserveSig]
            int GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, out IntPtr deviceId);

            [PreserveSig]
            int ClearAllPersistedApplicationDefaultEndpoints();
        }
    }
}
