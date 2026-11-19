using PF.Core.Enums.Hardware;
using PF.Core.Models.Device.Hardware.IO.Analog;

namespace PF.Core.Interfaces.Device.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量通道原语 —— 由真正持有厂商 SDK 句柄的板卡实现，读写的是<b>原始值</b>（未换算）。
    ///
    /// <para>刻意不并入 <see cref="Card.IMotionCard"/>：框架以 NuGet 分发，往 IMotionCard 加成员会让
    /// 下游自行实现的板卡全部编译失败。不支持模拟量的板卡不实现本接口即可，挂在它下面的模拟量设备
    /// 会在健康检查中报"所在板卡不支持模拟量"。</para>
    ///
    /// <para>与 <see cref="IAnalogCarrier"/> 的关系同 IMotionCard 与 IEncoderCarrier：
    /// 本接口是通道原语的最终落点，IAnalogCarrier 只回答"我最终落到哪块卡"。</para>
    ///
    /// <para>契约：全部方法同步、不阻塞（板卡 SDK 读写过程映像）；失败返回 null / false，不抛异常。</para>
    /// </summary>
    public interface IAnalogCard : IHardwareDevice
    {
        /// <summary>读取 AI 原始值；失败返回 null。</summary>
        double? ReadAnalogInputRaw(AnalogAddress address);

        /// <summary>写出 AO 原始值；失败返回 false。</summary>
        bool WriteAnalogOutputRaw(AnalogAddress address, double raw);

        /// <summary>回读 AO 当前原始值；失败返回 null。</summary>
        double? ReadAnalogOutputRaw(AnalogAddress address);

        /// <summary>
        /// 连接时下发通道 AD/DA 模式（<paramref name="mode"/> 含义取决于具体板卡）。
        /// 不需要配置模式的板卡直接返回 true。
        /// </summary>
        bool ConfigureAnalogChannel(AnalogAddress address, AnalogDirection direction, int mode);
    }
}
