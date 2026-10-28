using System.Drawing;

namespace MKei
{
    /// <summary>
    /// "Nocturne" theme - a soft, low-contrast dark palette: near-black wine
    /// surfaces, warm bone text (never pure white, which reads harsh on a
    /// dark ground), a muted garnet accent for "on"/start/positive states
    /// and a dusky amethyst accent for "off"/stop. Deliberately desaturated
    /// and gentle rather than the high-contrast neon-on-black look most
    /// generated dark themes default to.
    /// </summary>
    internal static class Theme
    {
        // Surfaces
        public static readonly Color Window = ColorTranslator.FromHtml("#17121A");   // outer window
        public static readonly Color Card = ColorTranslator.FromHtml("#221C26");     // panels/cards
        public static readonly Color CardBorder = ColorTranslator.FromHtml("#39303F");
        public static readonly Color Divider = ColorTranslator.FromHtml("#2E2733");

        // Text
        public static readonly Color Ink = ColorTranslator.FromHtml("#E9E0E6");      // primary text (warm bone, not pure white)
        public static readonly Color InkMuted = ColorTranslator.FromHtml("#9C8C99"); // secondary text
        public static readonly Color InkFaint = ColorTranslator.FromHtml("#6B5D68"); // hints / disabled

        // Accents
        public static readonly Color Sage = ColorTranslator.FromHtml("#9C4256");      // garnet - "on" / start / positive
        public static readonly Color SageDark = ColorTranslator.FromHtml("#7C3245");
        public static readonly Color SageTint = ColorTranslator.FromHtml("#3A2531");

        public static readonly Color Clay = ColorTranslator.FromHtml("#7A5C87");      // amethyst - "off" / stop
        public static readonly Color ClayDark = ColorTranslator.FromHtml("#5F4769");
        public static readonly Color ClayTint = ColorTranslator.FromHtml("#332B3C");

        public static readonly Color Amber = ColorTranslator.FromHtml("#C7A15B");     // mixed / warning state, candlelight gold

        public static readonly Color TrackOff = ColorTranslator.FromHtml("#3A2F3F");

        // Fonts
        public static Font Title => new Font("Segoe UI Semibold", 12.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Heading => new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Body => new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font BodySmall => new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font ButtonFont => new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Mono => new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);

        public static Color Mix(Color a, Color b, double t)
        {
            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bl = (int)(a.B + (b.B - a.B) * t);
            return Color.FromArgb(r, g, bl);
        }
    }
}
