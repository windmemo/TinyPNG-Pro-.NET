using System;
using System.Drawing;
using System.Windows.Forms;
using TinyPNGPro.App.Controls;
using TinyPNGPro.App.Theme;

namespace TinyPNGPro.App.Views
{
    public sealed class AboutForm : Form
    {
        private Label _titleLabel;
        private Label _subtitleLabel;
        private RichTextBox _textBox;
        private RoundedButton _closeButton;

        // 关于页内部的小节标题，统一加粗并以主题主色显示。
        private static readonly string[] SectionHeaders =
        {
            "▍这是什么",
            "▍能力范围",
            "▍技术栈",
            "▍构建方式",
            "▍数据与配置（均与 exe 同目录）",
            "▍交互与托盘"
        };

        public AboutForm()
        {
            SuspendLayout();
            Text = "帮助与关于 - " + AppInfo.Title;
            ClientSize = new Size(520, 600);
            MinimumSize = new Size(480, 540);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = AppTheme.Current.Bg;
            ForeColor = AppTheme.Current.Fg;
            Padding = new Padding(16);
            ShowInTaskbar = true;

            BuildUi();
            ApplyTheme();
            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 56;
            header.BackColor = AppTheme.Current.Bg;

            _titleLabel = new Label();
            _titleLabel.Text = AppInfo.Title;
            _titleLabel.AutoSize = true;
            _titleLabel.Font = AppTheme.GetFont(17f, FontStyle.Bold);
            _titleLabel.ForeColor = AppTheme.Current.Fg;
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.Location = new Point(0, 0);
            header.Controls.Add(_titleLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.Text = "构建于 " + AppInfo.ReleaseDate;
            _subtitleLabel.AutoSize = true;
            _subtitleLabel.Font = AppTheme.GetFont(8.5f, FontStyle.Regular);
            _subtitleLabel.ForeColor = AppTheme.Current.Dim;
            _subtitleLabel.BackColor = Color.Transparent;
            _subtitleLabel.Location = new Point(0, 30);
            header.Controls.Add(_subtitleLabel);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 48;
            footer.BackColor = AppTheme.Current.Bg;

            _closeButton = new RoundedButton();
            _closeButton.Kind = RoundedButtonKind.Primary;
            _closeButton.Text = "关闭";
            _closeButton.Size = new Size(88, 32);
            _closeButton.Click += delegate { Close(); };
            footer.Controls.Add(_closeButton);
            footer.Resize += delegate
            {
                _closeButton.Location = new Point(
                    Math.Max(0, (footer.ClientSize.Width - _closeButton.Width) / 2), 8);
            };

            _textBox = new RichTextBox();
            _textBox.Dock = DockStyle.Fill;
            _textBox.ReadOnly = true;
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = AppTheme.Current.Video;
            _textBox.ForeColor = AppTheme.Current.Fg;
            _textBox.Font = AppTheme.GetFont(9.5f, FontStyle.Regular);
            _textBox.Text = AppInfo.AboutText();
            _textBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _textBox.Select(0, 0);

            Controls.Add(_textBox);
            Controls.Add(footer);
            Controls.Add(header);
            ApplySectionStyle();
        }

        /// <summary>给小节标题加粗并染主色；正文继承主题前景色。</summary>
        private void ApplySectionStyle()
        {
            if (_textBox == null || _textBox.TextLength == 0)
            {
                return;
            }
            ThemeColors t = AppTheme.Current;
            Font headerFont = AppTheme.GetFont(9.5f, FontStyle.Bold);
            for (int i = 0; i < SectionHeaders.Length; i++)
            {
                int index = _textBox.Text.IndexOf(SectionHeaders[i], StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }
                _textBox.Select(index, SectionHeaders[i].Length);
                _textBox.SelectionFont = headerFont;
                _textBox.SelectionColor = t.Primary;
            }
            _textBox.Select(0, 0);
            _textBox.ScrollToCaret();
        }

        public void ApplyTheme()
        {
            ThemeColors t = AppTheme.Current;
            BackColor = t.Bg;
            ForeColor = t.Fg;
            AppTheme.Apply(this);
            _titleLabel.ForeColor = t.Fg;
            _subtitleLabel.ForeColor = t.Dim;
            _textBox.BackColor = t.Video;
            _textBox.ForeColor = t.Fg;
            ApplySectionStyle();
            Invalidate(true);
        }
    }
}
