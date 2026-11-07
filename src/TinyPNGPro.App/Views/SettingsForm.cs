using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using TinyPNGPro.App.Controls;
using TinyPNGPro.App.Theme;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Startup;
using TinyPNGPro.Core.TinyPng;

namespace TinyPNGPro.App.Views
{
    public sealed class SettingsForm : Form
    {
        private readonly AppHost _host;
        private readonly AppConfig _config;

        private RoundedButton _tabApi;
        private RoundedButton _tabAdvanced;
        private RoundedButton _tabAbout;
        private Panel _contentHost;
        private Panel _apiPanel;
        private Panel _advancedPanel;
        private Panel _aboutPanel;
        private RoundedButton _saveButton;
        private RoundedButton _cancelButton;

        private readonly TextBox[] _keyBoxes = new TextBox[3];
        private readonly RadioButton[] _keyRadios = new RadioButton[3];
        private readonly Button[] _validateButtons = new Button[3];
        private readonly Label[] _validateLabels = new Label[3];

        private CheckBox _replaceCheck;
        private CheckBox _onlyPngCheck;
        private TextBox _outputPathBox;
        private ListBox _watchFolderList;
        private CheckBox _autoStartCheck;
        private CheckBox _loggingCheck;
        private CheckBox _topmostCheck;
        private NumericUpDown _timeoutInput;
        private NumericUpDown _notificationInput;
        private RadioButton _darkRadio;
        private RadioButton _lightRadio;

        private readonly List<Label> _mutedLabels = new List<Label>();
        private readonly List<Label> _dimLabels = new List<Label>();

        private string _activeTab = "api";

        public SettingsForm(AppHost host)
        {
            _host = host;
            _config = host.Config;

            SuspendLayout();
            Text = "设置 - " + AppInfo.Title;
            ClientSize = new Size(600, 640);
            MinimumSize = new Size(560, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = AppTheme.Current.Bg;
            ForeColor = AppTheme.Current.Fg;
            ShowInTaskbar = true;
            Padding = new Padding(16);

            BuildUi();
            LoadFromConfig();
            SwitchTab("api");
            ApplyTheme();
            ResumeLayout(false);
            PerformLayout();
        }

        // ---------------- UI ----------------

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 34;
            header.BackColor = AppTheme.Current.Bg;

            Label title = new Label();
            title.Text = "设置";
            title.AutoSize = true;
            title.Font = AppTheme.GetFont(13f, FontStyle.Bold);
            title.ForeColor = AppTheme.Current.Fg;
            title.BackColor = Color.Transparent;
            title.Location = new Point(0, 2);
            header.Controls.Add(title);

            FlowLayoutPanel tabs = new FlowLayoutPanel();
            tabs.Dock = DockStyle.Top;
            tabs.Height = 44;
            tabs.WrapContents = false;
            tabs.BackColor = AppTheme.Current.Bg;
            tabs.Padding = new Padding(0, 6, 0, 0);

            _tabApi = CreateTabButton("API 设置");
            _tabAdvanced = CreateTabButton("高级设置");
            _tabAbout = CreateTabButton("关于");
            _tabApi.Click += delegate { SwitchTab("api"); };
            _tabAdvanced.Click += delegate { SwitchTab("advanced"); };
            _tabAbout.Click += delegate { SwitchTab("about"); };
            tabs.Controls.Add(_tabApi);
            tabs.Controls.Add(_tabAdvanced);
            tabs.Controls.Add(_tabAbout);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 52;
            footer.BackColor = AppTheme.Current.Bg;

            _saveButton = new RoundedButton();
            _saveButton.Kind = RoundedButtonKind.Primary;
            _saveButton.Text = "保存";
            _saveButton.Size = new Size(88, 32);
            _saveButton.Click += delegate { SaveSettings(); };

            _cancelButton = new RoundedButton();
            _cancelButton.Kind = RoundedButtonKind.Ghost;
            _cancelButton.Text = "取消";
            _cancelButton.Size = new Size(88, 32);
            _cancelButton.Click += delegate { Close(); };

            footer.Controls.Add(_saveButton);
            footer.Controls.Add(_cancelButton);
            footer.Resize += delegate
            {
                _saveButton.Location = new Point(footer.ClientSize.Width - _saveButton.Width, 10);
                _cancelButton.Location = new Point(
                    _saveButton.Left - _cancelButton.Width - 8, 10);
            };

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = AppTheme.Current.Bg;

            _apiPanel = BuildApiPanel();
            _advancedPanel = BuildAdvancedPanel();
            _aboutPanel = BuildAboutPanel();
            _contentHost.Controls.Add(_aboutPanel);
            _contentHost.Controls.Add(_advancedPanel);
            _contentHost.Controls.Add(_apiPanel);

            Controls.Add(_contentHost);
            Controls.Add(footer);
            Controls.Add(tabs);
            Controls.Add(header);
        }

