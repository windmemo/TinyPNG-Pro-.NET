using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TinyPNGPro.App.Controls;

namespace TinyPNGPro.App.Theme
{
    public sealed class ThemeColors
    {
        public string Mode;
        public Color Bg;
        public Color Soft;
        public Color Card;
        public Color Elev;
        public Color Video;
        public Color Primary;
        public Color Primary2;
        public Color PrimaryH;
        public Color Fg;
        public Color Muted;
        public Color Dim;
        public Color Border;
        public Color BorderStrong;
        public Color Success;
        public Color Error;
        public Color Warning;
        public Color MeterBg;

        public Color ChipOkBg;
        public Color ChipOkFg;
        public Color ChipOkBorder;
        public Color ChipErrBg;
        public Color ChipErrFg;
        public Color ChipErrBorder;
        public Color ChipWarnBg;
        public Color ChipWarnFg;
        public Color ChipWarnBorder;
        public Color ChipInfoBg;
        public Color ChipInfoFg;
        public Color ChipInfoBorder;
        public Color ChipIdleBg;
        public Color ChipIdleFg;
        public Color ChipIdleBorder;
    }

    /// <summary>双主题（夜间/明亮）配色与字体，全部自绘取色。</summary>
    public static class AppTheme
    {
        private static ThemeColors _current;
        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();
        private static string _fontFamily;

        static AppTheme()
        {
            _current = BuildDark();
        }

        public static ThemeColors Current
        {
            get { return _current; }
        }

        public static bool IsDark
        {
            get { return _current.Mode == "dark"; }
        }

        public static void SetMode(string mode)
        {
            _current = mode == "light" ? BuildLight() : BuildDark();
        }

        private static Color Hex(string hex)
        {
            string value = hex.TrimStart('#');
            int r = Convert.ToInt32(value.Substring(0, 2), 16);
            int g = Convert.ToInt32(value.Substring(2, 2), 16);
            int b = Convert.ToInt32(value.Substring(4, 2), 16);
            return Color.FromArgb(r, g, b);
        }

