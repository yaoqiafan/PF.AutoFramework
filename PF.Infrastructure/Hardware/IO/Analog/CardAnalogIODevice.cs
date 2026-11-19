using PF.Core.Constants;
using PF.Core.Enums.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Analog;
using PF.Core.Interfaces.Logging;
using PF.Core.Models.Device.Hardware.IO.Analog;
using System.Diagnostics;

namespace PF.Infrastructure.Hardware.IO.Analog
{
    /// <summary>
    /// 卡类模拟量设备通用代理基类（Proxy Wrapper），与 BaseIODevice / BaseAuxEncoder 同构：
    /// 本类不依赖厂商 SDK，通道原语委托给 <see cref="AttachedDeviceBase{TParent}.Parent"/> 的
    /// <see cref="IAnalogCarrier.AnalogCard"/>；换算、限幅、仿真由 <see cref="AnalogChannelEngine"/> 完成。
    ///
    /// <para>挂载拓扑（父设备类型 <see cref="IAnalogCarrier"/>）：</para>
    /// <list type="bullet">
    ///   <item>ParentDeviceId = 运动控制卡 DeviceId → 挂在卡下，单独使用；</item>
    ///   <item>ParentDeviceId = 某个 IO 模块 DeviceId → 挂在该 IO 模块下随其分组（初始化顺序、故障联动）。
    ///     <b>不继承节点号</b>，<see cref="NodeId"/> 仍按本设备配置。</item>
    /// </list>
    ///
    /// <para>契约要点：同步读写直接走板卡 SDK（不阻塞）；越限拒绝写出返回 false；
    /// 写出失败只记日志不报警（与数字量 WriteOutput 一致，是否报警由业务决定）。</para>
    /// </summary>
    public abstract class CardAnalogIODevice : AttachedDeviceBase<IAnalogCarrier>, IAnalogIOController, IAnalogSimulationControl
    {
        /// <summary>等待类方法的采样间隔（毫秒），与 BaseIODevice.WaitInputAsync 一致</summary>
        protected const int WaitPollingIntervalMs = 20;

        /// <summary>构造卡类模拟量设备。</summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备名称</param>
        /// <param name="nodeId">节点号；null 表示板卡本体（接线盒）AD/DA</param>
        /// <param name="channels">通道表（启动加载：不合法的行会被忽略并记日志）</param>
        /// <param name="isSimulated">是否仿真</param>
        /// <param name="logger">日志服务</param>
        protected CardAnalogIODevice(string deviceId, string deviceName, int? nodeId,
            IEnumerable<AnalogChannelConfig>? channels, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.AnalogIO;
            NodeId = nodeId;
            Engine = AnalogChannelEngine.CreateLenient(channels, msg => _logger?.Error($"[{deviceName}] {msg}"));
        }

        /// <summary>节点号；null 表示板卡本体（接线盒）AD/DA</summary>
        public int? NodeId { get; }

        /// <summary>通道引擎</summary>
        protected AnalogChannelEngine Engine { get; }

        /// <summary>最终落点板卡：挂在 IO 模块下时穿透该模块落到其所在板卡；不支持模拟量时为 null</summary>
        protected IAnalogCard? Card => Parent?.AnalogCard;

        private AnalogAddress AddressOf(int channel) => new(NodeId, channel);

        /// <inheritdoc/>
        protected override void OnAttached(IAnalogCarrier parent)
        {
            var card = parent.AnalogCard;
            if (card == null)
                _logger?.Warn($"[{DeviceName}] 已挂载到 '{parent.DeviceName}'，但其所在板卡不支持模拟量（未实现 IAnalogCard），本设备将不可用");
            else
                _logger?.Info($"[{DeviceName}] 已挂载到 '{parent.DeviceName}'，落点板卡: '{card.DeviceName}'，节点: {(NodeId?.ToString() ?? "板卡本体")}");
        }

        #region 通道表

        /// <inheritdoc/>
        public IReadOnlyList<AnalogChannelConfig> InputChannels => Engine.Inputs;

        /// <inheritdoc/>
        public IReadOnlyList<AnalogChannelConfig> OutputChannels => Engine.Outputs;

        /// <inheritdoc/>
        public AnalogConfigResult UpdateChannels(IEnumerable<AnalogChannelConfig> channels)
        {
            var result = Engine.Update(channels);
            if (result.Success) _logger?.Info($"[{DeviceName}] 模拟量通道表已更新");
            else _logger?.Warn($"[{DeviceName}] 模拟量通道表更新被拒绝：{string.Join("；", result.Errors)}");
            return result;
        }

        #endregion

        #region AI

        /// <inheritdoc/>
        public virtual AnalogReading ReadAnalogInputDetail(int channel)
        {
            var cfg = Engine.Find(AnalogDirection.Input, channel);
            var now = DateTime.Now;
            if (cfg == null) return AnalogChannelEngine.ToReading(null, null, now);

            if (IsSimulated) return AnalogChannelEngine.ToReading(cfg, Engine.SimReadInputRaw(cfg), now);

            var card = Card;
            double? raw = card?.ReadAnalogInputRaw(AddressOf(channel));
            return AnalogChannelEngine.ToReading(cfg, raw, now);
        }

