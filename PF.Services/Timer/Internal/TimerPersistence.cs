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

        private async Task SaveToDiskAsync()
        {
            try
            {
                string json;
                lock (_lock)
                    json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });

                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
            }
            catch
            {
                // 持久化失败不影响运行时行为
            }
        }
    }
}
