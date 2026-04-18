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
        /// 是否屏蔽此输入点的扫描。
        /// true = 屏蔽（跳过事件发布）；false = 正常扫描（默认值）。
        /// </summary>
        bool IsMuted { get; }
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
