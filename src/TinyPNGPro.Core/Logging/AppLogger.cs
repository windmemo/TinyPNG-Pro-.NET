using System;
using System.Globalization;
using System.IO;
using System.Text;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.Core.Logging
{
    /// <summary>
    /// 简易日切文件日志（logs/YYYY-MM-DD.log）。
    /// 行格式：yyyy-MM-dd HH:mm:ss - LEVEL - message，与 Python 版保持一致。
    /// 日志写入失败不得影响主流程。
    /// </summary>
    public static class AppLogger
    {
        private static readonly object Lock = new object();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static volatile bool _enabled = true;
        private static volatile bool _configAvailable;
        private static DateTime _flagCheckedUtc = DateTime.MinValue;

        public static void MarkConfigAvailable()
        {
            _configAvailable = true;
        }

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _flagCheckedUtc = DateTime.UtcNow;
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warning(string message)
        {
            Write("WARNING", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Write(string level, string message)
        {
            if (!IsEnabled())
            {
                return;
            }
            lock (Lock)
            {
                try
                {
                    string dir = AppPaths.LogDirectory;
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    DateTime now = DateTime.Now;
                    string date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    string line = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} - {1} - {2}{3}",
                        now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        level,
                        message,
                        Environment.NewLine);
                    File.AppendAllText(Path.Combine(dir, date + ".log"), line, Utf8NoBom);
                }
                catch
                {
                    // 日志失败隔离：绝不向上抛出
                }
            }
        }

        private static bool IsEnabled()
        {
            if (!_configAvailable)
            {
                return _enabled;
            }
            DateTime now = DateTime.UtcNow;
            if (_flagCheckedUtc != DateTime.MinValue &&
                (now - _flagCheckedUtc).TotalSeconds < 2.0)
            {
                return _enabled;
            }
            _flagCheckedUtc = now;
            try
            {
                _enabled = AppConfig.Instance.GetLoggingEnabled();
            }
            catch
            {
                // 读取失败时维持上次状态
            }
            return _enabled;
        }
    }
}
