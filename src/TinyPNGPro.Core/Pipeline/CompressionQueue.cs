using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TinyPNGPro.Core.Configuration;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Platform;
using TinyPNGPro.Core.TinyPng;

namespace TinyPNGPro.Core.Pipeline
{
    public sealed class CompressionTask
    {
        public string InputPath;
        public string OutputPath;
        public bool IsAuto;
        public string BatchId;
        public string Status = "pending";
        public string Error;
        public CompressionResult Result;
        public bool Finished;
    }

    public sealed class BatchSummary
    {
        public string BatchId;
        public int Total;
        public int Ok;
        public int Failed;
        public long OriginalSize;
        public long CompressedSize;
        public double Ratio;
        public double Elapsed;
        public List<string> Errors = new List<string>();
    }

    internal sealed class BatchState
    {
        public string Id;
        public bool IsAuto;
        public int Total;
        public int Remaining;
        public int Ok;
        public int Failed;
        public long OriginalSize;
        public long CompressedSize;
        public List<string> Errors = new List<string>();
        public DateTime StartedUtc;
        public bool Finalized;
    }

    /// <summary>
    /// 唯一压缩管线：手动任务优先、单 worker 串行、批量汇总、抑制表与账本登记。
    /// 网络请求全部在后台线程执行，UI 线程只接收事件。
    /// </summary>
    public sealed class CompressionQueue
    {
        private const double RecentWriteTtlSeconds = 30.0;
        private const double InFlightTtlSeconds = 120.0;

        private readonly AppConfig _config;
        private readonly ProcessedRegistry _registry;

        private readonly BlockingCollection<CompressionTask> _manualQueue =
            new BlockingCollection<CompressionTask>();
        private readonly BlockingCollection<CompressionTask> _autoQueue =
            new BlockingCollection<CompressionTask>();

        private readonly object _batchLock = new object();
        private readonly Dictionary<string, BatchState> _batches = new Dictionary<string, BatchState>();

        private readonly object _activeLock = new object();
        private readonly Dictionary<string, DateTime> _active = new Dictionary<string, DateTime>();

        private Thread _workerThread;
        private volatile bool _running;

        public event Action<string, bool> TaskStarted;
        public event Action<string, bool, string, bool> TaskFinished;
        public event Action<BatchSummary, bool> BatchFinished;
        public event Action<int> QueueChanged;

        public CompressionQueue(AppConfig config, ProcessedRegistry registry)
        {
            _config = config;
            _registry = registry;
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }
            _running = true;
            _workerThread = new Thread(WorkerLoop);
            _workerThread.IsBackground = true;
            _workerThread.Name = "TinyPNG-Worker";
            _workerThread.Start();
            AppLogger.Info("Compression manager started");
        }

        public void Stop()
        {
            if (!_running)
            {
                return;
            }
            _running = false;
            try
            {
                _manualQueue.CompleteAdding();
                _autoQueue.CompleteAdding();
            }
            catch
            {
            }
            if (_workerThread != null)
            {
                try
                {
                    _workerThread.Join(5000);
                }
                catch
                {
                }
            }
            try
            {
                _registry.Flush(true);
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Failed to flush processed state: " + ex.Message);
            }
            AppLogger.Info("Compression manager stopped");
        }

        public int QueueSize
        {
            get { return _manualQueue.Count + _autoQueue.Count; }
        }

        public bool IsAllowed(string path)
        {
            return PathUtil.IsAllowedFile(path, _config.GetOnlyPng());
        }

        public bool IsRecordedSame(string path)
        {
            return _registry.IsRecordedSame(path);
        }

