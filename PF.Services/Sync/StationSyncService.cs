using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using System.Collections.Concurrent;

namespace PF.Services.Sync
{
    /// <summary>
    /// 工站间信号量同步服务
    ///
    /// 内部实现：每个具名信号量对应一个 SemaphoreSlim 实例，
    /// 存储在 ConcurrentDictionary 中以支持多线程并发读取。
    ///
    /// 生命周期：
    ///   启动阶段 → Register(...)  注册所有信号量（单线程，顺序调用）
    ///   运行阶段 → WaitAsync / Release（多线程并发调用，线程安全）
    ///   复位阶段 → ResetAll()     重建所有信号量至初始状态（各工站线程已停止后调用）
    ///   释放阶段 → Dispose()      销毁所有 SemaphoreSlim（应用退出时）
    /// </summary>
    public sealed class StationSyncService : IStationSyncService, IDisposable
    {
        // 使用 record struct 紧凑地存储信号量及其注册时的初始参数
        private readonly record struct SignalEntry(SemaphoreSlim Sem, int InitialCount, int MaxCount);

        private readonly ConcurrentDictionary<string, SignalEntry> _signals = new();
        private readonly ILogService _logger;

        public StationSyncService(ILogService logger) => _logger = logger;

        // ── 注册 ──────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void Register(string name, int initialCount = 0, int maxCount = 1)
        {
            var entry = new SignalEntry(new SemaphoreSlim(initialCount, maxCount), initialCount, maxCount);

            if (!_signals.TryAdd(name, entry))
                throw new InvalidOperationException(
                    $"[SyncService] 信号量 '{name}' 已注册，不允许重复注册。");

            _logger.Info($"[SyncService] 已注册信号量 '{name}' (初始={initialCount}, 最大={maxCount})");
        }

        // ── 运行时操作（线程安全）────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task WaitAsync(string name, CancellationToken token = default)
        {
            var entry = GetEntry(name);
            _logger.Debug($"[SyncService] [{name}] 等待中 (当前计数={entry.Sem.CurrentCount})");
            await entry.Sem.WaitAsync(token);
            _logger.Debug($"[SyncService] [{name}] 已获取");
        }

        /// <inheritdoc/>
        public void Release(string name)
        {
            var entry = GetEntry(name);
            entry.Sem.Release();
            _logger.Debug($"[SyncService] [{name}] 已释放 (释放后计数={entry.Sem.CurrentCount})");
        }

        // ── 复位 ──────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// 重建策略：销毁旧 SemaphoreSlim，用注册时保存的初始参数新建一个。
        /// 前提：调用此方法时所有工站线程必须已停止（否则正在 Wait 的线程会持有
        /// 旧对象引用，新建对象对其无效）。MasterController.ResetAll() 在
        /// 子工站全部停止后才调用此方法，满足前提条件。
        /// </remarks>
        public void ResetAll()
        {
            foreach (var name in _signals.Keys)
            {
                var old = _signals[name];
                old.Sem.Dispose();

                _signals[name] = new SignalEntry(
                    new SemaphoreSlim(old.InitialCount, old.MaxCount),
                    old.InitialCount,
                    old.MaxCount);

                _logger.Info($"[SyncService] 信号量 '{name}' 已复位 → 初始计数={old.InitialCount}");
            }
        }

        // ── 销毁 ──────────────────────────────────────────────────────────────

        public void Dispose()
        {
            foreach (var entry in _signals.Values)
                entry.Sem.Dispose();
            _signals.Clear();
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────────

        private SignalEntry GetEntry(string name)
        {
            if (!_signals.TryGetValue(name, out var entry))
                throw new KeyNotFoundException(
                    $"[SyncService] 信号量 '{name}' 未注册。请先调用 Register('{name}', ...)。");
            return entry;
        }
    }
}
