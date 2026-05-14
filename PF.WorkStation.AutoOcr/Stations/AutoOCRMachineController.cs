using PF.Core.Constants;
using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System.Collections.Generic;
using System.Linq;

namespace PF.WorkStation.AutoOcr.Stations
{
    #region Inter-Station Synchronization Signals (跨工站同步信号枚举)

    /// <summary>
    /// 定义全自动 OCR 机台内部，各工站之间协同流转的握手同步信号。
    /// 配合 <see cref="IStationSyncService"/> 使用，实现生产者-消费者模式的跨线程/跨工站调度。
    /// </summary>
    public enum WorkstationSignals
    {
        // ── 工位 1 核心流转信号 ──

        /// <summary>物理启动按钮按下，作为整个流转的源头触发信号</summary>
        工位1启动按钮按下,
        /// <summary>上下料工站就绪，通知拉料工站可以执行 Y 轴取料</summary>
        工位1允许拉料,
        /// <summary>拉料工站完成取料退出，通知上下料工站可以执行后续 Z 轴动作</summary>
        工位1拉料完成,
        /// <summary>上下料工站准备就绪，允许拉料工站将检测完的晶圆推回料盒</summary>
        工位1允许退料,
        /// <summary>拉料工站推回晶圆完毕并安全撤出，流转闭环</summary>
        工位1退料完成,
        /// <summary>拉料工站将晶圆拉出至检测位，通知 OCR 视觉工站可以开始拍照解码</summary>
        工位1允许检测,
        /// <summary>OCR 视觉工站检测完毕，通知拉料工站可以开始退料或进行异常排单</summary>
        工位1检测完成,
        /// <summary>人工介入处理异常或手动下料完成的确认信号</summary>
        工位1人工下料完成,

        // ── 工位 2 核心流转信号 (部分预留扩展) ──

        /// <summary>物理启动按钮按下，触发工位 2 流转</summary>
        工位2启动按钮按下,
        /// <summary>工位2允许拉料</summary>
        工位2允许拉料,
        /// <summary>工位2拉料完成</summary>
        工位2拉料完成,
        /// <summary>工位2允许退料</summary>
        工位2允许退料,
        /// <summary>工位2退料完成</summary>
        工位2退料完成,
        /// <summary>工位 2 拉料就位，允许执行检测</summary>
        工位2允许检测,
        /// <summary>工位2检测完成</summary>
        工位2检测完成,
        /// <summary>工位2人工下料完成</summary>
        工位2人工下料完成,
        /***********复位完成标志*********/
        检测模组复位完成,

        工位1拉料复位完成,

        工位2拉料复位完成,

    }

    #endregion

    /// <summary>
    /// 全自动 OCR 机台全局主控制器 (Master Controller)
    /// <para>职责：继承自 <see cref="BaseMasterController"/>，负责管理顶层全局硬件输入（如物理启动按钮）、
    /// 维护安全光栅/门禁的监控机制，并初始化全线各子工站的交互信号量字典。</para>
    /// </summary>
    public class AutoOCRMachineController : BaseMasterController
    {
        #region Fields (依赖服务)

        /// <summary>
        /// 硬件输入状态监视器，用于安全门、急停等全局安全信号的独立后台监控
        /// </summary>
        private readonly IHardwareInputMonitor _hardwareInputMonitor;

        /// <summary>
        /// 全局工站同步服务，用于注册、阻塞等待和释放跨工站交互的信号量
        /// </summary>
        private readonly IStationSyncService _sync;

        private readonly IParamService _paramService;

        private volatile bool _isReinitializationRequired;
        private Core.Enums.MachineState _previousState = Core.Enums.MachineState.Uninitialized;

        private static readonly HashSet<string> _reinitTriggerParams = new()
        {
            E_Params.WorkStation1_Muted.ToString(),
            E_Params.WorkStation2_Muted.ToString(),
        };

        public override bool IsReinitializationRequired => _isReinitializationRequired;
        public override event EventHandler? ReinitializationRequired;

        #endregion

        #region Constructor & Signal Registration (构造与信号注册)

