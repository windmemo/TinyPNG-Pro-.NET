using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.Core.Configuration
{
    /// <summary>
    /// 线程安全的 INI 配置管理器（进程内单例）。
    /// 兼容 Python 版 config.ini：保留未知分区/键、UTF-8、两秒节流热重载、原子落盘。
    /// </summary>
    public sealed class AppConfig
    {
        private sealed class IniSection
        {
            public string Name;
            public List<KeyValuePair<string, string>> Entries;
        }

        private const double ReloadThrottleSeconds = 2.0;

        private static readonly object InstanceLock = new object();
        private static AppConfig _instance;

        private readonly object _lock = new object();
        private readonly string _path;
        private readonly List<IniSection> _sections = new List<IniSection>();
        private DateTime _lastWriteUtc = DateTime.MinValue;
        private long _lastLength = -1;
        private DateTime _lastCheckUtc = DateTime.MinValue;

        private AppConfig()
        {
            _path = AppPaths.ConfigPath;
            lock (_lock)
            {
                if (File.Exists(_path))
                {
                    LoadLocked();
                }
                else
                {
                    CreateDefaultsLocked();
                }
            }
        }

        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AppConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        public string ConfigPath
        {
            get { return _path; }
        }

        // ---------------- 底层读写 ----------------

        private void CreateDefaultsLocked()
        {
            _sections.Clear();
            AddSectionLocked("API");
            SetRawLocked("API", "api_key_1", string.Empty);
            SetRawLocked("API", "api_key_2", string.Empty);
            SetRawLocked("API", "api_key_3", string.Empty);
            SetRawLocked("API", "active_key", "1");
            SetRawLocked("API", "compression_count", "0");

            AddSectionLocked("Compression");
            SetRawLocked("Compression", "replace_original", "True");
            SetRawLocked("Compression", "only_png", "True");
            SetRawLocked("Compression", "output_path", string.Empty);

            AddSectionLocked("AutoCompression");
            SetRawLocked("AutoCompression", "enabled", "False");
            SetRawLocked("AutoCompression", "watch_folders", string.Empty);

            AddSectionLocked("Settings");
            SetRawLocked("Settings", "timeout", "30");
            SetRawLocked("Settings", "close_to_tray", "True");
            SetRawLocked("Settings", "always_on_top", "False");
            SetRawLocked("Settings", "notification_duration", "5");
            SetRawLocked("Settings", "logging_enabled", "True");
            SetRawLocked("Settings", "theme", "dark");

            AddSectionLocked("Startup");
            SetRawLocked("Startup", "auto_start", "False");

            SaveLocked();
        }

        private IniSection FindSectionLocked(string name)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                if (string.Equals(_sections[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return _sections[i];
                }
            }
            return null;
        }

        private IniSection AddSectionLocked(string name)
        {
            IniSection section = FindSectionLocked(name);
            if (section != null)
            {
                return section;
            }
            section = new IniSection();
            section.Name = name;
            section.Entries = new List<KeyValuePair<string, string>>();
            _sections.Add(section);
            return section;
        }

        private string GetRawLocked(string sectionName, string key, string fallback)
        {
            IniSection section = FindSectionLocked(sectionName);
            if (section == null)
            {
                return fallback;
            }
            for (int i = section.Entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(section.Entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return section.Entries[i].Value;
                }
            }
            return fallback;
        }

        private void SetRawLocked(string sectionName, string key, string value)
        {
            IniSection section = AddSectionLocked(sectionName);
            for (int i = 0; i < section.Entries.Count; i++)
            {
                if (string.Equals(section.Entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    section.Entries[i] = new KeyValuePair<string, string>(section.Entries[i].Key, value);
                    return;
                }
            }
            section.Entries.Add(new KeyValuePair<string, string>(key, value));
        }

        private void EnsureFreshLocked()
        {
            DateTime now = DateTime.UtcNow;
            if (_lastCheckUtc != DateTime.MinValue &&
                (now - _lastCheckUtc).TotalSeconds < ReloadThrottleSeconds)
            {
                return;
            }
            _lastCheckUtc = now;

            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }
                DateTime write = File.GetLastWriteTimeUtc(_path);
                long length = new FileInfo(_path).Length;
                if (write == _lastWriteUtc && length == _lastLength)
                {
                    return;
                }
            }
            catch
            {
                return;
            }
            LoadLocked();
        }

        private void LoadLocked()
        {
            _sections.Clear();
            try
            {
                string text = File.ReadAllText(_path, Encoding.UTF8);
                if (text.Length > 0 && text[0] == '\uFEFF')
                {
                    text = text.Substring(1);
                }
                string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                IniSection current = null;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    {
                        continue;
                    }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        current = AddSectionLocked(line.Substring(1, line.Length - 2).Trim());
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (current == null)
                    {
                        current = AddSectionLocked("DEFAULT");
                    }
                    SetRawLocked(current.Name, key, value);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Write("WARNING", "Failed to load config: " + ex.Message);
                _sections.Clear();
            }
            RecordStatLocked();
        }

        private void RecordStatLocked()
        {
            try
            {
                if (File.Exists(_path))
                {
                    _lastWriteUtc = File.GetLastWriteTimeUtc(_path);
                    _lastLength = new FileInfo(_path).Length;
                }
                else
                {
                    _lastWriteUtc = DateTime.MinValue;
                    _lastLength = -1;
                }
            }
            catch
            {
                _lastWriteUtc = DateTime.MinValue;
                _lastLength = -1;
            }
            _lastCheckUtc = DateTime.UtcNow;
        }

        private void SaveLocked()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _sections.Count; i++)
            {
                IniSection section = _sections[i];
                if (i > 0)
                {
                    sb.AppendLine();
                }
                sb.Append('[').Append(section.Name).AppendLine("]");
                for (int j = 0; j < section.Entries.Count; j++)
                {
                    sb.Append(section.Entries[j].Key).Append(" = ")
                      .Append(section.Entries[j].Value).AppendLine();
                }
            }

            string tmp = _path + ".tmp";
            string text = sb.ToString();
            Exception lastError = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    PathUtil.EnsureDirectory(_path);
                    File.WriteAllText(tmp, text, new UTF8Encoding(false));
                    if (File.Exists(_path))
                    {
                        try
                        {
                            File.Replace(tmp, _path, null);
                        }
                        catch
                        {
                            File.Delete(_path);
                            File.Move(tmp, _path);
                        }
                    }
                    else
                    {
                        File.Move(tmp, _path);
                    }
                    RecordStatLocked();
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Thread.Sleep(20);
                }
            }
            AppLogger.Write("ERROR", "Failed to save config: " + (lastError == null ? "unknown" : lastError.Message));
        }

        // ---------------- 公共访问 ----------------

        public string GetValue(string section, string key, string fallback)
        {
            lock (_lock)
            {
                EnsureFreshLocked();
                return GetRawLocked(section, key, fallback);
            }
        }

        public bool GetBool(string section, string key, bool fallback)
        {
            string raw = GetValue(section, key, null);
            if (raw == null)
            {
                return fallback;
            }
            string v = raw.Trim().ToLowerInvariant();
            if (v == "1" || v == "yes" || v == "true" || v == "on")
            {
                return true;
            }
            if (v == "0" || v == "no" || v == "false" || v == "off")
            {
                return false;
            }
            return fallback;
        }

        public int GetInt(string section, string key, int fallback)
        {
            string raw = GetValue(section, key, null);
            if (raw == null)
            {
                return fallback;
            }
            try
            {
                return int.Parse(raw.Trim(), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public void SetValue(string section, string key, string value)
        {
            lock (_lock)
            {
                SetRawLocked(section, key, value);
                SaveLocked();
            }
        }

        public void SetValues(string section, Dictionary<string, string> values)
        {
            lock (_lock)
            {
                foreach (KeyValuePair<string, string> kv in values)
                {
                    SetRawLocked(section, kv.Key, kv.Value);
                }
                SaveLocked();
            }
        }

        // ---------------- 业务字段 ----------------

        public string[] GetApiKeys()
        {
            string[] keys = new string[3];
            for (int i = 0; i < 3; i++)
            {
                keys[i] = GetValue("API", "api_key_" + (i + 1), string.Empty);
            }
            return keys;
        }

        public int GetActiveKey()
        {
            int value = GetInt("API", "active_key", 1);
            return value < 1 ? 1 : (value > 3 ? 3 : value);
        }

        public void SetActiveKey(int index)
        {
            if (index < 1 || index > 3)
            {
                return;
            }
            SetValue("API", "active_key", index.ToString(CultureInfo.InvariantCulture));
        }

        public string GetCurrentApiKey()
        {
            string[] keys = GetApiKeys();
            int index = GetActiveKey() - 1;
            if (index >= 0 && index < keys.Length)
            {
                return keys[index];
            }
            return string.Empty;
        }

        public int GetCompressionCount()
        {
            return GetInt("API", "compression_count", 0);
        }

        public void SetCompressionCount(int count)
        {
            SetValue("API", "compression_count", count.ToString(CultureInfo.InvariantCulture));
        }

        public bool GetReplaceOriginal()
        {
            return GetBool("Compression", "replace_original", true);
        }

        public void SetReplaceOriginal(bool value)
        {
            SetValue("Compression", "replace_original", value ? "True" : "False");
        }

        public bool GetOnlyPng()
        {
            return GetBool("Compression", "only_png", true);
        }

        public string GetOutputPath()
        {
            return GetValue("Compression", "output_path", string.Empty);
        }

        public bool GetAutoCompressionEnabled()
        {
            return GetBool("AutoCompression", "enabled", false);
        }

        public void SetAutoCompressionEnabled(bool value)
        {
            SetValue("AutoCompression", "enabled", value ? "True" : "False");
        }

        public string[] GetWatchFolders()
        {
            string raw = GetValue("AutoCompression", "watch_folders", string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return new string[0];
            }
            List<string> folders = new List<string>();
            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (item.Length > 0)
                {
                    folders.Add(item);
                }
            }
            return folders.ToArray();
        }

        public int GetTimeout()
        {
            int value = GetInt("Settings", "timeout", 30);
            return value < 10 ? 10 : (value > 300 ? 300 : value);
        }

        public bool GetAlwaysOnTop()
        {
            return GetBool("Settings", "always_on_top", false);
        }

        public void SetAlwaysOnTop(bool value)
        {
            SetValue("Settings", "always_on_top", value ? "True" : "False");
        }

        public int GetNotificationDuration()
        {
            int value = GetInt("Settings", "notification_duration", 5);
            return value < 1 ? 1 : (value > 30 ? 30 : value);
        }

        public bool GetLoggingEnabled()
        {
            return GetBool("Settings", "logging_enabled", true);
        }

        public string GetTheme()
        {
            string value = GetValue("Settings", "theme", "dark");
            value = value.Trim().ToLowerInvariant();
            return value == "light" ? "light" : "dark";
        }

        public void SetTheme(string value)
        {
            SetValue("Settings", "theme", value == "light" ? "light" : "dark");
        }
    }
}
