using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.TowerLight;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 三色灯逻辑控制服务实现。
    ///
    /// 并发模型：
    ///   · 所有槽位（_slots）的读写均在 _lock 内完成，消除状态竞态。
    ///   · 每个通道持有独立的 CancellationTokenSource；切换状态前先 Cancel + Dispose 旧的。
    ///   · BlinkLoopAsync 退出时不向硬件写值，DO 状态由外层 ApplyEffectiveStateCore 负责，
    ///     避免"闪烁 → 常亮"切换时 finally 覆盖新状态的 Race Condition。
    ///
    /// 蜂鸣器屏蔽：
    ///   · 通过 <see cref="IParamService"/> 读写参数（参照安全门屏蔽模式）。
    ///   · 构造函数同步加载初始屏蔽状态，属性 setter 持久化到数据库。
    ///   · RequestedState 始终记录业务方的真实意图，屏蔽解除后可还原。
    /// </summary>
    public class TowerLightService : ITowerLightService
    {
        private readonly ITowerLightDoWriter _writer;
        private readonly ILogService _logger;
        private readonly IParamService _paramService;
        private readonly object _lock = new();

        private sealed class ChannelSlot
        {
            public LightState RequestedState { get; set; } = LightState.Off;
            public int BlinkIntervalMs { get; set; } = 500;
            public CancellationTokenSource? Cts { get; set; }
        }

        private readonly Dictionary<LightColor, ChannelSlot> _slots = new()
        {
            { LightColor.Red,    new ChannelSlot() },
            { LightColor.Yellow, new ChannelSlot() },
            { LightColor.Green,  new ChannelSlot() },
            { LightColor.Buzzer, new ChannelSlot() },
        };

        // 参数键名与类型名（与 E_Params.BuzzerMuted 对应）
        private const string BuzzerMutedParamKey = "BuzzerMuted";
        private const string EParamsTypeName = "E_Params";

        // 内部字段（由参数服务驱动）
        private bool _isBuzzerMuted;

        /// <summary>
        /// 全局蜂鸣器屏蔽开关。
        /// true = 静音：蜂鸣器请求的任何状态均以 Off 生效，已在运行的任务立即取消；
        /// false = 解除：按 RequestedState 重新评估并即时生效。
        ///
        /// 本属性读写参照安全门屏蔽模式：值持久化到 <see cref="E_Params.BuzzerMuted"/> 参数。
        /// </summary>
        public bool IsBuzzerMuted
        {
            get => _isBuzzerMuted;
            set
            {
                if (_isBuzzerMuted == value) return;

                _isBuzzerMuted = value;

                // 持久化到参数数据库（参照安全门屏蔽模式）
                try
                {
                    _ = _paramService.SetParamAsync(EParamsTypeName, BuzzerMutedParamKey, value)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"【三色灯】保存蜂鸣器屏蔽参数失败：{ex.Message}");
                }

                lock (_lock)
                {
                    var slot = _slots[LightColor.Buzzer];
                    var effective = ComputeEffective(LightColor.Buzzer, slot.RequestedState);
                    ApplyEffectiveStateCore(slot, LightColor.Buzzer.ToString(), effective, slot.BlinkIntervalMs);
                    _logger.Info($"【三色灯】蜂鸣器屏蔽已{(value ? "开启" : "关闭")}，当前有效状态：{effective}");
                }
            }
        }

        public TowerLightService(
            ITowerLightDoWriter writer,
            ILogService logger,
            IParamService paramService)
        {
            _writer = writer;
            _logger = logger;
            _paramService = paramService;

            // 构造函数同步加载初始屏蔽状态（参照安全门屏蔽的 StartSafetyMonitoring 行为）
            try
            {
                _isBuzzerMuted = _paramService.GetParamAsync<bool>(
                    BuzzerMutedParamKey,
                    false).GetAwaiter().GetResult();

                if (_isBuzzerMuted)
                    _logger.Info("【三色灯】蜂鸣器已屏蔽（调试模式）。");
            }
            catch (Exception ex)
            {
                _logger.Warn($"【三色灯】读取蜂鸣器屏蔽参数失败，使用默认值 false：{ex.Message}");
                _isBuzzerMuted = false;
            }

            _paramService.ParamChanged += OnParamChanged;
        }

        // ── 公开控制 API ─────────────────────────────────────────────────────────

        public void SetLight(LightColor color, LightState state, int blinkIntervalMs = 500)
        {
            lock (_lock)
            {
                var slot = _slots[color];
                slot.RequestedState = state;
                slot.BlinkIntervalMs = blinkIntervalMs;
                var effective = ComputeEffective(color, state);
                ApplyEffectiveStateCore(slot, color.ToString(), effective, blinkIntervalMs);
            }
        }

        public void SetLights(IReadOnlyDictionary<LightColor, LightState> states, int blinkIntervalMs = 500)
        {
            lock (_lock)
            {
                foreach (var (color, state) in states)
                {
                    var slot = _slots[color];
                    slot.RequestedState = state;
                    slot.BlinkIntervalMs = blinkIntervalMs;
                    var effective = ComputeEffective(color, state);
                    ApplyEffectiveStateCore(slot, color.ToString(), effective, blinkIntervalMs);
                }
            }
        }

        public void TurnOffAll()
        {
            lock (_lock)
            {
                foreach (var (color, slot) in _slots)
                {
                    slot.Cts?.Cancel();
                    slot.Cts?.Dispose();
                    slot.Cts = null;
                    _writer.Write(color.ToString(), false);
                    slot.RequestedState = LightState.Off;
                    slot.BlinkIntervalMs = 500;
                }
                _logger.Info("【三色灯】所有通道已关闭。");
            }
        }

        // ── 私有核心逻辑 ─────────────────────────────────────────────────────────

        private void OnParamChanged(object? sender, ParamChangedEventArgs e)
        {
            if (e.ParamName == BuzzerMutedParamKey && e.NewValue is bool b)
                IsBuzzerMuted = b;
        }

        private LightState ComputeEffective(LightColor color, LightState requested)
            => (color == LightColor.Buzzer && _isBuzzerMuted) ? LightState.Off : requested;

        /// <summary>
        /// 在持锁上下文中应用有效状态：先清理旧 CTS，再按 effective 驱动硬件与频闪任务。
        /// </summary>
        private void ApplyEffectiveStateCore(ChannelSlot slot, string tag, LightState effective, int intervalMs)
        {
            slot.Cts?.Cancel();
            slot.Cts?.Dispose();
            slot.Cts = null;

            switch (effective)
            {
                case LightState.Off:
                    _writer.Write(tag, false);
                    break;

                case LightState.On:
                    _writer.Write(tag, true);
                    break;

                case LightState.Blinking:
                    _writer.Write(tag, false);                // 初始已知态：灭
                    slot.Cts = new CancellationTokenSource();
                    _ = BlinkLoopAsync(tag, intervalMs, slot.Cts.Token);
                    break;
            }
        }

        /// <summary>
        /// 软件频闪主循环（PeriodicTimer，无 Thread.Sleep）。
        ///
        /// 注意：catch 块仅静默退出，<b>不在 finally 写硬件</b>。
        /// 原因：Cancel() 由外层 ApplyEffectiveStateCore 在 lock 内同步调用，
        /// 外层已将 DO 点写入新目标状态（On/Off），若此处写 false 会覆盖该状态。
        /// </summary>
        private async Task BlinkLoopAsync(string tag, int intervalMs, CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
            bool phase = true;  // 第一次 tick 写 true（亮），后续交替
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    _writer.Write(tag, phase);
                    phase = !phase;
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
