using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MKei
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            using var mutex = new System.Threading.Mutex(true, @"Local\MKei.SingleInstance", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                ActivateRunningInstance();
                return;
            }

            ApplicationConfiguration.Initialize();
            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private static void ActivateRunningInstance()
        {
            IntPtr mainWindow = IntPtr.Zero;
            IntPtr routerWindow = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                var title = new StringBuilder(256);
                GetWindowText(window, title, title.Capacity);
                if (title.ToString() == "Current Apps")
                {
                    routerWindow = window;
                    return false;
                }

                if (title.ToString() == "MKei")
                    mainWindow = window;
                return true;
            }, IntPtr.Zero);

            var windowToActivate = routerWindow != IntPtr.Zero ? routerWindow : mainWindow;
            if (windowToActivate == IntPtr.Zero)
                return;

            ShowWindow(windowToActivate, 9); // SW_RESTORE
            SetForegroundWindow(windowToActivate);
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}
