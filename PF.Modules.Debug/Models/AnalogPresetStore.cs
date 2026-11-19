using PF.Core.Constants;
using PF.Core.Enums.Hardware;
using PF.Core.Models.Device.Hardware.IO.Analog;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 模拟量传感器预设：一组"信号类型 + 量程 + 单位"，调试页「套用预设」时填到选中通道。
    /// 型号差异就是一条预设，不需要写代码。
    /// </summary>
    public class AnalogPreset
    {
        /// <summary>预设名称（如"4~20mA 真空变送器 -100~0kPa"）</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>信号类型</summary>
        public AnalogSignalType SignalType { get; set; } = AnalogSignalType.Voltage0To10;
        /// <summary>原始量程下限</summary>
        public double RawMin { get; set; }
        /// <summary>原始量程上限</summary>
        public double RawMax { get; set; } = 10;
        /// <summary>工程量程下限</summary>
        public double EngMin { get; set; }
        /// <summary>工程量程上限</summary>
        public double EngMax { get; set; } = 10;
        /// <summary>断线阈值（原始值）</summary>
        public double? WireBreakBelowRaw { get; set; }
        /// <summary>单位</summary>
        public string Unit { get; set; } = string.Empty;
        /// <summary>小数位</summary>
        public int Decimals { get; set; } = 2;

        /// <summary>套用到通道（只改换算相关字段，不动方向/通道号/名称/限幅/仿真）</summary>
        public void ApplyTo(AnalogChannelConfig cfg)
        {
            cfg.SignalType = SignalType;
            cfg.RawMin = RawMin;
            cfg.RawMax = RawMax;
            cfg.EngMin = EngMin;
            cfg.EngMax = EngMax;
            cfg.WireBreakBelowRaw = WireBreakBelowRaw;
            cfg.Unit = Unit;
            cfg.Decimals = Decimals;
        }

        /// <summary>由通道生成预设</summary>
        public static AnalogPreset From(AnalogChannelConfig cfg, string name) => new()
        {
            Name = name,
            SignalType = cfg.SignalType,
            RawMin = cfg.RawMin,
            RawMax = cfg.RawMax,
            EngMin = cfg.EngMin,
            EngMax = cfg.EngMax,
            WireBreakBelowRaw = cfg.WireBreakBelowRaw,
            Unit = cfg.Unit,
            Decimals = cfg.Decimals,
        };

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// 预设库：<c>{ConfigPath}\AnalogPresets.json</c>，文件不存在时写入内置的几种通用信号类型。
    /// 可直接编辑该文件增删预设，或在调试页「选中行存为预设」。
    /// </summary>
    public static class AnalogPresetStore
    {
        // 必须是表达式属性：ConfigPath 在 ConstGlobalParam.Initialize 之前访问会抛异常
        private static string FilePath => Path.Combine(ConstGlobalParam.ConfigPath, "AnalogPresets.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static List<AnalogPreset> BuiltIn() =>
        [
            new() { Name = "0~10V 通用（0~10）", SignalType = AnalogSignalType.Voltage0To10, RawMin = 0, RawMax = 10, EngMin = 0, EngMax = 10, Unit = "V" },
            new() { Name = "±10V 通用（-10~10）", SignalType = AnalogSignalType.VoltagePm10, RawMin = -10, RawMax = 10, EngMin = -10, EngMax = 10, Unit = "V" },
            new() { Name = "0~5V 通用（0~5）", SignalType = AnalogSignalType.Voltage0To5, RawMin = 0, RawMax = 5, EngMin = 0, EngMax = 5, Unit = "V" },
            new() { Name = "4~20mA 通用（0~100%）", SignalType = AnalogSignalType.Current4To20, RawMin = 4, RawMax = 20, EngMin = 0, EngMax = 100, WireBreakBelowRaw = 3.6, Unit = "%", Decimals = 1 },
            new() { Name = "0~20mA 通用（0~100%）", SignalType = AnalogSignalType.Current0To20, RawMin = 0, RawMax = 20, EngMin = 0, EngMax = 100, Unit = "%", Decimals = 1 },
            new() { Name = "0~10V 比例阀（0~100%）", SignalType = AnalogSignalType.Voltage0To10, RawMin = 0, RawMax = 10, EngMin = 0, EngMax = 100, Unit = "%", Decimals = 0 },
        ];

        /// <summary>读取预设库；文件缺失时写入内置预设，读失败时返回内置预设（经 <paramref name="onError"/> 报告）</summary>
        public static List<AnalogPreset> Load(Action<string>? onError = null)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    var builtIn = BuiltIn();
                    Save(builtIn);
                    return builtIn;
                }
                return JsonSerializer.Deserialize<List<AnalogPreset>>(File.ReadAllText(FilePath), Options) ?? BuiltIn();
            }
            catch (Exception ex)
            {
                onError?.Invoke($"读取模拟量预设库失败，已使用内置预设：{ex.Message}");
                return BuiltIn();
            }
        }

        /// <summary>保存预设库</summary>
        public static void Save(IEnumerable<AnalogPreset> presets)
        {
            PF.Infrastructure.Persistence.AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(presets.ToList(), Options));
        }
    }
}