        /// <summary>
        /// 实例化 OCR 机台主控，并向底层注入核心服务与子工站集合
        /// </summary>
        public AutoOCRMachineController(
            ILogService logger,
            IAlarmService alarmService,
            HardwareInputEventBus hardwareEventBus,
            IHardwareInputMonitor hardwareInputMonitor,
            IStationSyncService sync,
            IParamService paramService,
            IEnumerable<IStation> subStations)
            : base(logger, hardwareEventBus, subStations, alarmService)
        {
            _hardwareInputMonitor = hardwareInputMonitor;
            _sync = sync;
            _paramService = paramService;
            _paramService.ParamChanged += OnParamChanged;

            // 根据主控状态驱动 Safety 监控线程的启停
            MasterStateChanged += OnMasterStateChanged;

            // ── 工位 1 信号注册 (明确指定 Scope 作用域以实现精准的生命周期管理) ──

            // 启动按钮与人工下料信号均归属各自上下料工站 scope，
            // 防止 ResetSingleSignal 广播取消 global scope 时跨工站误伤对方的 WaitAsync
            _sync.Register(nameof(WorkstationSignals.工位1启动按钮按下), scope: E_WorkStation.工位1上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1人工下料完成), scope: E_WorkStation.工位1上下料工站.ToString());

