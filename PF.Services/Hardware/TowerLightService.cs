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
    /// </summary>
    public class TowerLightService : ITowerLightService
    {
        private readonly ITowerLightDoWriter _writer; // 硬件数字输出（DO）写入器
        private readonly ILogService _logger;         // 日志服务
        private readonly IParamService _paramService; // 参数/配置服务
        private readonly object _lock = new();        // 用于保护 _slots 字典和硬件写入顺序的同步锁

        /// <summary>
        /// 内部类：定义每个硬件通道（红/黄/绿/蜂鸣器）的状态槽位
        /// </summary>
        private sealed class ChannelSlot
        {
            // 业务方请求的状态（可能由于屏蔽等原因与实际硬件状态不一致）
            public LightState RequestedState { get; set; } = LightState.Off;
            // 闪烁间隔（毫秒）
            public int BlinkIntervalMs { get; set; } = 500;
            // 控制异步闪烁循环任务取消的令牌源
            public CancellationTokenSource? Cts { get; set; }
        }

        // 维护四个物理通道的状态字典
        private readonly Dictionary<LightColor, ChannelSlot> _slots = new()
        {
            { LightColor.Red,    new ChannelSlot() },
            { LightColor.Yellow, new ChannelSlot() },
            { LightColor.Green,  new ChannelSlot() },
            { LightColor.Buzzer, new ChannelSlot() },
        };

        private const string BuzzerMutedParamKey = "BuzzerMuted"; // 数据库中的参数名
        private const string EParamsTypeName = "E_Params";       // 参数所属的类型名
        private bool _isBuzzerMuted;                             // 缓存当前的蜂鸣器屏蔽状态

        /// <summary>
        /// 全局蜂鸣器屏蔽开关。
        /// 逻辑：屏蔽时强行将蜂鸣器硬件置为 Off，但不改变 RequestedState，以便解除屏蔽时恢复。
        /// </summary>
        public bool IsBuzzerMuted
        {
            get => _isBuzzerMuted;
            set
            {
                if (_isBuzzerMuted == value) return;

                _isBuzzerMuted = value;

                // 1. 异步将设置持久化到数据库
                try
                {
                    _ = _paramService.SetParamAsync(EParamsTypeName, BuzzerMutedParamKey, value)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"【三色灯】保存蜂鸣器屏蔽参数失败：{ex.Message}");
                }

                // 2. 立即在硬件上生效（需持锁以防此时有其他线程修改蜂鸣器状态）
                lock (_lock)
                {
                    var slot = _slots[LightColor.Buzzer];
                    // 重新计算“屏蔽逻辑”后的有效状态
                    var effective = ComputeEffective(LightColor.Buzzer, slot.RequestedState);
                    ApplyEffectiveStateCore(slot, LightColor.Buzzer.ToString(), effective, slot.BlinkIntervalMs);
                    _logger.Info($"【三色灯】蜂鸣器屏蔽已{(value ? "开启" : "关闭")}，当前有效状态：{effective}");
                }
            }
        }
        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="logger"></param>
        /// <param name="paramService"></param>
        public TowerLightService(
            ITowerLightDoWriter writer,
            ILogService logger,
            IParamService paramService)
        {
            _writer = writer;
            _logger = logger;
            _paramService = paramService;

            // 初始化：从数据库同步加载初始屏蔽状态
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

            // 订阅参数变更事件，实现 UI 修改参数后自动同步到本服务
            _paramService.ParamChanged += OnParamChanged;
        }

        // ── 公开控制 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 设置指定通道的状态
        /// </summary>
        public void SetLight(LightColor color, LightState state, int blinkIntervalMs = 500)
        {
            lock (_lock)
            {
                var slot = _slots[color];
                slot.RequestedState = state; // 记录原始请求
                slot.BlinkIntervalMs = blinkIntervalMs;

                // 计算受屏蔽逻辑影响后的最终状态
                var effective = ComputeEffective(color, state);
                ApplyEffectiveStateCore(slot, color.ToString(), effective, blinkIntervalMs);
            }
        }

        /// <summary>
        /// 批量设置灯光状态
        /// </summary>
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

        /// <summary>
        /// 关闭所有通道并重置内部记录
        /// </summary>
        public void TurnOffAll()
        {
            lock (_lock)
            {
                foreach (var (color, slot) in _slots)
                {
                    slot.Cts?.Cancel(); // 停止可能的闪烁循环
                    slot.Cts?.Dispose();
                    slot.Cts = null;

                    _writer.Write(color.ToString(), false); // 硬件拉低
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

        /// <summary>
        /// 计算有效状态：如果是蜂鸣器且处于屏蔽状态，则强行返回 Off，否则返回请求状态
        /// </summary>
        private LightState ComputeEffective(LightColor color, LightState requested)
            => (color == LightColor.Buzzer && _isBuzzerMuted) ? LightState.Off : requested;

        /// <summary>
        /// 核心转换逻辑：将抽象的 LightState 转换为具体的物理 DO 操作或异步闪烁任务
        /// </summary>
        private void ApplyEffectiveStateCore(ChannelSlot slot, string tag, LightState effective, int intervalMs)
        {
            // 无论新状态是什么，都必须先停止并清理旧的异步闪烁任务（如果有）
            slot.Cts?.Cancel();
            slot.Cts?.Dispose();
            slot.Cts = null;

            switch (effective)
            {
                case LightState.Off:
                    _writer.Write(tag, false); // 物理关闭
                    break;

                case LightState.On:
                    _writer.Write(tag, true);  // 物理开启
                    break;

                case LightState.Blinking:
                    _writer.Write(tag, false); // 确保从“灭”的状态开始闪烁
                    slot.Cts = new CancellationTokenSource();
                    // 启动火警式异步循环，不阻塞当前线程
                    _ = BlinkLoopAsync(tag, intervalMs, slot.Cts.Token);
                    break;
            }
        }

        /// <summary>
        /// 软件驱动的频闪循环
        /// </summary>
        private async Task BlinkLoopAsync(string tag, int intervalMs, CancellationToken token)
        {
            // 使用 PeriodicTimer 代替 Thread.Sleep，更精确且不占用线程
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
            bool phase = true;  // 亮/灭相位翻转标志
            try
            {
                // 等待下一个周期，如果 token 被取消则退出循环
                while (await timer.WaitForNextTickAsync(token))
                {
                    _writer.Write(tag, phase);
                    phase = !phase; // 切换状态
                }
            }
            catch (OperationCanceledException)
            {
                // 正常的任务取消，无需处理
            }
            // 注意：此处不执行 _writer.Write(tag, false)。
            // 因为当任务被取消时，ApplyEffectiveStateCore 正在 lock 内写入新状态，
            // 若此处再次写入 false，可能会覆盖掉刚刚设置的“常亮”或“屏蔽”状态。
        }
    }
}