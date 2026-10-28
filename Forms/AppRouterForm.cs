using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

using NAudio.CoreAudioApi;

namespace MKei
{
    public partial class AppRouterForm : Form
    {
        private static AppRouterForm? _openInstance;

        /// <summary>
        /// Opens this window, but only one at a time. Call this from your main
        /// form's button handler instead of `new AppRouterForm().Show()`:
        ///
        ///     openAppsButton.Click += (s, e) => AppRouterForm.OpenSingleInstance();
        ///
        /// If a window is already open, this does nothing - per spec, it does
        /// NOT bring the existing window to the front, it's just a no-op.
        /// </summary>
        public static void OpenSingleInstance()
        {
            if (_openInstance != null && !_openInstance.IsDisposed)
                return;

            _openInstance = new AppRouterForm();
            _openInstance.FormClosed += (s, e) => _openInstance = null;
            _openInstance.Show();
        }


        private uint selectedProcessId;

        private readonly MMDeviceEnumerator enumerator = new();

        private readonly AudioRouter router = new();

        private MMDevice? cableDevice;


        ListView appList = new();
        StatusPill status = new();
        RoundedButton toggle = new();
        RoundedButton refresh = new();
        ImageList icons = new();
        CardPanel card = new();
        Label lblTitle = new();
        Label lblListHeading = new();


        public AppRouterForm()
        {
            Text = "Current Apps";
            Size = new Size(700, 460);
            BackColor = Theme.Window;
            Font = Theme.Body;
            StartPosition = FormStartPosition.CenterParent;

            icons.ImageSize = new Size(28, 28);
            icons.ColorDepth = ColorDepth.Depth32Bit;

            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(24, 18);
            lblTitle.Font = Theme.Title;
            lblTitle.ForeColor = Theme.Ink;
            lblTitle.Text = "Current Apps";
            Controls.Add(lblTitle);

            card.Location = new Point(20, 56);
            card.Size = new Size(644, 358);
            card.Radius = 14;
            Controls.Add(card);

            lblListHeading.AutoSize = true;
            lblListHeading.Location = new Point(20, 16);
            lblListHeading.Font = Theme.Heading;
            lblListHeading.ForeColor = Theme.InkMuted;
            lblListHeading.Text = "APPS WITH AN ACTIVE AUDIO SESSION";
            card.Controls.Add(lblListHeading);

            appList.Location = new Point(20, 42);
            appList.Size = new Size(604, 224);
            appList.View = View.Details;
            appList.FullRowSelect = true;
            appList.MultiSelect = true; // some apps (Apple Music + AMPLibraryAgent,
                                        // for example) run their actual audio session
                                        // in a helper process separate from the one
                                        // shown as the "main" app - selecting and
                                        // toggling both together routes all of them.
            appList.SmallImageList = icons;
            appList.HeaderStyle = ColumnHeaderStyle.None;
            appList.BorderStyle = BorderStyle.None;
            appList.BackColor = Theme.Card;
            appList.GridLines = false;
            appList.OwnerDraw = true;
            appList.Columns.Add("Application", 580);
            appList.DrawColumnHeader += (s, e) => e.DrawDefault = false;
            appList.DrawItem += AppList_DrawItem;
            appList.DrawSubItem += AppList_DrawSubItem;
            appList.SelectedIndexChanged += AppSelected;
            appList.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && appList.HitTest(e.Location).Item is { } item && !item.Selected)
                {
                    appList.SelectedItems.Clear();
                    item.Selected = true;
                }
            };
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Refresh", null, RefreshSelectedRouting);
            contextMenu.Opening += (s, e) =>
                contextMenu.Items[0].Enabled = appList.SelectedItems.Count > 0;
            appList.ContextMenuStrip = contextMenu;
            card.Controls.Add(appList);

            var divider = new Panel { BackColor = Theme.Divider, Location = new Point(20, 276), Size = new Size(604, 1) };
            card.Controls.Add(divider);

            status.Location = new Point(20, 296);
            status.Size = new Size(360, 26);
            status.SetState("No app selected", Theme.InkFaint);
            card.Controls.Add(status);

            toggle.Text = "Toggle Routing";
            toggle.Location = new Point(430, 292);
            toggle.Size = new Size(150, 34);
            toggle.Variant = ButtonVariant.Solid;
            toggle.AccentColor = Theme.Sage;
            toggle.AccentColorDark = Theme.SageDark;
            toggle.Visible = false;
            toggle.Click += Toggle_Click;
            card.Controls.Add(toggle);

            // Refresh sits in the card's top-right corner, beside the heading.
            refresh.Text = "Refresh";
            refresh.Variant = ButtonVariant.Ghost;
            refresh.Size = new Size(96, 30);
            refresh.Location = new Point(card.Width - 96 - 20, 12);
            refresh.Click += (s, e) => RefreshApps();
            card.Controls.Add(refresh);

            if (!AudioPolicyConfig.IsSupported)
            {
                MessageBox.Show(
                    "Per-app device redirection needs Windows 11 (build 21390 or later). " +
                    $"This machine is on build {Environment.OSVersion.Version.Build}, so toggling " +
                    "won't work here.",
                    "Unsupported Windows build");
            }

