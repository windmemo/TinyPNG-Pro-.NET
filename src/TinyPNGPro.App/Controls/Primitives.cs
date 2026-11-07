using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TinyPNGPro.App.Theme;

namespace TinyPNGPro.App.Controls
{
    internal static class DrawUtil
    {
        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width)
            {
                d = bounds.Width;
            }
            if (d > bounds.Height)
            {
                d = bounds.Height;
            }
            if (d <= 0)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle bounds, int radius, Brush brush)
        {
            using (GraphicsPath path = Rounded(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRounded(Graphics g, Rectangle bounds, int radius, Pen pen)
        {
            using (GraphicsPath path = Rounded(bounds, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void FillRoundRect(Graphics g, Rectangle bounds, int radius, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                FillRounded(g, bounds, radius, brush);
            }
        }

        public static Color Grayscale(Color color)
        {
            int gray = (int)(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);
            return Color.FromArgb(color.A, gray, gray, gray);
        }
    }

    public sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 轻量投影：两层低透明圆角即可获得柔和抬升感，避免引入额外位图。
            using (SolidBrush shadow = new SolidBrush(
                Color.FromArgb(AppTheme.IsDark ? 46 : 20, 0, 0, 0)))
            {
                DrawUtil.FillRounded(e.Graphics,
                    new Rectangle(1, 3, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 16, shadow);
                DrawUtil.FillRounded(e.Graphics,
                    new Rectangle(3, 6, Math.Max(1, Width - 6), Math.Max(1, Height - 5)), 16, shadow);
            }

            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect, t.Card, t.Video, LinearGradientMode.Vertical))
            {
                DrawUtil.FillRounded(e.Graphics, rect, 16, brush);
            }
            using (Pen pen = new Pen(t.Border, 1))
            {
                DrawUtil.DrawRounded(e.Graphics, rect, 16, pen);
            }
            base.OnPaint(e);
        }
    }

    public enum RoundedButtonKind
    {
        Primary,
        Ghost,
        Icon,
        Accent,
        Danger,
        Close
    }

    public sealed class RoundedButton : Control
    {
        private RoundedButtonKind _kind = RoundedButtonKind.Ghost;
        private bool _hover;
        private bool _down;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = AppTheme.GetFont(8.5f, FontStyle.Bold);
        }

        public RoundedButtonKind Kind
        {
            get { return _kind; }
            set
            {
                _kind = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _down = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fore = t.Fg;
            int radius = Height / 2;

            if (_kind == RoundedButtonKind.Primary)
            {
                Color start = _down ? t.Primary : (_hover ? t.PrimaryH : t.Primary);
                Color end = t.Primary2;
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(Width, 1), Math.Max(Height, 1)),
                    start, end, LinearGradientMode.Horizontal))
                {
                    DrawUtil.FillRounded(e.Graphics, rect, radius, brush);
                }
                fore = Color.White;
            }
            else if (_kind == RoundedButtonKind.Ghost)
            {
                Color bg = _down ? t.Card : (_hover ? t.Elev : Color.Transparent);
                if (bg.A > 0)
                {
                    DrawUtil.FillRoundRect(e.Graphics, rect, radius, bg);
                }
                using (Pen pen = new Pen(_hover ? t.Primary2 : t.Border, 1))
                {
                    DrawUtil.DrawRounded(e.Graphics, rect, radius, pen);
                }
                fore = _hover ? t.Fg : t.Muted;
            }
            else if (_kind == RoundedButtonKind.Icon)
            {
                radius = 8;
                if (_hover)
                {
                    DrawUtil.FillRoundRect(e.Graphics, rect, radius, _down ? t.Card : t.Elev);
                }
                fore = _hover ? t.Fg : t.Dim;
            }
            else if (_kind == RoundedButtonKind.Accent)
            {
                if (_hover)
                {
                    DrawUtil.FillRoundRect(e.Graphics, rect, radius, t.Elev);
                }
                using (Pen pen = new Pen(t.Primary, 1))
                {
                    DrawUtil.DrawRounded(e.Graphics, rect, radius, pen);
                }
                fore = t.Primary2;
            }
            else if (_kind == RoundedButtonKind.Close)
            {
                radius = 8;
                if (_hover)
                {
                    DrawUtil.FillRoundRect(e.Graphics, rect, radius, Color.FromArgb(30, t.Error));
                }
                fore = _hover ? t.Error : t.Dim;
            }
            else
            {
                if (_hover)
                {
                    DrawUtil.FillRoundRect(e.Graphics, rect, radius, Color.FromArgb(24, t.Error));
                }
                using (Pen pen = new Pen(t.Error, 1))
                {
                    DrawUtil.DrawRounded(e.Graphics, rect, radius, pen);
                }
                fore = t.Error;
            }

            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                using (SolidBrush brush = new SolidBrush(fore))
                {
                    e.Graphics.DrawString(Text, Font, brush, new RectangleF(0, 0, Width, Height), format);
                }
            }
        }
    }

    public sealed class StatusChip : Control
    {
        private string _kind = "idle";
        private string _chipText = "就绪";

        public StatusChip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = AppTheme.GetFont(8.5f, FontStyle.Bold);
            SetChip("idle", "就绪");
        }

        public string Kind
        {
            get { return _kind; }
        }

        public string ChipText
        {
            get { return _chipText; }
        }

        public void SetChip(string kind, string text)
        {
            _kind = kind;
            _chipText = text;
            Height = 22;
            Width = TextRenderer.MeasureText(text, Font).Width + 24;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            DrawUtil.FillRoundRect(e.Graphics, rect, Height / 2, AppTheme.ChipBg(_kind));
            using (Pen pen = new Pen(AppTheme.ChipBorder(_kind), 1))
            {
                DrawUtil.DrawRounded(e.Graphics, rect, Height / 2, pen);
            }
            TextRenderer.DrawText(e.Graphics, _chipText, Font, rect,
                AppTheme.ChipFg(_kind),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }
    }

    public sealed class ThinProgressBar : Control
    {
        private int _value;
        private int _maxValue = 500;

        public ThinProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 6;
        }

        public int Value
        {
            get { return _value; }
            set
            {
                _value = value;
                Invalidate();
            }
        }

        public int MaxValue
        {
            get { return _maxValue; }
            set
            {
                _maxValue = value < 1 ? 1 : value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width, Height);
            DrawUtil.FillRoundRect(e.Graphics, track, Height / 2, t.MeterBg);

            int clamped = _value < 0 ? 0 : (_value > _maxValue ? _maxValue : _value);
            if (clamped > 0)
            {
                int fillWidth = (int)((double)clamped / _maxValue * Width);
                if (fillWidth < Height)
                {
                    fillWidth = Height;
                }
                Rectangle fill = new Rectangle(0, 0, Math.Min(fillWidth, Width), Height);
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(Width, 1), Math.Max(Height, 1)),
                    t.Primary, t.Primary2, LinearGradientMode.Horizontal))
                {
                    DrawUtil.FillRounded(e.Graphics, fill, Height / 2, brush);
                }
            }
        }
    }
}
