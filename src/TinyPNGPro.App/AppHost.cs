using System;
using System.Drawing;
using System.Windows.Forms;
using TinyPNGPro.App.Theme;
using TinyPNGPro.App.Views;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Pipeline;
using TinyPNGPro.Core.Platform;
using TinyPNGPro.Core.Watching;

namespace TinyPNGPro.App
{
    /// <summary>应用装配与生命周期：配置、队列、监控、托盘、窗口、通知。</summary>
    public sealed class AppHost
    {
        private readonly AppConfig _config;
        private readonly ProcessedRegistry _registry;
        private readonly CompressionQueue _queue;
        private readonly FolderWatcher _watcher;

        private NotifyIcon _tray;
        private Icon _appIcon;
        private MainForm _mainForm;
        private SettingsForm _settingsForm;
        private LogViewerForm _logViewerForm;
        private AboutForm _aboutForm;
        private bool _exiting;

        public AppHost()
        {
            _config = AppConfig.Instance;
            AppLogger.MarkConfigAvailable();
            _registry = new ProcessedRegistry();
            _queue = new CompressionQueue(_config, _registry);
            _watcher = new FolderWatcher(_config, _queue, _registry);
        }

        public AppConfig Config
        {
            get { return _config; }
        }

        public CompressionQueue Queue
        {
            get { return _queue; }
        }

        public FolderWatcher Watcher
        {
            get { return _watcher; }
        }

        public MainForm MainForm
        {
            get { return _mainForm; }
        }

        public Icon AppIcon
        {
            get { return _appIcon; }
        }

        public bool IsExiting
        {
            get { return _exiting; }
        }

        public void Start()
        {
            AppTheme.SetMode(_config.GetTheme());
            _appIcon = LoadAppIcon();

            _queue.TaskFinished += OnTaskFinished;
            _queue.BatchFinished += OnBatchFinished;
            _queue.Start();

            if (_config.GetAutoCompressionEnabled())
            {
                _watcher.Start();
            }

            _mainForm = new MainForm(this);
            CreateTray();
            _mainForm.Shown += OnMainFormShown;
            AppLogger.Info(AppInfo.Title + " v" + AppInfo.Version + " started");
        }

        public void Shutdown()
        {
            try
            {
                _watcher.Stop();
            }
            catch
            {
            }
            try
            {
                _queue.Stop();
            }
            catch
            {
            }
            try
            {
                _registry.Flush(true);
            }
            catch
            {
            }
            DisposeTray();
        }