            // 局部作用域信号 (绑定至具体执行工站，工站复位时会自动清理其 Scope 下的信号残存)
            _sync.Register(nameof(WorkstationSignals.工位1允许拉料), scope: E_WorkStation.工位1上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1拉料完成), scope: E_WorkStation.工位1拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1允许退料), scope: E_WorkStation.工位1上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1退料完成), scope: E_WorkStation.工位1拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1允许检测), scope: E_WorkStation.工位1拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1检测完成), scope: E_WorkStation.OCR检测工站.ToString());

            // ── 工位 2 信号注册 ──

            _sync.Register(nameof(WorkstationSignals.工位2启动按钮按下), scope: E_WorkStation.工位2上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2人工下料完成), scope: E_WorkStation.工位2上下料工站.ToString());

            // 局部作用域信号
            _sync.Register(nameof(WorkstationSignals.工位2允许拉料), scope: E_WorkStation.工位2上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2拉料完成), scope: E_WorkStation.工位2拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2允许退料), scope: E_WorkStation.工位2上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2退料完成), scope: E_WorkStation.工位2拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2允许检测), scope: E_WorkStation.工位2拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2检测完成), scope: E_WorkStation.OCR检测工站.ToString());


            _sync.Register(nameof(WorkstationSignals.检测模组复位完成), maxCount: 2, scope: "复位");

            _sync.Register(nameof(WorkstationSignals.工位1拉料复位完成), scope: "复位");

            _sync.Register(nameof(WorkstationSignals.工位2拉料复位完成), scope: "复位");
            MasterAlarmTriggered += AutoOCRMachineController_MasterAlarmTriggered;
        }

        private void AutoOCRMachineController_MasterAlarmTriggered(object? sender, StationAlarmEventArgs e)
        {
            if (MasterCameFromInitAlarm)
            {
                _sync?.ResetScope("复位");
            }
        }

        #endregion

        #region Hardware Event Routing (全局硬件事件路由)

        /// <summary>
        /// 拦截并处理底层广播的物理硬件输入事件。
        /// 工位1/2安全门使用独立通道（SafeDoor1/SafeDoor2），直接在此处理，不走基类通用 SafeDoor 路径。
        /// 其余标准输入（Start/Pause/Reset）仍由基类路由。
        /// </summary>
        /// <param name="inputType">事件类型标识符</param>
        protected override void OnHardwareInputReceived(string inputType)
        {
            // 工位独立安全门：PauseAll + 触发各自专属报警码，不调用基类（基类只处理通用 SafeDoor）
            switch (inputType)
            {
                case HardwareInputType.SafeDoor1:
                    PauseAll();
                    TriggerMasterAlarm(AlarmCodes.Safety.SafeDoorOpen1);
                    return;
                case HardwareInputType.SafeDoor2:
                    PauseAll();
                    TriggerMasterAlarm(AlarmCodes.Safety.SafeDoorOpen2);
                    return;
            }

            // 优先执行基类中封装的标准路由 (如标准 Start/Stop/Reset/Pause 状态机流转)
            base.OnHardwareInputReceived(inputType);
            if (CurrentState == Core.Enums.MachineState.Running)
            {
                switch (inputType)
                {
                    case HardwareInputTypeExtension.WorkStation1Start:
                        _sync.Release(nameof(WorkstationSignals.工位1启动按钮按下), scope: E_WorkStation.工位1上下料工站.ToString());
                        break;
                    case HardwareInputTypeExtension.WorkStation2Start:
                        _sync.Release(nameof(WorkstationSignals.工位2启动按钮按下), scope: E_WorkStation.工位2上下料工站.ToString());
                        break;
                }
            }
        }




        private void OnParamChanged(object? sender, ParamChangedEventArgs e)
        {
            if (!_reinitTriggerParams.Contains(e.ParamName)) return;
            _isReinitializationRequired = true;
            ReinitializationRequired?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 监听主控状态变迁，根据状态驱动 Safety 监控线程的启停。
        /// Running  → 启动 Safety 监控（此时屏蔽参数会被重新从数据库加载）。
        /// 安全门监控需在 Running / Paused / Idle / Alarm 等所有运行态下保持运行，
        /// 否则安全门关闭的恢复事件无法被检测到，导致 ClearAlarm 不被调用，
        /// 下一次安全门触发时报警服务因去重而无法弹窗。
        /// Standard 监控由 App.xaml.cs 在启动画面结束后统一启动，此处不干预。
        /// </summary>
        private void OnMasterStateChanged(object? sender, Core.Enums.MachineState newState)
        {
            try
            {
                // 从 Initializing → Idle 表示初始化成功，清除重初始化标记
                if (newState == Core.Enums.MachineState.Idle
                    && _previousState == Core.Enums.MachineState.Initializing)
                {
                    _isReinitializationRequired = false;
                }
                _previousState = newState;

                if (newState == Core.Enums.MachineState.Running)
                {
                    _logger.Info("【主控】机台进入 Running，启动 Safety 监控...");
                    _hardwareInputMonitor.StartSafetyMonitoring();
                    // 兜底：清除 IsEnabled 残留的 false 状态，确保安全门监控有效
                    _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.工位1门锁), true);
                    _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.工位2门锁), true);
                }
                else if (newState == Core.Enums.MachineState.Paused)
                {
                    _logger.Info("【主控】机台进入 Paused，禁用安全门检测（允许操作员进门查看）...");
                    _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.工位1门锁), false);
                    _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.工位2门锁), false);
                }
                else if (newState == Core.Enums.MachineState.Uninitialized)
                {
                    _hardwareInputMonitor.StopSafetyMonitoring();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】Safety 监控状态切换异常：{ex.Message}");
            }
        }

        #endregion

        #region Hardware Restore Routing (安全信号恢复路由)

        /// <summary>
        /// 安全门关闭时，仅清除该扇门自身的报警并重新启用其检测。
        /// 工位1/2各自独立处理，互不影响。其余通用安全门走基类路径。
        /// </summary>
        protected override void OnHardwareInputRestored(string inputType)
        {
            switch (inputType)
            {
                case HardwareInputType.SafeDoor1:
                {
                    var door = _hardwareInputMonitor.GetSafetyDoorSnapshot()
                        .FirstOrDefault(d => d.Name == nameof(E_InPutName.工位1门锁));
                    if (door?.IsActive == false)
                    {
                        ClearMasterAlarm(AlarmCodes.Safety.SafeDoorOpen1);
                        _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.工位1门锁), true);
                        _logger.Info("【主控】工位1安全门已关闭，清除报警并重新启用检测。");
                    }
                    else
                    {
                        _logger.Info("【主控】工位1安全门仍处于开启状态，保持报警激活。");
                    }
                    break;
                }
                case HardwareInputType.SafeDoor2:
                {
                    var door = _hardwareInputMonitor.GetSafetyDoorSnapshot()
                        .FirstOrDefault(d => d.Name == nameof(E_InPutName.工位2门锁));
                    if (door?.IsActive == false)
                    {
                        ClearMasterAlarm(AlarmCodes.Safety.SafeDoorOpen2);
                        _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.工位2门锁), true);
                        _logger.Info("【主控】工位2安全门已关闭，清除报警并重新启用检测。");
                    }
                    else
                    {
                        _logger.Info("【主控】工位2安全门仍处于开启状态，保持报警激活。");
                    }
                    break;
                }
                default:
                    base.OnHardwareInputRestored(inputType);
                    break;
            }
        }

        #endregion

        #region Lifecycle Hooks (生命周期钩子)

        /// <summary>
        /// 重写父类钩子：处理全线复位成功后的收尾清理工作。
        /// <para>各子工站已经在自己的 ExecuteResetAsync 中按 Scope 清理了本工站作用域内的信号量。
        /// 故主控在此仅清理 <c>global</c> 作用域（如物理按钮按下、人工确认等无明确工站归属的共享信号量），
        /// 避免越权重置其他工站的专属 Scope 造成流转逻辑错乱。</para>
        /// </summary>
        protected override void OnAfterResetSuccess()
        {
            // 仅初始化报警复位时重置全局信号量；运行期报警复位保留信号量以支持断点续跑
            if (MasterCameFromInitAlarm)
            {
                _logger.Info("【主控】初始化报警复位，重置 global 作用域信号量...");
                _sync.ResetScope("global");
            }
            else
            {
                _logger.Info("【主控】运行期报警复位，保留信号量以支持断点续跑。");
                _sync.ResetScope("复位");
            }
        }

        #endregion
    }
}