            HandleCreated += (s, e) => WindowChrome.Apply(this, Theme.Window, Theme.Ink, Theme.CardBorder);

            LoadApps();
        }


        /// <summary>Reloads the app list and clears the current selection/status, without closing the window.</summary>
        void RefreshApps()
        {
            appList.Items.Clear();
            icons.Images.Clear();
            cableDevice = null; // re-resolve in case the device list changed

            LoadApps();

            status.SetState("No app selected", Theme.InkFaint);
            toggle.Visible = false;
        }


        MMDevice? FindCableDevice()
        {
            return cableDevice ??=
                enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .FirstOrDefault(x => x.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase));
        }


        void LoadApps()
        {
            var seenProcessIds = new HashSet<uint>();

            // Scan every active render device, not just the current system
            // default - an app already routed to CABLE (whether by this app in
            // a previous run, or manually via Windows' own Volume Mixer) has
            // its session sitting on CABLE, not on the default device, and
            // would otherwise silently never show up in this list.
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                List<AudioSessionControl> sessions;
                try
                {
                    var manager = device.AudioSessionManager;
                    sessions = Enumerable.Range(0, manager.Sessions.Count).Select(i => manager.Sessions[i]).ToList();
                }
                catch
                {
                    continue;
                }

                foreach (var session in sessions)
                {
                    try
                    {
                        uint pid =
                            session.GetProcessID;


                        if (pid == 0)
                            continue;

                        if (!seenProcessIds.Add(pid))
                            continue; // already listed from another device


                        Process process;


                        try
                        {
                            process =
                                Process.GetProcessById(
                                    (int)pid);
                        }
                        catch
                        {
                            continue;
                        }



                        string displayName =
                            process.ProcessName;


                        try
                        {
                            displayName =
                                FileVersionInfo
                                .GetVersionInfo(
                                    process.MainModule.FileName)
                                .FileDescription;
                        }
                        catch
                        {

                        }



                        try
                        {
                            Icon? icon =
                                Icon.ExtractAssociatedIcon(
                                    process.MainModule.FileName);


                            if (icon != null)
                            {
                                icons.Images.Add(
                                    process.ProcessName,
                                    icon);
                            }
                        }
                        catch
                        {

                        }



                        ListViewItem item =
                            new(displayName);


                        item.ImageKey =
                            process.ProcessName;


                        item.Tag =
                            new AudioApp
                            {
                                Name = displayName,
                                ProcessId = pid
                            };


                        appList.Items.Add(item);

                    }
                    catch
                    {

                    }
                }
            }

            // Note: this only scans sessions that are already open right now -
            // an app that hasn't made any sound yet won't have a session, and
            // so won't show up until it does and you reopen this window.
        }




        /// <summary>
        /// True if the app is "on" from either angle: its live session is
        /// actually playing through CABLE right now, OR its persisted
        /// preference points at CABLE (which happens immediately on toggle,
        /// even though Windows won't migrate an already-open session until
        /// the app opens a new one). Checking only live placement is what
        /// made toggling get stuck - persisted state is what the toggle
        /// button actually controls, so it has to be part of "on".
        /// </summary>
        bool IsRoutedToCable(uint processId, MMDevice cable)
        {
            if (router.IsProcessOnDevice((int)processId, cable.ID))
                return true;

            string? persisted = AudioPolicyConfig.GetAppDevice(processId);
            return persisted != null && persisted == cable.ID;
        }


        void AppSelected(object? sender, EventArgs e)
        {
            if (appList.SelectedItems.Count == 0)
                return;


            AudioApp app =
                (AudioApp)
                appList.SelectedItems[0].Tag;


            selectedProcessId =
                app.ProcessId;


            RefreshStatusForSelection();

            toggle.Visible = true;
            appList.Invalidate();
        }


        /// <summary>
        /// Checks every currently selected app's *actual current* audio
        /// session, not any state this app remembers, so it correctly shows
        /// ON if the app(s) were already routed to VB-Cable via Windows' own
        /// Volume Mixer or a previous run. With multiple items selected,
        /// shows ON only if all of them are, and a mixed count otherwise -
        /// pressing Toggle from a mixed state routes everything to CABLE.
        /// </summary>
        void RefreshStatusForSelection()
        {
            var cable = FindCableDevice();
            if (cable == null || appList.SelectedItems.Count == 0)
            {
                status.SetState("No app selected", Theme.InkFaint);
                return;
            }

            var selectedApps = appList.SelectedItems.Cast<ListViewItem>()
                .Select(i => (AudioApp)i.Tag)
                .ToList();

            int onCount = selectedApps.Count(a => IsRoutedToCable(a.ProcessId, cable));

            if (selectedApps.Count == 1)
            {
                status.SetState(onCount == 1 ? "Routed to CABLE" : "Not routed", onCount == 1 ? Theme.Sage : Theme.InkFaint);
            }
            else if (onCount == selectedApps.Count)
            {
                status.SetState($"Routed to CABLE ({selectedApps.Count} selected)", Theme.Sage);
            }
            else if (onCount == 0)
            {
                status.SetState($"Not routed ({selectedApps.Count} selected)", Theme.InkFaint);
            }
            else
            {
                status.SetState($"Mixed ({onCount}/{selectedApps.Count} on)", Theme.Amber);
            }
        }




        void Toggle_Click(object? sender, EventArgs e)
        {
            if (appList.SelectedItems.Count == 0)
                return;


            var cable = FindCableDevice();

            if (cable == null)
            {
                MessageBox.Show(
                    "VB Audio Cable not found");

                return;
            }


            var selectedApps = appList.SelectedItems.Cast<ListViewItem>()
                .Select(i => (AudioApp)i.Tag)
                .ToList();

            // If everything selected is already on, this pass turns it all
            // off; otherwise (all off, or mixed) it turns everything on -
            // avoids a half-selected group ending up in a half-on state.
            bool turnOn = selectedApps.Any(a => !IsRoutedToCable(a.ProcessId, cable));

            var failures = new List<string>();

            foreach (var app in selectedApps)
            {
                try
                {
                    if (turnOn)
                        router.RouteProcess((int)app.ProcessId, cable.ID);
                    else
                        router.RestoreProcess((int)app.ProcessId);
                }
                catch (PlatformNotSupportedException ex)
                {
                    MessageBox.Show(ex.Message, "Unsupported Windows build");
                    return;
                }
                catch (Exception ex)
                {
                    failures.Add($"{app.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (failures.Count > 0)
            {
                MessageBox.Show(string.Join("\n\n", failures), "Route failed for some apps");
            }

            if (turnOn && !MainForm.IsCaptureRunning)
            {
                MessageBox.Show(
                    "Routing is on, but the main window's capture/playback (\"Start\") isn't " +
                    "running. Redirecting an app's audio to VB-Cable moves it there - nothing " +
                    "plays it back to your speakers unless the main window is actively capturing " +
                    "from CABLE Output and playing it back. Right now that audio is going nowhere " +
                    "you can hear, though other programs that read from CABLE Output directly " +
                    "(e.g. Discord set to use it as a microphone) will still pick it up.",
                    "Local playback isn't running");
            }

            // Windows applies the new persisted device to *new* sessions the
            // app opens - an already-open session generally follows along too,
            // but if a given app doesn't pick it up until its next sound,
            // that's a Windows behavior, not a bug in this code.
            RefreshStatusForSelection();
        }

        void RefreshSelectedRouting(object? sender, EventArgs e)
        {
            var cable = FindCableDevice();
            if (cable == null)
            {
                MessageBox.Show("VB Audio Cable not found");
                return;
            }

            var failures = new List<string>();
            foreach (var app in appList.SelectedItems.Cast<ListViewItem>().Select(i => (AudioApp)i.Tag))
            {
                if (!IsRoutedToCable(app.ProcessId, cable))
                    continue;

                try
                {
                    router.RestoreProcess((int)app.ProcessId);
                    router.RouteProcess((int)app.ProcessId, cable.ID);
                }
                catch (Exception ex)
                {
                    failures.Add($"{app.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (failures.Count > 0)
                MessageBox.Show(string.Join("\n\n", failures), "Route refresh failed for some apps");

            RefreshStatusForSelection();
            appList.Invalidate();
        }


        /// <summary>Full-row background + left accent bar for selected/hovered rows, replacing the OS blue highlight.</summary>
        private void AppList_DrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            var g = e.Graphics;
            bool selected = e.Item.Selected;

            Color back = selected ? Theme.SageTint : Theme.Card;
            using (var b = new SolidBrush(back))
                g.FillRectangle(b, e.Bounds);

            if (selected)
            {
                using var accent = new SolidBrush(Theme.Sage);
                g.FillRectangle(accent, new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));
            }

            e.DrawDefault = false;
        }

        private void AppList_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var app = (AudioApp)e.Item.Tag;
            var cable = FindCableDevice();
            bool routed = cable != null && IsRoutedToCable(app.ProcessId, cable);

            int iconSize = 28;
            var iconRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + (e.Bounds.Height - iconSize) / 2, iconSize, iconSize);
            if (icons.Images.ContainsKey(e.Item.ImageKey))
                g.DrawImage(icons.Images[e.Item.ImageKey], iconRect);

            var textRect = new Rectangle(iconRect.Right + 12, e.Bounds.Y, e.Bounds.Width - iconRect.Right - 90, e.Bounds.Height);
            TextRenderer.DrawText(g, e.Item.Text, Theme.Body, textRect, Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // Small routed/not-routed dot on the right, mirroring the toggle state at a glance.
            int dotD = 8;
            var dot = new Rectangle(e.Bounds.Right - dotD - 12, e.Bounds.Y + (e.Bounds.Height - dotD) / 2, dotD, dotD);
            using var dotBrush = new SolidBrush(routed ? Theme.Sage : Theme.InkFaint);
            g.FillEllipse(dotBrush, dot);
        }


        class AudioApp
        {
            public string Name { get; set; } = "";

            public uint ProcessId { get; set; }
        }

    }
}
