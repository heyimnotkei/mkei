using System;
using System.Linq;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Drawing;
using Microsoft.Win32;

namespace MKei
{
    public partial class MainForm : Form
    {
        private WasapiCapture? _capture;
        private WasapiOut? _playback;
        private BufferedWaveProvider? _buffer;
        private WaveFormat? _fmt;
        private MMDevice? _inDev;
        private MMDevice? _outDev;
        private MMDeviceEnumerator _devEnum = new MMDeviceEnumerator();

        /// <summary>
        /// True while the capture-from-CABLE + mono-downmix + playback loop is
        /// actually running. Redirecting an app's audio to CABLE only moves it
        /// there - this is what reads it back out to real speakers, so
        /// AppRouterForm checks this to warn testers when they've turned
        /// routing on without this running (audio silently going nowhere
        /// audible to them locally).
        /// </summary>
        public static bool IsCaptureRunning { get; private set; }

        private bool _forceMono = true;
        private float _volume = 1.0f; // volume 0.0 - 1.0
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;
        private bool _actuallyClosing;
        private bool _minimizeToTray = true;
        private ContextMenuStrip? _settingsMenu;
        private const string SettingsKey = @"Software\MKei";

        public MainForm()
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            _minimizeToTray = LoadMinimizeToTrayPreference();
            WindowChrome.Apply(this, Theme.Window, Theme.Ink, Theme.CardBorder);
            LoadDevices();
            chkMono.Checked = true;
            statusPill.SetState("Stopped", Theme.InkFaint);
            InitializeTrayIcon();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.Apply(this, Theme.Window, Theme.Ink, Theme.CardBorder);
        }

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();

            _trayMenu.Items.Add("Open", null, (s, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
                Activate();
            });

            var refreshRouting = new ToolStripMenuItem("Refresh", null, (s, e) => AppRouterForm.RefreshRouting());
            _trayMenu.Items.Add(refreshRouting);
            _trayMenu.Opening += (s, e) => refreshRouting.Enabled = AppRouterForm.CanRefreshRouting;

