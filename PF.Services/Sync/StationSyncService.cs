using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PF.Services.Sync
{
    /// <summary>
    /// 工站间信号量同步服务
    ///
    /// 架构升级要点：
    ///
    ///   1. Scoped 分组
    ///      信号量按工站（scope）分组存储在 ScopeContext 内，每个 scope 拥有独立的
    ///      复位广播令牌（ResetCts）。ResetScope 只影响目标 scope，不干扰其他工站。
    ///
    ///   2. Drain 屏障（排空屏障）
    ///      WaitAsync 进入时原子递增 InFlightCount，finally 中原子递减。
    ///      ResetScope 在 Cancel 广播令牌后，用 SpinWait 等待 InFlightCount 归零，
    ///      确保所有飞行中的 SemaphoreSlim.WaitAsync 内部续体都已完成退出，
    ///      再执行 Dispose + 重建，彻底消除 ObjectDisposedException 竞态窗口。
    ///
    ///   3. 删除 isAutodispose
    ///      Release 不再承担重建信号量的职责，统一由 ResetScope / ResetAll 负责。
    ///
    /// 生命周期：
    ///   启动阶段 → Register(...)  注册所有信号量（单线程，顺序调用）
    ///   运行阶段 → WaitAsync / Release（多线程并发调用，线程安全）
    ///   复位阶段 → ResetAll() / ResetScope()  排空后重建（各工站线程已停止后调用）
    ///   释放阶段 → Dispose()                  销毁全部资源（应用退出时）
    /// </summary>
    public sealed class StationSyncService : IStationSyncService, IDisposable
    {
        // ── 内部数据结构 ──────────────────────────────────────────────────────

        private readonly record struct SignalEntry(SemaphoreSlim Sem, int InitialCount, int MaxCount);

        /// <summary>
        /// 每个 scope（工站）持有的上下文：
        ///   · Signals      — 本 scope 下所有具名信号量
        ///   · ResetCts     — 专属复位广播令牌（Interlocked.Exchange 原子替换）
        ///   · InFlightCount — 当前正在 WaitAsync 内部执行的线程数（Interlocked 原子操作）
        /// </summary>
        private sealed class ScopeContext
        {
            public readonly ConcurrentDictionary<string, SignalEntry> Signals = new();
            public CancellationTokenSource ResetCts = new();
            public int InFlightCount;  // 通过 Interlocked.Increment/Decrement 操作
        }

        private readonly ConcurrentDictionary<string, ScopeContext> _scopes = new();
        private readonly ILogService _logger;

        private const string DefaultScope = "global";

        // ── 构造 ─────────────────────────────────────────────────────────────

        public StationSyncService(ILogService logger) => _logger = logger;

        // ── 注册 ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void Register(string name, int initialCount = 0, int maxCount = 1,
                             string scope = DefaultScope)
        {
            var ctx = _scopes.GetOrAdd(scope, _ => new ScopeContext());
            var entry = new SignalEntry(new SemaphoreSlim(initialCount, maxCount),
                                        initialCount, maxCount);

            if (!ctx.Signals.TryAdd(name, entry))
                throw new InvalidOperationException(
                    $"[SyncService] 信号量 '{name}'（scope='{scope}'）已注册，不允许重复注册。");

            _logger.Info($"[SyncService] [{scope}] 已注册信号量 '{name}'" +
                          $" (初始={initialCount}, 最大={maxCount})");
        }

        // ── 运行时操作（线程安全）────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// 排空屏障设计：
        ///   · 进入时 Interlocked.Increment(InFlightCount)
        ///   · finally 中 Interlocked.Decrement(InFlightCount)
        /// 无论正常通过还是被取消/异常退出，计数均能正确归零，
        /// 使 ResetScope 的 SpinWait 能够安全判断"飞行中线程已全部退出"。
        /// </remarks>
        public async Task WaitAsync(string name, CancellationToken token = default,
                                    string scope = DefaultScope)
        {
            var ctx = GetScope(scope);
            var entry = GetEntry(ctx, name, scope);

            _logger.Debug($"[SyncService] [{scope}/{name}] 等待中" +
                           $" (当前计数={entry.Sem.CurrentCount})");

            // 进入飞行区：原子递增，确保 ResetScope 能感知到此线程尚在执行
            Interlocked.Increment(ref ctx.InFlightCount);
            try
            {
                // 合并业务令牌与 scope 复位广播令牌：任意一个触发均可立即打断等待
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    token, ctx.ResetCts.Token);

                await entry.Sem.WaitAsync(linked.Token).ConfigureAwait(false);
                _logger.Debug($"[SyncService] [{scope}/{name}] 已获取");
            }
            finally
            {
                // 离开飞行区：无论成功、被取消还是异常，均原子递减
                Interlocked.Decrement(ref ctx.InFlightCount);
            }
        }

        /// <inheritdoc/>
        public void Release(string name, string scope = DefaultScope)
        {
            var ctx = GetScope(scope);
            var entry = GetEntry(ctx, name, scope);

            // 预检：计数已满则跳过，避免 SemaphoreFullException
            if (entry.Sem.CurrentCount >= entry.MaxCount)
            {
                _logger.Debug($"[SyncService] [{scope}/{name}] 信号量已达最大值" +
                               $" ({entry.MaxCount})，跳过多余释放。");
                return;
            }

            try
            {
                entry.Sem.Release();
                _logger.Debug($"[SyncService] [{scope}/{name}] 已释放" +
                               $" (释放后计数={entry.Sem.CurrentCount})");
            }
            catch (SemaphoreFullException)
            {
                // 极端竞态：预检通过后另一线程抢先释放触达上限，安全忽略
                _logger.Debug($"[SyncService] [{scope}/{name}] 释放冲突：" +
                               "信号量已被其他线程充满，忽略此次释放。");
            }
        }

        // ── 复位 ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void ResetAll()
        {
            foreach (var scopeName in _scopes.Keys)
                ResetScope(scopeName);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 三步走：
        ///   1. 广播取消：Cancel 旧 ResetCts，使所有 WaitAsync 调用方
        ///      立即抛出 OperationCanceledException 并执行 finally 中的 Decrement。
        ///   2. Drain 屏障：SpinWait 等待 InFlightCount 归零（带 1000ms 超时保护，
        ///      防止极端情况下业务线程阻塞导致无限等待）。
        ///   3. 安全重建：此时无任何线程持有旧 SemaphoreSlim，可安全 Dispose 并重建。
        /// </remarks>
        public void ResetScope(string scope)
        {
            if (!_scopes.TryGetValue(scope, out var ctx))
            {
                _logger.Warn($"[SyncService] ResetScope：scope '{scope}' 不存在，跳过。");
                return;
            }

            // ─ 步骤 1：广播取消，通知所有飞行中的 WaitAsync 退出 ────────────
            var oldCts = Interlocked.Exchange(ref ctx.ResetCts, new CancellationTokenSource());
            oldCts.Cancel();
            oldCts.Dispose();

            // ─ 步骤 2：Drain 屏障，等待 InFlightCount 归零 ───────────────────
            // Cancel 后，所有在 SemaphoreSlim.WaitAsync 中阻塞的线程将立即被唤醒、
            // 捕获 OperationCanceledException，并在 finally 块中执行 Decrement。
            // SpinWait 在微秒~毫秒级完成，CPU 占用极低且不阻塞线程池。
            var sw = Stopwatch.StartNew();
            var spin = new SpinWait();
            while (Volatile.Read(ref ctx.InFlightCount) > 0)
            {
                if (sw.ElapsedMilliseconds > 1000)
                {
                    _logger.Warn($"[SyncService] scope '{scope}' Drain 屏障超时（1000ms），" +
                                  "仍有飞行中线程未退出，强制继续重建信号量。" +
                                 $" InFlightCount={Volatile.Read(ref ctx.InFlightCount)}");
                    break;
                }
                spin.SpinOnce();
            }

            // ─ 步骤 3：安全重建信号量 ─────────────────────────────────────────
            foreach (var name in ctx.Signals.Keys)
            {
                var old = ctx.Signals[name];
                old.Sem.Dispose();

                ctx.Signals[name] = new SignalEntry(
                    new SemaphoreSlim(old.InitialCount, old.MaxCount),
                    old.InitialCount,
                    old.MaxCount);

                _logger.Info($"[SyncService] [{scope}] 信号量 '{name}' 已复位" +
                              $" → 初始计数={old.InitialCount}");
            }
        }

        /// <inheritdoc/>
        public void ResetSingleSignal(string name, string scope = DefaultScope)
        {
            if (!_scopes.TryGetValue(scope, out var ctx))
            {
                _logger.Warn($"[SyncService] ResetSingleSignal：scope '{scope}' 不存在，跳过。");
                return;
            }
            if (!ctx.Signals.ContainsKey(name))
            {
                _logger.Warn($"[SyncService] ResetSingleSignal：信号量 '{name}'（scope='{scope}'）未注册，跳过。");
                return;
            }

            // 步骤 1：广播取消（同一 scope 内所有 WaitAsync 均被唤醒，代价可接受）
            var oldCts = Interlocked.Exchange(ref ctx.ResetCts, new CancellationTokenSource());
            oldCts.Cancel();
            oldCts.Dispose();

            // 步骤 2：Drain 屏障，等待此 scope 的飞行计数归零
            var sw = Stopwatch.StartNew();
            var spin = new SpinWait();
            while (Volatile.Read(ref ctx.InFlightCount) > 0)
            {
                if (sw.ElapsedMilliseconds > 1000)
                {
                    _logger.Warn($"[SyncService] scope '{scope}' 单点复位 Drain 超时（1000ms），" +
                                 $" InFlightCount={Volatile.Read(ref ctx.InFlightCount)}，强制继续。");
                    break;
                }
                spin.SpinOnce();
            }

            // 步骤 3：仅重建目标信号量
            var old = ctx.Signals[name];
            old.Sem.Dispose();
            ctx.Signals[name] = new SignalEntry(
                new SemaphoreSlim(old.InitialCount, old.MaxCount),
                old.InitialCount,
                old.MaxCount);

            _logger.Info($"[SyncService] [{scope}] 信号量 '{name}' 单点复位完成" +
                          $" → 初始计数={old.InitialCount}");
        }

        // ── 销毁 ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            foreach (var ctx in _scopes.Values)
            {
                // 广播取消，唤醒所有仍在等待的线程（应用退出场景）
                var cts = Interlocked.Exchange(ref ctx.ResetCts, new CancellationTokenSource());
                cts.Cancel();
                cts.Dispose();

                foreach (var entry in ctx.Signals.Values)
                    entry.Sem.Dispose();
            }
            _scopes.Clear();
        }

        // ── 状态快照（供监控 UI 轮询）────────────────────────────────────────

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, (int InitialCount, int CurrentCount)> GetSnapshot()
        {
            var result = new Dictionary<string, (int, int)>();
            foreach (var (scopeName, ctx) in _scopes)
            {
                foreach (var (name, entry) in ctx.Signals)
                {
                    // Key 格式："scope/name"，与 "global/工位1允许拉料" 对齐
                    result[$"{scopeName}/{name}"] = (entry.InitialCount, entry.Sem.CurrentCount);
                }
            }
            return result;
        }

        // ── 私有辅助 ─────────────────────────────────────────────────────────

        private ScopeContext GetScope(string scope)
        {
            if (!_scopes.TryGetValue(scope, out var ctx))
                throw new KeyNotFoundException(
                    $"[SyncService] scope '{scope}' 不存在，" +
                    $"请先调用 Register(..., scope: \"{scope}\") 注册至少一个信号量。");
            return ctx;
        }

        private static SignalEntry GetEntry(ScopeContext ctx, string name, string scope)
        {
            if (!ctx.Signals.TryGetValue(name, out var entry))
                throw new KeyNotFoundException(
                    $"[SyncService] 信号量 '{name}'（scope='{scope}'）未注册。" +
                    $"请先调用 Register('{name}', scope: \"{scope}\")。");
            return entry;
        }
    }
}
