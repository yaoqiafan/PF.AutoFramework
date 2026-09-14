namespace PF.Core.Interfaces.Device.Hardware.Card
{
    /// <summary>
    /// 辅助编码器宿主能力接口 —— 只表达"能提供辅助编码器通道原语操作的底层板卡"，
    /// 不关心自己是板卡本身还是转发者。
    ///
    /// <para>由 <see cref="IMotionCard"/> 直接实现（板卡自身即通道的持有者，<c>EncoderCard</c> 返回自己），
    /// 也由 <see cref="Motor.Basic.IAxis"/> 实现（转发到其已挂载的 <c>ParentCard</c>）。</para>
    ///
    /// <para>辅助编码器（IAuxEncoder，见 <c>Encoder.Basic.IAuxEncoder</c>）以本接口为挂载父设备类型，
    /// 因此同一份编码器抽象支持两种拓扑：</para>
    /// <list type="bullet">
    ///   <item>HardwareConfig.ParentDeviceId 填运动控制卡 DeviceId → 直接挂在卡下，单独使用
    ///     （如产线跟随编码器、外部测长轮，与具体轴无关）；</item>
    ///   <item>HardwareConfig.ParentDeviceId 填某根轴的 DeviceId → 挂在该轴下，
    ///     作为该轴的辅助反馈/触发源随轴分组编排。</item>
    /// </list>
    /// <para>两种拓扑最终都通过 <see cref="EncoderCard"/> 落到同一块物理板卡的
    /// SetExtraPos / SetLtcLatchMode 等通道级原语上——与线扫相机"挂/不挂采集卡"是同一思路的镜像。</para>
    /// </summary>
    public interface IEncoderCarrier : IHardwareDevice
    {
        /// <summary>实际持有辅助编码器通道操作能力的运动控制卡（自身即为卡时返回自己）。</summary>
        IMotionCard EncoderCard { get; }
    }
}
