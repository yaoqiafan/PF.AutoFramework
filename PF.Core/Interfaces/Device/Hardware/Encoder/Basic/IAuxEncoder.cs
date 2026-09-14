using PF.Core.Interfaces.Device.Hardware.Card;

namespace PF.Core.Interfaces.Device.Hardware.Encoder.Basic
{
    /// <summary>
    /// 辅助编码器接口 —— 独立于任何轴的编码器通道抽象。
    ///
    /// <para>继承链：ConcreteAuxEncoder（如 EtherCatAuxEncoder）→ BaseAuxEncoder → BaseDevice → IHardwareDevice
    ///                                                                                        → IAuxEncoder
    ///                                                                                        → IAttachedDevice</para>
    ///
    /// <para>挂载父设备类型为 <see cref="IEncoderCarrier"/>（由 <see cref="Card.IMotionCard"/> 与
    /// <see cref="Motor.Basic.IAxis"/> 共同实现），因此同一份编码器抽象支持两种拓扑，与线扫相机
    /// "挂/不挂采集卡"是同一思路的镜像：</para>
    /// <list type="bullet">
    ///   <item>HardwareConfig.ParentDeviceId 填运动控制卡 DeviceId → 直接挂在卡下，单独使用
    ///     （如产线跟随编码器、外部测长轮，与具体轴无关）；</item>
    ///   <item>HardwareConfig.ParentDeviceId 填某根轴的 DeviceId → 挂在该轴下，
    ///     作为该轴的辅助反馈/触发源随轴分组编排。</item>
    /// </list>
    /// <para>无论挂在哪一层，最终都通过 IEncoderCarrier.EncoderCard 落到同一块物理板卡的
    /// SetExtraPos / SetLtcLatchMode 等通道级原语上。</para>
    /// </summary>
    public interface IAuxEncoder : IHardwareDevice
    {
        /// <summary>本编码器在板卡内的物理通道号（0-based，由子类/配置提供）。</summary>
        int Channel { get; }

        /// <summary>编码器倍率（脉冲当量换算系数）。默认 2，对齐现有板卡驱动的既有用法。</summary>
        double Multiplier { get; set; }

        /// <summary>写入编码器当前位置（用于清零/预置计数值）。</summary>
        Task<bool> SetPositionAsync(int position, CancellationToken token = default);

        /// <summary>读取编码器当前位置；读取失败返回 null。</summary>
        Task<double?> GetPositionAsync(CancellationToken token = default);

        /// <summary>设置以本编码器通道为触发源的硬件位置锁存模式。</summary>
        /// <param name="latchNo">锁存器 ID</param>
        /// <param name="ltcMode">锁存模式</param>
        /// <param name="ltcLogic">锁存逻辑</param>
        /// <param name="filter">滤波器</param>
        /// <param name="latchSource">锁存源</param>
        /// <param name="token">取消令牌</param>
        Task<bool> SetLatchModeAsync(int latchNo, int ltcMode = 1, int ltcLogic = 0, double filter = 0, double latchSource = 0, CancellationToken token = default);

        /// <summary>读取本编码器通道上的锁存个数。</summary>
        Task<int> GetLatchNumberAsync(int latchNo, CancellationToken token = default);

        /// <summary>读取本编码器通道上的锁存位置。</summary>
        Task<double?> GetLatchPositionAsync(int latchNo, CancellationToken token = default);
    }
}
