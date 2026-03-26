using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.Demo.Sync;
using Stateless;

namespace PF.Workstation.Demo
{
    /// <summary>
    /// 全局主控状态机（主线程管理器）
    ///
    /// 生命周期：
    ///   Uninitialized → InitializeAllAsync()  → Initializing → Idle
    ///   Idle          → StartAllAsync()        → Running
    ///   Running       → StopAll()              → Idle
    ///   Alarm         → ResetAllAsync()        → Resetting → Idle
    ///
    /// 职责：
    ///   · 管理所有子工站的生命周期（初始化/启动/暂停/恢复/停止/复位）
    ///   · 在构造时向 IStationSyncService 注册本方案所需的所有流水线信号量
    ///   · 在系统复位时重置信号量，确保下一轮启动状态正确
    ///
    /// 并发安全设计：
    ///   · 所有状态机跳转通过 _machineLock（SemaphoreSlim 1,1）独占执行。
    ///   · Running 状态使用 OnEntryAsync，启动所有子工站前等待其旧任务结束。
    ///   · ResetAllAsync 采用熔断机制：任一子工站复位失败则立即中断，
    ///     绝不在有异常的情况下触发 ResetDone，杜绝"假复位"。
    /// </summary>
    /// <summary>
    /// Demo机器专用的主控调度器 (业务层)
    /// 仅负责注册本机器特有的资源（如特有的协同信号量）
    /// </summary>
    public class DemoMachineController : BaseMasterController
    {
        private readonly IStationSyncService _sync;

        public DemoMachineController(
            ILogService logger,
            HardwareInputEventBus hardwareEventBus,
            IStationSyncService sync,
            IEnumerable<StationBase> subStations)
            : base(logger, hardwareEventBus, subStations)
        {
            _sync = sync;

            // 注册这台机器专有的流水线协同信号量
            _sync.Register(WorkstationSignals.SlotEmpty, initialCount: 1, maxCount: 1);
            _sync.Register(WorkstationSignals.ProductReady, initialCount: 0, maxCount: 1);
        }

        /// <summary>
        /// 重写父类钩子：在所有子工站物理复位成功后，安全重置本机器的流水线信号量
        /// </summary>
        protected override void OnAfterResetSuccess()
        {
            _logger.Info("【Demo机台主控】各工站物理复位完毕，正在重置流水线信号量...");
            _sync.ResetAll();
        }
    }
}
