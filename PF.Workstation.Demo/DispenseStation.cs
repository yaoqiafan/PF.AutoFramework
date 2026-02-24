using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.Demo.Sync;

namespace PF.Workstation.Demo
{
    /// <summary>
    /// 【工站层示例】点胶工站（流水线协同示例）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  流水线协同逻辑
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///  工站线程（Task.Run 新线程池线程）
    ///    │  ProcessLoopAsync(token)
    ///    │    │
    ///    │    ├─ _pauseEvent.Wait(token)          ← 暂停点①
    ///    │    │
    ///    │    ├─ await _sync.WaitAsync(ProductReady)  ← ★ 协同点：等待产品到位
    ///    │    │     阻塞直到取放工站放料完成并 Release(ProductReady)
    ///    │    │
    ///    │    ├─ _pauseEvent.Wait(token)          ← 暂停点②（防叠加暂停）
    ///    │    │
    ///    │    ├─ [执行点胶动作]
    ///    │    │
    ///    │    └─ _sync.Release(SlotEmpty)         ← ★ 协同点：通知取放工站槽位已释放
    ///    │
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    public class DispenseStation : StationBase
    {
        private readonly IStationSyncService _sync;

        // 可以在构造函数继续注入点胶机构模组 (IMechanism)
        public DispenseStation(IStationSyncService sync, ILogService logger)
            : base("点胶工站", logger)
        {
            _sync = sync;
        }

        protected override async Task ProcessLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ①
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                // ── Step 1: 等待产品到位（★ 流水线协同核心）──────────────────
                // 初始状态 ProductReady=0，本工站在此阻塞。
                // 取放工站放料完成后调用 Release(ProductReady)，本工站才被唤醒。
                _logger.Info($"[{StationName}] [1/3] 等待产品到位...");
                await _sync.WaitAsync(WorkstationSignals.ProductReady, token);

                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ②（在获取信号量后、执行动作前再次检查暂停）
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                // ── Step 2: 执行点胶动作 ───────────────────────────────────
                _logger.Info($"[{StationName}] [2/3] 开始执行点胶动作...");
                // await _dispenseMechanism.DispenseAsync(token);
                await Task.Delay(2000, token); // 模拟耗时动作
                _logger.Success($"[{StationName}] 点胶完成。");

                // ── Step 3: 释放槽位（★ 流水线协同核心）──────────────────────
                // 通知取放工站：工作台槽位已空闲，可以放入下一个产品。
                _sync.Release(WorkstationSignals.SlotEmpty);
                _logger.Info($"[{StationName}] [3/3] 已通知取放工站：槽位已释放");
            }
        }
    }
}
