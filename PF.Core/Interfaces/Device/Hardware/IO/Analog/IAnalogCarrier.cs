namespace PF.Core.Interfaces.Device.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量宿主能力接口 —— 只回答"挂在我下面的模拟量设备最终落到哪块板卡"，
    /// 与 <see cref="Card.IEncoderCarrier"/> 同构。
    ///
    /// <para>卡类模拟量设备（<c>CardAnalogIODevice</c>）以本接口为挂载父设备类型，因此同一份抽象支持两种拓扑：</para>
    /// <list type="bullet">
    ///   <item>HardwareConfig.ParentDeviceId 填运动控制卡 DeviceId → 直接挂在卡下，单独使用
    ///     （板卡自身实现本接口，<see cref="AnalogCard"/> 返回自己）；</item>
    ///   <item>HardwareConfig.ParentDeviceId 填某个 IO 模块的 DeviceId → 挂在该 IO 模块下随其分组
    ///     （<c>BaseIODevice</c> 实现本接口，转发到它所在的板卡）。</item>
    /// </list>
    /// <para>挂在 IO 模块下只带来分组、初始化顺序与故障联动，<b>不继承节点号</b>：
    /// 模拟量设备的 NodeId 仍需单独配置。</para>
    /// </summary>
    public interface IAnalogCarrier : IHardwareDevice
    {
        /// <summary>
        /// 实际持有模拟量通道原语的板卡；所在板卡不支持模拟量（未实现 <see cref="IAnalogCard"/>）
        /// 或尚未挂载时返回 null。
        /// </summary>
        IAnalogCard? AnalogCard { get; }
    }
}