            _trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                _actuallyClosing = true;
                _trayIcon!.Visible = false;
                Close();
            });

            _trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "MKei",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };

            _trayIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
                Activate();
            };
        }

        private static bool LoadMinimizeToTrayPreference()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                return key?.GetValue("MinimizeToTray") is not int value || value != 0;
            }
            catch { return true; }
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            _settingsMenu ??= CreateSettingsMenu();
            _settingsMenu.Show(btnSettings, new Point(0, btnSettings.Height));
        }

        private ContextMenuStrip CreateSettingsMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripLabel("When the window is closed"));
            var minimize = new ToolStripMenuItem("Minimize to system tray");
            var close = new ToolStripMenuItem("Fully close MKei");
            void UpdateChecks()
            {
                minimize.Checked = _minimizeToTray;
                close.Checked = !_minimizeToTray;
            }
            minimize.Click += (_, _) => SetMinimizeToTray(true, minimize, close);
            close.Click += (_, _) => SetMinimizeToTray(false, minimize, close);
            menu.Items.Add(minimize);
            menu.Items.Add(close);
            UpdateChecks();
            menu.Opening += (_, _) => UpdateChecks();
            return menu;
        }

        private void SetMinimizeToTray(bool value, ToolStripMenuItem minimize, ToolStripMenuItem close)
        {
            _minimizeToTray = value;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
                key?.SetValue("MinimizeToTray", value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch { }
            minimize.Checked = value;
            close.Checked = !value;
        }
        private void LoadDevices()
        {
            cmbInput.Items.Clear();
            foreach (var dev in _devEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                cmbInput.Items.Add(new DevItem(dev));

            var cable = _devEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .FirstOrDefault(d => d.FriendlyName.IndexOf("CABLE Output", StringComparison.OrdinalIgnoreCase) >= 0);

            if (cable != null)
                cmbInput.SelectedItem = cmbInput.Items.Cast<DevItem>().First(x => x.Device.ID == cable.ID);
            else if (cmbInput.Items.Count > 0)
                cmbInput.SelectedIndex = 0;

            cmbOutput.Items.Clear();
            foreach (var dev in _devEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                cmbOutput.Items.Add(new DevItem(dev));

            var defOut = _devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (defOut != null)
                cmbOutput.SelectedItem = cmbOutput.Items.Cast<DevItem>().FirstOrDefault(x => x.Device.ID == defOut.ID)
                                         ?? (cmbOutput.Items.Count > 0 ? cmbOutput.Items[0] : null);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_capture != null) return;

            _inDev = (cmbInput.SelectedItem as DevItem)?.Device;
            _outDev = (cmbOutput.SelectedItem as DevItem)?.Device;

            if (_inDev == null || _outDev == null)
            {
                MessageBox.Show("Select valid input and output devices.");
                return;
            }

            _capture = new WasapiCapture(_inDev);
            _fmt = _capture.WaveFormat;
            _buffer = new BufferedWaveProvider(_fmt) { DiscardOnBufferOverflow = true };

            _capture.DataAvailable += (s, a) =>
            {
                if (_buffer == null || _fmt == null) return;

                if (_forceMono && _fmt.Channels >= 2)
                {
                    if (_fmt.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        int frameCount = a.BytesRecorded / _fmt.BlockAlign;
                        float[] input = new float[a.BytesRecorded / 4];
                        Buffer.BlockCopy(a.Buffer, 0, input, 0, a.BytesRecorded);

                        float[] output = new float[frameCount * 2];

                        int index = 0;
                        for (int i = 0; i < frameCount; i++)
                        {
                            float l = input[index++];
                            float r = (_fmt.Channels > 1) ? input[index++] : l;

                            float m = 0.5f * (l + r) * _volume;

                            output[i * 2] = m;
                            output[i * 2 + 1] = m;

                            for (int c = 2; c < _fmt.Channels; c++) index++;
                        }

                        byte[] outBytes = new byte[output.Length * 4];
                        Buffer.BlockCopy(output, 0, outBytes, 0, outBytes.Length);
                        _buffer.AddSamples(outBytes, 0, outBytes.Length);
                    }
                    else if (_fmt.BitsPerSample == 16)
                    {
                        int frames = a.BytesRecorded / _fmt.BlockAlign;
                        byte[] outBytes = new byte[frames * 4];

                        int inOffset = 0;
                        int outOffset = 0;

                        for (int i = 0; i < frames; i++)
                        {
                            short l = BitConverter.ToInt16(a.Buffer, inOffset);
                            short r = (_fmt.Channels > 1) ? BitConverter.ToInt16(a.Buffer, inOffset + 2) : l;

                            int m = (int)(((l + r) / 2f) * _volume);
                            short ms = (short)m;

                            outBytes[outOffset] = (byte)(ms & 0xFF);
                            outBytes[outOffset + 1] = (byte)((ms >> 8) & 0xFF);
                            outBytes[outOffset + 2] = (byte)(ms & 0xFF);
                            outBytes[outOffset + 3] = (byte)((ms >> 8) & 0xFF);

                            inOffset += _fmt.BlockAlign;
                            outOffset += 4;
                        }

                        _buffer.AddSamples(outBytes, 0, outBytes.Length);
                    }
                }
                else
                {
                    if (_fmt.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        float[] samples = new float[a.BytesRecorded / 4];
                        Buffer.BlockCopy(a.Buffer, 0, samples, 0, a.BytesRecorded);

                        for (int i = 0; i < samples.Length; i++)
                            samples[i] *= _volume;

                        byte[] outBytes = new byte[a.BytesRecorded];
                        Buffer.BlockCopy(samples, 0, outBytes, 0, outBytes.Length);
                        _buffer.AddSamples(outBytes, 0, outBytes.Length);
                    }
                    else if (_fmt.BitsPerSample == 16)
                    {
                        byte[] outBytes = new byte[a.BytesRecorded];

                        for (int i = 0; i < a.BytesRecorded; i += 2)
                        {
                            short sample = BitConverter.ToInt16(a.Buffer, i);
                            sample = (short)(sample * _volume);

                            outBytes[i] = (byte)(sample & 0xFF);
                            outBytes[i + 1] = (byte)((sample >> 8) & 0xFF);
                        }

                        _buffer.AddSamples(outBytes, 0, outBytes.Length);
                    }
                    else
                    {
                        _buffer.AddSamples(a.Buffer, 0, a.BytesRecorded);
                    }
                }
            };

            _capture.StartRecording();

            _playback = new WasapiOut(_outDev, AudioClientShareMode.Shared, true, 25);
            _playback.Init(_buffer);
            _playback.Play();

            IsCaptureRunning = true;

            statusPill.SetState("Running", Theme.Sage);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopAudio();
        }

        private void chkMono_CheckedChanged(object sender, EventArgs e)
        {
            _forceMono = chkMono.Checked;
        }

        private void trackVolume_Scroll(object sender, EventArgs e)
        {
            _volume = (float)Math.Pow(trackVolume.Value / 100f, 2);
            lblVolume.Text = $"VOLUME  ·  {trackVolume.Value}%";
        }

        private void BtnCurrentApps_Click(object sender, EventArgs e)
        {
            AppRouterForm.OpenSingleInstance();
        }

        private void StopAudio()
        {
            try { _playback?.Stop(); } catch { }
            try { _capture?.StopRecording(); } catch { }

            _playback?.Dispose();
            _capture?.Dispose();

            _playback = null;
            _capture = null;
            _buffer = null;
            _fmt = null;

            IsCaptureRunning = false;

            statusPill.SetState("Stopped", Theme.InkFaint);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_actuallyClosing && _minimizeToTray &&
                e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;

                Hide();
                ShowInTaskbar = false;
                _trayIcon?.ShowBalloonTip(
                    2000,
                    "MKei",
                    "Still running in system tray.",
                    ToolTipIcon.Info);

                return;
            }
            _trayIcon?.Dispose();
            _settingsMenu?.Dispose();

            StopAudio();

            base.OnFormClosing(e);
        }

        private class DevItem
        {
            public MMDevice Device { get; }
            public DevItem(MMDevice d) { Device = d; }
            public override string ToString() => Device.FriendlyName;
        }
    }
}
