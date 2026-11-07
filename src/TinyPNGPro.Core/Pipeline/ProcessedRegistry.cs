using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.Core.Pipeline
{
    /// <summary>
    /// 自动压缩去重账本（processed_state.json）。
    /// 结构：{"version":1,"entries":{"&lt;小写绝对路径&gt;":[mtime,size]}}，与 Python 版兼容。
    /// </summary>
    public sealed class ProcessedRegistry
    {
        private sealed class Entry
        {
            public double Mtime;
            public long Size;
        }

        private const double SaveIntervalSeconds = 2.0;
        private const double MaxAgeSeconds = 30.0 * 24.0 * 3600.0;
        private const int MaxEntries = 5000;
        private const double MtimeToleranceSeconds = 0.001;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly object _lock = new object();
        private readonly string _path;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private bool _dirty;
        private DateTime _lastSaveUtc = DateTime.MinValue;

        public ProcessedRegistry()
        {
            _path = AppPaths.StatePath;
            Load();
        }

        public string StatePath
        {
            get { return _path; }
        }

        private static double ToEpoch(DateTime utc)
        {
            return (utc - Epoch).Ticks / (double)TimeSpan.TicksPerSecond;
        }

        private static bool TryStat(string filePath, out double mtime, out long size)
        {
            mtime = 0;
            size = 0;
            try
            {
                FileInfo info = new FileInfo(filePath);
                if (!info.Exists)
                {
                    return false;
                }
                mtime = ToEpoch(info.LastWriteTimeUtc);
                size = info.Length;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_path))
                    {
                        return;
                    }
                    string text = File.ReadAllText(_path, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    object rootObj = serializer.DeserializeObject(text);
                    if (!(rootObj is Dictionary<string, object>))
                    {
                        return;
                    }
                    Dictionary<string, object> root = (Dictionary<string, object>)rootObj;
                    object entriesObj = root.ContainsKey("entries") ? root["entries"] : null;
                    if (!(entriesObj is Dictionary<string, object>))
                    {
                        return;
                    }
                    foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)entriesObj)
                    {
                        object[] arr = ToArray(kv.Value);
                        if (arr == null || arr.Length != 2)
                        {
                            continue;
                        }
                        Entry entry = new Entry();
                        entry.Mtime = ToDouble(arr[0]);
                        entry.Size = ToLong(arr[1]);
                        _entries[kv.Key] = entry;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("Failed to load processed state: " + ex.Message);
                    _entries.Clear();
                }
            }
        }

        private static object[] ToArray(object value)
        {
            if (value is object[])
            {
                return (object[])value;
            }
            if (value is System.Collections.ArrayList)
            {
                return ((System.Collections.ArrayList)value).ToArray();
            }
            return null;
        }

        private static double ToDouble(object value)
        {
            if (value == null)
            {
                return 0.0;
            }
            if (value is double)
            {
                return (double)value;
            }
            if (value is int)
            {
                return (double)(int)value;
            }
            if (value is long)
            {
                return (double)(long)value;
            }
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0.0;
            }
        }

        private static long ToLong(object value)
        {
            if (value == null)
            {
                return 0;
            }
            if (value is int)
            {
                return (long)(int)value;
            }
            if (value is long)
            {
                return (long)value;
            }
            if (value is double)
            {
                return (long)(double)value;
            }
            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return 0;
            }
        }

        public bool IsRecordedSame(string filePath)
        {
            double mtime;
            long size;
            if (!TryStat(filePath, out mtime, out size))
            {
                return false;
            }
            string key = PathUtil.NormKey(filePath);
            lock (_lock)
            {
                Entry entry = _entries.ContainsKey(key) ? _entries[key] : null;
                if (entry == null)
                {
                    return false;
                }
                return entry.Size == size && Math.Abs(entry.Mtime - mtime) <= MtimeToleranceSeconds;
            }
        }

        public void Mark(string filePath)
        {
            double mtime;
            long size;
            if (!TryStat(filePath, out mtime, out size))
            {
                return;
            }
            string key = PathUtil.NormKey(filePath);
            lock (_lock)
            {
                Entry entry = new Entry();
                entry.Mtime = mtime;
                entry.Size = size;
                _entries[key] = entry;
                _dirty = true;
            }
            Flush(false);
        }

        public void Prune()
        {
            lock (_lock)
            {
                PruneLocked();
                _dirty = true;
            }
            Flush(true);
        }

        private void PruneLocked()
        {
            double now = ToEpoch(DateTime.UtcNow);
            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, Entry> kv in _entries)
            {
                if (now - kv.Value.Mtime > MaxAgeSeconds)
                {
                    stale.Add(kv.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                _entries.Remove(stale[i]);
            }
            if (_entries.Count > MaxEntries)
            {
                List<KeyValuePair<string, Entry>> ordered = new List<KeyValuePair<string, Entry>>(_entries);
                ordered.Sort(delegate(KeyValuePair<string, Entry> a, KeyValuePair<string, Entry> b)
                {
                    return b.Value.Mtime.CompareTo(a.Value.Mtime);
                });
                _entries.Clear();
                for (int i = 0; i < MaxEntries; i++)
                {
                    _entries[ordered[i].Key] = ordered[i].Value;
                }
            }
        }

        public void Flush(bool force)
        {
            lock (_lock)
            {
                if (!force)
                {
                    if (!_dirty)
                    {
                        return;
                    }
                    if (_lastSaveUtc != DateTime.MinValue &&
                        (DateTime.UtcNow - _lastSaveUtc).TotalSeconds < SaveIntervalSeconds)
                    {
                        return;
                    }
                }
                PruneLocked();
                try
                {
                    Dictionary<string, object> root = new Dictionary<string, object>();
                    root["version"] = 1;
                    Dictionary<string, object> entries = new Dictionary<string, object>();
                    foreach (KeyValuePair<string, Entry> kv in _entries)
                    {
                        entries[kv.Key] = new object[] { kv.Value.Mtime, kv.Value.Size };
                    }
                    root["entries"] = entries;

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(root);

                    string tmp = _path + ".tmp";
                    File.WriteAllText(tmp, json, new UTF8Encoding(false));
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
                    _dirty = false;
                    _lastSaveUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("Failed to save processed state: " + ex.Message);
                }
            }
        }
    }
}
