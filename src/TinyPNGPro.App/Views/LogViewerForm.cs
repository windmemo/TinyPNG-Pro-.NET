using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TinyPNGPro.App.Controls;
using TinyPNGPro.App.Theme;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.App.Views
{
    public sealed class LogViewerForm : Form
    {
        private readonly AppConfig _config;

        private ComboBox _dateBox;
        private TextBox _logBox;
        private Label _dateLabel;
        private Label _statusLabel;
        private RoundedButton _refreshButton;
        private RoundedButton _clearButton;
        private RoundedButton _closeButton;

        public LogViewerForm(AppConfig config)
        {
            _config = config;

            SuspendLayout();
            Text = "日志查看 - " + AppInfo.Title;
            ClientSize = new Size(820, 620);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = AppTheme.Current.Bg;
            ForeColor = AppTheme.Current.Fg;
            Padding = new Padding(16);
            ShowInTaskbar = true;

            BuildUi();
            RefreshDates();
            ApplyTheme();
            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildUi()
        {
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 40;
            top.BackColor = AppTheme.Current.Bg;

            _dateLabel = new Label();
            _dateLabel.Text = "日志日期";
            _dateLabel.AutoSize = true;
            _dateLabel.Font = AppTheme.GetFont(8.5f, FontStyle.Bold);
            _dateLabel.ForeColor = AppTheme.Current.Muted;
            _dateLabel.BackColor = Color.Transparent;
            _dateLabel.Location = new Point(0, 8);
            top.Controls.Add(_dateLabel);

            _dateBox = new ComboBox();
            _dateBox.Location = new Point(64, 4);
            _dateBox.Size = new Size(160, 24);
            _dateBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _dateBox.FlatStyle = FlatStyle.Flat;
            _dateBox.BackColor = AppTheme.Current.Video;
            _dateBox.ForeColor = AppTheme.Current.Fg;
            _dateBox.SelectedIndexChanged += delegate { LoadLog(); };
            top.Controls.Add(_dateBox);

            _refreshButton = new RoundedButton();
            _refreshButton.Kind = RoundedButtonKind.Ghost;
            _refreshButton.Text = "刷新";
            _refreshButton.Size = new Size(68, 28);
            _refreshButton.Location = new Point(236, 2);
            _refreshButton.Click += delegate { RefreshDates(); LoadLog(); };
            top.Controls.Add(_refreshButton);

            _clearButton = new RoundedButton();
            _clearButton.Kind = RoundedButtonKind.Danger;
            _clearButton.Text = "清空日志";
            _clearButton.Size = new Size(88, 28);
            _clearButton.Location = new Point(312, 2);
            _clearButton.Click += delegate { ClearCurrentLog(); };
            top.Controls.Add(_clearButton);

            _closeButton = new RoundedButton();
            _closeButton.Kind = RoundedButtonKind.Ghost;
            _closeButton.Text = "关闭";
            _closeButton.Size = new Size(68, 28);
            _closeButton.Click += delegate { Close(); };
            top.Controls.Add(_closeButton);

            _statusLabel = new Label();
            _statusLabel.Text = string.Empty;
            _statusLabel.AutoSize = false;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.Font = AppTheme.GetFont(8f, FontStyle.Regular);
            _statusLabel.ForeColor = AppTheme.Current.Dim;
            _statusLabel.BackColor = Color.Transparent;
            _statusLabel.Dock = DockStyle.Bottom;
            _statusLabel.Height = 22;

            _logBox = new TextBox();
            _logBox.Dock = DockStyle.Fill;
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Both;
            _logBox.WordWrap = false;
            _logBox.BorderStyle = BorderStyle.FixedSingle;
            _logBox.BackColor = AppTheme.Current.Video;
            _logBox.ForeColor = AppTheme.Current.Fg;
            _logBox.Font = new Font("Consolas", 9.5f, FontStyle.Regular);

            Controls.Add(_logBox);
            Controls.Add(_statusLabel);
            Controls.Add(top);

            Resize += delegate
            {
                _closeButton.Location = new Point(ClientSize.Width - Padding.Right - _closeButton.Width, 2);
            };
        }

        private string LogDirectory
        {
            get { return AppPaths.LogDirectory; }
        }

        private void RefreshDates()
        {
            string selected = _dateBox.SelectedItem == null
                ? null
                : _dateBox.SelectedItem.ToString();
            _dateBox.Items.Clear();

            List<string> dates = new List<string>();
            try
            {
                if (Directory.Exists(LogDirectory))
                {
                    string[] files = Directory.GetFiles(LogDirectory, "*.log");
                    for (int i = 0; i < files.Length; i++)
                    {
                        string name = Path.GetFileNameWithoutExtension(files[i]);
                        dates.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "读取日志目录失败：" + ex.Message;
            }
            dates.Sort();
            dates.Reverse();
            for (int i = 0; i < dates.Count; i++)
            {
                _dateBox.Items.Add(dates[i]);
            }

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (selected != null && _dateBox.Items.Contains(selected))
            {
                _dateBox.SelectedItem = selected;
            }
            else if (_dateBox.Items.Contains(today))
            {
                _dateBox.SelectedItem = today;
            }
            else if (_dateBox.Items.Count > 0)
            {
                _dateBox.SelectedIndex = 0;
            }
            else
            {
                _logBox.Text = "暂无日志。启用日志记录并执行压缩后，将在此显示。";
                _statusLabel.Text = "日志目录：" + LogDirectory;
            }
        }

        private void LoadLog()
        {
            if (_dateBox.SelectedItem == null)
            {
                return;
            }
            string date = _dateBox.SelectedItem.ToString();
            string path = Path.Combine(LogDirectory, date + ".log");
            try
            {
                if (!File.Exists(path))
                {
                    _logBox.Text = "未找到日志文件：" + path;
                    _statusLabel.Text = string.Empty;
                    return;
                }
                string text = File.ReadAllText(path);
                _logBox.Text = text;
                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.ScrollToCaret();
                long size = new FileInfo(path).Length;
                _statusLabel.Text = "文件：" + path + "    大小：" + PathUtil.FormatFileSize(size);
            }
            catch (Exception ex)
            {
                _logBox.Text = "读取日志失败：" + ex.Message;
            }
        }

        private void ClearCurrentLog()
        {
            if (_dateBox.SelectedItem == null)
            {
                return;
            }
            string date = _dateBox.SelectedItem.ToString();
            string path = Path.Combine(LogDirectory, date + ".log");
            if (!File.Exists(path))
            {
                return;
            }
            DialogResult result = MessageBox.Show(this,
                "确定清空 " + date + " 的日志文件？此操作不可恢复。",
                AppInfo.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
            try
            {
                File.Delete(path);
                RefreshDates();
                LoadLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "清空日志失败：" + ex.Message,
                    AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ApplyTheme()
        {
            ThemeColors t = AppTheme.Current;
            BackColor = t.Bg;
            ForeColor = t.Fg;
            AppTheme.Apply(this);
            _dateLabel.ForeColor = t.Muted;
            _statusLabel.ForeColor = t.Dim;
            _logBox.BackColor = t.Video;
            _logBox.ForeColor = t.Fg;
            Invalidate(true);
        }
    }
}