        public string GetDefaultOutputPath(string inputPath)
        {
            if (_config.GetReplaceOriginal())
            {
                return inputPath;
            }
            string dir = Path.GetDirectoryName(inputPath);
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            string outputDir = _config.GetOutputPath();
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                outputDir = string.IsNullOrEmpty(dir) ? "." : dir;
            }
            return Path.Combine(outputDir, fileName + PathUtil.CompressedSuffix + ext);
        }

        public bool IsRecentlyWritten(string path)
        {
            string key = PathUtil.NormKey(path);
            if (key.Length == 0)
            {
                return false;
            }
            lock (_activeLock)
            {
                DateTime expiry;
                if (_active.TryGetValue(key, out expiry))
                {
                    return expiry > DateTime.UtcNow;
                }
            }
            return false;
        }

        private void MarkActive(string path, double ttlSeconds)
        {
            string key = PathUtil.NormKey(path);
            if (key.Length == 0)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            DateTime expiry = now.AddSeconds(ttlSeconds);
            lock (_activeLock)
            {
                List<string> expired = new List<string>();
                foreach (KeyValuePair<string, DateTime> kv in _active)
                {
                    if (kv.Value < now)
                    {
                        expired.Add(kv.Key);
                    }
                }
                for (int i = 0; i < expired.Count; i++)
                {
                    _active.Remove(expired[i]);
                }
                _active[key] = expiry;
            }
        }

        public bool AddAutoTask(string inputPath)
        {
            return AddTask(inputPath, null, true, null);
        }

        public string AddBatch(string[] paths)
        {
            List<string> candidates = new List<string>();
            for (int i = 0; i < paths.Length; i++)
            {
                if (IsAllowed(paths[i]) && File.Exists(paths[i]))
                {
                    candidates.Add(paths[i]);
                }
            }
            if (candidates.Count == 0)
            {
                return null;
            }

            BatchState batch = new BatchState();
            batch.Id = Guid.NewGuid().ToString("N");
            batch.IsAuto = false;
            batch.StartedUtc = DateTime.UtcNow;
            lock (_batchLock)
            {
                _batches[batch.Id] = batch;
            }

            int added = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (AddTask(candidates[i], null, false, batch.Id))
                {
                    added++;
                }
            }

            bool emitNow = false;
            lock (_batchLock)
            {
                if (added == 0)
                {
                    _batches.Remove(batch.Id);
                    return null;
                }
                batch.Total = added;
                batch.Finalized = true;
                if (batch.Remaining <= 0)
                {
                    _batches.Remove(batch.Id);
                    emitNow = true;
                }
            }
            if (emitNow)
            {
                RaiseBatchFinished(BuildSummary(batch), false);
            }
            AppLogger.Info("Batch " + batch.Id + " queued with " + added.ToString() + " file(s)");
            return batch.Id;
        }

        private bool AddTask(string inputPath, string outputPath, bool isAuto, string batchId)
        {
            if (!IsAllowed(inputPath))
            {
                AppLogger.Warning("Invalid file type: " + inputPath);
                return false;
            }
            if (outputPath == null)
            {
                outputPath = GetDefaultOutputPath(inputPath);
            }

            CompressionTask task = new CompressionTask();
            task.InputPath = inputPath;
            task.OutputPath = outputPath;
            task.IsAuto = isAuto;
            task.BatchId = batchId;

            if (isAuto)
            {
                try
                {
                    _registry.Mark(inputPath);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("Failed to mark registry for " + inputPath + ": " + ex.Message);
                }
            }

            if (batchId != null)
            {
                lock (_batchLock)
                {
                    BatchState batch = _batches.ContainsKey(batchId) ? _batches[batchId] : null;
                    if (batch != null)
                    {
                        batch.Remaining++;
                    }
                }
            }

            if (isAuto)
            {
                _autoQueue.Add(task);
            }
            else
            {
                _manualQueue.Add(task);
            }

            AppLogger.Info("Added compression task: " + inputPath + " -> " + outputPath);
            RaiseQueueChanged();
            return true;
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                CompressionTask task = null;
                try
                {
                    if (!_manualQueue.TryTake(out task, 0))
                    {
                        if (!_autoQueue.TryTake(out task, 500))
                        {
                            continue;
                        }
                    }
                }
                catch
                {
                    continue;
                }

                try
                {
                    ProcessTask(task);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Unhandled worker error for " + task.InputPath + ": " + ex.Message);
                    try
                    {
                        FinishTask(task, false, ex.Message, null);
                    }
                    catch (Exception inner)
                    {
                        AppLogger.Error("Failed to finalize task " + task.InputPath + ": " + inner.Message);
                    }
                }
            }
        }

        private void ProcessTask(CompressionTask task)
        {
            task.Status = "processing";
            MarkActive(task.InputPath, InFlightTtlSeconds);
            RaiseTaskStarted(Path.GetFileName(task.InputPath), task.IsAuto);

            string apiKey = _config.GetCurrentApiKey();
            if (string.IsNullOrEmpty(apiKey) || apiKey.Trim().Length == 0)
            {
                FinishTask(task, false, "未配置 API 密钥", null);
                return;
            }

            try
            {
                TinyPngClient client = new TinyPngClient(apiKey);
                CompressionResult result = client.CompressFile(
                    task.InputPath, task.OutputPath, _config.GetTimeout());
                task.Result = result;

                string original = PathUtil.FormatFileSize(result.OriginalSize);
                string compressed = PathUtil.FormatFileSize(result.CompressedSize);
                string ratio = string.Format("{0:F2}%", result.Ratio * 100.0);
                AppLogger.Info("Compression completed: " + task.InputPath + " -> " + task.OutputPath);
                AppLogger.Info("Original: " + original + ", Compressed: " + compressed + ", Ratio: " + ratio);

                FinishTask(task, true, original + " -> " + compressed + " (" + ratio + ")", result);
            }
            catch (Exception ex)
            {
                FinishTask(task, false, ex.Message, null);
            }
        }

        private void FinishTask(CompressionTask task, bool ok, string message, CompressionResult result)
        {
            if (task.Finished)
            {
                return;
            }
            task.Finished = true;
            task.Status = ok ? "completed" : "failed";
            task.Error = ok ? null : message;

            string inputPath = task.InputPath;
            string outputPath = task.OutputPath;
            bool sameTarget = string.IsNullOrEmpty(outputPath) ||
                              PathUtil.NormKey(outputPath) == PathUtil.NormKey(inputPath);

            try
            {
                MarkActive(inputPath, RecentWriteTtlSeconds);
                if (!sameTarget)
                {
                    MarkActive(outputPath, RecentWriteTtlSeconds);
                }
                _registry.Mark(inputPath);
                if (!sameTarget)
                {
                    _registry.Mark(outputPath);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Failed to record processed state for " + inputPath + ": " + ex.Message);
            }

            if (ok && result != null)
            {
                try
                {
                    _config.SetCompressionCount(result.CompressionCount);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning("Failed to update compression count: " + ex.Message);
                }
            }

            RaiseTaskFinished(Path.GetFileName(inputPath), ok, message, task.IsAuto);
            RecordBatch(task, ok, message, result);
            RaiseQueueChanged();
        }

        private void RecordBatch(CompressionTask task, bool ok, string message, CompressionResult result)
        {
            if (string.IsNullOrEmpty(task.BatchId))
            {
                return;
            }
            bool emitNow = false;
            BatchState finishedBatch = null;
            lock (_batchLock)
            {
                BatchState batch = _batches.ContainsKey(task.BatchId) ? _batches[task.BatchId] : null;
                if (batch == null)
                {
                    return;
                }
                batch.Remaining--;
                if (ok && result != null)
                {
                    batch.Ok++;
                    batch.OriginalSize += result.OriginalSize;
                    batch.CompressedSize += result.CompressedSize;
                }
                else
                {
                    batch.Failed++;
                    batch.Errors.Add(Path.GetFileName(task.InputPath) + ": " + message);
                }
                if (batch.Finalized && batch.Remaining <= 0)
                {
                    _batches.Remove(task.BatchId);
                    finishedBatch = batch;
                    emitNow = true;
                }
            }
            if (emitNow)
            {
                RaiseBatchFinished(BuildSummary(finishedBatch), false);
            }
        }

        private static BatchSummary BuildSummary(BatchState batch)
        {
            BatchSummary summary = new BatchSummary();
            summary.BatchId = batch.Id;
            summary.Total = batch.Total;
            summary.Ok = batch.Ok;
            summary.Failed = batch.Failed;
            summary.OriginalSize = batch.OriginalSize;
            summary.CompressedSize = batch.CompressedSize;
            summary.Ratio = batch.OriginalSize > 0
                ? 1.0 - (double)batch.CompressedSize / (double)batch.OriginalSize
                : 0.0;
            summary.Elapsed = Math.Max((DateTime.UtcNow - batch.StartedUtc).TotalSeconds, 0.0);
            summary.Errors.AddRange(batch.Errors);
            return summary;
        }

        private void RaiseTaskStarted(string name, bool isAuto)
        {
            Action<string, bool> handler = TaskStarted;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(name, isAuto);
            }
            catch (Exception ex)
            {
                AppLogger.Warning("TaskStarted handler error: " + ex.Message);
            }
        }

        private void RaiseTaskFinished(string name, bool ok, string message, bool isAuto)
        {
            Action<string, bool, string, bool> handler = TaskFinished;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(name, ok, message, isAuto);
            }
            catch (Exception ex)
            {
                AppLogger.Warning("TaskFinished handler error: " + ex.Message);
            }
        }

        private void RaiseBatchFinished(BatchSummary summary, bool isAuto)
        {
            Action<BatchSummary, bool> handler = BatchFinished;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(summary, isAuto);
            }
            catch (Exception ex)
            {
                AppLogger.Warning("BatchFinished handler error: " + ex.Message);
            }
        }

        private void RaiseQueueChanged()
        {
            Action<int> handler = QueueChanged;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(QueueSize);
            }
            catch (Exception ex)
            {
                AppLogger.Warning("QueueChanged handler error: " + ex.Message);
            }
        }
    }
}
