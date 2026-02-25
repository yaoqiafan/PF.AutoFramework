using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Hardware.IO.Basic
{
    /// <summary>
    /// IO 控制器抽象基类
    ///
    /// 继承链：ConcreteIO → BaseIODevice → BaseDevice → IHardwareDevice
    ///                                              → IIOController
    ///                                              → IAttachedDevice
    ///
    /// BaseDevice 已提供：
    ///   · 连接重试、模拟模式拦截、统一报警、IDisposable 清理
    ///
    /// 本类额外提供：
    ///   · IAttachedDevice 父板卡关联 — HardwareManagerService 初始化后自动注入父板卡
    ///
    /// 子类只需实现 IIOController 的具体 IO 读写操作，以及 BaseDevice 的三个钩子方法。
    /// 可通过 <see cref="ParentCard"/> 属性访问所挂载的板卡 SDK 资源。
    /// </summary>
    public abstract class BaseIODevice : BaseDevice, IIOController, IAttachedDevice
    {
        #region IAttachedDevice 实现

        /// <inheritdoc/>
        public IMotionCard? ParentCard { get; private set; }

        /// <inheritdoc/>
        public void AttachToCard(IMotionCard card)
        {
            ParentCard = card;
            _logger?.Info($"[{DeviceName}] 已挂载到板卡: '{card.DeviceName}' (CardIndex={card.CardIndex})");
        }

        #endregion

        protected BaseIODevice(string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.IOController;
        }

        // ── IIOController 抽象方法（由具体子类实现）──────────────────────────────

        /// <inheritdoc/>
        public abstract int InputCount { get; }

        /// <inheritdoc/>
        public abstract int OutputCount { get; }

        /// <inheritdoc/>
        public abstract bool ReadInput(int portIndex);

        /// <inheritdoc/>
        public abstract void WriteOutput(int portIndex, bool value);

        /// <inheritdoc/>
        public abstract bool ReadOutput(int portIndex);

        /// <inheritdoc/>
        public abstract Task<bool> WaitInputAsync(int portIndex, bool targetState,
            int timeoutMs = 5000, CancellationToken token = default);
    }
}