        private RoundedButton CreateTabButton(string text)
        {
            RoundedButton button = new RoundedButton();
            button.Kind = RoundedButtonKind.Ghost;
            button.Text = text;
            button.Size = new Size(92, 30);
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private Panel BuildApiPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = AppTheme.Current.Bg;

            Label section = CreateLabel("API 设置", 10f, true, AppTheme.Current.Muted);
            section.Location = new Point(0, 0);
            panel.Controls.Add(section);
            _mutedLabels.Add(section);

            Label activeHint = CreateLabel("选择当前使用的密钥，压缩任务将使用该密钥。", 8.5f, false, AppTheme.Current.Dim);
            activeHint.Location = new Point(0, 24);
            panel.Controls.Add(activeHint);
            _dimLabels.Add(activeHint);

            for (int i = 0; i < 3; i++)
            {
                int y = 52 + i * 48;

                RadioButton radio = new RadioButton();
                radio.Text = string.Empty;
                radio.AutoSize = true;
                radio.Location = new Point(0, y + 6);
                radio.BackColor = Color.Transparent;
                radio.ForeColor = AppTheme.Current.Muted;
                panel.Controls.Add(radio);
                _keyRadios[i] = radio;

                Label caption = CreateLabel("Key " + (i + 1), 8.5f, false, AppTheme.Current.Muted);
                caption.Location = new Point(24, y + 7);
                panel.Controls.Add(caption);
                _mutedLabels.Add(caption);

                TextBox box = new TextBox();
                box.Location = new Point(92, y + 2);
                box.Size = new Size(330, 24);
                box.BorderStyle = BorderStyle.FixedSingle;
                box.BackColor = AppTheme.Current.Video;
                box.ForeColor = AppTheme.Current.Fg;
                box.Font = AppTheme.GetFont(9f, FontStyle.Regular);
                box.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                panel.Controls.Add(box);
                _keyBoxes[i] = box;

                Button validate = new Button();
                validate.Text = "验证";
                validate.Size = new Size(64, 26);
                validate.Location = new Point(430, y + 2);
                validate.FlatStyle = FlatStyle.Flat;
                validate.FlatAppearance.BorderColor = AppTheme.Current.Border;
                validate.BackColor = AppTheme.Current.Elev;
                validate.ForeColor = AppTheme.Current.Muted;
                validate.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                panel.Controls.Add(validate);
                _validateButtons[i] = validate;

                Label status = CreateLabel(string.Empty, 8f, false, AppTheme.Current.Dim);
                status.Location = new Point(92, y + 30);
                status.AutoSize = false;
                status.Size = new Size(340, 16);
                panel.Controls.Add(status);
                _validateLabels[i] = status;
                _dimLabels.Add(status);

                int index = i + 1;
                radio.CheckedChanged += delegate
                {
                    if (radio.Checked)
                    {
                        SelectActiveKey(index);
                    }
                };
                validate.Click += delegate { ValidateKey(index - 1); };
            }

            Label quota = CreateLabel("免费额度每月 500 次，同一密钥共享额度。", 8.5f, false, AppTheme.Current.Dim);
            quota.Location = new Point(0, 206);
            panel.Controls.Add(quota);
            _dimLabels.Add(quota);

            Label security = CreateLabel("密钥以明文保存在 exe 同目录的 config.ini（与原版一致）。", 8.5f, false, AppTheme.Current.Dim);
            security.Location = new Point(0, 230);
            security.AutoSize = false;
            security.Size = new Size(540, 22);
            panel.Controls.Add(security);
            _dimLabels.Add(security);

            return panel;
        }

