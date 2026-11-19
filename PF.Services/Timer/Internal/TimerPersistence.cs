using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Services.Timer.Internal
{
    /// <summary>
    /// 定点调度末次执行时间的轻量 JSON 持久化层。
    /// 读取仅发生在启动时，写入在每次调度触发后异步执行，不阻塞定时器线程。
    /// </summary>
    internal sealed class TimerPersistence
    {
        private readonly string _filePath;
        private readonly object _lock = new();
        private Dictionary<string, DateTime> _data = new();

        public TimerPersistence(string filePath)
        {
            _filePath = filePath;
            LoadFromDisk();
        }

        /// <summary>读取指定 key 的末次执行时间，不存在则返回 DateTime.MinValue。</summary>
        public DateTime Load(string key)
        {
            lock (_lock)
                return _data.TryGetValue(key, out var value) ? value : DateTime.MinValue;
        }

        /// <summary>更新指定 key 的末次执行时间，并异步写盘。</summary>
        public void Save(string key, DateTime time)
        {
            lock (_lock)
                _data[key] = time;
            _ = SaveToDiskAsync();
        }

        private void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath);
                _data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? new();
            }
            catch
            {
                _data = new();
            }
        }

        // 多个定点任务同一分钟触发时会并发写同一个文件：串行化 + 原子写入，避免写坏后丢失全部末次执行时间
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        private async Task SaveToDiskAsync()
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                string json;
                lock (_lock)
                    json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });

                PF.Infrastructure.Persistence.AtomicFile.WriteAllText(_filePath, json);
            }
            catch
            {
                // 持久化失败不影响运行时行为
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
