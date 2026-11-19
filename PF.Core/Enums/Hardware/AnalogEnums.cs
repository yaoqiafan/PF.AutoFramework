using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PF.Core.Enums.Hardware
{
    /// <summary>模拟量通道方向</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AnalogDirection
    {
        /// <summary>模拟量输入（AI）</summary>
        [Description("输入")]
        Input,

        /// <summary>模拟量输出（AO）</summary>
        [Description("输出")]
        Output,
    }

    /// <summary>
    /// 模拟量信号类型。
    /// <para>只用于"从预设填充"时给出 RawMin / RawMax 的默认值，换算本身只看通道的 RawMin / RawMax——
    /// 板卡返回的原始值究竟是电压/电流还是码值取决于具体模块，硬件到位后以手册为准调整原始量程即可。</para>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AnalogSignalType
    {
        /// <summary>0 ~ 10 V</summary>
        [Description("0~10V")]
        Voltage0To10,

        /// <summary>-10 ~ +10 V</summary>
        [Description("±10V")]
        VoltagePm10,

        /// <summary>0 ~ 5 V</summary>
        [Description("0~5V")]
        Voltage0To5,

        /// <summary>4 ~ 20 mA</summary>
        [Description("4~20mA")]
        Current4To20,

        /// <summary>0 ~ 20 mA</summary>
        [Description("0~20mA")]
        Current0To20,

        /// <summary>直通：数据源直接给出工程值（如智能传感器返回浮点数），RawMin=EngMin、RawMax=EngMax</summary>
        [Description("直通(工程值)")]
        Direct,

        /// <summary>自定义原始量程</summary>
        [Description("自定义")]
        Custom,
    }

    /// <summary>模拟量读数状态</summary>
    public enum AnalogStatus
    {
        /// <summary>正常</summary>
        [Description("正常")]
        Ok,

        /// <summary>原始值低于量程下限（仍给出工程值）</summary>
        [Description("低于量程")]
        BelowRange,

        /// <summary>原始值高于量程上限（仍给出工程值）</summary>
        [Description("高于量程")]
        AboveRange,

        /// <summary>断线（原始值低于断线阈值，不给工程值）</summary>
        [Description("断线")]
        WireBreak,

        /// <summary>通讯/读取失败（不给工程值）</summary>
        [Description("通讯失败")]
        CommError,

        /// <summary>数据过期：扫描型设备超过过期时限未刷新（不给工程值）</summary>
        [Description("数据过期")]
        Stale,

        /// <summary>通道未在通道表中配置（不给工程值）</summary>
        [Description("未配置")]
        NotConfigured,
    }

    /// <summary>模拟量 AI 通道在仿真模式下的取值方式</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AnalogSimMode
    {
        /// <summary>读数为 SimValue；区间等待直接视为满足（与数字量仿真"等待即满足"一致，默认）</summary>
        [Description("等待直接通过")]
        AlwaysPass,

        /// <summary>读数为 SimValue（可在调试页修改）；区间等待按实际值判断</summary>
        [Description("固定值")]
        Fixed,

        /// <summary>按一阶滞后跟随 SimFollowChannel 指定的 AO 通道（换算到本通道工程量程）；区间等待按实际值判断</summary>
        [Description("跟随输出")]
        FollowOutput,
    }
}
