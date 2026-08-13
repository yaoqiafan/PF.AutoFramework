using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Hardware
{
    /// <summary>
    /// 可挂载子设备抽象基类 —— 把父设备注入的样板代码收敛到一处。
    ///
    /// <para>子设备（轴/IO 挂运动控制卡，线扫相机挂采集卡）继承本类并指定期望的父设备类型，
    /// 即可获得类型安全的 <see cref="Parent"/> 属性，无需自己实现挂载逻辑，也不必转型。</para>
    ///
    /// <para>挂载由 HardwareManagerService 在设备实例化后、连接之前完成：
    /// 它只认非泛型的 <see cref="IAttachedDevice"/>，调用 <see cref="IAttachedDevice.TryAttachTo"/>；
    /// 父设备类型不匹配时返回 false 由服务层记警告，不抛异常——
    /// 配置里把相机的 ParentDeviceId 误填成运动控制卡不应该让整个初始化崩掉。</para>
    /// </summary>
    /// <typeparam name="TParent">期望的父设备类型，如 IMotionCard、IFrameGrabberCard</typeparam>
    public abstract class AttachedDeviceBase<TParent> : BaseDevice, IAttachedDevice<TParent>
        where TParent : class, IHardwareDevice
    {
        /// <summary>构造可挂载子设备。</summary>
        protected AttachedDeviceBase(string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
        }

        /// <inheritdoc/>
        public TParent? Parent { get; private set; }

        /// <inheritdoc/>
        IHardwareDevice? IAttachedDevice.ParentDevice => Parent;

        /// <inheritdoc/>
        public void AttachTo(TParent parent)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            OnAttached(parent);
        }

        /// <inheritdoc/>
        bool IAttachedDevice.TryAttachTo(IHardwareDevice parent)
        {
            if (parent is not TParent typed) return false;

            AttachTo(typed);
            return true;
        }

        /// <summary>
        /// 挂载完成回调。默认记录一条挂载日志，子类可重写以追加自身逻辑
        /// （如从父设备取用 SDK 句柄）。
        /// </summary>
        protected virtual void OnAttached(TParent parent)
        {
            _logger?.Info($"[{DeviceName}] 已挂载到父设备: '{parent.DeviceName}' ({parent.DeviceId})");
        }
    }
}