        private static Color Alpha(Color color, int alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static ThemeColors BuildDark()
        {
            ThemeColors t = new ThemeColors();
            t.Mode = "dark";
            t.Bg = Hex("#0F1115");
            t.Soft = Hex("#14161C");
            t.Card = Hex("#171A21");
            t.Elev = Hex("#1E222B");
            t.Video = Hex("#11141A");
            t.Primary = Hex("#7C5CFF");
            t.Primary2 = Hex("#5AC8FF");
            t.PrimaryH = Hex("#9680FF");
            t.Fg = Hex("#EDEDF2");
            t.Muted = Hex("#9CA3AF");
            t.Dim = Hex("#6B7280");
            t.Border = Hex("#2A2E3A");
            t.BorderStrong = Hex("#3A4150");
            t.Success = Hex("#34D399");
            t.Error = Hex("#F87171");
            t.Warning = Hex("#FBBF24");
            t.MeterBg = Hex("#232733");

            t.ChipOkBg = Alpha(Hex("#34D399"), 36);
            t.ChipOkFg = Hex("#6EE7B7");
            t.ChipOkBorder = Alpha(Hex("#34D399"), 92);
            t.ChipErrBg = Alpha(Hex("#F87171"), 36);
            t.ChipErrFg = Hex("#FCA5A5");
            t.ChipErrBorder = Alpha(Hex("#F87171"), 92);
            t.ChipWarnBg = Alpha(Hex("#FBBF24"), 36);
            t.ChipWarnFg = Hex("#FCD34D");
            t.ChipWarnBorder = Alpha(Hex("#FBBF24"), 92);
            t.ChipInfoBg = Alpha(Hex("#7C5CFF"), 41);
            t.ChipInfoFg = Hex("#B7A6FF");
            t.ChipInfoBorder = Alpha(Hex("#7C5CFF"), 102);
            t.ChipIdleBg = Alpha(Hex("#94A3B8"), 33);
            t.ChipIdleFg = Hex("#CBD5E1");
            t.ChipIdleBorder = Alpha(Hex("#94A3B8"), 77);
            return t;
        }

        private static ThemeColors BuildLight()
        {
            ThemeColors t = new ThemeColors();
            t.Mode = "light";
            t.Bg = Hex("#EEF1F5");
            t.Soft = Hex("#FFFFFF");
            t.Card = Hex("#FFFFFF");
            t.Elev = Hex("#E4E8EE");
            t.Video = Hex("#FAFBFD");
            t.Primary = Hex("#6D4BFF");
            t.Primary2 = Hex("#0EA5E9");
            t.PrimaryH = Hex("#8B6BFF");
            t.Fg = Hex("#111418");
            t.Muted = Hex("#3F4A59");
            t.Dim = Hex("#6C7480");
            t.Border = Hex("#D4DAE3");
            t.BorderStrong = Hex("#BFC7D2");
            t.Success = Hex("#059669");
            t.Error = Hex("#DC2626");
            t.Warning = Hex("#B45309");
            t.MeterBg = Hex("#E4E8EE");

            t.ChipOkBg = Hex("#E8F8F1");
            t.ChipOkFg = Hex("#047857");
            t.ChipOkBorder = Hex("#A7E3CB");
            t.ChipErrBg = Hex("#FDEEEE");
            t.ChipErrFg = Hex("#B91C1C");
            t.ChipErrBorder = Hex("#F5C2C2");
            t.ChipWarnBg = Hex("#FEF6E7");
            t.ChipWarnFg = Hex("#92400E");
            t.ChipWarnBorder = Hex("#F2D08A");
            t.ChipInfoBg = Hex("#EEEBFF");
            t.ChipInfoFg = Hex("#4C1D95");
            t.ChipInfoBorder = Hex("#CFC4FF");
            t.ChipIdleBg = Hex("#EFF2F6");
            t.ChipIdleFg = Hex("#475569");
            t.ChipIdleBorder = Hex("#D4DAE3");
            return t;
        }

        public static string FontFamilyName
        {
            get
            {
                if (_fontFamily == null)
                {
                    string candidate = "Microsoft YaHei UI";
                    bool found = false;
                    try
                    {
                        foreach (FontFamily family in FontFamily.Families)
                        {
                            if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                    _fontFamily = found ? candidate : "Segoe UI";
                }
                return _fontFamily;
            }
        }

        public static Font GetFont(float size, FontStyle style)
        {
            string key = size.ToString() + "|" + ((int)style).ToString();
            Font cached = FontCache.ContainsKey(key) ? FontCache[key] : null;
            if (cached != null)
            {
                return cached;
            }
            Font font = new Font(FontFamilyName, size, style, GraphicsUnit.Point);
            FontCache[key] = font;
            return font;
        }

        public static Color ChipBg(string kind)
        {
            if (kind == "ok") return _current.ChipOkBg;
            if (kind == "err") return _current.ChipErrBg;
            if (kind == "warn") return _current.ChipWarnBg;
            if (kind == "info") return _current.ChipInfoBg;
            return _current.ChipIdleBg;
        }

        public static Color ChipFg(string kind)
        {
            if (kind == "ok") return _current.ChipOkFg;
            if (kind == "err") return _current.ChipErrFg;
            if (kind == "warn") return _current.ChipWarnFg;
            if (kind == "info") return _current.ChipInfoFg;
            return _current.ChipIdleFg;
        }

        public static Color ChipBorder(string kind)
        {
            if (kind == "ok") return _current.ChipOkBorder;
            if (kind == "err") return _current.ChipErrBorder;
            if (kind == "warn") return _current.ChipWarnBorder;
            if (kind == "info") return _current.ChipInfoBorder;
            return _current.ChipIdleBorder;
        }

        /// <summary>把当前主题应用到标准 WinForms 控件（自定义控件自行取色并重绘）。</summary>
        public static void Apply(Control root)
        {
            if (root == null)
            {
                return;
            }
            ThemeColors t = _current;

            if (root is RoundedButton || root is PillToggle || root is StatusChip ||
                root is ThinProgressBar || root is DropZone || root is CardPanel)
            {
                root.BackColor = Color.Transparent;
                root.Invalidate();
            }
            else if (root is Form)
            {
                root.BackColor = t.Bg;
                root.ForeColor = t.Fg;
            }
            else if (root is Label)
            {
                root.ForeColor = t.Fg;
                root.BackColor = Color.Transparent;
            }
            else if (root is TextBox || root is RichTextBox)
            {
                root.BackColor = t.Video;
                root.ForeColor = t.Fg;
            }
            else if (root is ListBox)
            {
                root.BackColor = t.Video;
                root.ForeColor = t.Fg;
            }
            else if (root is ComboBox)
            {
                ComboBox combo = (ComboBox)root;
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = t.Video;
                combo.ForeColor = t.Fg;
            }
            else if (root is NumericUpDown)
            {
                root.BackColor = t.Video;
                root.ForeColor = t.Fg;
            }
            else if (root is CheckBox || root is RadioButton)
            {
                root.BackColor = Color.Transparent;
                root.ForeColor = t.Muted;
            }
            else if (root is Button)
            {
                Button button = (Button)root;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = t.Elev;
                button.ForeColor = t.Muted;
                button.FlatAppearance.BorderColor = t.Border;
            }
            else if (root is Panel || root is GroupBox)
            {
                root.BackColor = t.Bg;
                root.ForeColor = t.Muted;
            }

            if (root is GroupBox)
            {
                root.ForeColor = t.Muted;
            }

            for (int i = 0; i < root.Controls.Count; i++)
            {
                Apply(root.Controls[i]);
            }
        }
    }
}
