using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums;

namespace PF.Core.Events.FileTransfer;

/// <summary>
/// 传输完成事件参数（发送完成、接收完成均触发，用 <see cref="Direction"/> 区分）。
/// 接收方向的数据消费推荐使用统一入口 <see cref="OpenReadStream"/> / <see cref="SaveToFileAsync"/> /
/// <see cref="GetBytesAsync"/>——通道内部按阈值自动选择内存重组或落盘，消费方无需分辨两种形态；
/// <see cref="Data"/> / <see cref="FilePath"/> 仅作为底层属性保留（如调试面板展示落盘位置）。
/// </summary>
public sealed class FileTransferCompletedEventArgs : EventArgs
{
    // 落盘文件被 SaveToFileAsync 移走后的标记：所有权已转移，再读取应给出明确错误而非 FileNotFoundException
    private int _fileTaken;

    /// <summary>GetBytesAsync 在内存中物化数据的上限；超过的落盘传输应改用流式消费或落到目标位置</summary>
    private const long MaxMaterializeBytes = 512L * 1024 * 1024;

    /// <summary>该次传输的元数据</summary>
    public required FileTransferMetadata Metadata { get; init; }

    /// <summary>传输方向</summary>
    public required TransferDirection Direction { get; init; }

    /// <summary>
    /// 【底层属性，一般用 <see cref="GetBytesAsync"/> 等统一入口】收到的完整数据。
    /// 仅 <see cref="TransferDirection.Received"/> 且数据量不超过
    /// FileTransferOptions.InMemoryReceiveThresholdBytes（内存重组模式）时有值，与 <see cref="FilePath"/> 二选一
    /// </summary>
    public byte[]? Data { get; init; }

    /// <summary>
    /// 【底层属性，一般用 <see cref="SaveToFileAsync"/> 等统一入口】落盘临时文件的完整路径。
    /// 仅 <see cref="TransferDirection.Received"/> 且数据量超过内存重组阈值（落盘模式）时有值，
    /// 与 <see cref="Data"/> 二选一。直接经此路径消费时，文件由消费方使用完毕后负责删除；
    /// 经 <see cref="SaveToFileAsync"/> 消费则随移动自然转移所有权，无需额外清理
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>传输结果汇总</summary>
    public required FileTransferResult Result { get; init; }

    /// <summary>本事件是否携带可消费的数据负载（仅接收方向的完成事件携带）</summary>
    public bool HasPayload => Data != null || FilePath != null;

    /// <summary>
    /// 统一读取入口：不论通道内部是内存重组还是落盘，都返回可读流，消费方一套代码。
    /// 流由调用方负责 Dispose；落盘模式下临时文件在流关闭前不应被删除或移动。
    /// </summary>
    /// <exception cref="InvalidOperationException">事件不携带负载（发送方向），或落盘文件已被 <see cref="SaveToFileAsync"/> 取走</exception>
    public Stream OpenReadStream()
    {
        if (Data != null) return new MemoryStream(Data, writable: false);
        if (FilePath != null)
        {
            ThrowIfFileTaken();
            return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        throw NoPayload();
    }

    /// <summary>
    /// 把接收到的数据以文件形态交付到 <paramref name="targetPath"/>（已存在则覆盖，目标目录不存在时自动创建）：
    /// 落盘模式直接移动临时文件（同卷即改名，零拷贝），此后文件所有权归调用方、本事件的负载不再可读；
    /// 内存模式将数据写盘，可重复调用。
    /// </summary>
    /// <exception cref="InvalidOperationException">事件不携带负载，或落盘文件已被上一次调用取走</exception>
    public async Task SaveToFileAsync(string targetPath, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("targetPath 不能为空", nameof(targetPath));

        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        if (Data != null)
        {
            await File.WriteAllBytesAsync(targetPath, Data, token).ConfigureAwait(false);
            return;
        }

        if (FilePath != null)
        {
            if (Interlocked.Exchange(ref _fileTaken, 1) == 1)
                throw new InvalidOperationException("落盘文件已被上一次 SaveToFileAsync 取走，无法再次交付");
            // 同卷 Move 是目录项改名（零拷贝）；跨卷退化为复制+删除，放线程池避免阻塞调用方
            await Task.Run(() => File.Move(FilePath, targetPath, overwrite: true), token).ConfigureAwait(false);
            return;
        }

        throw NoPayload();
    }

    /// <summary>
    /// 把接收到的数据以字节数组形态取回：内存模式直接返回重组缓冲（调用方不应修改其内容），落盘模式读回内存。
    /// 落盘且长度超过 512MB 时抛出——该量级应改用 <see cref="OpenReadStream"/> 流式消费或
    /// <see cref="SaveToFileAsync"/> 直接落到目标位置，而不是整块搬回内存。
    /// </summary>
    /// <exception cref="InvalidOperationException">事件不携带负载、文件已被取走，或数据量超过内存物化上限</exception>
    public async Task<byte[]> GetBytesAsync(CancellationToken token = default)
    {
        if (Data != null) return Data;

        if (FilePath != null)
        {
            ThrowIfFileTaken();
            var length = new FileInfo(FilePath).Length;
            if (length > MaxMaterializeBytes)
                throw new InvalidOperationException(
                    $"数据量 {length} 字节超过内存物化上限 {MaxMaterializeBytes} 字节，请改用 OpenReadStream() 或 SaveToFileAsync()");
            return await File.ReadAllBytesAsync(FilePath, token).ConfigureAwait(false);
        }

        throw NoPayload();
    }

    private void ThrowIfFileTaken()
    {
        if (Volatile.Read(ref _fileTaken) == 1)
            throw new InvalidOperationException("落盘文件已被 SaveToFileAsync 取走，本事件的负载不再可读");
    }

    private static InvalidOperationException NoPayload() =>
        new("本事件不携带数据负载：仅接收方向（Direction == Received）的完成事件可消费数据");
}
