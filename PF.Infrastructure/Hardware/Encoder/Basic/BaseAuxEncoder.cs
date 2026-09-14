using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.Encoder.Basic;
using PF.Core.Interfaces.Logging;
using System.Runtime.CompilerServices;

namespace PF.Infrastructure.Hardware.Encoder.Basic
{
    /// <summary>
    /// 辅助编码器通用代理基类（Proxy Wrapper），与 <see cref="Motor.Basic.BaseAxisDevice"/> 同构：
    ///
    /// <para>本类不依赖厂商 SDK，所有通道操作委托给 <see cref="AttachedDeviceBase{TParent}.Parent"/>
    /// 的 <see cref="IEncoderCarrier.EncoderCard"/>，最终落到某块运动控制卡的
    /// SetExtraPos / SetLtcLatchMode 等通道级原语上。</para>
    ///
    /// <para>挂载拓扑（父设备类型 <see cref="IEncoderCarrier"/>，由 <see cref="IMotionCard"/> 与
    /// <see cref="PF.Core.Interfaces.Device.Hardware.Motor.Basic.IAxis"/> 共同实现）：</para>
    /// <list type="bullet">
    ///   <item>ParentDeviceId = 运动控制卡 DeviceId → 挂在卡下，单独使用；</item>
    ///   <item>ParentDeviceId = 某根轴 DeviceId → 挂在该轴下，随轴分组/编排。</item>
    /// </list>
    /// <para>两种拓扑均通过 Parent.EncoderCard 解析到同一块物理板卡，新增品牌板卡时无需改动本类
    /// ——与 BaseAxisDevice 把运动指令委托给 ParentCard 是同一套代理/委托模式。</para>
    /// </summary>
    public abstract class BaseAuxEncoder : AttachedDeviceBase<IEncoderCarrier>, IAuxEncoder
    {
        // 模拟模式下的虚拟编码器位置：由 SetPositionAsync 更新，替代真实板卡的通道读数
        private double _simulatedPosition;

        /// <summary>构造辅助编码器。</summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备名称</param>
        /// <param name="channel">本编码器在板卡内的物理通道号</param>
        /// <param name="isSimulated">是否为模拟模式</param>
        /// <param name="logger">日志服务</param>
        protected BaseAuxEncoder(string deviceId, string deviceName, int channel, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Channel = channel;
            Category = Core.Enums.HardwareCategory.AuxEncoder;
        }

        /// <summary>
        /// 归属的运动控制卡：由 <see cref="AttachedDeviceBase{TParent}.Parent"/>.EncoderCard 解析，
        /// 挂在轴下时会穿透该轴落到轴的 ParentCard。
        /// </summary>
        protected IMotionCard? Card => Parent?.EncoderCard;

        /// <inheritdoc/>
        protected override void OnAttached(IEncoderCarrier parent)
        {
            _logger?.Info($"[{DeviceName}] 已挂载到 '{parent.DeviceName}'，落点板卡: '{parent.EncoderCard.DeviceName}' (CardIndex={parent.EncoderCard.CardIndex})");
        }

        /// <inheritdoc/>
        public int Channel { get; }

        /// <inheritdoc/>
        public double Multiplier { get; set; } = 2;

        /// <inheritdoc/>
        public virtual async Task<bool> SetPositionAsync(int position, CancellationToken token = default)
        {
            EnsureCarrierAttached();
            if (IsSimulated) { await Task.Delay(200, token); _simulatedPosition = position; return true; }

            return await Card!.SetExtraPos(Channel, position, Multiplier, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public virtual async Task<double?> GetPositionAsync(CancellationToken token = default)
        {
            EnsureCarrierAttached();
            if (IsSimulated) { await Task.Delay(200, token); return _simulatedPosition; }

            return await Card!.GetExtraPos(Channel, Multiplier, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public virtual async Task<bool> SetLatchModeAsync(int latchNo, int ltcMode = 1, int ltcLogic = 0, double filter = 0, double latchSource = 0, CancellationToken token = default)
        {
            EnsureCarrierAttached();
            if (IsSimulated) { await Task.Delay(200, token); return true; }

            return await Card!.SetLtcLatchMode(latchNo, Channel, ltcMode, ltcLogic, filter, latchSource, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public virtual async Task<int> GetLatchNumberAsync(int latchNo, CancellationToken token = default)
        {
            EnsureCarrierAttached();
            if (IsSimulated) { await Task.Delay(200, token); return 1; }

            return await Card!.GetLtcLatchNumber(latchNo, Channel, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public virtual async Task<double?> GetLatchPositionAsync(int latchNo, CancellationToken token = default)
        {
            EnsureCarrierAttached();
            // 模拟模式返回当前虚拟位置（锁存语义即"捕获触发瞬间的编码器位置"），不返回 0 哨兵值
            if (IsSimulated) { await Task.Delay(200, token); return _simulatedPosition; }

            return await Card!.GetLtcLatchPos(latchNo, Channel, Multiplier, token).ConfigureAwait(false);
        }

        // ── BaseDevice 生命周期钩子 ─────────────────────────────────────────────
        // 辅助编码器自身无独立物理连接：真实的连接/断开/复位由挂载的板卡负责，
        // 这三个钩子恒为空操作，与 BaseIODevice/BaseAxisDevice 的代理语义一致。

        /// <inheritdoc/>
        protected override Task<bool> InternalConnectAsync(CancellationToken token) => Task.FromResult(true);

        /// <inheritdoc/>
        protected override Task InternalDisconnectAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        protected override Task InternalResetAsync(CancellationToken token) => Task.CompletedTask;

        /// <inheritdoc/>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            // 连接类报警：宿主（板卡或轴）断开→报警，重连→防抖后自动消除
            bool faulted = Card == null || !Card.IsConnected;
            UpdateAutoClearableHealth(faulted, Core.Constants.AlarmCodes.Hardware.AuxEncoderCardDisconnected,
                $"辅助编码器 [{DeviceName}] 所在运动控制卡已断开，不可用");
            return Task.CompletedTask;
        }

        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>检查父设备（板卡或轴）是否已挂载，未挂载则记录错误日志并抛出 InvalidOperationException。</summary>
        private void EnsureCarrierAttached([CallerMemberName] string caller = "")
        {
            if (Card is null)
            {
                var msg = $"[{DeviceName}] '{caller}'：辅助编码器尚未挂载到运动控制卡或轴，请先调用 AttachTo()。";
                _logger?.Error(msg);
                throw new InvalidOperationException(msg);
            }
        }
    }
}
