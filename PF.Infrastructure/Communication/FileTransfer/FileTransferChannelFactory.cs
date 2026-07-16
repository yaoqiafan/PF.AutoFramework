using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Interfaces.Communication.FileTransfer;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Communication.FileTransfer;

/// <summary><see cref="IFileTransferChannelFactory"/> 实现</summary>
public sealed class FileTransferChannelFactory : IFileTransferChannelFactory
{
    private readonly ILogService? _logger;

    /// <summary>初始化</summary>
    public FileTransferChannelFactory(ILogService? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IFileTransferChannel Create(FileTransferOptions options, string channelName = "Default")
        => new FileTransferChannel(options, channelName, _logger);
}
