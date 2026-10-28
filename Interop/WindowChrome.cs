using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MKei
{
    /// <summary>
    /// Tints the *native* title bar to match the app's palette and asks for
    /// rounded window corners, using the documented DWM attributes Windows 11
    /// exposes (DWMWA_CAPTION_COLOR / DWMWA_TEXT_COLOR / DWMWA_BORDER_COLOR /
    /// DWMWA_WINDOW_CORNER_PREFERENCE). This gives a custom-colored, on-theme
    /// window frame without reimplementing drag/resize/snap behavior that a
    /// borderless form would need - and it degrades silently (plain default
    /// title bar) on older Windows builds instead of throwing.
    /// </summary>
    internal static class WindowChrome
    {
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void Apply(Form form, Color caption, Color text, Color border)
        {
            if (Environment.OSVersion.Version.Build < 22000)
                return; // pre-Windows 11: leave the default title bar alone

            try
            {
                int cap = ColorToRef(caption);
                int txt = ColorToRef(text);
                int brd = ColorToRef(border);
                int corner = DWMWCP_ROUND;

                DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, ref cap, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, ref txt, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DWMWA_BORDER_COLOR, ref brd, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            }
            catch
            {
                // Cosmetic only - never let this affect functionality.
            }
        }

        // DWM wants 0x00BBGGRR, not managed Color's ARGB order.
        private static int ColorToRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
    }
}
