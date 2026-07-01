using PF.Core.Enums;

namespace PF.Core.Entities.Communication;

/// <summary>
/// 通讯实例配置模型（持久化到数据库，结构对齐 <see cref="PF.Core.Entities.Hardware.HardwareConfig"/>）。
///
/// 设计原则：
///   · 每一个通讯实例对应一条 CommunicationConfig 记录
///   · ImplementationClassName 用于 ICommunicationManagerService.RegisterFactory 查找，与实际类名解耦
///   · ConnectionParameters 键值对由具体工厂函数解析；结构复杂的字段（如 FileTransfer 的 Links 列表）
///     序列化成 JSON 字符串存在某个 key 下
/// </summary>
public class CommunicationConfig
{
    /// <summary>实例唯一标识，全局不重复</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>显示名称，用于 UI 展示</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>分类</summary>
    public CommunicationCategory Category { get; set; }

    /// <summary>服务端/客户端角色</summary>
    public CommunicationRole Role { get; set; }

    /// <summary>
    /// 具体实现类名，与 ICommunicationManagerService.RegisterFactory 的 key 对应。
    /// 例：RegisterFactory("TcpServer", ...)，此处填 "TcpServer"。
    /// </summary>
    public string ImplementationClassName { get; set; } = string.Empty;

    /// <summary>是否在程序启动时自动实例化此通讯实例</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否由 ICommunicationManagerService 自动调用 StartAsync（自动连接/监听）。
    /// 独立运行的通讯实例（调试测试用、FileTransfer 服务器等）应为 true；
    /// 被硬件设备包裹、连接生命周期交由 BaseDevice/HardwareManagerService 现有重试逻辑接管的实例
    /// （如 HKBarcodeScan/KeyenceIntelligentCamera 底层的 TCP 通道）应设为 false，
    /// 否则通讯管理器抢先连接后，硬件层自己的 InternalConnectAsync 再次调用 ConnectAsync 会因为
    /// "已处于 Connected/Connecting 状态"而抛异常。
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// 连接参数字典，由对应工厂函数解析。
    /// TCP Server 示例：{ "IP": "0.0.0.0", "Port": "9000" }
    /// FileTransfer 示例：{ "Role": "Server", "LinksJson": "[{...},{...}]" }
    /// </summary>
    public Dictionary<string, string> ConnectionParameters { get; set; } = new();

    /// <summary>备注说明</summary>
    public string Remarks { get; set; } = string.Empty;
}
