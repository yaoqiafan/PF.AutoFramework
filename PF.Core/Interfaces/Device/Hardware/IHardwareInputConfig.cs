using System.Collections.Generic;

namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 扫描分组：Standard = 普通按键（允许防抖），Safety = 安全传感器（零延迟）。
    /// </summary>
    public enum InputScanGroup
    {
        /// <summary>普通按键（允许防抖）</summary>
        Standard,
        /// <summary>安全传感器（零延迟）</summary>
        Safety
    }

    /// <summary>
    /// 单个硬件输入点的配置描述。
    /// </summary>
    public interface IHardwareInputConfig
    {
        /// <summary>输入类型标识，对应 HardwareInputType 常量或工站自定义字符串。</summary>
        string InputType { get; }

        /// <summary>IO 卡端口号，传入 IIOController.ReadInput(int portIndex)。</summary>
        int Port { get; }

        /// <summary>防抖等待时间（毫秒），Safety 组必须设为 0。</summary>
        int DebounceMs { get; }

        /// <summary>可读名称，用于日志输出。</summary>
        string Name { get; }

        /// <summary>所属扫描分组，决定该输入在哪个线程中被轮询。</summary>
        InputScanGroup ScanGroup { get; }

        /// <summary>
        /// 是否屏蔽此输入点的扫描（运行时可修改）。
        /// true = 屏蔽（跳过事件发布）；false = 正常扫描（默认值）。
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// 接线方式。false = 常闭 NC（默认，断开触发）；true = 常开 NO（闭合触发）。
        /// </summary>
        bool NormallyOpen { get; }

        /// <summary>
        /// 运行时屏蔽参数键名，对应 IParamService 中的 SystemConfigParam 键。
        /// 为 null 时不进行动态屏蔽状态加载。
        /// </summary>
        string? MuteParamKey { get; }

        /// <summary>
        /// 该安全门绑定的门锁输出端口（传入 IIOController.WriteOutput）。
        /// null 或空 = 该点位无门锁（默认）；一个门磁对应多把锁时配置多个端口。
        /// 仅 Safety 组有效。
        /// </summary>
        IReadOnlyList<int>? LockOutputPorts => null;

        /// <summary>
        /// 门锁输出有效电平：true = 输出 ON 为上锁（默认）；false = 输出 OFF 为上锁。
        /// </summary>
        bool LockOutputActiveHigh => true;

        /// <summary>
        /// 信号仅在上锁后有效（锁定监控型安全开关：信号含义为"门已关且已锁定"，解锁时恒为激活态）。
        /// true 时：解锁期间不扫描该门、状态视为未知；上锁后须经 <see cref="LockSettleMs"/> 稳定才信任信号，
        /// 并由 PreLockAndVerifyAsync / ArmSafetyDoorAsync"先锁后验"。须同时配置 <see cref="LockOutputPorts"/>。
        /// </summary>
        bool SignalValidOnlyWhenLocked => false;

        /// <summary>
        /// 上锁后信号稳定所需时间（毫秒）。仅 <see cref="SignalValidOnlyWhenLocked"/> 为 true 时生效；
        /// 填 0 时按默认 500ms。
        /// </summary>
        int LockSettleMs => 0;
    }

    /// <summary>
    /// 硬件输入监控服务接口
    /// </summary>
    public interface IHardwareInputMonitor : IDisposable
    {
        /// <summary>
        /// 启动普通按键监控（系统启动时调用，全局常驻运行）
        /// </summary>
        void StartStandardMonitoring(CancellationToken externalToken = default);

        /// <summary>
        /// 停止普通按键监控
        /// </summary>
        void StopStandardMonitoring();

        /// <summary>
        /// 启动安全装置监控（工站开始运行时调用）
        /// </summary>
        void StartSafetyMonitoring(CancellationToken externalToken = default);

        /// <summary>
        /// 停止安全装置监控（工站停止运行时调用）
        /// </summary>
        void StopSafetyMonitoring();

        /// <summary>
        /// 停止所有监控线程
        /// </summary>
        void StopAll();

        /// <summary>
        /// 设置指定安全门的启用状态（运行时业务控制，与 IsMuted 屏蔽参数独立）。
        /// </summary>
        /// <param name="name">安全门名称，对应 IHardwareInputConfig.Name。</param>
        /// <param name="enabled">true = 启用（默认），false = 不启用。</param>
        void SetSafetyDoorEnabled(string name, bool enabled);

        /// <summary>
        /// Safety 扫描线程当前是否正在运行。
        /// </summary>
        bool IsSafetyMonitoringRunning { get; }

        /// <summary>
        /// 获取所有安全门的当前状态快照。
        /// </summary>
        IReadOnlyList<SafetyDoorState> GetSafetyDoorSnapshot();

        /// <summary>
        /// 上锁许可（由主控按机台状态驱动）。
        /// true：已启用且未屏蔽的安全门上锁，并对其开门进行检测；
        /// false：全部解锁，Safety 组输入不再触发（门关闭的恢复事件照常发布）。
        /// 由 false 切到 true 时对所有安全门重新布防：门若仍处于打开态，下一轮扫描立即触发。
        /// </summary>
        void SetLockPermit(bool permit);

        /// <summary>当前是否持有上锁许可。</summary>
        bool IsLockPermitted { get; }

        /// <summary>
        /// 返回"已启用、未屏蔽、但当前未关闭"的安全门名称（IO 读不到的也计入，名称后附原因）。
        /// 主控进入上锁状态前据此拒绝启动。
        /// </summary>
        IReadOnlyList<string> GetOpenArmedDoors();

        /// <summary>
        /// 将所有安全门恢复为启用状态（清除工站开门窗口残留的停用标记）。
        /// </summary>
        void ResetAllSafetyDoorsEnabled();

        /// <summary>
        /// 主控进入上锁状态前的"先锁后验"：预上锁（只给已启用未屏蔽门的锁输出上电，不打开开门检测），
        /// 等锁定监控型门的信号稳定，再检查门。
        /// 返回未关好的门（空 = 全部确认通过）。调用方无论成败都须在状态切换后调用 <see cref="ReleasePreLock"/>。
        /// 确认通过时，之前因开门报过警、至今未发恢复事件的门会补发恢复事件（锁定监控型门解锁后收不到恢复沿）。
        /// </summary>
        Task<IReadOnlyList<string>> PreLockAndVerifyAsync(CancellationToken token = default);

        /// <summary>撤销预上锁。已持有上锁许可的门锁不受影响。</summary>
        void ReleasePreLock();

        /// <summary>
        /// 工站关闭"允许开门"窗口时调用：启用该门、上锁、等信号稳定、确认门已关好。
        /// 确认期间扫描线程跳过该门。确认失败则把该门重新停用（窗口重新打开）并报 HW_SAFE_003，返回 false，
        /// 工站应停在原步序重新等待，而不是继续动作。当前无上锁许可时只启用该门，直接返回 true。
        /// </summary>
        Task<bool> ArmSafetyDoorAsync(string name, CancellationToken token = default);
    }


/// <summary>
/// 实体操作面板的 IO 监控配置接口。
/// PF.Services 层依赖此接口而非具体实现类，实现依赖倒置。
/// </summary>
public interface IPanelIoConfig
    {
        /// <summary>绑定的 IO 板卡 DeviceId，用于从 IHardwareManagerService 解析设备。</summary>
        string IoDeviceId { get; }

        /// <summary>本面板需要监控的所有输入点配置列表。</summary>
        IEnumerable<IHardwareInputConfig> MonitoredInputs { get; }
    }
}
