using PF.Core.Interfaces.Device.Hardware.Card;

namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 挂载设备接口 — 表示该设备依附于某块运动控制卡
    ///
    /// 低耦合父子注入方案：
    ///   · 轴/IO 等子设备实现此接口，声明自己"可被挂载"
    ///   · HardwareManagerService 初始化完父板卡后，调用子设备的 AttachToCard(card)
    ///   · 服务层只依赖接口，不引用任何具体设备类，符合依赖倒置原则
    ///   · 子设备通过 ParentCard 属性在运行时访问父板卡的资源（如 SDK 句柄）
    /// </summary>
    public interface IAttachedDevice
    {
        /// <summary>
        /// 归属的父运动控制卡实例。
        /// 在 HardwareManagerService 调用 AttachToCard 之前为 null。
        /// </summary>
        IMotionCard? ParentCard { get; }

        /// <summary>
        /// 将父板卡实例绑定到本子设备（由 HardwareManagerService 在子设备实例化后调用）
        /// </summary>
        /// <param name="card">已初始化的父板卡实例</param>
        void AttachToCard(IMotionCard card);
    }
}
