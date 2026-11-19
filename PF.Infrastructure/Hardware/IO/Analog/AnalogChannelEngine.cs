using PF.Core.Enums.Hardware;
using PF.Core.Models.Device.Hardware.IO.Analog;

namespace PF.Infrastructure.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量通道引擎 —— 通道表快照、换算、限幅、断线判断与仿真状态。
    ///
    /// <para>它是普通类而不是设备：卡类设备（<see cref="CardAnalogIODevice"/>，挂在板卡或 IO 模块下）
    /// 与将来的扫描型通讯设备（直接继承 BaseDevice，不挂板卡）基类不同、无法共用继承链，
    /// 于是把与"通道"有关的逻辑收在这里，两类设备各持有一份，行为完全一致。</para>
    ///
    /// <para>线程安全：通道表为不可变快照，更新时整体替换（<see cref="Volatile"/>），读路径不加锁；
    /// 仿真状态由单把锁保护（只在仿真模式下使用，性能无关紧要）。</para>
    /// </summary>
    public sealed class AnalogChannelEngine
    {
        private sealed class Snapshot
        {
            public Snapshot(IEnumerable<AnalogChannelConfig> rows)
            {
                var list = rows.Select(r => r.Clone()).ToList();
                Map = list.ToDictionary(r => (r.Direction, r.Channel));
                Inputs = list.Where(r => r.Direction == AnalogDirection.Input).OrderBy(r => r.Channel).ToList();
                Outputs = list.Where(r => r.Direction == AnalogDirection.Output).OrderBy(r => r.Channel).ToList();
            }

            public Dictionary<(AnalogDirection, int), AnalogChannelConfig> Map { get; }
            public List<AnalogChannelConfig> Inputs { get; }
            public List<AnalogChannelConfig> Outputs { get; }
        }

        private Snapshot _snapshot;

        // ── 仿真状态（工程值）──
        private readonly object _simLock = new();
        private readonly Dictionary<int, double> _simOutputs = new();          // AO 通道 → 写入值
        private readonly Dictionary<int, double> _simInputs = new();           // AI 通道 → 手动值（Fixed / AlwaysPass）
        private readonly Dictionary<int, (double Value, long Ticks)> _simFollow = new(); // AI 通道 → 一阶滞后状态
        private readonly Dictionary<int, AnalogSimMode> _simModeOverrides = new();     // 调试页临时切换，只在内存

        private AnalogChannelEngine(IEnumerable<AnalogChannelConfig> rows) => _snapshot = new Snapshot(rows);

        /// <summary>
        /// 启动加载用：逐行校验，丢弃不合法的行（经 <paramref name="onDropped"/> 报告原因），
        /// 跟随目标不存在的 FollowOutput 行降级为 AlwaysPass。缺失的通道读出来是 NotConfigured。
        /// </summary>
        public static AnalogChannelEngine CreateLenient(IEnumerable<AnalogChannelConfig>? rows, Action<string>? onDropped = null)
        {
            var kept = new List<AnalogChannelConfig>();
            var keys = new HashSet<(AnalogDirection, int)>();

            foreach (var row in rows ?? Enumerable.Empty<AnalogChannelConfig>())
            {
                if (row == null) continue;
                var rowErrors = ValidateRow(row);
                if (rowErrors.Count > 0)
                {
                    onDropped?.Invoke($"{row}：{string.Join("；", rowErrors)}，该通道已忽略");
                    continue;
                }
                if (!keys.Add((row.Direction, row.Channel)))
                {
                    onDropped?.Invoke($"{row}：通道号重复，已忽略重复行");
                    continue;
                }
                kept.Add(row);
            }

            var aoChannels = kept.Where(r => r.Direction == AnalogDirection.Output).Select(r => r.Channel).ToHashSet();
            foreach (var row in kept.Where(r => r.Direction == AnalogDirection.Input && r.SimMode == AnalogSimMode.FollowOutput))
            {
                if (row.SimFollowChannel is not int f || !aoChannels.Contains(f))
                {
                    onDropped?.Invoke($"{row}：仿真跟随的 AO 通道不存在，仿真模式已降级为【等待直接通过】");
                    row.SimMode = AnalogSimMode.AlwaysPass;
                }
            }

            return new AnalogChannelEngine(kept);
        }

        #region 通道表

        /// <summary>AI 通道表（副本，修改不影响引擎）</summary>
        public IReadOnlyList<AnalogChannelConfig> Inputs => Volatile.Read(ref _snapshot).Inputs.Select(r => r.Clone()).ToList();

        /// <summary>AO 通道表（副本，修改不影响引擎）</summary>
        public IReadOnlyList<AnalogChannelConfig> Outputs => Volatile.Read(ref _snapshot).Outputs.Select(r => r.Clone()).ToList();

        /// <summary>查找通道（返回引擎内部对象，调用方不得修改）；不存在返回 null</summary>
        public AnalogChannelConfig? Find(AnalogDirection direction, int channel)
            => Volatile.Read(ref _snapshot).Map.TryGetValue((direction, channel), out var cfg) ? cfg : null;

        /// <summary>严格校验后整表原子替换；任何一处不合法则整表拒绝，原表不变。</summary>
        public AnalogConfigResult Update(IEnumerable<AnalogChannelConfig>? rows)
        {
            var list = (rows ?? Enumerable.Empty<AnalogChannelConfig>()).Where(r => r != null).ToList();
            var errors = Validate(list);
            if (errors.Count > 0) return AnalogConfigResult.Fail(errors);

            Volatile.Write(ref _snapshot, new Snapshot(list));
            return AnalogConfigResult.Ok();
        }

        /// <summary>严格校验整张通道表（逐行 + 重复通道号 + 仿真跟随引用）</summary>
        public static List<string> Validate(IReadOnlyCollection<AnalogChannelConfig> rows)
        {
            var errors = new List<string>();
            foreach (var row in rows)
            {
                foreach (var e in ValidateRow(row)) errors.Add($"{row}：{e}");
            }

            foreach (var dup in rows.GroupBy(r => (r.Direction, r.Channel)).Where(g => g.Count() > 1))
                errors.Add($"{(dup.Key.Direction == AnalogDirection.Input ? "AI" : "AO")}{dup.Key.Channel}：通道号重复");

            var aoChannels = rows.Where(r => r.Direction == AnalogDirection.Output).Select(r => r.Channel).ToHashSet();
            foreach (var row in rows.Where(r => r.Direction == AnalogDirection.Input && r.SimMode == AnalogSimMode.FollowOutput))
            {
                if (row.SimFollowChannel is not int f)
                    errors.Add($"{row}：仿真模式为【跟随输出】但未指定跟随的 AO 通道");
                else if (!aoChannels.Contains(f))
                    errors.Add($"{row}：仿真跟随的 AO{f} 不存在");
            }
            return errors;
        }

        private static List<string> ValidateRow(AnalogChannelConfig row)
        {
            var errors = new List<string>();
            if (row.Channel < 0) errors.Add("通道号不能为负");
            if (!IsFinite(row.RawMin) || !IsFinite(row.RawMax) || row.RawMin == row.RawMax) errors.Add("原始量程上下限不能相等");
            if (!IsFinite(row.EngMin) || !IsFinite(row.EngMax) || row.EngMin == row.EngMax) errors.Add("工程量程上下限不能相等");
            if (row.Decimals is < 0 or > 10) errors.Add("小数位须在 0~10 之间");
            if (row.SimLagMs < 0) errors.Add("仿真滞后时间不能为负");
            if (!IsFinite(row.SimValue)) errors.Add("仿真初值不是有效数字");

            if (row.Direction == AnalogDirection.Output)
            {
                if (!IsFinite(row.OutputMin) || !IsFinite(row.OutputMax) || row.OutputMin > row.OutputMax)
                    errors.Add("输出限幅下限不能大于上限");
                else if (row.SafeValue is double s && (s < row.OutputMin || s > row.OutputMax))
                    errors.Add($"安全值 {s} 不在输出限幅 [{row.OutputMin}, {row.OutputMax}] 内");
            }
            return errors;
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        #endregion

        #region 换算

        /// <summary>原始值 → 工程值（线性）</summary>
        public static double RawToEng(AnalogChannelConfig cfg, double raw)
            => cfg.EngMin + (raw - cfg.RawMin) * (cfg.EngMax - cfg.EngMin) / (cfg.RawMax - cfg.RawMin);

        /// <summary>工程值 → 原始值（线性）</summary>
        public static double EngToRaw(AnalogChannelConfig cfg, double eng)
            => cfg.RawMin + (eng - cfg.EngMin) * (cfg.RawMax - cfg.RawMin) / (cfg.EngMax - cfg.EngMin);

        /// <summary>
        /// 把一次原始读数转换为 <see cref="AnalogReading"/>：未配置 → NotConfigured；raw 为 null → CommError；
        /// 低于断线阈值 → WireBreak；超出原始量程 → Below/AboveRange（仍给工程值）。
        /// </summary>
        public static AnalogReading ToReading(AnalogChannelConfig? cfg, double? raw, DateTime timestamp)
        {
            if (cfg == null) return new AnalogReading(null, null, AnalogStatus.NotConfigured, timestamp);
            if (raw is not double r || !IsFinite(r)) return new AnalogReading(null, null, AnalogStatus.CommError, timestamp);
            if (cfg.WireBreakBelowRaw is double wb && r < wb) return new AnalogReading(null, r, AnalogStatus.WireBreak, timestamp);

            double lo = Math.Min(cfg.RawMin, cfg.RawMax), hi = Math.Max(cfg.RawMin, cfg.RawMax);
            var status = r < lo ? AnalogStatus.BelowRange : r > hi ? AnalogStatus.AboveRange : AnalogStatus.Ok;
            return new AnalogReading(RawToEng(cfg, r), r, status, timestamp);
        }

        /// <summary>
        /// AO 写出前检查：通道存在、数值有效、在 [OutputMin, OutputMax] 内（越限拒绝，不截断），
        /// 通过后给出原始值。
        /// </summary>
        public bool TryPrepareOutput(int channel, double value, out AnalogChannelConfig? cfg, out double raw, out string error)
        {
            raw = 0;
            error = string.Empty;
            cfg = Find(AnalogDirection.Output, channel);
            if (cfg == null) { error = $"AO{channel} 未配置"; return false; }
            if (!IsFinite(value)) { error = $"AO{channel} 写入值不是有效数字"; return false; }
            if (value < cfg.OutputMin || value > cfg.OutputMax)
            {
                error = $"AO{channel}【{cfg.Name}】写入值 {value} 超出限幅 [{cfg.OutputMin}, {cfg.OutputMax}]，已拒绝写出";
                return false;
            }
            raw = EngToRaw(cfg, value);
            return true;
        }

        #endregion

        #region 仿真

        /// <summary>清空全部仿真状态（仿真连接时调用，回到通道配置里的 SimValue）</summary>
        public void ResetSimulation()
        {
            lock (_simLock)
            {
                _simOutputs.Clear();
                _simInputs.Clear();
                _simFollow.Clear();
                _simModeOverrides.Clear();
            }
        }

        /// <summary>AI 通道当前生效的仿真模式（调试页临时切换优先于配置；跟随目标缺失时退化为 Fixed）</summary>
        public AnalogSimMode GetEffectiveSimMode(int channel)
        {
            var cfg = Find(AnalogDirection.Input, channel);
            if (cfg == null) return AnalogSimMode.AlwaysPass;

            AnalogSimMode mode;
            lock (_simLock) mode = _simModeOverrides.TryGetValue(channel, out var m) ? m : cfg.SimMode;

            if (mode == AnalogSimMode.FollowOutput && !CanFollow(cfg)) mode = AnalogSimMode.Fixed;
            return mode;
        }

        /// <summary>该 AI 通道是否配置了可用的跟随 AO（决定调试页能否切到【跟随输出】）</summary>
        public bool CanFollow(int channel) => Find(AnalogDirection.Input, channel) is { } cfg && CanFollow(cfg);

        private bool CanFollow(AnalogChannelConfig aiCfg)
            => aiCfg.SimFollowChannel is int f && Find(AnalogDirection.Output, f) != null;

        /// <summary>调试页临时切换 AI 仿真模式（只在内存，不落盘）</summary>
        public void SetSimMode(int channel, AnalogSimMode mode)
        {
            lock (_simLock)
            {
                _simModeOverrides[channel] = mode;
                _simFollow.Remove(channel); // 切到跟随时从手动值（未设置则为 SimValue）重新起步，见 SimReadInputRaw
            }
        }

        /// <summary>调试页注入 AI 仿真值（工程值）；跟随模式下相当于给滞后状态一个扰动</summary>
        public void SetSimInput(int channel, double value)
        {
            lock (_simLock)
            {
                _simInputs[channel] = value;
                if (_simFollow.ContainsKey(channel))
                    _simFollow[channel] = (value, Environment.TickCount64);
            }
        }

        /// <summary>仿真读 AI，返回原始值（再经 <see cref="ToReading"/> 判定状态，与真实路径一致）</summary>
        public double? SimReadInputRaw(AnalogChannelConfig aiCfg)
        {
            var mode = GetEffectiveSimMode(aiCfg.Channel);
            double eng;

            lock (_simLock)
            {
                if (mode == AnalogSimMode.FollowOutput && Find(AnalogDirection.Output, aiCfg.SimFollowChannel!.Value) is { } aoCfg)
                {
                    // AO 工程值按"量程占比"映射到本 AI 的工程量程：比例阀 50% → 真空 -100~0 kPa 的 -50
                    double ao = _simOutputs.TryGetValue(aoCfg.Channel, out var v) ? v : aoCfg.SimValue;
                    double fraction = (ao - aoCfg.EngMin) / (aoCfg.EngMax - aoCfg.EngMin);
                    double target = aiCfg.EngMin + fraction * (aiCfg.EngMax - aiCfg.EngMin);

                    long now = Environment.TickCount64;
                    if (!_simFollow.TryGetValue(aiCfg.Channel, out var state))
                        state = (_simInputs.TryGetValue(aiCfg.Channel, out var init) ? init : aiCfg.SimValue, now);

                    double dt = now - state.Ticks;
                    double next = aiCfg.SimLagMs <= 0
                        ? target
                        : state.Value + (target - state.Value) * (1 - Math.Exp(-dt / aiCfg.SimLagMs));
                    _simFollow[aiCfg.Channel] = (next, now);
                    eng = next;
                }
                else
                {
                    eng = _simInputs.TryGetValue(aiCfg.Channel, out var v) ? v : aiCfg.SimValue;
                }
            }
            return EngToRaw(aiCfg, eng);
        }

        /// <summary>仿真写 AO（工程值，已通过限幅检查）</summary>
        public void SimWriteOutput(int channel, double value)
        {
            lock (_simLock) _simOutputs[channel] = value;
        }

        /// <summary>仿真回读 AO（工程值）</summary>
        public double SimReadOutput(AnalogChannelConfig aoCfg)
        {
            lock (_simLock) return _simOutputs.TryGetValue(aoCfg.Channel, out var v) ? v : aoCfg.SimValue;
        }

        #endregion
    }
}
