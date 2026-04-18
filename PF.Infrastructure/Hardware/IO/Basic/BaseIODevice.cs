using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using System.Runtime.CompilerServices;

namespace PF.Infrastructure.Hardware.IO.Basic
{
    /// <summary>
    /// IO 控制器通用代理基类（Proxy Wrapper）
    ///
    /// 继承链：ConcreteIO（可选）→ BaseIODevice → BaseDevice → IHardwareDevice
    ///                                                      → IIOController
    ///                                                      → IAttachedDevice
    ///
    /// 重构说明（代理/委托模式）：
    ///   · 本类不再包含抽象 IO 读写方法，不依赖厂商 SDK。
    ///   · ReadInput / WriteOutput / ReadOutput 均委托给 ParentCard（IMotionCard）的对应方法执行。
    ///   · WaitInputAsync 在本类内使用 ReadInput 轮询实现，天然复用代理链，无需再委托给板卡。
    ///   · InputCount / OutputCount 保留 abstract，由子类/配置提供（表示本控制器管辖的端口数量）。
    ///   · 新增硬件品牌时，只需实现一个 XXXMotionCard 类，无需再修改本类或 IO 设备代码。
    ///
    /// WaitInputAsync 轮询策略：
    ///   每 20ms 采样一次 ReadInput，使用 Task.Delay + ConfigureAwait(false) 避免死锁；
    ///   若超时或取消令牌触发则返回 false 并记录警告日志。
    ///   若板卡 SDK 提供原生等待机制，子类可 override 此方法以获得更低延迟。
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

        /// <summary>
        /// 构造IO设备
        /// </summary>
        protected BaseIODevice(string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.IOController;
        }




        // ── IIOController 端口数量（保留 abstract — 由子类/配置提供本控制器管辖的端口总数）─

        /// <inheritdoc/>
        public abstract int InputCount { get; }

        /// <inheritdoc/>
        public abstract int OutputCount { get; }

        // ── IIOController IO 读写方法（委托给 ParentCard，替代原来的抽象方法）──────────

        /// <summary>
        /// 读取指定输入端口信号（委托给父板卡执行）。
        /// </summary>
        /// <param name="portIndex">端口号（板卡内物理端口索引）</param>
        /// <returns>true = 高电平（有信号），false = 低电平（无信号）</returns>
        public virtual bool? ReadInput(int portIndex)
        {
            EnsureCardAttached();
            if (IsSimulated )
            {
                return false;
            }
            return ParentCard!.ReadInputPort(portIndex);
        }


        /// <summary>
        /// 使用枚举名称读取输入端口
        /// </summary>
        public virtual bool? ReadInput<T>(T InPutName) where T : Enum
        {
            if (IsSimulated)
            {
                return false;
            }
            return this.ReadInput(Convert.ToInt32(InPutName));
        }
        /// <summary>
        /// 设置指定输出端口信号（委托给父板卡执行）。
        /// </summary>
        /// <param name="portIndex">端口号（板卡内物理端口索引）</param>
        /// <param name="value">true = 开启输出，false = 关闭输出</param>
        public virtual bool WriteOutput(int portIndex, bool value)
        {
            EnsureCardAttached();
            if (IsSimulated)
            {
                return true ;
            }
            return ParentCard!.WriteOutputPort(portIndex, value);
        }

        /// <summary>
        /// 使用枚举名称写入输出端口
        /// </summary>
        public virtual bool WriteOutput<T>(T OutputName, bool value) where T : Enum
        {
            if (IsSimulated)
            {
                return true ;
            }
            return this.WriteOutput(Convert.ToInt32(OutputName), value);
        }

        /// <summary>
        /// 读取指定输出端口的当前锁存状态（委托给父板卡执行，用于 UI 回显）。
        /// </summary>
        /// <param name="portIndex">端口号（板卡内物理端口索引）</param>
        public virtual bool? ReadOutput(int portIndex)
        {
            EnsureCardAttached();
            if (IsSimulated)
            {
                return false;
            }
            return ParentCard!.ReadOutputPort(portIndex);
        }

        /// <summary>
        /// 使用枚举名称读取输出端口
        /// </summary>
        public virtual bool? ReadOutput<T>(T InPutName) where T : Enum
        {
            if (IsSimulated)
            {
                return false;
            }
            return this.ReadOutput(Convert.ToInt32(InPutName));
        }



        // ── IIOController WaitInputAsync（本类内轮询实现，复用 ReadInput 代理链）────────

        /// <summary>
        /// 异步等待指定输入端口达到目标状态（自带防卡死超时机制）。
        ///
        /// 实现策略：每 20ms 轮询一次 ReadInput，使用非阻塞 Task.Delay 避免占用线程，
        /// ConfigureAwait(false) 防止同步上下文死锁。
        /// 超时或取消令牌触发时返回 false 并记录警告日志。
        /// 若板卡 SDK 提供原生等待机制，子类可 override 此方法以获得更低延迟。
        /// </summary>
        /// <param name="portIndex">端口号（板卡内物理端口索引）</param>
        /// <param name="targetState">期望的目标状态（true = 高电平，false = 低电平）</param>
        /// <param name="timeoutMs">超时时间（毫秒，默认 5000ms）</param>
        /// <param name="token">取消令牌</param>
        /// <returns>在超时前达到目标状态返回 true，超时或取消返回 false</returns>
        public virtual async Task<bool> WaitInputAsync(
            int portIndex,
            bool targetState,
            int timeoutMs = 5000,
            CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated)
            {
                return false;
            }
            const int PollingIntervalMs = 20;
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (ReadInput(portIndex) == targetState)
                    return true;

                try
                {
                    await Task.Delay(PollingIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger?.Warn(
                $"[{DeviceName}] WaitInputAsync 超时或已取消：" +
                $"端口 {portIndex} 未在 {timeoutMs}ms 内达到目标状态 {targetState}");
            return false;
        }






        /// <summary>
        /// 使用枚举名称异步等待输入端口达到目标状态
        /// </summary>
        public virtual Task<bool> WaitInputAsync<T>(T InputName, bool targetState, int timeoutMs = 5000, CancellationToken token = default) where T : Enum
        {
            return this.WaitInputAsync(Convert.ToInt32(InputName), targetState, timeoutMs, token);
        }



        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 检查父板卡是否已挂载，未挂载则记录错误日志并抛出 InvalidOperationException。
        /// </summary>
        private void EnsureCardAttached([CallerMemberName] string caller = "")
        {
            if (ParentCard is null)
            {
                var msg = $"[{DeviceName}] '{caller}'：设备尚未挂载到板卡，请先调用 AttachToCard()。";
                _logger?.Error(msg);
                throw new InvalidOperationException(msg);
            }
        }


    }
}
