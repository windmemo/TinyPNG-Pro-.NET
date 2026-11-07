using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TinyPNGPro.App.Controls;
using TinyPNGPro.App.Theme;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Pipeline;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.App.Views
{
    public sealed class MainForm : Form
    {
        private readonly AppHost _host;
        private readonly AppConfig _config;

        private Panel _header;
        private Label _brandTiny;
        private Label _brandPro;
        private PillToggle _topmostToggle;
        private RoundedButton _themeButton;
        private RoundedButton _helpButton;
        private RoundedButton _settingsButton;
        private RoundedButton _minButton;
        private RoundedButton _closeButton;
        private DropZone _dropZone;
        private CardPanel _resultCard;
        private StatusChip _statusChip;
        private Label _resultLabel;
        private Label _detailLabel;
        private Panel _statusBar;
        private PillToggle _autoToggle;
        private PillToggle _replaceToggle;
        private Label _queueLabel;
        private Label _countLabel;
        private RoundedButton _exitButton;
        private ThinProgressBar _meter;
        private ToolTip _toolTip;
        private ContextMenuStrip _contextMenu;
        private Region _windowRegion;
        private bool _suppressToggle;

        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;

        public MainForm(AppHost host)
        {
            _host = host;
            _config = host.Config;

            SuspendLayout();
            Text = AppInfo.Title;
            // 头部品牌 + 动作区需要约 540px 宽度，窗口最小宽度按实测内容定，
            // 避免缩放后控件互相覆盖（原 520px 会把「置顶」压到品牌文字下方）。
            ClientSize = new Size(560, 600);
            MinimumSize = new Size(548, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = AppTheme.Current.Bg;
            ForeColor = AppTheme.Current.Fg;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw, true);
            TrySetIcon();

            BuildUi();
            LoadFromConfig();
            ApplyTheme();

            _host.Queue.TaskStarted += OnTaskStarted;
            _host.Queue.TaskFinished += OnTaskFinished;
            _host.Queue.BatchFinished += OnBatchFinished;
            _host.Queue.QueueChanged += OnQueueChanged;

            ResumeLayout(false);
            PerformLayout();

            WindowDrag.Attach(this);
            BuildContextMenu();
            ApplyWindowRegion();
        }

        private void TrySetIcon()
        {
            try
            {
                if (_host.AppIcon != null)
                {
                    Icon = _host.AppIcon;
                }
            }
            catch
            {
            }
        }

        // ---------------- UI 构建 ----------------

        private void BuildUi()
        {
            Padding = new Padding(16, 14, 16, 14);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 1;
            grid.RowCount = 4;
            grid.BackColor = AppTheme.Current.Bg;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 116f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 74f));

            grid.Controls.Add(BuildHeader(), 0, 0);
            grid.Controls.Add(BuildDropZone(), 0, 1);
            grid.Controls.Add(BuildResultCard(), 0, 2);
            grid.Controls.Add(BuildStatusBar(), 0, 3);
            Controls.Add(grid);
        }

        private Control BuildHeader()
        {
            _header = new Panel();
            _header.Dock = DockStyle.Fill;
            _header.Margin = new Padding(0); // 默认 Margin=3 会让整行比下方卡片内缩 3px
            _header.BackColor = AppTheme.Current.Bg;

            // 品牌区 Dock=Fill、动作区 Dock=Right（先加品牌再加动作，Fill 最后结算剩余空间）。
            // 原先品牌是绝对坐标，窗口偏窄时会直接压住「置顶」。
            // 品牌放进独立面板：父级会裁剪，空间不足时只截断品牌，不会覆盖动作区。
            Panel brandHost = new Panel();
            brandHost.Dock = DockStyle.Fill;
            brandHost.Margin = new Padding(0);
            brandHost.BackColor = Color.Transparent;

            FlowLayoutPanel brand = new FlowLayoutPanel();
            brand.AutoSize = true;
            brand.WrapContents = false;
            brand.BackColor = Color.Transparent;

            _brandTiny = new Label();
            _brandTiny.Text = "TinyPNG";
            _brandTiny.AutoSize = true;
            _brandTiny.Font = AppTheme.GetFont(17f, FontStyle.Bold);
            _brandTiny.ForeColor = AppTheme.Current.Fg;
            _brandTiny.BackColor = Color.Transparent;
            _brandTiny.Margin = new Padding(0, 0, 4, 0);

            _brandPro = new Label();
            _brandPro.Text = "Pro";
            _brandPro.AutoSize = true;
            _brandPro.Font = AppTheme.GetFont(17f, FontStyle.Bold);
            _brandPro.ForeColor = AppTheme.Current.Primary2;
            _brandPro.BackColor = Color.Transparent;
            _brandPro.Margin = new Padding(0, 0, 0, 0);

            brand.Controls.Add(_brandTiny);
            brand.Controls.Add(_brandPro);
            brandHost.Controls.Add(brand);
            // 品牌与右侧控件同一垂直中线：46px 行高内居中。
            brand.Location = new Point(0, Math.Max(0, (46 - brand.PreferredSize.Height) / 2));

            // 顶部统一栅格：控件行高 26px，同组间距 6px，窗口按钮组与功能组间隔 12px。
            // 所有元素显式定位在同一基线，避免芯片/图标/开关三种控件各自按内容高度浮动。
            Panel actions = new Panel();
            actions.Dock = DockStyle.Right;
            actions.Margin = new Padding(0);
            actions.BackColor = Color.Transparent;

            _closeButton = new RoundedButton();
            _closeButton.Kind = RoundedButtonKind.Close;
            _closeButton.Text = "\u2715";
            _closeButton.Font = AppTheme.GetFont(10f, FontStyle.Bold);
            _closeButton.Click += delegate { Hide(); };

            _minButton = new RoundedButton();
            _minButton.Kind = RoundedButtonKind.Icon;
            _minButton.Text = "\u2212";
            _minButton.Font = AppTheme.GetFont(10.5f, FontStyle.Bold);
            _minButton.Click += delegate { WindowState = FormWindowState.Minimized; };

            _helpButton = new RoundedButton();
            _helpButton.Kind = RoundedButtonKind.Icon;
            _helpButton.Text = "?";
            _helpButton.Font = AppTheme.GetFont(10f, FontStyle.Bold);

            _settingsButton = new RoundedButton();
            _settingsButton.Kind = RoundedButtonKind.Ghost; // 与「主题」同款，头部不再出现第二个强调色
            _settingsButton.Text = "设置";

            _themeButton = new RoundedButton();
            _themeButton.Kind = RoundedButtonKind.Ghost;
            _themeButton.Text = "主题";

            _topmostToggle = new PillToggle();
            _topmostToggle.Text = "置顶";

            actions.Controls.Add(_topmostToggle);
            actions.Controls.Add(_themeButton);
            actions.Controls.Add(_settingsButton);
            actions.Controls.Add(_helpButton);
            actions.Controls.Add(_minButton);
            actions.Controls.Add(_closeButton);

            const int rowHeight = 26;
            const int rowY = (46 - rowHeight) / 2;
            const int chipWidth = 56;
            const int gap = 6;
            const int windowGap = 12;
            int x = 0;
            _topmostToggle.SetBounds(x, rowY, _topmostToggle.Width, rowHeight);
            x += _topmostToggle.Width + gap;
            _themeButton.SetBounds(x, rowY, chipWidth, rowHeight);
            x += chipWidth + gap;
            _settingsButton.SetBounds(x, rowY, chipWidth, rowHeight);
            x += chipWidth + gap;
            _helpButton.SetBounds(x, rowY, rowHeight, rowHeight);
            x += rowHeight + windowGap;
            _minButton.SetBounds(x, rowY, rowHeight, rowHeight);
            x += rowHeight + gap;
            _closeButton.SetBounds(x, rowY, rowHeight, rowHeight);
            actions.Width = x + rowHeight;

            _header.Controls.Add(brandHost);
            _header.Controls.Add(actions);

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_settingsButton, "打开设置");
            _toolTip.SetToolTip(_helpButton, "帮助与关于");
            _toolTip.SetToolTip(_themeButton, "切换夜间 / 明亮主题");
            _toolTip.SetToolTip(_topmostToggle, "窗口置顶");
            _toolTip.SetToolTip(_minButton, "最小化");
            _toolTip.SetToolTip(_closeButton, "隐藏到托盘");

            _settingsButton.Click += delegate { _host.ShowSettings(); };
            _helpButton.Click += delegate { _host.ShowAbout(); };
            _themeButton.Click += delegate { _host.ToggleTheme(); };
            _topmostToggle.CheckedChanged += delegate
            {
                if (_suppressToggle)
                {
                    return;
                }
                TopMost = _topmostToggle.Checked;
                _config.SetAlwaysOnTop(_topmostToggle.Checked);
            };

            return _header;
        }

        private Control BuildDropZone()
        {
            _dropZone = new DropZone();
            _dropZone.Dock = DockStyle.Fill;
            _dropZone.Margin = new Padding(0, 6, 0, 8);
            _dropZone.IconImage = LoadDropIcon();
            _dropZone.FilesDropped += HandleFiles;
            _dropZone.SelectButton.Click += delegate { SelectFiles(); };

            AllowDrop = true;
            return _dropZone;
        }

        private Control BuildResultCard()
        {
            _resultCard = new CardPanel();
            _resultCard.Dock = DockStyle.Fill;
            _resultCard.Margin = new Padding(0, 0, 0, 8);
            _resultCard.Resize += delegate { LayoutResult(); };

            _statusChip = new StatusChip();
            _statusChip.SetChip("idle", "就绪");

            _resultLabel = new Label();
            _resultLabel.Text = "拖动图片到此处开始压缩";
            _resultLabel.AutoSize = false;
            _resultLabel.TextAlign = ContentAlignment.MiddleCenter;
            _resultLabel.Font = AppTheme.GetFont(11.5f, FontStyle.Bold);
            _resultLabel.ForeColor = AppTheme.Current.Fg;
            _resultLabel.BackColor = Color.Transparent;

            _detailLabel = new Label();
            _detailLabel.Text = "支持 PNG / JPEG / WebP";
            _detailLabel.AutoSize = false;
            _detailLabel.TextAlign = ContentAlignment.MiddleCenter;
            _detailLabel.Font = AppTheme.GetFont(8.5f, FontStyle.Regular);
            _detailLabel.ForeColor = AppTheme.Current.Muted;
            _detailLabel.BackColor = Color.Transparent;
            _detailLabel.AutoEllipsis = true;

            _resultCard.Controls.Add(_statusChip);
            _resultCard.Controls.Add(_resultLabel);
            _resultCard.Controls.Add(_detailLabel);
            return _resultCard;
        }

        private Control BuildStatusBar()
        {
            _statusBar = new Panel();
            _statusBar.Dock = DockStyle.Fill;
            _statusBar.Margin = new Padding(0);
            _statusBar.BackColor = AppTheme.Current.Bg;
            _statusBar.Resize += delegate { LayoutStatus(); };

            _autoToggle = new PillToggle();
            _autoToggle.Text = "自动压缩";
            _autoToggle.CheckedChanged += delegate
            {
                if (_suppressToggle)
                {
                    return;
                }
                _config.SetAutoCompressionEnabled(_autoToggle.Checked);
                _host.RestartWatcherPerConfig();
                if (_autoToggle.Checked)
                {
                    if (_config.GetWatchFolders().Length == 0)
                    {
                        SetResult("warn", "缺少监控目录", "请先在设置中添加监控文件夹", "自动压缩需要至少一个有效目录");
                    }
                    else
                    {
                        SetResult("info", "监控已开启", "正在监控指定文件夹", "仅处理监控启动后新增的图片");
                    }
                }
            };

            _replaceToggle = new PillToggle();
            _replaceToggle.Text = "替换原文件";
            _replaceToggle.CheckedChanged += delegate
            {
                if (_suppressToggle)
                {
                    return;
                }
                _config.SetReplaceOriginal(_replaceToggle.Checked);
            };

            _queueLabel = new Label();
            _queueLabel.Text = "排队 0";
            _queueLabel.AutoSize = true;
            _queueLabel.Font = AppTheme.GetFont(8.5f, FontStyle.Regular);
            _queueLabel.ForeColor = AppTheme.Current.Dim;
            _queueLabel.BackColor = Color.Transparent;

            _countLabel = new Label();
            _countLabel.Text = "0/500";
            _countLabel.AutoSize = false;
            _countLabel.Width = 64;
            _countLabel.Height = 20;
            _countLabel.TextAlign = ContentAlignment.MiddleRight;
            _countLabel.Font = AppTheme.GetFont(8.5f, FontStyle.Bold);
            _countLabel.ForeColor = AppTheme.Current.Primary2;
            _countLabel.BackColor = Color.Transparent;

            _exitButton = new RoundedButton();
            _exitButton.Kind = RoundedButtonKind.Danger;
            _exitButton.Text = "退出";
            _exitButton.Size = new Size(54, 26);
            _exitButton.Click += delegate { _host.ExitApplication(); };

            _meter = new ThinProgressBar();
            _meter.MaxValue = 500;

            _statusBar.Controls.Add(_autoToggle);
            _statusBar.Controls.Add(_replaceToggle);
            _statusBar.Controls.Add(_queueLabel);
            _statusBar.Controls.Add(_countLabel);
            _statusBar.Controls.Add(_exitButton);
            _statusBar.Controls.Add(_meter);
            return _statusBar;
        }

        private Image LoadDropIcon()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Stream stream = assembly.GetManifestResourceStream("IconPng");
                if (stream == null)
                {
                    return null;
                }
                using (Stream input = stream)
                {
                    using (Image image = Image.FromStream(input))
                    {
                        return new Bitmap(image);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // ---------------- 布局与状态 ----------------

        private void LayoutResult()
        {
            if (_resultCard == null)
            {
                return;
            }
            int width = _resultCard.ClientSize.Width;
            _statusChip.Location = new Point(Math.Max(0, (width - _statusChip.Width) / 2), 14);
            _resultLabel.SetBounds(20, 46, Math.Max(60, width - 40), 28);
            _detailLabel.SetBounds(20, 76, Math.Max(60, width - 40), 22);
        }

        private void LayoutStatus()
        {
            if (_statusBar == null)
            {
                return;
            }
            int width = _statusBar.ClientSize.Width;
            int height = _statusBar.ClientSize.Height;
            // 底部状态条内所有控件按 26px 行高垂直居中对齐，避免文字与开关错位。
            const int rowHeight = 26;
            int rowY = Math.Max(0, (height - 6 - rowHeight) / 2);
            _autoToggle.Location = new Point(0, rowY);
            _replaceToggle.Location = new Point(_autoToggle.Width + 10, rowY);
            _queueLabel.Location = new Point(_replaceToggle.Right + 10,
                rowY + (rowHeight - _queueLabel.Height) / 2);
            _exitButton.Location = new Point(width - _exitButton.Width,
                rowY + (rowHeight - _exitButton.Height) / 2);
            _countLabel.Location = new Point(_exitButton.Left - _countLabel.Width - 10,
                rowY + (rowHeight - _countLabel.Height) / 2);
            _meter.SetBounds(0, height - 6, width, 6);
        }

        private void SetResult(string kind, string chip, string title, string detail)
        {
            _statusChip.SetChip(kind, chip);
            _resultLabel.Text = title;
            _detailLabel.Text = detail;
            LayoutResult();
        }

        private void UpdateCount()
        {
            int count = _config.GetCompressionCount();
            if (count < 0)
            {
                count = 0;
            }
            _countLabel.Text = count.ToString() + "/500";
            _meter.Value = count;
        }

        private void LoadFromConfig()
        {
            _suppressToggle = true;
            bool auto = _config.GetAutoCompressionEnabled();
            _autoToggle.Checked = auto;
            _replaceToggle.Checked = _config.GetReplaceOriginal();
            bool topmost = _config.GetAlwaysOnTop();
            _topmostToggle.Checked = topmost;
            TopMost = topmost;
            _suppressToggle = false;
            UpdateCount();
            _queueLabel.Text = "排队 " + _host.Queue.QueueSize.ToString();
        }

        public void RefreshFromConfig()
        {
            _suppressToggle = true;
            _autoToggle.Checked = _config.GetAutoCompressionEnabled();
            _replaceToggle.Checked = _config.GetReplaceOriginal();
            _topmostToggle.Checked = _config.GetAlwaysOnTop();
            TopMost = _config.GetAlwaysOnTop();
            _suppressToggle = false;
            UpdateCount();
            _queueLabel.Text = "排队 " + _host.Queue.QueueSize.ToString();
        }

        public void ApplyTheme()
        {
            ThemeColors t = AppTheme.Current;
            BackColor = t.Bg;
            ForeColor = t.Fg;
            AppTheme.Apply(this);

            _header.BackColor = t.Bg;
            _statusBar.BackColor = t.Bg;
            _brandTiny.ForeColor = t.Fg;
            _brandPro.ForeColor = t.Primary2;
            _resultLabel.ForeColor = t.Fg;
            _detailLabel.ForeColor = t.Muted;
            _queueLabel.ForeColor = t.Dim;
            _countLabel.ForeColor = t.Primary2;
            if (_contextMenu != null)
            {
                _contextMenu.BackColor = t.Card;
                for (int i = 0; i < _contextMenu.Items.Count; i++)
                {
                    _contextMenu.Items[i].ForeColor = t.Fg;
                }
            }
            _statusChip.Invalidate();
            _dropZone.Invalidate();
            _meter.Invalidate();
            LayoutResult();
            LayoutStatus();
            Invalidate(true);
        }

        // ---------------- 队列事件 ----------------

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

        private void OnTaskStarted(string name, bool isAuto)
        {
            SafeUi(delegate
            {
                SetResult("info", "压缩中", "正在处理 " + name, "上传与下载在后台执行，可继续添加任务");
            });
        }

        private void OnTaskFinished(string name, bool ok, string message, bool isAuto)
        {
            SafeUi(delegate
            {
                if (ok)
                {
                    SetResult("ok", "完成", "压缩完成：" + name, message);
                }
                else
                {
                    SetResult("err", "失败", "压缩失败：" + name, message);
                }
                UpdateCount();
            });
        }

        private void OnBatchFinished(BatchSummary summary, bool isAuto)
        {
            SafeUi(delegate
            {
                if (isAuto)
                {
                    return;
                }
                long saved = summary.OriginalSize - summary.CompressedSize;
                if (saved < 0)
                {
                    saved = 0;
                }
                string detail = "节省 " + PathUtil.FormatFileSize(saved) +
                                " · 压缩率 " + PathUtil.FormatRatio(summary.OriginalSize, summary.CompressedSize) +
                                " · 用时 " + string.Format("{0:F1}s", summary.Elapsed);
                if (summary.Failed == 0)
                {
                    SetResult("ok", "批量完成", "已完成 " + summary.Ok.ToString() + " 张图片", detail);
                }
                else
                {
                    SetResult("warn", "部分失败",
                        "成功 " + summary.Ok.ToString() + " 张 / 失败 " + summary.Failed.ToString() + " 张",
                        summary.Errors.Count > 0 ? summary.Errors[0] : detail);
                }
                UpdateCount();
            });
        }

        private void OnQueueChanged(int size)
        {
            SafeUi(delegate
            {
                _queueLabel.Text = "排队 " + size.ToString();
            });
        }

        // ---------------- 文件处理 ----------------

        private void SelectFiles()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要压缩的图片";
                dialog.Multiselect = true;
                dialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp|所有文件|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    HandleFiles(dialog.FileNames);
                }
            }
        }

        private void HandleFiles(string[] files)
        {
            if (files == null || files.Length == 0)
            {
                return;
            }
            bool onlyPng = _config.GetOnlyPng();
            List<string> accepted = new List<string>();
            int skipped = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (Directory.Exists(file))
                {
                    skipped++;
                    continue;
                }
                if (PathUtil.IsAllowedFile(file, onlyPng) && File.Exists(file))
                {
                    accepted.Add(file);
                }
                else
                {
                    skipped++;
                }
            }

            if (accepted.Count == 0)
            {
                SetResult("warn", "未加入任务",
                    "没有可压缩的图片",
                    onlyPng ? "当前设置仅压缩 PNG 文件" : "支持 PNG / JPEG / WebP 格式");
                return;
            }

            string batchId = _host.Queue.AddBatch(accepted.ToArray());
            if (batchId == null)
            {
                SetResult("warn", "未加入任务", "任务未能入队", "请检查文件是否存在且类型受支持");
                return;
            }

            SetResult("info", "已入队",
                "已加入 " + accepted.Count.ToString() + " 张图片",
                skipped > 0 ? "已跳过 " + skipped.ToString() + " 个不支持的项目" : "等待后台压缩");
        }

        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            if (drgevent.Data != null && drgevent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                drgevent.Effect = DragDropEffects.Copy;
                return;
            }
            base.OnDragEnter(drgevent);
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            try
            {
                if (drgevent.Data != null && drgevent.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])drgevent.Data.GetData(DataFormats.FileDrop);
                    HandleFiles(files);
                }
            }
            catch
            {
            }
            base.OnDragDrop(drgevent);
        }

        // ---------------- 窗口外观 / 交互 ----------------

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryEnableWin11Corners();
            ApplyWindowRegion();
        }

        private void TryEnableWin11Corners()
        {
            try
            {
                if (Environment.OSVersion.Version.Build >= 22000)
                {
                    int preference = 2; // DWMWCP_ROUND
                    DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
                }
            }
            catch
            {
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Normal)
            {
                ApplyWindowRegion();
            }
        }

        private void ApplyWindowRegion()
        {
            if (!IsHandleCreated || Width < 2 || Height < 2)
            {
                return;
            }
            // Win11 由 DWM 负责圆角；Win10 用 Region 兜底。
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                return;
            }
            GraphicsPath path = DrawUtil.Rounded(new Rectangle(0, 0, Width, Height), 12);
            Region region = new Region(path);
            path.Dispose();
            Region old = _windowRegion;
            _windowRegion = region;
            Region = region;
            if (old != null)
            {
                old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemeColors t = AppTheme.Current;
            Rectangle rect = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect, t.Bg, t.Soft, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
            using (Pen pen = new Pen(t.Border, 1))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                DrawUtil.DrawRounded(e.Graphics,
                    new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12, pen);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if (m.Result == (IntPtr)HTCLIENT)
                {
                    int lp = m.LParam.ToInt32();
                    int x = (short)(lp & 0xFFFF);
                    int y = (short)((lp >> 16) & 0xFFFF);
                    Point p = PointToClient(new Point(x, y));
                    int edge = 6;
                    bool left = p.X >= 0 && p.X <= edge;
                    bool right = p.X < ClientSize.Width && p.X >= ClientSize.Width - edge;
                    bool top = p.Y >= 0 && p.Y <= edge;
                    bool bottom = p.Y < ClientSize.Height && p.Y >= ClientSize.Height - edge;

                    int hit = 0;
                    if (top && left) hit = 13;          // HTTOPLEFT
                    else if (top && right) hit = 14;    // HTTOPRIGHT
                    else if (bottom && left) hit = 16;  // HTBOTTOMLEFT
                    else if (bottom && right) hit = 17; // HTBOTTOMRIGHT
                    else if (left) hit = 10;            // HTLEFT
                    else if (right) hit = 11;           // HTRIGHT
                    else if (top) hit = 12;             // HTTOP
                    else if (bottom) hit = 15;          // HTBOTTOM
                    if (hit != 0)
                    {
                        m.Result = (IntPtr)hit;
                    }
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Renderer = new ThemedMenuRenderer();
            _contextMenu.ShowImageMargin = false;
            _contextMenu.ShowCheckMargin = false;
            _contextMenu.BackColor = AppTheme.Current.Card;
            _contextMenu.Font = AppTheme.GetFont(9f, FontStyle.Regular);
            _contextMenu.Padding = new Padding(4);
            _contextMenu.Opened += delegate { ApplyMenuRegion(); };

            _contextMenu.Items.Add(CreateMenuItem("打开设置", delegate { _host.ShowSettings(); }));
            _contextMenu.Items.Add(CreateMenuItem("查看日志", delegate { _host.ShowLogViewer(); }));
            _contextMenu.Items.Add(CreateMenuItem("最小化到托盘", delegate { Hide(); }));
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(CreateMenuItem("退出", delegate { _host.ExitApplication(); }));

            AssignContextMenu(this);
        }

        private ToolStripMenuItem CreateMenuItem(string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Padding = new Padding(8, 5, 8, 5);
            item.ForeColor = AppTheme.Current.Fg;
            item.Click += handler;
            return item;
        }

        private void ApplyMenuRegion()
        {
            if (_contextMenu == null || _contextMenu.Width < 2 || _contextMenu.Height < 2)
            {
                return;
            }
            GraphicsPath path = DrawUtil.Rounded(
                new Rectangle(0, 0, _contextMenu.Width, _contextMenu.Height), 10);
            Region region = new Region(path);
            path.Dispose();
            Region old = _contextMenu.Region;
            _contextMenu.Region = region;
            if (old != null)
            {
                old.Dispose();
            }
        }

        private void AssignContextMenu(Control control)
        {
            control.ContextMenuStrip = _contextMenu;
            for (int i = 0; i < control.Controls.Count; i++)
            {
                AssignContextMenu(control.Controls[i]);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_host.IsExiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
