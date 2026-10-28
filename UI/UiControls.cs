using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MKei
{
    internal enum ButtonVariant { Solid, Outline, Ghost }

    /// <summary>Pill-shaped button, flat and hand-drawn rather than the default Windows button chrome.</summary>
    internal sealed class RoundedButton : Button
    {
        public ButtonVariant Variant { get; set; } = ButtonVariant.Solid;
        public Color AccentColor { get; set; } = Theme.Sage;
        public Color AccentColorDark { get; set; } = Theme.SageDark;

        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            // Deliberately NOT using BackColor = Transparent here: WinForms'
            // "transparent" Button rendering asks the parent to paint into a
            // cached bitmap behind the control, and combined with UserPaint +
            // owner drawing it reliably leaves stale ghost pixels and black
            // un-painted corners behind on repaint/resize. Painting a real,
            // opaque background (matching the card these buttons sit on) and
            // drawing the rounded shape on top of that is what avoids it.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Theme.Card;
            FlatAppearance.MouseDownBackColor = Theme.Card;
            BackColor = Theme.Card;
            Font = Theme.ButtonFont;
            Cursor = Cursors.Hand;
            Height = 36;
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; _pressed = false; Invalidate(); };
            MouseDown += (s, e) => { _pressed = true; Invalidate(); };
            MouseUp += (s, e) => { _pressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Paint a fully opaque background first - see the comment in the
            // constructor about why this control never uses transparency.
            g.Clear(BackColor);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Height;
            using var path = RoundedPath(rect, radius);

            bool enabled = Enabled;
            Color fill, border, text;

            switch (Variant)
            {
                case ButtonVariant.Outline:
                    fill = _pressed ? Theme.Mix(Theme.Card, AccentColor, 0.30)
                         : _hover ? Theme.Mix(Theme.Card, AccentColor, 0.16)
                         : Theme.Card;
                    border = AccentColor;
                    text = Theme.Ink;
                    break;
                case ButtonVariant.Ghost:
                    fill = _pressed ? Theme.Divider : _hover ? Theme.Mix(Theme.Card, Theme.Divider, 0.6) : Theme.Card;
                    border = Theme.CardBorder;
                    text = Theme.Ink;
                    break;
                default:
                    fill = !enabled ? Theme.Mix(AccentColor, Theme.Card, 0.55)
                         : _pressed ? AccentColorDark
                         : _hover ? Theme.Mix(AccentColor, Theme.Ink, 0.10)
                         : AccentColor;
                    border = fill;
                    text = Theme.Ink;
                    break;
            }

            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (Variant == ButtonVariant.Outline || Variant == ButtonVariant.Ghost)
                using (var p = new Pen(border, 1.3f)) g.DrawPath(p, path);

            TextRenderer.DrawText(g, Text, Font, rect, enabled ? text : Theme.InkFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundedPath(Rectangle r, int diameter)
        {
            var path = new GraphicsPath();
            diameter = Math.Min(diameter, Math.Min(r.Width, r.Height));
            var arc = new Rectangle(r.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = r.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = r.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = r.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>A calm pill toggle switch, standing in for a checkbox without the OS glyph.</summary>
    internal sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            Height = 26;
            MinimumSize = new Size(0, 26);
            BackColor = Theme.Card;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int trackW = 42, trackH = 22;
            int trackY = (Height - trackH) / 2;
            var track = new Rectangle(0, trackY, trackW, trackH);

            Color trackColor = Checked ? Theme.Sage : Theme.TrackOff;
            using (var b = new SolidBrush(trackColor))
                g.FillPath(b, RoundRect(track, trackH));

            int thumbD = trackH - 4;
            int thumbX = Checked ? track.Right - thumbD - 2 : track.Left + 2;
            var thumb = new Rectangle(thumbX, trackY + 2, thumbD, thumbD);
            using (var b = new SolidBrush(Theme.Ink))
                g.FillEllipse(b, thumb);

            var textRect = new Rectangle(trackW + 10, 0, Width - trackW - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundRect(Rectangle r, int d)
        {
            var path = new GraphicsPath();
            var arc = new Rectangle(r.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = r.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = r.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = r.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Drop-in replacement for TrackBar with the same Value/Minimum/Maximum/
    /// Scroll surface MainForm already used, but drawn as a slim filled track
    /// with a round thumb instead of the OS groove-and-notch control.
    /// </summary>
    internal sealed class FlatSlider : Control
    {
        public event EventHandler? Scroll;

        private int _value = 100;
        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 100;

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Max(Minimum, Math.Min(Maximum, value));
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
            }
        }

        private bool _dragging;

        public FlatSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Height = 28;
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
        }

        private float Fraction => Maximum == Minimum ? 0 : (float)(_value - Minimum) / (Maximum - Minimum);

        private Rectangle TrackRect => new Rectangle(9, Height / 2 - 3, Width - 18, 6);

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var track = TrackRect;
            using (var b = new SolidBrush(Theme.TrackOff))
                g.FillPath(b, Pill(track));

            int filledW = (int)(track.Width * Fraction);
            if (filledW > 0)
            {
                var filled = new Rectangle(track.X, track.Y, Math.Max(filledW, track.Height), track.Height);
                using var b = new SolidBrush(Theme.Sage);
                g.FillPath(b, Pill(filled));
            }

            int thumbD = 16;
            int thumbX = track.X + (int)(track.Width * Fraction) - thumbD / 2;
            thumbX = Math.Max(track.X - thumbD / 2 + 3, Math.Min(track.Right - thumbD / 2 - 3, thumbX));
            var thumb = new Rectangle(thumbX, Height / 2 - thumbD / 2, thumbD, thumbD);

            using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillEllipse(shadow, thumb.X, thumb.Y + 1, thumb.Width, thumb.Height);
            using (var b = new SolidBrush(Theme.Ink))
                g.FillEllipse(b, thumb);
            using (var p = new Pen(Theme.SageDark, 1.4f))
                g.DrawEllipse(p, thumb);
        }

        private static GraphicsPath Pill(Rectangle r)
        {
            var path = new GraphicsPath();
            int d = r.Height;
            if (r.Width < d) { path.AddEllipse(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _dragging = true;
            UpdateFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) UpdateFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        private void UpdateFromMouse(int x)
        {
            var track = TrackRect;
            float f = track.Width <= 0 ? 0 : (float)(x - track.X) / track.Width;
            f = Math.Max(0f, Math.Min(1f, f));
            Value = Minimum + (int)Math.Round(f * (Maximum - Minimum));
            Scroll?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Small colored-dot + label used for run/route status instead of a plain text label.</summary>
    internal sealed class StatusPill : Control
    {
        private string _stateText = "Stopped";
        private Color _dotColor = Theme.InkFaint;

        public StatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Body;
            Height = 24;
            AutoSize = false;
            BackColor = Theme.Card;
        }

        public void SetState(string text, Color dotColor)
        {
            _stateText = text;
            _dotColor = dotColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int dotD = 9;
            var dot = new Rectangle(2, (Height - dotD) / 2, dotD, dotD);
            using (var glow = new SolidBrush(Color.FromArgb(60, _dotColor)))
                g.FillEllipse(glow, dot.X - 2, dot.Y - 2, dotD + 4, dotD + 4);
            using (var b = new SolidBrush(_dotColor))
                g.FillEllipse(b, dot);

            var textRect = new Rectangle(dotD + 10, 0, Width - dotD - 10, Height);
            TextRenderer.DrawText(g, _stateText, Font, textRect, Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Rounded card panel used to group related controls with a soft border.</summary>
    internal sealed class CardPanel : Panel
    {
        public int Radius { get; set; } = 12;

        /// <summary>Fill color of the rounded card itself. BackColor is left as the page
        /// background (Theme.Window) so the four corners outside the rounded path show
        /// the page behind the card instead of a squared-off card color.</summary>
        public Color FillColor { get; set; } = Theme.Card;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundRect(rect, Radius);
            using (var b = new SolidBrush(FillColor)) g.FillPath(b, path);
            using (var p = new Pen(Theme.CardBorder, 1f)) g.DrawPath(p, path);
        }

        private static GraphicsPath RoundRect(Rectangle r, int d)
        {
            var path = new GraphicsPath();
            var arc = new Rectangle(r.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = r.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = r.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = r.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class ComboStyler
    {
        /// <summary>Applies the theme's flat look to a ComboBox, including its own-drawn dropdown list.</summary>
        public static void Style(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Theme.Card;
            combo.ForeColor = Theme.Ink;
            combo.Font = Theme.Body;
            // Native painting avoids owner-draw popup text flickering on some Windows builds.
            combo.DrawMode = DrawMode.Normal;
        }
    }
}
