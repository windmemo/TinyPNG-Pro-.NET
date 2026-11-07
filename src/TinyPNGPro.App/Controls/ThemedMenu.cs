using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TinyPNGPro.App.Theme;

namespace TinyPNGPro.App.Controls
{
    /// <summary>轻量主题化右键菜单渲染器，零第三方依赖。</summary>
    public sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemedMenuRenderer()
            : base(new ThemedMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            ThemeColors t = AppTheme.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (SolidBrush brush = new SolidBrush(t.Card))
            {
                DrawUtil.FillRounded(g, rect, 10, brush);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            Rectangle rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using (SolidBrush brush = new SolidBrush(t.Elev))
            {
                DrawUtil.FillRounded(e.Graphics, rect, 7, brush);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            ThemeColors t = AppTheme.Current;
            e.TextColor = e.Item.Enabled ? t.Fg : t.Dim;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            ThemeColors t = AppTheme.Current;
            Rectangle rect = new Rectangle(10, e.Item.Height / 2, e.Item.Width - 20, 1);
            using (SolidBrush brush = new SolidBrush(t.Border))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            ThemeColors t = AppTheme.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (Pen pen = new Pen(t.BorderStrong, 1))
            {
                DrawUtil.DrawRounded(g, rect, 10, pen);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
        }
    }

    internal sealed class ThemedMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground
        {
            get { return AppTheme.Current.Card; }
        }

        public override Color MenuBorder
        {
            get { return AppTheme.Current.BorderStrong; }
        }

        public override Color MenuItemBorder
        {
            get { return AppTheme.Current.Elev; }
        }

        public override Color MenuItemSelected
        {
            get { return AppTheme.Current.Elev; }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get { return AppTheme.Current.Elev; }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return AppTheme.Current.Elev; }
        }

        public override Color ImageMarginGradientBegin
        {
            get { return AppTheme.Current.Card; }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return AppTheme.Current.Card; }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return AppTheme.Current.Card; }
        }
    }
}
