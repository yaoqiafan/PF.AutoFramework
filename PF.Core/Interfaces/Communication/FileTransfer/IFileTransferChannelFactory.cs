using PF.Core.Entities.Communication.FileTransfer;

namespace PF.Core.Interfaces.Communication.FileTransfer;

/// <summary>创建 <see cref="IFileTransferChannel"/> 实例的工厂</summary>
public interface IFileTransferChannelFactory
{
    /// <summary>按配置创建一个传输通道实例，创建后需调用 StartAsync 才会开始监听/连接</summary>
    IFileTransferChannel Create(FileTransferOptions options, string channelName = "Default");
}
