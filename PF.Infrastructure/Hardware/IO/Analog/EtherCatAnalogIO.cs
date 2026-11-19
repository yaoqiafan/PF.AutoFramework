using PF.Core.Interfaces.Logging;
using PF.Core.Models.Device.Hardware.IO.Analog;

namespace PF.Infrastructure.Hardware.IO.Analog
{
    /// <summary>
    /// 总线卡模拟量模块（ImplementationClassName = "EtherCatAnalogIO"），与 EtherCatIO 同级的薄壳。
    ///
    /// <para>ParentDeviceId 填运动控制卡 → 单独使用；填 IO 模块 → 随该模块分组。
    /// 连接参数：<c>NodeId</c>（留空 = 板卡本体 AD/DA）、<c>Channels</c>（通道表 JSON，见 <see cref="AnalogChannelJson"/>）。</para>
    /// </summary>
    public class EtherCatAnalogIO : CardAnalogIODevice
    {
        /// <summary>构造总线卡模拟量模块。</summary>
        public EtherCatAnalogIO(string deviceId, string deviceName, int? nodeId,
            IEnumerable<AnalogChannelConfig>? channels, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, nodeId, channels, isSimulated, logger)
        {
        }
    }
}
