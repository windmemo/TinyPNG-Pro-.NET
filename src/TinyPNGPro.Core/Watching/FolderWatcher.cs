using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Pipeline;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.Core.Watching
{
    /// <summary>
    /// 文件夹监控（非递归）：仅处理监控启动后新出现/重新保存的图片。
    /// 水位判定 + 去重账本 + 抑制表 + 就绪探测，避免旧文件误压与写回死循环。
    /// </summary>
    public sealed class FolderWatcher
    {
        private readonly AppConfig _config;
        private readonly CompressionQueue _queue;
        private readonly ProcessedRegistry _registry;

        private readonly object _lock = new object();
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly HashSet<string> _pending = new HashSet<string>();

        private bool _running;
        private DateTime _watchStartLocal;
        private System.Threading.Timer _healthTimer;
        private DateTime _lastPruneUtc = DateTime.MinValue;

        public FolderWatcher(AppConfig config, CompressionQueue queue, ProcessedRegistry registry)
        {
            _config = config;
            _queue = queue;
            _registry = registry;
        }

        public bool IsRunning
        {
            get { return _running; }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_running)
                {
                    return;
                }
                _watchStartLocal = DateTime.Now;
                _running = true;

                string[] folders = _config.GetWatchFolders();
                for (int i = 0; i < folders.Length; i++)
                {
                    string folder = folders[i];
                    if (!Directory.Exists(folder))
                    {
                        AppLogger.Warning("Watch folder not found: " + folder);
                        continue;
                    }
                    try
                    {
                        FileSystemWatcher watcher = new FileSystemWatcher(folder);
                        watcher.IncludeSubdirectories = false;
                        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                               NotifyFilters.LastWrite | NotifyFilters.Size;
                        watcher.InternalBufferSize = 32768;
                        watcher.Created += OnFileEvent;
                        watcher.Changed += OnFileEvent;
                        watcher.Renamed += OnFileRenamed;
                        watcher.Error += OnWatcherError;
                        watcher.EnableRaisingEvents = true;
                        _watchers.Add(watcher);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("Failed to start watcher for " + folder + ": " + ex.Message);
                    }
                }

                if (_watchers.Count == 0)
                {
                    AppLogger.Warning("Auto compression enabled but no valid watch folder");
                }

                _healthTimer = new System.Threading.Timer(CheckHealth, null, 30000, 30000);
                AppLogger.Info("Folder watcher started (" + _watchers.Count.ToString() + " folder(s))");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_running && _watchers.Count == 0)
                {
                    return;
                }
                _running = false;
                if (_healthTimer != null)
                {
                    try
                    {
                        _healthTimer.Dispose();
                    }
                    catch
                    {
                    }
                    _healthTimer = null;
                }
                DisposeWatchersLocked();
                _pending.Clear();
                AppLogger.Info("Folder watcher stopped");
            }
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        private void DisposeWatchersLocked()
        {
            for (int i = 0; i < _watchers.Count; i++)
            {
                try
                {
                    _watchers[i].EnableRaisingEvents = false;
                    _watchers[i].Dispose();
                }
                catch
                {
                }
            }
            _watchers.Clear();
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            HandleCandidate(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            HandleCandidate(e.FullPath);
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            AppLogger.Warning("Watcher error: " + e.GetException().Message);
        }

        private void HandleCandidate(string path)
        {
            if (!_running)
            {
                return;
            }
            if (!PathUtil.IsSupportedFile(path) || PathUtil.IsCompressedOutput(path))
            {
                return;
            }
            if (!_queue.IsAllowed(path))
            {
                return;
            }
            if (_queue.IsRecentlyWritten(path))
            {
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    return;
                }
                if (File.GetLastWriteTime(path) <= _watchStartLocal)
                {
                    return;
                }
            }
            catch
            {
                // 取不到状态时按旧文件跳过（fail-safe）
                return;
            }

            if (_registry.IsRecordedSame(path))
            {
                return;
            }

            string key = PathUtil.NormKey(path);
            lock (_pending)
            {
                if (_pending.Contains(key))
                {
                    return;
                }
                _pending.Add(key);
            }

            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                WaitReadyAndQueue(path, key);
            });
        }

        private void WaitReadyAndQueue(string path, string key)
        {
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline)
                {
                    if (!_running)
                    {
                        return;
                    }
                    try
                    {
                        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            stream.Close();
                        }
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(120);
                    }
                }

                if (!_running || !File.Exists(path))
                {
                    return;
                }
                if (_queue.IsRecentlyWritten(path) || _registry.IsRecordedSame(path))
                {
                    return;
                }
                _queue.AddAutoTask(path);
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Failed to queue watcher file " + path + ": " + ex.Message);
            }
            finally
            {
                lock (_pending)
                {
                    _pending.Remove(key);
                }
            }
        }

        private void CheckHealth(object state)
        {
            if (!_running)
            {
                return;
            }
            try
            {
                lock (_lock)
                {
                    for (int i = 0; i < _watchers.Count; i++)
                    {
                        try
                        {
                            if (!_watchers[i].EnableRaisingEvents)
                            {
                                _watchers[i].EnableRaisingEvents = true;
                                AppLogger.Warning("Watcher restarted for: " + _watchers[i].Path);
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Warning("Watcher health check failed: " + ex.Message);
                        }
                    }
                }

                if ((DateTime.UtcNow - _lastPruneUtc).TotalSeconds > 600)
                {
                    _lastPruneUtc = DateTime.UtcNow;
                    _registry.Prune();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Watcher health loop error: " + ex.Message);
            }
        }
    }
}
