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
    ///
    /// 并发安全增强（Direction 4）：
    ///   · _resetCts 广播令牌：ResetAll() 调用时先 Cancel 旧令牌，
    ///     使所有正在 WaitAsync 的调用方立即收到 OperationCanceledException 并安全退出。
    ///   · 只有在确认没有线程阻塞在旧信号量上之后，再执行 Dispose + 重建操作，
    ///     彻底消除 ObjectDisposedException 与死锁风险。
    /// </summary>
    public sealed class StationSyncService : IStationSyncService, IDisposable
    {
        // 使用 record struct 紧凑地存储信号量及其注册时的初始参数
        private readonly record struct SignalEntry(SemaphoreSlim Sem, int InitialCount, int MaxCount);

        private readonly ConcurrentDictionary<string, SignalEntry> _signals = new();
        private readonly ILogService _logger;

        // ── 复位广播令牌 ──────────────────────────────────────────────────────
        // ResetAll() 调用时取消此令牌，使所有阻塞在 WaitAsync 的线程
        // 立即退出，防止旧 SemaphoreSlim 被 Dispose 时抛出 ObjectDisposedException。
        // 用 Interlocked.Exchange 原子替换，提供完整的内存有序语义。
        private CancellationTokenSource _resetCts = new();

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
        /// <remarks>
        /// 内部将调用方传入的 <paramref name="token"/> 与 _resetCts.Token 合并为联合令牌。
        /// 无论是调用方主动取消（急停/停止）还是 ResetAll() 广播取消，
        /// 都能立即打断等待，彻底消除因 Dispose 旧信号量引发 ObjectDisposedException 的风险。
        /// </remarks>
        public async Task WaitAsync(string name, CancellationToken token = default)
        {
            var entry = GetEntry(name);
            _logger.Debug($"[SyncService] [{name}] 等待中 (当前计数={entry.Sem.CurrentCount})");

            // 将业务取消令牌与复位广播令牌合并：任意一个触发都能立即打断等待
            // 读取 _resetCts 引用（引用类型在 64-bit 平台的读写本身是原子的）
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token, _resetCts.Token);

            await entry.Sem.WaitAsync(linked.Token);
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
        /// 安全重置流程（两步走）：
        ///   1. 广播取消：Cancel 旧 _resetCts，使所有尚在阻塞的 WaitAsync 调用
        ///      立即抛出 OperationCanceledException 并退出，保证旧信号量无人持有。
        ///   2. 重建信号量：Dispose 旧 SemaphoreSlim，以注册时的初始参数新建一个。
        /// 前提：此方法由 MasterControllerView.ResetAllAsync() 在所有子工站成功复位后调用。
        ///       结合熔断机制，确保调用时子工站业务线程已停止或正在退出。
        /// </remarks>
        public void ResetAll()
        {
            // 步骤 1：取消所有正在 WaitAsync 中阻塞的调用，确保旧信号量无人持有
            var oldCts = Interlocked.Exchange(ref _resetCts, new CancellationTokenSource());
            oldCts.Cancel();
            oldCts.Dispose();

            // 步骤 2：安全地销毁旧信号量并重建（此时不再有线程阻塞在其上）
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
            // 广播取消，唤醒所有仍在等待的线程（应用退出场景）
            var cts = Interlocked.Exchange(ref _resetCts, new CancellationTokenSource());
            cts.Cancel();
            cts.Dispose();

            foreach (var entry in _signals.Values)
                entry.Sem.Dispose();

            _signals.Clear();
        }

        // ── 状态快照（供监控 UI 轮询）────────────────────────────────────────

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, (int InitialCount, int CurrentCount)> GetSnapshot()
        {
            return _signals.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value.InitialCount, kv.Value.Sem.CurrentCount));
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
