using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Encoder.Basic;

namespace PF.Infrastructure.Hardware.Encoder
{
    /// <summary>
    /// 辅助编码器实现 —— 与 <see cref="Motor.EtherCatAxis"/>、<see cref="IO.EtherCatIO"/> 同构：
    /// 自身无独立总线协议，纯粹是挂载父设备（运动控制卡或某根轴）后的通道级代理。
    /// 新增品牌板卡时无需新增本类，只需该品牌的 IMotionCard 实现正确处理 SetExtraPos 等即可。
    /// </summary>
    public class EtherCatAuxEncoder : BaseAuxEncoder
    {
        /// <summary>构造辅助编码器。</summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备名称</param>
        /// <param name="channel">本编码器在板卡内的物理通道号</param>
        /// <param name="isSimulated">是否为模拟模式</param>
        /// <param name="logger">日志服务</param>
        public EtherCatAuxEncoder(string deviceId, string deviceName, int channel, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, channel, isSimulated, logger)
        {
        }
    }
}