        private Panel BuildAdvancedPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = AppTheme.Current.Bg;
            panel.AutoScroll = true;

            int y = 0;
            Label compression = CreateLabel("压缩设置", 10f, true, AppTheme.Current.Muted);
            compression.Location = new Point(0, y);
            panel.Controls.Add(compression);
            _mutedLabels.Add(compression);

            _replaceCheck = new CheckBox();
            _replaceCheck.Text = "替换原文件（关闭时输出到指定目录，文件名加 _compressed）";
            _replaceCheck.AutoSize = true;
            _replaceCheck.Location = new Point(0, y + 26);
            _replaceCheck.BackColor = Color.Transparent;
            _replaceCheck.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_replaceCheck);

            _onlyPngCheck = new CheckBox();
            _onlyPngCheck.Text = "仅压缩 PNG 文件";
            _onlyPngCheck.AutoSize = true;
            _onlyPngCheck.Location = new Point(0, y + 52);
            _onlyPngCheck.BackColor = Color.Transparent;
            _onlyPngCheck.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_onlyPngCheck);

            Label outputLabel = CreateLabel("输出目录（留空则输出到原目录）", 8.5f, false, AppTheme.Current.Muted);
            outputLabel.Location = new Point(0, y + 86);
            panel.Controls.Add(outputLabel);
            _mutedLabels.Add(outputLabel);

            _outputPathBox = new TextBox();
            _outputPathBox.Location = new Point(0, y + 106);
            _outputPathBox.Size = new Size(452, 24);
            _outputPathBox.BorderStyle = BorderStyle.FixedSingle;
            _outputPathBox.BackColor = AppTheme.Current.Video;
            _outputPathBox.ForeColor = AppTheme.Current.Fg;
            _outputPathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            panel.Controls.Add(_outputPathBox);

            Button browse = new Button();
            browse.Text = "浏览…";
            browse.Size = new Size(92, 26);
            browse.Location = new Point(464, y + 106);
            browse.FlatStyle = FlatStyle.Flat;
            browse.FlatAppearance.BorderColor = AppTheme.Current.Border;
            browse.BackColor = AppTheme.Current.Elev;
            browse.ForeColor = AppTheme.Current.Muted;
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            browse.Click += delegate { BrowseOutputFolder(); };
            panel.Controls.Add(browse);

            y = 148;
            Label watch = CreateLabel("监控文件夹（自动压缩，非递归）", 10f, true, AppTheme.Current.Muted);
            watch.Location = new Point(0, y);
            panel.Controls.Add(watch);
            _mutedLabels.Add(watch);

            _watchFolderList = new ListBox();
            _watchFolderList.Location = new Point(0, y + 24);
            _watchFolderList.Size = new Size(556, 86);
            _watchFolderList.BorderStyle = BorderStyle.FixedSingle;
            _watchFolderList.BackColor = AppTheme.Current.Video;
            _watchFolderList.ForeColor = AppTheme.Current.Fg;
            _watchFolderList.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            panel.Controls.Add(_watchFolderList);

            RoundedButton addFolder = new RoundedButton();
            addFolder.Kind = RoundedButtonKind.Ghost;
            addFolder.Text = "添加文件夹";
            addFolder.Size = new Size(100, 28);
            addFolder.Location = new Point(0, y + 118);
            addFolder.Click += delegate { AddWatchFolder(); };
            panel.Controls.Add(addFolder);

            RoundedButton removeFolder = new RoundedButton();
            removeFolder.Kind = RoundedButtonKind.Ghost;
            removeFolder.Text = "移除选中";
            removeFolder.Size = new Size(100, 28);
            removeFolder.Location = new Point(108, y + 118);
            removeFolder.Click += delegate { RemoveWatchFolder(); };
            panel.Controls.Add(removeFolder);

            y = 300;
            Label system = CreateLabel("系统设置", 10f, true, AppTheme.Current.Muted);
            system.Location = new Point(0, y);
            panel.Controls.Add(system);
            _mutedLabels.Add(system);

            _autoStartCheck = new CheckBox();
            _autoStartCheck.Text = "开机自动启动";
            _autoStartCheck.AutoSize = true;
            _autoStartCheck.Location = new Point(0, y + 24);
            _autoStartCheck.BackColor = Color.Transparent;
            _autoStartCheck.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_autoStartCheck);

            _loggingCheck = new CheckBox();
            _loggingCheck.Text = "启用日志记录";
            _loggingCheck.AutoSize = true;
            _loggingCheck.Location = new Point(140, y + 24);
            _loggingCheck.BackColor = Color.Transparent;
            _loggingCheck.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_loggingCheck);

            _topmostCheck = new CheckBox();
            _topmostCheck.Text = "主窗口置顶";
            _topmostCheck.AutoSize = true;
            _topmostCheck.Location = new Point(280, y + 24);
            _topmostCheck.BackColor = Color.Transparent;
            _topmostCheck.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_topmostCheck);

            y = 348;
            Label request = CreateLabel("请求与通知", 10f, true, AppTheme.Current.Muted);
            request.Location = new Point(0, y);
            panel.Controls.Add(request);
            _mutedLabels.Add(request);

            Label timeoutLabel = CreateLabel("请求超时（秒）", 8.5f, false, AppTheme.Current.Muted);
            timeoutLabel.Location = new Point(0, y + 24);
            panel.Controls.Add(timeoutLabel);
            _mutedLabels.Add(timeoutLabel);

            _timeoutInput = new NumericUpDown();
            _timeoutInput.Minimum = 10;
            _timeoutInput.Maximum = 300;
            _timeoutInput.Location = new Point(104, y + 22);
            _timeoutInput.Size = new Size(70, 24);
            _timeoutInput.BackColor = AppTheme.Current.Video;
            _timeoutInput.ForeColor = AppTheme.Current.Fg;
            _timeoutInput.BorderStyle = BorderStyle.FixedSingle;
            panel.Controls.Add(_timeoutInput);

            Label notificationLabel = CreateLabel("托盘通知时长（秒）", 8.5f, false, AppTheme.Current.Muted);
            notificationLabel.Location = new Point(200, y + 24);
            panel.Controls.Add(notificationLabel);
            _mutedLabels.Add(notificationLabel);

            _notificationInput = new NumericUpDown();
            _notificationInput.Minimum = 1;
            _notificationInput.Maximum = 30;
            _notificationInput.Location = new Point(330, y + 22);
            _notificationInput.Size = new Size(70, 24);
            _notificationInput.BackColor = AppTheme.Current.Video;
            _notificationInput.ForeColor = AppTheme.Current.Fg;
            _notificationInput.BorderStyle = BorderStyle.FixedSingle;
            panel.Controls.Add(_notificationInput);

            Label themeLabel = CreateLabel("主题外观", 8.5f, false, AppTheme.Current.Muted);
            themeLabel.Location = new Point(0, y + 58);
            panel.Controls.Add(themeLabel);
            _mutedLabels.Add(themeLabel);

            _darkRadio = new RadioButton();
            _darkRadio.Text = "夜间";
            _darkRadio.AutoSize = true;
            _darkRadio.Location = new Point(104, y + 56);
            _darkRadio.BackColor = Color.Transparent;
            _darkRadio.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_darkRadio);

            _lightRadio = new RadioButton();
            _lightRadio.Text = "明亮";
            _lightRadio.AutoSize = true;
            _lightRadio.Location = new Point(176, y + 56);
            _lightRadio.BackColor = Color.Transparent;
            _lightRadio.ForeColor = AppTheme.Current.Muted;
            panel.Controls.Add(_lightRadio);

            // 输出路径框与监控列表原来用固定像素宽度，窗口缩到最小尺寸时右侧会被裁掉；
            // 这里跟随面板可用宽度重排，浏览按钮始终贴在输入框右侧。
            panel.Resize += delegate
            {
                int available = Math.Max(220, panel.ClientSize.Width - 10);
                _outputPathBox.Width = Math.Max(160, available - 104);
                browse.Location = new Point(_outputPathBox.Right + 12, browse.Location.Y);
                _watchFolderList.Width = available;
            };

            return panel;
        }

        private Panel BuildAboutPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = AppTheme.Current.Bg;

            RichTextBox text = new RichTextBox();
            text.Dock = DockStyle.Fill;
            text.ReadOnly = true;
            text.BorderStyle = BorderStyle.None;
            text.BackColor = AppTheme.Current.Video;
            text.ForeColor = AppTheme.Current.Fg;
            text.Font = AppTheme.GetFont(9f, FontStyle.Regular);
            text.ScrollBars = RichTextBoxScrollBars.Vertical;
            text.Text = BuildAboutText();
            panel.Controls.Add(text);
            return panel;
        }

        private static string BuildAboutText()
        {
            return AppInfo.AboutText();
        }

        private Label CreateLabel(string text, float size, bool bold, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = AppTheme.GetFont(size, bold ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }

        // ---------------- 数据 ----------------

        private void LoadFromConfig()
        {
            string[] keys = _config.GetApiKeys();
            int active = _config.GetActiveKey();
            for (int i = 0; i < 3; i++)
            {
                _keyBoxes[i].Text = keys[i];
                _keyRadios[i].Checked = (active == i + 1);
            }

            _replaceCheck.Checked = _config.GetReplaceOriginal();
            _onlyPngCheck.Checked = _config.GetOnlyPng();
            _outputPathBox.Text = _config.GetOutputPath();

            _watchFolderList.Items.Clear();
            string[] folders = _config.GetWatchFolders();
            for (int i = 0; i < folders.Length; i++)
            {
                _watchFolderList.Items.Add(folders[i]);
            }

            _autoStartCheck.Checked = AutoStartService.IsEnabled();
            _loggingCheck.Checked = _config.GetLoggingEnabled();
            _topmostCheck.Checked = _config.GetAlwaysOnTop();
            _timeoutInput.Value = _config.GetTimeout();
            _notificationInput.Value = _config.GetNotificationDuration();

            bool dark = _config.GetTheme() == "dark";
            _darkRadio.Checked = dark;
            _lightRadio.Checked = !dark;
        }

        private void SelectActiveKey(int index)
        {
            if (index < 1 || index > 3)
            {
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                _keyRadios[i].Checked = (i + 1 == index);
            }
        }

        private void ValidateKey(int index)
        {
            string key = _keyBoxes[index].Text.Trim();
            Label status = _validateLabels[index];
            if (key.Length == 0)
            {
                status.Text = "未填写密钥";
                status.ForeColor = AppTheme.Current.Dim;
                return;
            }
            status.Text = "验证中…";
            status.ForeColor = AppTheme.Current.Muted;
            Thread thread = new Thread(delegate()
            {
                bool valid = new TinyPngClient(key).ValidateApiKey();
                SafeUi(delegate
                {
                    status.Text = valid ? "密钥有效" : "密钥无效或网络异常";
                    status.ForeColor = valid ? AppTheme.Current.Success : AppTheme.Current.Error;
                });
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void SafeUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
            }
        }

        private void BrowseOutputFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择压缩输出目录";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _outputPathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void AddWatchFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择要自动监控的文件夹";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string path = dialog.SelectedPath;
                    for (int i = 0; i < _watchFolderList.Items.Count; i++)
                    {
                        if (string.Equals(_watchFolderList.Items[i].ToString(), path,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                    _watchFolderList.Items.Add(path);
                }
            }
        }

        private void RemoveWatchFolder()
        {
            int index = _watchFolderList.SelectedIndex;
            if (index >= 0)
            {
                _watchFolderList.Items.RemoveAt(index);
            }
        }

        private void SaveSettings()
        {
            Dictionary<string, string> api = new Dictionary<string, string>();
            for (int i = 0; i < 3; i++)
            {
                api["api_key_" + (i + 1)] = _keyBoxes[i].Text.Trim();
            }
            int active = 1;
            for (int i = 0; i < 3; i++)
            {
                if (_keyRadios[i].Checked)
                {
                    active = i + 1;
                }
            }
            api["active_key"] = active.ToString();
            _config.SetValues("API", api);

            Dictionary<string, string> compression = new Dictionary<string, string>();
            compression["replace_original"] = _replaceCheck.Checked ? "True" : "False";
            compression["only_png"] = _onlyPngCheck.Checked ? "True" : "False";
            compression["output_path"] = _outputPathBox.Text.Trim();
            _config.SetValues("Compression", compression);

            List<string> folders = new List<string>();
            for (int i = 0; i < _watchFolderList.Items.Count; i++)
            {
                string folder = _watchFolderList.Items[i].ToString().Trim();
                if (folder.Length > 0)
                {
                    folders.Add(folder);
                }
            }
            Dictionary<string, string> auto = new Dictionary<string, string>();
            auto["watch_folders"] = string.Join("|", folders.ToArray());
            _config.SetValues("AutoCompression", auto);

            Dictionary<string, string> settings = new Dictionary<string, string>();
            settings["timeout"] = ((int)_timeoutInput.Value).ToString();
            settings["notification_duration"] = ((int)_notificationInput.Value).ToString();
            settings["logging_enabled"] = _loggingCheck.Checked ? "True" : "False";
            settings["always_on_top"] = _topmostCheck.Checked ? "True" : "False";
            settings["theme"] = _darkRadio.Checked ? "dark" : "light";
            _config.SetValues("Settings", settings);

            Dictionary<string, string> startup = new Dictionary<string, string>();
            startup["auto_start"] = _autoStartCheck.Checked ? "True" : "False";
            _config.SetValues("Startup", startup);

            AutoStartService.SetEnabled(_autoStartCheck.Checked);
            _host.ApplySettings();
            DialogResult = DialogResult.OK;
            Close();
        }

        // ---------------- 主题与标签切换 ----------------

        private void SwitchTab(string tab)
        {
            _activeTab = tab;
            _apiPanel.Visible = tab == "api";
            _advancedPanel.Visible = tab == "advanced";
            _aboutPanel.Visible = tab == "about";
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            _tabApi.Kind = _activeTab == "api" ? RoundedButtonKind.Accent : RoundedButtonKind.Ghost;
            _tabAdvanced.Kind = _activeTab == "advanced" ? RoundedButtonKind.Accent : RoundedButtonKind.Ghost;
            _tabAbout.Kind = _activeTab == "about" ? RoundedButtonKind.Accent : RoundedButtonKind.Ghost;
            _tabApi.Invalidate();
            _tabAdvanced.Invalidate();
            _tabAbout.Invalidate();
        }

        public void ApplyTheme()
        {
            ThemeColors t = AppTheme.Current;
            BackColor = t.Bg;
            ForeColor = t.Fg;
            AppTheme.Apply(this);

            for (int i = 0; i < _mutedLabels.Count; i++)
            {
                _mutedLabels[i].ForeColor = t.Muted;
            }
            for (int i = 0; i < _dimLabels.Count; i++)
            {
                _dimLabels[i].ForeColor = t.Dim;
            }
            UpdateTabStyles();
            Invalidate(true);
        }
    }
}
