using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TinyPNGPro.App.Theme;

namespace TinyPNGPro.App.Controls
{
    public sealed class PillToggle : Control
    {
        private bool _checked;
        private bool _hover;

        public event EventHandler CheckedChanged;

        public PillToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = AppTheme.GetFont(8.5f, FontStyle.Bold);
            Height = 26;
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value)
                {
                    return;
                }
                _checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            RefreshMetrics();
        }

        /// <summary>
        /// 统一尺寸：左侧 15px 勾选框 + 7px 间距，右侧留 8px；
        /// 旧公式只预留 44px 给轨道，勾选框改版后容易裁到文字。
        /// </summary>
        private void RefreshMetrics()
        {
            int textWidth = TextRenderer.MeasureText(Text, Font).Width;
            Height = 26;
            Width = 46 + textWidth;
            Invalidate();
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
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // 外层芯片：勾选时染主色底 + 主色描边，未勾选时保持中性，
            // 深浅两套主题都只依赖明确的填充/描边差异来表达状态。
            int radius = Height / 2;
            Rectangle chip = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_checked)
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(Width, 1), Math.Max(Height, 1)),
                    Color.FromArgb(AppTheme.IsDark ? 62 : 44, t.Primary),
                    Color.FromArgb(AppTheme.IsDark ? 30 : 16, t.Primary2),
                    LinearGradientMode.Horizontal))
                {
                    DrawUtil.FillRounded(e.Graphics, chip, radius, brush);
                }
            }
            else if (_hover)
            {
                DrawUtil.FillRoundRect(e.Graphics, chip, radius, t.Elev);
            }

            using (Pen pen = new Pen(
                _checked ? t.Primary : (_hover ? t.Primary2 : t.BorderStrong),
                _checked ? 1.4f : 1f))
            {
                DrawUtil.DrawRounded(e.Graphics, chip, radius, pen);
            }

            // 勾选框本体 + 自绘勾号。
            int boxSize = 15;
            Rectangle box = new Rectangle(7, (Height - boxSize) / 2, boxSize, boxSize);
            if (_checked)
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    box, t.Primary, t.Primary2, LinearGradientMode.Horizontal))
                {
                    DrawUtil.FillRounded(e.Graphics, box, 4, brush);
                }
                using (Pen pen = new Pen(Color.White, 1.8f))
                {
                    pen.LineJoin = LineJoin.Round;
                    e.Graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(box.X + 3.3f, box.Y + 7.7f),
                        new PointF(box.X + 6.1f, box.Y + 10.5f),
                        new PointF(box.X + 11.7f, box.Y + 4.5f)
                    });
                }
            }
            else
            {
                DrawUtil.FillRoundRect(e.Graphics, box, 4, t.Card);
                using (Pen pen = new Pen(_hover ? t.Primary2 : t.Dim, 1f))
                {
                    DrawUtil.DrawRounded(e.Graphics, box, 4, pen);
                }
            }

            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                using (SolidBrush brush = new SolidBrush(_checked || _hover ? t.Fg : t.Muted))
                {
                    e.Graphics.DrawString(Text, Font, brush,
                        new RectangleF(30, 0, Math.Max(1, Width - 38), Height), format);
                }
            }
        }
    }

    public sealed class DropZone : Control
    {
        private bool _dragHover;
        private Image _iconImage;

        public event Action<string[]> FilesDropped;

        public readonly RoundedButton SelectButton;

        public DropZone()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AllowDrop = true;
            MinimumSize = new Size(200, 200);

            SelectButton = new RoundedButton();
            SelectButton.Kind = RoundedButtonKind.Primary;
            SelectButton.Text = "选择图像";
            SelectButton.Size = new Size(300, 40);
            Controls.Add(SelectButton);
        }

        public Image IconImage
        {
            get { return _iconImage; }
            set
            {
                _iconImage = value;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (SelectButton != null)
            {
                int x = (Width - SelectButton.Width) / 2;
                int y = Height - SelectButton.Height - 22;
                if (y < 120)
                {
                    y = Height - SelectButton.Height - 12;
                }
                SelectButton.Location = new Point(Math.Max(0, x), Math.Max(0, y));
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
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            int topAlpha = _dragHover ? 41 : 15;
            int bottomAlpha = _dragHover ? 31 : 13;
            Color top = Color.FromArgb(topAlpha, t.Primary.R, t.Primary.G, t.Primary.B);
            Color bottom = Color.FromArgb(bottomAlpha, t.Primary2.R, t.Primary2.G, t.Primary2.B);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect, top, bottom, LinearGradientMode.BackwardDiagonal))
            {
                DrawUtil.FillRounded(e.Graphics, rect, 16, brush);
            }

            using (Pen pen = _dragHover
                ? new Pen(t.Primary2, 1.5f)
                : new Pen(t.Border, 1f))
            {
                pen.DashStyle = _dragHover ? DashStyle.Dash : DashStyle.Solid;
                DrawUtil.DrawRounded(e.Graphics, rect, 16, pen);
            }

            if (_iconImage != null)
            {
                e.Graphics.DrawImage(_iconImage, new Rectangle(
                    (Width - 56) / 2, (int)(Height * 0.18), 56, 56));
            }

            // 注意：AppTheme.GetFont 返回全局缓存的 Font，绝不能放进 using，否则会被释放。
            Font titleFont = AppTheme.GetFont(11.5f, FontStyle.Bold);
            Font subFont = AppTheme.GetFont(8.5f, FontStyle.Regular);
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                using (SolidBrush brush = new SolidBrush(t.Fg))
                {
                    e.Graphics.DrawString("拖放图片到此处压缩", titleFont, brush,
                        new RectangleF(0, Height * 0.18f + 64, Width, 28), format);
                }
                using (SolidBrush brush = new SolidBrush(t.Dim))
                {
                    e.Graphics.DrawString("支持 PNG / JPEG / WebP", subFont, brush,
                        new RectangleF(0, Height * 0.18f + 92, Width, 22), format);
                }
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                _dragHover = true;
                e.Effect = DragDropEffects.Copy;
                Invalidate();
                return;
            }
            base.OnDragEnter(e);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            _dragHover = false;
            Invalidate();
            base.OnDragLeave(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            _dragHover = false;
            Invalidate();
            try
            {
                string[] files = e.Data != null
                    ? (string[])e.Data.GetData(DataFormats.FileDrop)
                    : null;
                if (files != null && files.Length > 0 && FilesDropped != null)
                {
                    FilesDropped(files);
                }
            }
            catch
            {
            }
            base.OnDragDrop(e);
        }
    }
}
