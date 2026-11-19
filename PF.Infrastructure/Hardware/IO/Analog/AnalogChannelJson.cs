using PF.Core.Models.Device.Hardware.IO.Analog;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PF.Infrastructure.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量设备在 <c>HardwareConfig.ConnectionParameters</c> 中的键名与通道表 JSON 读写。
    /// 工厂、调试页、<c>BaseMechanism.EnsureAnalogChannelsAsync</c> 共用，保证三处格式一致。
    /// </summary>
    public static class AnalogChannelJson
    {
        /// <summary>通道表（JSON 数组）的键名</summary>
        public const string ChannelsKey = "Channels";

        /// <summary>节点号的键名（留空 = 板卡本体 AD/DA）</summary>
        public const string NodeIdKey = "NodeId";

        private static readonly JsonSerializerOptions Options = new()
        {
            // 通道名多为中文，避免存成 \uXXXX 难以人工核对
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// 解析通道表；为空返回空表，格式错误时经 <paramref name="onError"/> 报告并返回空表（不抛异常，
        /// 一个设备配置写坏不应让整个硬件初始化崩掉）。
        /// </summary>
        public static List<AnalogChannelConfig> Parse(string? json, Action<string>? onError = null)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<AnalogChannelConfig>();
            try
            {
                return JsonSerializer.Deserialize<List<AnalogChannelConfig>>(json, Options) ?? new List<AnalogChannelConfig>();
            }
            catch (JsonException ex)
            {
                onError?.Invoke($"模拟量通道表 JSON 解析失败，已按空表处理：{ex.Message}");
                return new List<AnalogChannelConfig>();
            }
        }

        /// <summary>序列化通道表（按方向、通道号排序，便于人工比对）</summary>
        public static string Serialize(IEnumerable<AnalogChannelConfig> channels)
            => JsonSerializer.Serialize(channels.OrderBy(c => c.Direction).ThenBy(c => c.Channel).ToList(), Options);

        /// <summary>
        /// 解析节点号：空白 → true 且 <paramref name="nodeId"/> = null（板卡本体）；
        /// 非负整数 → true；其余（写错的内容）→ false，调用方应报告配置错误而不是悄悄当成板卡本体。
        /// </summary>
        public static bool TryParseNodeId(string? text, out int? nodeId)
        {
            nodeId = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (int.TryParse(text.Trim(), out var v) && v >= 0) { nodeId = v; return true; }
            return false;
        }
    }
}
