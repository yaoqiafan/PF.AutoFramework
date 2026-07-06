using PF.Core.Enums;

namespace PF.Core.Interfaces.Communication;

/// <summary>
/// 所有通讯实现（TCP Server/Client、FileTransfer 通道，以后的 Modbus 等）都要实现的公共契约。
/// 通讯调试面板通过 <see cref="ICommunicationManagerService.ActiveCommunications"/> 拿到所有实例，
/// 按 <see cref="Category"/>/<see cref="Role"/> 分组建树；具体导航到哪个调试视图由实例运行时类型上的
/// <see cref="PF.Core.Attributes.CommunicationUIAttribute"/> 决定，本接口不掺和 UI 路由。
/// </summary>
public interface ICommunication
{
    /// <summary>唯一标识，对应持久化配置里的 InstanceId</summary>
    string InstanceId { get; }

    /// <summary>分类，驱动树的一级分组</summary>
    CommunicationCategory Category { get; }

    /// <summary>服务端/客户端角色，驱动树的二级分组（None 时不分组）</summary>
    CommunicationRole Role { get; }

    /// <summary>树叶子节点显示文本</summary>
    string DisplayName { get; }

    /// <summary>启动（监听/连接），供 <see cref="ICommunicationManagerService"/> 统一调度</summary>
    Task<bool> StartAsync(CancellationToken token = default);

    /// <summary>停止</summary>
    Task StopAsync();
}