        private Icon LoadAppIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    return icon;
                }
            }
            catch
            {
            }
            try
            {
                Bitmap bitmap = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.FromArgb(124, 92, 255));
                    using (SolidBrush brush = new SolidBrush(Color.White))
                    {
                        g.DrawString("T", new Font("Segoe UI", 16, FontStyle.Bold), brush, 6, 2);
                    }
                }
                IntPtr handle = bitmap.GetHicon();
                return Icon.FromHandle(handle);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void CreateTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开主窗口", null, delegate { ShowMainWindow(); });
            menu.Items.Add("设置", null, delegate { ShowSettings(); });
            menu.Items.Add("查看日志", null, delegate { ShowLogViewer(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApplication(); });

            _tray = new NotifyIcon();
            _tray.Icon = _appIcon;
            _tray.Text = AppInfo.Title;
            _tray.Visible = true;
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowMainWindow(); };
            _tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowMainWindow();
                }
            };
        }

        private void DisposeTray()
        {
            if (_tray == null)
            {
                return;
            }
            try
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch
            {
            }
            _tray = null;
        }

        private void OnMainFormShown(object sender, EventArgs e)
        {
            try
            {
                _mainForm.Shown -= OnMainFormShown;
                if (string.IsNullOrEmpty(_config.GetCurrentApiKey()))
                {
                    DialogResult result = MessageBox.Show(
                        _mainForm,
                        "尚未配置 TinyPNG API 密钥，压缩功能将不可用。\n是否现在打开设置？",
                        AppInfo.Title,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    if (result == DialogResult.Yes)
                    {
                        ShowSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Startup prompt failed: " + ex.Message);
            }
        }

        public void ShowMainWindow()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                return;
            }
            if (!_mainForm.Visible)
            {
                _mainForm.Show();
            }
            if (_mainForm.WindowState == FormWindowState.Minimized)
            {
                _mainForm.WindowState = FormWindowState.Normal;
            }
            _mainForm.Activate();
            _mainForm.BringToFront();
        }

        public void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(this);
                _settingsForm.Show();
            }
            else
            {
                _settingsForm.Activate();
            }
        }

        public void ShowLogViewer()
        {
            if (_logViewerForm == null || _logViewerForm.IsDisposed)
            {
                _logViewerForm = new LogViewerForm(_config);
                _logViewerForm.Show();
            }
            else
            {
                _logViewerForm.Activate();
            }
        }

        public void ShowAbout()
        {
            if (_aboutForm == null || _aboutForm.IsDisposed)
            {
                _aboutForm = new AboutForm();
                _aboutForm.Show();
            }
            else
            {
                _aboutForm.Activate();
            }
        }

        public void ToggleTheme()
        {
            _config.SetTheme(AppTheme.IsDark ? "light" : "dark");
            ApplyTheme();
        }

        public void ApplySettings()
        {
            AppLogger.SetEnabled(_config.GetLoggingEnabled());
            ApplyTheme();
            RestartWatcherPerConfig();
            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.RefreshFromConfig();
            }
        }

        public void ApplyTheme()
        {
            AppTheme.SetMode(_config.GetTheme());
            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.ApplyTheme();
            }
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.ApplyTheme();
            }
            if (_logViewerForm != null && !_logViewerForm.IsDisposed)
            {
                _logViewerForm.ApplyTheme();
            }
            if (_aboutForm != null && !_aboutForm.IsDisposed)
            {
                _aboutForm.ApplyTheme();
            }
        }

        public void RestartWatcherPerConfig()
        {
            if (_config.GetAutoCompressionEnabled())
            {
                _watcher.Restart();
            }
            else
            {
                _watcher.Stop();
            }
        }

        public void ExitApplication()
        {
            if (_exiting)
            {
                return;
            }
            _exiting = true;
            AppLogger.Info("Exit requested");
            DisposeTray();
            try
            {
                _watcher.Stop();
            }
            catch
            {
            }
            try
            {
                _queue.Stop();
            }
            catch
            {
            }
            try
            {
                _registry.Flush(true);
            }
            catch
            {
            }
            Application.Exit();
        }

        private void OnTaskFinished(string name, bool ok, string message, bool isAuto)
        {
            if (!isAuto)
            {
                return;
            }
            ShowBalloon((ok ? "自动压缩完成：" : "自动压缩失败：") + name + " · " + message);
        }

        private void OnBatchFinished(BatchSummary summary, bool isAuto)
        {
            if (isAuto)
            {
                return;
            }
            if (summary.Failed == 0)
            {
                long saved = summary.OriginalSize - summary.CompressedSize;
                ShowBalloon("压缩完成：" + summary.Ok.ToString() + " 张，节省 " +
                            PathUtil.FormatFileSize(saved < 0 ? 0 : saved) +
                            "（" + PathUtil.FormatRatio(summary.OriginalSize, summary.CompressedSize) + "）");
            }
            else
            {
                ShowBalloon("压缩完成：" + summary.Ok.ToString() + " 成功，" +
                            summary.Failed.ToString() + " 失败");
            }
        }

        private void ShowBalloon(string text)
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                return;
            }
            try
            {
                if (_mainForm.InvokeRequired)
                {
                    _mainForm.BeginInvoke(new Action<string>(ShowBalloon), text);
                    return;
                }
                if (_tray == null)
                {
                    return;
                }
                int seconds = _config.GetNotificationDuration();
                _tray.BalloonTipTitle = AppInfo.Title;
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                _tray.ShowBalloonTip(seconds * 1000);
            }
            catch
            {
            }
        }
    }
}
