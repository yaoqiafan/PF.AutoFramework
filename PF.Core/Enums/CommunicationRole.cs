namespace PF.Core.Enums;

/// <summary>
/// 通讯实例的服务端/客户端角色。驱动调试面板树的二级分组：
/// None 时不建分组层，实例直接扁平挂在分类节点下（如串口点对点，没有服务端/客户端语义）。
/// </summary>
public enum CommunicationRole
{
    /// <summary>无服务端/客户端语义（如串口点对点）</summary>
    None,

    /// <summary>服务端</summary>
    Server,

    /// <summary>客户端</summary>
    Client
}
