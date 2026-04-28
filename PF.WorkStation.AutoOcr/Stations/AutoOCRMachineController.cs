using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System.Collections.Generic;

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
        工位2人工下料完成
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
            IEnumerable<IStation> subStations)
            : base(logger, hardwareEventBus, subStations, alarmService)
        {
            _hardwareInputMonitor = hardwareInputMonitor;
            _sync = sync;

            // 根据主控状态驱动 Safety 监控线程的启停
            MasterStateChanged += OnMasterStateChanged;

            // ── 工位 1 信号注册 (明确指定 Scope 作用域以实现精准的生命周期管理) ──

            // 全局作用域信号 (无归属的物理按钮动作)
            _sync.Register(nameof(WorkstationSignals.工位1启动按钮按下));
            _sync.Register(nameof(WorkstationSignals.工位1人工下料完成));

            // 局部作用域信号 (绑定至具体执行工站，工站复位时会自动清理其 Scope 下的信号残存)
            _sync.Register(nameof(WorkstationSignals.工位1允许拉料), scope: E_WorkStation.工位1上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1拉料完成), scope: E_WorkStation.工位1拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1允许退料), scope: E_WorkStation.工位1上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1退料完成), scope: E_WorkStation.工位1拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1允许检测), scope: E_WorkStation.工位1拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位1检测完成), scope: E_WorkStation.OCR检测工站.ToString());

            // ── 工位 2 信号注册 ──

            // 全局作用域信号
            _sync.Register(nameof(WorkstationSignals.工位2启动按钮按下));
            _sync.Register(nameof(WorkstationSignals.工位2人工下料完成));

            // 局部作用域信号
            _sync.Register(nameof(WorkstationSignals.工位2允许拉料), scope: E_WorkStation.工位2上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2拉料完成), scope: E_WorkStation.工位2拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2允许退料), scope: E_WorkStation.工位2上下料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2退料完成), scope: E_WorkStation.工位2拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2允许检测), scope: E_WorkStation.工位2拉料工站.ToString());
            _sync.Register(nameof(WorkstationSignals.工位2检测完成), scope: E_WorkStation.OCR检测工站.ToString());
        }

        #endregion

        #region Hardware Event Routing (全局硬件事件路由)

        /// <summary>
        /// 拦截并处理底层广播的物理硬件输入事件。
        /// 扩展了基类的标准按键处理，追加了具体工站的启动信号释放。
        /// Safety 监控的启停已改由 <see cref="OnMasterStateChanged"/> 根据机台状态统一管理。
        /// </summary>
        /// <param name="inputType">事件类型标识符</param>
        protected override void OnHardwareInputReceived(string inputType)
        {
            // 优先执行基类中封装的标准路由 (如标准 Start/Stop/Reset/Pause 状态机流转)
            base.OnHardwareInputReceived(inputType);
            if (CurrentState == Core.Enums.MachineState.Running)
            {
                switch (inputType)
                {
                    case HardwareInputTypeExtension.WorkStation1Start:
                        _sync.Release(nameof(WorkstationSignals.工位1启动按钮按下));
                        _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.电磁门锁1_2信号),true);
                        break;

                    case HardwareInputTypeExtension.WorkStation2Start:
                        _sync.Release(nameof(WorkstationSignals.工位2启动按钮按下));
                        _hardwareInputMonitor.SetSafetyDoorEnabled(nameof(E_InPutName.电磁门锁1_2信号), true);
                        break;
                }
            }
        }

        /// <summary>
        /// 监听主控状态变迁，根据状态驱动 Safety 监控线程的启停。
        /// Running  → 启动 Safety 监控（此时屏蔽参数会被重新从数据库加载）。
        /// 非Running → 停止 Safety 监控（Uninitialized / Idle / Alarm 均停止）。
        /// Standard 监控由 App.xaml.cs 在启动画面结束后统一启动，此处不干预。
        /// </summary>
        private void OnMasterStateChanged(object? sender, Core.Enums.MachineState newState)
        {
            try
            {
                if (newState == Core.Enums.MachineState.Running)
                {
                    _logger.Info("【主控】机台进入 Running，启动 Safety 监控...");
                    _hardwareInputMonitor.StartSafetyMonitoring();
                }
                else
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
            }
        }

        #endregion
    }
}