        /// <inheritdoc/>
        public AnalogReading ReadAnalogInputDetail<T>(T name) where T : Enum => ReadAnalogInputDetail(Convert.ToInt32(name));

        /// <inheritdoc/>
        public double? ReadAnalogInput(int channel) => ReadAnalogInputDetail(channel).Value;

        /// <inheritdoc/>
        public double? ReadAnalogInput<T>(T name) where T : Enum => ReadAnalogInput(Convert.ToInt32(name));

        #endregion

        #region AO

        /// <inheritdoc/>
        public virtual bool WriteAnalogOutput(int channel, double value)
        {
            if (!Engine.TryPrepareOutput(channel, value, out _, out var raw, out var error))
            {
                _logger?.Warn($"[{DeviceName}] {error}");
                return false;
            }

            if (IsSimulated)
            {
                Engine.SimWriteOutput(channel, value);
                return true;
            }

            var card = Card;
            if (card == null)
            {
                _logger?.Warn($"[{DeviceName}] AO{channel} 写出失败：所在板卡不可用或不支持模拟量");
                return false;
            }

            bool ok = card.WriteAnalogOutputRaw(AddressOf(channel), raw);
            if (!ok) _logger?.Warn($"[{DeviceName}] AO{channel} 写出失败（{AddressOf(channel)}，原始值 {raw:G6}）");
            return ok;
        }

        /// <inheritdoc/>
        public bool WriteAnalogOutput<T>(T name, double value) where T : Enum => WriteAnalogOutput(Convert.ToInt32(name), value);

        /// <summary>
        /// 卡类设备的板卡写出本身是同步且极快的，异步版本只是包装（不新开线程）。
        /// </summary>
        public virtual Task<bool> WriteAnalogOutputAsync(int channel, double value, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(WriteAnalogOutput(channel, value));
        }

        /// <inheritdoc/>
        public Task<bool> WriteAnalogOutputAsync<T>(T name, double value, CancellationToken token = default) where T : Enum
            => WriteAnalogOutputAsync(Convert.ToInt32(name), value, token);

        /// <inheritdoc/>
        public virtual double? ReadAnalogOutput(int channel)
        {
            var cfg = Engine.Find(AnalogDirection.Output, channel);
            if (cfg == null) return null;
            if (IsSimulated) return Engine.SimReadOutput(cfg);

            double? raw = Card?.ReadAnalogOutputRaw(AddressOf(channel));
            return raw is double r ? AnalogChannelEngine.RawToEng(cfg, r) : null;
        }

        /// <inheritdoc/>
        public double? ReadAnalogOutput<T>(T name) where T : Enum => ReadAnalogOutput(Convert.ToInt32(name));

        #endregion

        #region 等待

        /// <inheritdoc/>
        public virtual async Task<bool> WaitAnalogInputInRangeAsync(int channel, double min, double max,
            int timeoutMs = 5000, int stableMs = 0, CancellationToken token = default)
        {
            if (min > max) (min, max) = (max, min);

            var cfg = Engine.Find(AnalogDirection.Input, channel);
            if (cfg == null)
            {
                _logger?.Warn($"[{DeviceName}] WaitAnalogInputInRangeAsync：AI{channel} 未配置");
                return false;
            }

            // 与数字量仿真"等待即满足"一致，避免原有流程在仿真下全部超时
            if (IsSimulated && Engine.GetEffectiveSimMode(channel) == AnalogSimMode.AlwaysPass)
                return true;

            var sw = Stopwatch.StartNew();
            long? stableSince = null;

            // 外部取消（Stop/Pause）直接上抛，不吞掉：否则调用方会把"用户暂停"误判为"等待超时"而触发假报警
            while (true)
            {
                token.ThrowIfCancellationRequested();

                double? v = ReadAnalogInput(channel);
                if (v is double value && value >= min && value <= max)
                {
                    stableSince ??= sw.ElapsedMilliseconds;
                    if (sw.ElapsedMilliseconds - stableSince.Value >= stableMs) return true;
                }
                else
                {
                    stableSince = null;
                }

                if (sw.ElapsedMilliseconds >= timeoutMs) break;
                await WaitNextSampleAsync(token).ConfigureAwait(false);
            }

            var last = ReadAnalogInputDetail(channel);
            _logger?.Warn($"[{DeviceName}] WaitAnalogInputInRangeAsync 超时：AI{channel}【{cfg.Name}】未在 {timeoutMs}ms 内" +
                          $"稳定进入 [{min}, {max}]（稳定要求 {stableMs}ms），最后读数 {last.Value?.ToString("G6") ?? "无"}（{last.Status}）");
            return false;
        }

