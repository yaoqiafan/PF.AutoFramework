using System.Text;
using System.Text.Json;

namespace PF.Core.Constants
{
    /// <summary>
    /// 本机通道（127.0.0.1:6800，主程序 ↔ SECS 服务）的帧定义。两端（框架 InternalClient 与独立仓库的
    /// PF.SecsGem.Service）都只从这里取常量，帧格式只有这一份。
    ///
    /// <para><b>帧格式</b></para>
    /// <list type="table">
    /// <item><term>0x00 报文帧</term><description>[0x00][长度 4 字节大端][SECS 报文 N 字节]（双向，原有）</description></item>
    /// <item><term>0x02 Host 状态帧</term><description>[0x02][状态 1 字节]（服务 → 主程序，原有）</description></item>
    /// <item><term>0x03 握手帧</term><description>[0x03][<see cref="SecsGemLocalHello"/> 的 JSON][0x0A]（双向，新增）</description></item>
    /// <item><term>0x04 链路配置帧</term><description>[0x04][SecsGemSystemParam 的 JSON][0x0A]（服务 → 主程序，新增）</description></item>
    /// </list>
    ///
    /// <para><b>为什么新帧是"JSON + 换行"而不是"长度 + 正文"</b>：旧版两端遇到不认识的命令字节都是
    /// 丢一个字节再重新同步。若新帧带 4 字节大端长度，长度首字节几乎总是 0x00，旧版会把它认成报文帧，
    /// 按错误长度一直等下去，之后的真实报文全部被吞掉。<see cref="BuildTextFrame"/> 产出的 JSON 只含可打印
    /// ASCII（非 ASCII 与控制字符一律转义），不会出现 0x00 / 0x02，旧版逐字节丢弃即可安全越过整帧。</para>
    ///
    /// <para><b>版本规则</b>：只加帧类型或加 JSON 字段时不升 <see cref="ProtocolVersion"/>（两端都忽略不认识的）；
    /// 改变既有帧含义才加一，并按需提高 <see cref="MinCompatibleVersion"/>。</para>
    /// </summary>
    public static class SecsGemLocalChannel
    {
        /// <summary>本机通道端口。</summary>
        public const int Port = 6800;

        /// <summary>本端实现的本机通道协议版本。</summary>
        public const int ProtocolVersion = 1;

        /// <summary>本端能兼容的对端最低协议版本。</summary>
        public const int MinCompatibleVersion = 1;

        /// <summary>报文帧。</summary>
        public const byte SecsMessageFrame = 0x00;

        /// <summary>Host 连接状态帧。</summary>
        public const byte HostStatusFrame = 0x02;

        /// <summary>握手帧：双方在连接建立后各发一次。</summary>
        public const byte HelloFrame = 0x03;

        /// <summary>链路配置帧：服务推送当前生效的链路配置。</summary>
        public const byte LinkConfigFrame = 0x04;

        /// <summary>文本帧结束符。</summary>
        public const byte TextFrameTerminator = 0x0A;

        /// <summary>文本帧正文上限；超过仍没等到结束符视为脏数据，丢弃命令字节重新同步。</summary>
        public const int MaxTextFrameLength = 64 * 1024;

        /// <summary>主程序接入后等待服务握手帧的时间；超时未收到按"旧版服务"处理。</summary>
        public static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(2);

        /// <summary>服务独立保存的链路配置文件（配置根目录下，不在任何项目子目录里）。</summary>
        public static string LinkConfigPath => Path.Combine(ConstGlobalParam.ConfigRoot, "SecsGemService", "link.json");

        /// <summary>是否为文本帧（握手 / 链路配置）的命令字节。</summary>
        public static bool IsTextFrame(byte command) => command is HelloFrame or LinkConfigFrame;

        /// <summary>文本帧序列化选项：默认转义规则保证输出只含可打印 ASCII，不能换成宽松编码器。</summary>
        public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        /// <summary>组一个文本帧：[命令][JSON][0x0A]。</summary>
        public static byte[] BuildTextFrame<T>(byte command, T payload)
        {
            if (!IsTextFrame(command)) throw new ArgumentOutOfRangeException(nameof(command), $"0x{command:X2} 不是文本帧");
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
            var frame = new byte[json.Length + 2];
            frame[0] = command;
            Buffer.BlockCopy(json, 0, frame, 1, json.Length);
            frame[^1] = TextFrameTerminator;
            return frame;
        }

        /// <summary>解析文本帧正文（不含命令字节与结束符）；格式错误返回 null。</summary>
        public static T? ParseTextBody<T>(ReadOnlySpan<byte> body) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>对端的协议版本与本端是否兼容。</summary>
        public static bool IsCompatible(SecsGemLocalHello peer) =>
            peer.ProtocolVersion >= MinCompatibleVersion && ProtocolVersion >= peer.MinCompatibleVersion;
    }

    /// <summary>本机通道握手内容。</summary>
    public sealed class SecsGemLocalHello
    {
        /// <summary>发送方角色：<see cref="RoleService"/> 或 <see cref="RoleMainProgram"/>。</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>发送方实现的协议版本。</summary>
        public int ProtocolVersion { get; set; }

        /// <summary>发送方能兼容的对端最低协议版本。</summary>
        public int MinCompatibleVersion { get; set; }

        /// <summary>发送方程序版本（服务版本 / 主程序版本）。</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>主程序的项目名（服务发送时为空）。</summary>
        public string? ProjectName { get; set; }

        /// <summary>服务是否独立管理链路配置（为 true 时主程序以服务下发的配置为准，本地系统参数只读）。</summary>
        public bool ManagesLinkConfig { get; set; }

        /// <summary>角色：SECS 服务。</summary>
        public const string RoleService = "Service";

        /// <summary>角色：主程序。</summary>
        public const string RoleMainProgram = "MainProgram";

        /// <summary>按本端常量生成握手内容。</summary>
        public static SecsGemLocalHello Create(string role, string version, string? projectName = null, bool managesLinkConfig = false) => new()
        {
            Role = role,
            ProtocolVersion = SecsGemLocalChannel.ProtocolVersion,
            MinCompatibleVersion = SecsGemLocalChannel.MinCompatibleVersion,
            Version = version,
            ProjectName = projectName,
            ManagesLinkConfig = managesLinkConfig,
        };
    }

    /// <summary>主程序侧看到的本机服务握手状态。</summary>
    public enum SecsGemServiceHandshake
    {
        /// <summary>未连接或等待握手帧。</summary>
        Pending,

        /// <summary>握手成功，协议兼容。</summary>
        Compatible,

        /// <summary>收到握手帧，但协议版本不兼容。</summary>
        Incompatible,

        /// <summary>超时未收到握手帧：服务为旧版本（托盘项目之前的版本）。</summary>
        Legacy,
    }
}
