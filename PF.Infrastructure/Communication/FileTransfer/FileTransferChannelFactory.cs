using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Interfaces.Communication.FileTransfer;

namespace PF.Infrastructure.Communication.FileTransfer;

/// <summary><see cref="IFileTransferChannelFactory"/> 实现</summary>
public sealed class FileTransferChannelFactory : IFileTransferChannelFactory
{
    /// <inheritdoc/>
    public IFileTransferChannel Create(FileTransferOptions options, string channelName = "Default")
        => new FileTransferChannel(options, channelName);
}