        /// <inheritdoc/>
        public Task<bool> WaitAnalogInputInRangeAsync<T>(T name, double min, double max,
            int timeoutMs = 5000, int stableMs = 0, CancellationToken token = default) where T : Enum
            => WaitAnalogInputInRangeAsync(Convert.ToInt32(name), min, max, timeoutMs, stableMs, token);

        /// <summary>
        /// 等待下一次采样。卡类设备直接按 <see cref="WaitPollingIntervalMs"/> 轮询；
        /// 有扫描周期的设备应重写为等待"扫描完成"信号，避免空转。
        /// </summary>
        protected virtual Task WaitNextSampleAsync(CancellationToken token) => Task.Delay(WaitPollingIntervalMs, token);

        #endregion

        #region 安全

        /// <inheritdoc/>
        public virtual Task<bool> ApplySafeOutputsAsync(CancellationToken token = default)
        {
            bool allOk = true;
            foreach (var ao in Engine.Outputs.Where(o => o.SafeValue.HasValue))
            {
                token.ThrowIfCancellationRequested();
                bool ok = WriteAnalogOutput(ao.Channel, ao.SafeValue!.Value);
                allOk &= ok;
                if (ok) _logger?.Info($"[{DeviceName}] AO{ao.Channel}【{ao.Name}】已回到安全值 {ao.SafeValue}{ao.Unit}");
            }
            if (!allOk) _logger?.Warn($"[{DeviceName}] 部分 AO 未能回到安全值");
            return Task.FromResult(allOk);
        }

        #endregion

        #region 仿真注入（IAnalogSimulationControl）

        /// <inheritdoc/>
        public AnalogSimMode GetSimMode(int inputChannel) => Engine.GetEffectiveSimMode(inputChannel);

        /// <inheritdoc/>
        public void SetSimMode(int inputChannel, AnalogSimMode mode) => Engine.SetSimMode(inputChannel, mode);

        /// <inheritdoc/>
        public bool CanFollowOutput(int inputChannel) => Engine.CanFollow(inputChannel);

        /// <inheritdoc/>
        public void SetSimInput(int inputChannel, double value) => Engine.SetSimInput(inputChannel, value);

        #endregion

        #region BaseDevice 生命周期钩子

        /// <summary>连接：对配置了 Mode 的通道下发 AD/DA 模式，任一失败返回 false（由 BaseDevice 的重试兜底）。</summary>
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            var card = Card;
            if (card == null)
            {
                _logger?.Error($"[{DeviceName}] 连接失败：未挂载到板卡或 IO 模块，或所在板卡不支持模拟量，请检查父设备配置");
                return Task.FromResult(false);
            }

            foreach (var cfg in Engine.Inputs.Concat(Engine.Outputs).Where(c => c.Mode.HasValue))
            {
                token.ThrowIfCancellationRequested();
                if (!card.ConfigureAnalogChannel(AddressOf(cfg.Channel), cfg.Direction, cfg.Mode!.Value))
                {
                    _logger?.Error($"[{DeviceName}] 下发 {cfg} 模式 {cfg.Mode} 失败");
                    return Task.FromResult(false);
                }
            }
            return Task.FromResult(true);
        }

        /// <summary>仿真连接：清空仿真状态，回到通道配置里的 SimValue / SimMode。</summary>
        protected override Task<bool> InternalConnectSimulatedAsync(CancellationToken token)
        {
            Engine.ResetSimulation();
            return Task.FromResult(true);
        }

        /// <summary>
        /// 断开：尽力把 AO 写回安全值，失败只记日志。
        /// <para>只是兜底：ReloadAllAsync 不保证子设备先于板卡断开，板卡可能已关闭；
        /// 仿真态断开时 BaseDevice 不调用本钩子。主路径是业务在停机时显式调用 <see cref="ApplySafeOutputsAsync"/>。</para>
        /// </summary>
        protected override async Task InternalDisconnectAsync()
        {
            try
            {
                if (Card is { IsConnected: true })
                    await ApplySafeOutputsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[{DeviceName}] 断开前写安全值失败: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        protected override Task InternalResetAsync(CancellationToken token) => Task.CompletedTask;

        /// <summary>
        /// 健康检查：父设备断开、所在板卡不支持模拟量、板卡断开 → <see cref="AlarmCodes.Hardware.AnalogModuleUnavailable"/>，
        /// 恢复后防抖自动消除。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            var parent = Parent;
            var card = Card;
            string? reason =
                parent == null ? "尚未挂载到板卡或 IO 模块" :
                !parent.IsConnected ? $"父设备 [{parent.DeviceName}] 已断开" :
                card == null ? "所在板卡不支持模拟量（未实现 IAnalogCard），请检查父设备配置" :
                !card.IsConnected ? $"所在板卡 [{card.DeviceName}] 已断开" :
                null;

            UpdateAutoClearableHealth(reason != null, AlarmCodes.Hardware.AnalogModuleUnavailable,
                $"模拟量模块 [{DeviceName}] 不可用：{reason}");
            return Task.CompletedTask;
        }

        #endregion
    }
}
