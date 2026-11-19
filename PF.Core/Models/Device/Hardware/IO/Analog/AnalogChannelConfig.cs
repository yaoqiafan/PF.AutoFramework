using PF.Core.Enums.Hardware;

namespace PF.Core.Models.Device.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量通道配置（一行 = 一个 AI 或 AO 物理通道）。
    ///
    /// <para>整张通道表以 JSON 数组形式存放在 <c>HardwareConfig.ConnectionParameters["Channels"]</c>，
    /// 与 EtherCatAxis 的 <c>AxisParam</c> 同一做法（System.Text.Json 序列化）。</para>
    ///
    /// <para>传感器型号之间的差异全部落在这里（信号类型、量程、单位、限幅、安全值），
    /// 不为每个型号单独建类。</para>
    ///
    /// <para>换算：<c>eng = EngMin + (raw - RawMin) · (EngMax - EngMin) / (RawMax - RawMin)</c>，输出反算同理。</para>
    /// </summary>
    public class AnalogChannelConfig
    {
        #region 标识

        /// <summary>通道方向（AI / AO）；与 <see cref="Channel"/> 共同构成唯一键</summary>
        public AnalogDirection Direction { get; set; } = AnalogDirection.Input;

        /// <summary>
        /// 物理通道号。业务枚举的 int 值即为该通道号（AI、AO 各自独立编号），
        /// 与数字量"枚举值 = 端口号"的约定一致。
        /// </summary>
        public int Channel { get; set; }

        /// <summary>显示名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>工程单位（如 kPa、%、mm）</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>显示小数位</summary>
        public int Decimals { get; set; } = 2;

        #endregion

        #region 换算

        /// <summary>信号类型（仅用于预设填充原始量程，换算只看 RawMin/RawMax）</summary>
        public AnalogSignalType SignalType { get; set; } = AnalogSignalType.Voltage0To10;

        /// <summary>原始量程下限（板卡/数据源返回值的下限，如 4 mA）</summary>
        public double RawMin { get; set; }

        /// <summary>原始量程上限（如 20 mA）</summary>
        public double RawMax { get; set; } = 10;

        /// <summary>工程量程下限（RawMin 对应的工程值）</summary>
        public double EngMin { get; set; }

        /// <summary>工程量程上限（RawMax 对应的工程值）</summary>
        public double EngMax { get; set; } = 10;

        #endregion

        #region AI 专用

        /// <summary>
        /// 断线阈值（原始值）。AI 原始值低于此值判为断线，读数不给工程值；null 表示不做断线判断。
        /// 4~20mA 预设默认 3.6。
        /// </summary>
        public double? WireBreakBelowRaw { get; set; }

        #endregion

        #region AO 专用

        /// <summary>AO 允许写出的工程值下限；越限拒绝写出（不截断）</summary>
        public double OutputMin { get; set; }

        /// <summary>AO 允许写出的工程值上限；越限拒绝写出（不截断）</summary>
        public double OutputMax { get; set; } = 10;

        /// <summary>
        /// AO 停机安全值（工程值）。<c>IAnalogIOController.ApplySafeOutputsAsync</c> 会把它写出；
        /// null 表示停机时不处理这一路。
        /// </summary>
        public double? SafeValue { get; set; }

        #endregion

        #region 硬件

        /// <summary>连接时下发给载体的 AD/DA 模式（含义取决于具体板卡，见其驱动说明）；null 表示不下发</summary>
        public int? Mode { get; set; }

        #endregion

        #region 仿真（仅仿真模式生效）

        /// <summary>AI 仿真取值方式</summary>
        public AnalogSimMode SimMode { get; set; } = AnalogSimMode.AlwaysPass;

        /// <summary>仿真初始值（工程值）：AI 为读数初值；AO 为上电回读初值</summary>
        public double SimValue { get; set; }

        /// <summary><see cref="AnalogSimMode.FollowOutput"/> 时跟随的 AO 通道号</summary>
        public int? SimFollowChannel { get; set; }

        /// <summary><see cref="AnalogSimMode.FollowOutput"/> 的一阶滞后时间常数（毫秒），0 表示瞬时跟随</summary>
        public int SimLagMs { get; set; }

        #endregion

        /// <summary>
        /// 按信号类型填入原始量程默认值（以及 4~20mA 的断线阈值）。
        /// <see cref="AnalogSignalType.Direct"/> 令原始量程等于工程量程；<see cref="AnalogSignalType.Custom"/> 不改动。
        /// </summary>
        public void ApplySignalTypeDefaults()
        {
            switch (SignalType)
            {
                case AnalogSignalType.Voltage0To10: (RawMin, RawMax, WireBreakBelowRaw) = (0, 10, null); break;
                case AnalogSignalType.VoltagePm10:  (RawMin, RawMax, WireBreakBelowRaw) = (-10, 10, null); break;
                case AnalogSignalType.Voltage0To5:  (RawMin, RawMax, WireBreakBelowRaw) = (0, 5, null); break;
                case AnalogSignalType.Current4To20: (RawMin, RawMax, WireBreakBelowRaw) = (4, 20, 3.6); break;
                case AnalogSignalType.Current0To20: (RawMin, RawMax, WireBreakBelowRaw) = (0, 20, null); break;
                case AnalogSignalType.Direct:       (RawMin, RawMax, WireBreakBelowRaw) = (EngMin, EngMax, null); break;
            }
        }

        /// <summary>浅拷贝（全部字段均为值类型或不可变字符串，等价于深拷贝），供调试页编辑副本使用</summary>
        public AnalogChannelConfig Clone() => (AnalogChannelConfig)MemberwiseClone();

        /// <inheritdoc/>
        public override string ToString() => $"{(Direction == AnalogDirection.Input ? "AI" : "AO")}{Channel} {Name}";
    }
}
