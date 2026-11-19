using PF.Core.Enums.Hardware;
using PF.Core.Models.Device.Hardware.IO.Analog;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>模拟量调试页的枚举显示辅助</summary>
    internal static class AnalogEnumText
    {
        /// <summary>取枚举 [Description]，无则取名称</summary>
        public static string Of(Enum value)
            => value.GetType().GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
               ?? value.ToString();
    }

    /// <summary>仿真模式下拉项</summary>
    public sealed record AnalogSimModeOption(AnalogSimMode Mode, string Text);

    /// <summary>
    /// 模拟量调试页 AI 行：实时读数 + 仿真注入。
    /// 读数由页面定时器调用 <see cref="Apply"/> 刷新；仿真模式切换、注入经页面回调转给设备。
    /// </summary>
    public class AnalogInputRowViewModel : BindableBase
    {
        private readonly Action<int, AnalogSimMode> _onSimModeChanged;
        private bool _suppressSimModeCallback;

        /// <summary>构造 AI 行</summary>
        public AnalogInputRowViewModel(AnalogChannelConfig cfg, bool canFollow,
            Action<int, AnalogSimMode> onSimModeChanged, Action<AnalogInputRowViewModel> onApplySim)
        {
            Channel = cfg.Channel;
            Name = string.IsNullOrWhiteSpace(cfg.Name) ? $"AI{cfg.Channel}" : cfg.Name;
            Unit = cfg.Unit;
            Decimals = cfg.Decimals;
            EngMin = cfg.EngMin;
            EngMax = cfg.EngMax;
            RangeText = $"量程 {cfg.EngMin:G6} ~ {cfg.EngMax:G6} {cfg.Unit}  |  原始 {cfg.RawMin:G6} ~ {cfg.RawMax:G6}（{AnalogEnumText.Of(cfg.SignalType)}）";
            SimValueText = cfg.SimValue.ToString(CultureInfo.InvariantCulture);

            _onSimModeChanged = onSimModeChanged;
            SimModeOptions = Enum.GetValues<AnalogSimMode>()
                .Where(m => m != AnalogSimMode.FollowOutput || canFollow)
                .Select(m => new AnalogSimModeOption(m, AnalogEnumText.Of(m)))
                .ToList();
            ApplySimCommand = new DelegateCommand(() => onApplySim(this));
        }

        /// <summary>通道号</summary>
        public int Channel { get; }
        /// <summary>名称</summary>
        public string Name { get; }
        /// <summary>单位</summary>
        public string Unit { get; }
        /// <summary>小数位</summary>
        public int Decimals { get; }
        /// <summary>工程量程下限</summary>
        public double EngMin { get; }
        /// <summary>工程量程上限</summary>
        public double EngMax { get; }
        /// <summary>量程说明</summary>
        public string RangeText { get; }

        private string _valueText = "--";
        /// <summary>工程值</summary>
        public string ValueText { get => _valueText; private set => SetProperty(ref _valueText, value); }

        private string _rawText = "--";
        /// <summary>原始值</summary>
        public string RawText { get => _rawText; private set => SetProperty(ref _rawText, value); }

        private AnalogStatus _status = AnalogStatus.NotConfigured;
        /// <summary>读数状态（界面按它着色）</summary>
        public AnalogStatus Status { get => _status; private set => SetProperty(ref _status, value); }

        private string _statusText = "--";
        /// <summary>读数状态文字</summary>
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        private double _percent;
        /// <summary>读数在工程量程内的百分比（0~100，进度条用）</summary>
        public double Percent { get => _percent; private set => SetProperty(ref _percent, value); }

        private string _timeText = "--";
        /// <summary>采样时间</summary>
        public string TimeText { get => _timeText; private set => SetProperty(ref _timeText, value); }

        private bool _isSimulated;
        /// <summary>设备是否处于仿真模式（控制仿真面板显示）</summary>
        public bool IsSimulated { get => _isSimulated; set => SetProperty(ref _isSimulated, value); }

        /// <summary>可选仿真模式（未配置跟随 AO 的通道不提供【跟随输出】）</summary>
        public IReadOnlyList<AnalogSimModeOption> SimModeOptions { get; }

        private AnalogSimMode _simMode;
        /// <summary>当前仿真模式；用户切换时立即作用到设备（只在内存）</summary>
        public AnalogSimMode SimMode
        {
            get => _simMode;
            set
            {
                if (SetProperty(ref _simMode, value) && !_suppressSimModeCallback)
                    _onSimModeChanged(Channel, value);
            }
        }

        private string _simValueText;
        /// <summary>待注入的仿真值（工程值）</summary>
        public string SimValueText { get => _simValueText; set => SetProperty(ref _simValueText, value); }

        /// <summary>注入仿真值命令</summary>
        public DelegateCommand ApplySimCommand { get; }

        /// <summary>刷新读数</summary>
        public void Apply(AnalogReading reading, bool isSimulated, AnalogSimMode simMode)
        {
            string fmt = "F" + Math.Clamp(Decimals, 0, 10);
            ValueText = reading.Value?.ToString(fmt) ?? "--";
            RawText = reading.Raw?.ToString("G6") ?? "--";
            Status = reading.Status;
            StatusText = AnalogEnumText.Of(reading.Status);
            TimeText = reading.Timestamp.ToString("HH:mm:ss.fff");
            Percent = reading.Value is double v && EngMax != EngMin
                ? Math.Clamp((v - EngMin) / (EngMax - EngMin) * 100, 0, 100)
                : 0;

            IsSimulated = isSimulated;
            _suppressSimModeCallback = true;
            try { SimMode = simMode; }
            finally { _suppressSimModeCallback = false; }
        }
    }

    /// <summary>模拟量调试页 AO 行：回读 + 写出</summary>
    public class AnalogOutputRowViewModel : BindableBase
    {
        /// <summary>构造 AO 行</summary>
        public AnalogOutputRowViewModel(AnalogChannelConfig cfg, Func<AnalogOutputRowViewModel, double, Task> write)
        {
            Channel = cfg.Channel;
            Name = string.IsNullOrWhiteSpace(cfg.Name) ? $"AO{cfg.Channel}" : cfg.Name;
            Unit = cfg.Unit;
            Decimals = cfg.Decimals;
            OutputMin = cfg.OutputMin;
            OutputMax = cfg.OutputMax;
            LimitText = $"限幅 {cfg.OutputMin:G6} ~ {cfg.OutputMax:G6} {cfg.Unit}  |  安全值 {(cfg.SafeValue?.ToString("G6") ?? "未设置")}" +
                        $"  |  量程 {cfg.EngMin:G6} ~ {cfg.EngMax:G6}";
            TargetText = (cfg.SafeValue ?? cfg.OutputMin).ToString(CultureInfo.InvariantCulture);

            WriteCommand = new DelegateCommand(async () =>
            {
                if (!double.TryParse(TargetText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    && !double.TryParse(TargetText, out v))
                {
                    ResultText = "写入值不是有效数字";
                    return;
                }
                await write(this, v);
            });

            QuickWriteCommand = new DelegateCommand<string>(async p =>
            {
                if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)) return;
                double v = OutputMin + percent / 100.0 * (OutputMax - OutputMin);
                TargetText = v.ToString("G6", CultureInfo.InvariantCulture);
                await write(this, v);
            });
        }

        /// <summary>通道号</summary>
        public int Channel { get; }
        /// <summary>名称</summary>
        public string Name { get; }
        /// <summary>单位</summary>
        public string Unit { get; }
        /// <summary>小数位</summary>
        public int Decimals { get; }
        /// <summary>输出下限</summary>
        public double OutputMin { get; }
        /// <summary>输出上限</summary>
        public double OutputMax { get; }
        /// <summary>限幅说明</summary>
        public string LimitText { get; }

        private string _readbackText = "--";
        /// <summary>回读值</summary>
        public string ReadbackText { get => _readbackText; private set => SetProperty(ref _readbackText, value); }

        private string _targetText;
        /// <summary>待写入值</summary>
        public string TargetText { get => _targetText; set => SetProperty(ref _targetText, value); }

        private string _resultText = string.Empty;
        /// <summary>最近一次写出结果</summary>
        public string ResultText { get => _resultText; set => SetProperty(ref _resultText, value); }

        /// <summary>写出命令</summary>
        public DelegateCommand WriteCommand { get; }

        /// <summary>按限幅区间百分比快捷写出（参数 "0" / "50" / "100"）</summary>
        public DelegateCommand<string> QuickWriteCommand { get; }

        /// <summary>刷新回读</summary>
        public void Apply(double? readback)
            => ReadbackText = readback?.ToString("F" + Math.Clamp(Decimals, 0, 10)) ?? "--";
    }
}
