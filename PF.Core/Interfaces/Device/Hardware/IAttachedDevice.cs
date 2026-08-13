namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 挂载设备接口（非泛型契约）— 表示该设备依附于另一台父设备。
    ///
    /// 低耦合父子注入方案：
    ///   · 轴/IO 挂运动控制卡，线扫相机挂采集卡，均实现本接口声明自己"可被挂载"
    ///   · HardwareManagerService 初始化完父设备后，调用 <see cref="TryAttachTo"/> 完成注入
    ///   · 服务层只依赖本非泛型接口，不需要知道父设备的具体类型，新增父设备种类时注入点无需改动
    ///   · 设备实现方与业务代码应使用强类型的 <see cref="IAttachedDevice{TParent}"/>
    /// </summary>
    public interface IAttachedDevice
    {
        /// <summary>
        /// 归属的父设备实例（运动控制卡、采集卡等）。
        /// 在 HardwareManagerService 完成挂载之前为 null。
        /// </summary>
        IHardwareDevice? ParentDevice { get; }

        /// <summary>
        /// 尝试挂载到父设备。
        /// 父设备类型与本设备期望的类型不匹配时返回 false（由调用方记录警告），不抛异常。
        /// </summary>
        /// <param name="parent">已实例化的父设备</param>
        /// <returns>true = 挂载成功</returns>
        bool TryAttachTo(IHardwareDevice parent);
    }

    /// <summary>
    /// 挂载设备接口（强类型版本）— 子设备实现本接口以声明自己期望的父设备类型。
    /// <para>子设备通过 <see cref="Parent"/> 在运行时访问父设备资源（如 SDK 句柄），
    /// 无需转型，编译期即可确定类型。</para>
    /// </summary>
    /// <typeparam name="TParent">父设备类型，如 IMotionCard、IFrameGrabberCard</typeparam>
    public interface IAttachedDevice<TParent> : IAttachedDevice
        where TParent : class, IHardwareDevice
    {
        /// <summary>归属的父设备实例。挂载前为 null。</summary>
        TParent? Parent { get; }

        /// <summary>将父设备绑定到本子设备。</summary>
        void AttachTo(TParent parent);
    }
}
