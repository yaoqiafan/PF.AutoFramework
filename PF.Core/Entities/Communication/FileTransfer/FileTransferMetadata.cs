using PF.Core.Enums.FileTransfer;

namespace PF.Core.Entities.Communication.FileTransfer;

/// <summary>
/// 随一次传输携带的业务元数据。传输通道本身不解析 <see cref="ContentKind"/> 之后的语义，
/// 仅负责透传，由接收方业务层按需还原成文件或内存图像对象。
/// </summary>
public sealed class FileTransferMetadata
{
    /// <summary>传输唯一标识，未指定时自动生成</summary>
    public Guid TransferId { get; init; } = Guid.NewGuid();

    /// <summary>业务标签，接收端可据此分发到不同处理逻辑（如相机编号、文件类别）</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; init; } = DateTime.Now;

    /// <summary>内容语义，决定接收端如何还原字节数组</summary>
    public FileContentKind ContentKind { get; init; } = FileContentKind.RawFile;

    /// <summary><see cref="FileContentKind.RawFile"/> 时使用：落盘文件的扩展名（含点，如 ".bmp"）</summary>
    public string? FileExtension { get; init; }

    /// <summary><see cref="FileContentKind.RawPixelBuffer"/> 时使用：图像宽度（像素）</summary>
    public int? Width { get; init; }

    /// <summary><see cref="FileContentKind.RawPixelBuffer"/> 时使用：图像高度（像素）</summary>
    public int? Height { get; init; }

    /// <summary><see cref="FileContentKind.RawPixelBuffer"/> 时使用：通道数</summary>
    public int? Channels { get; init; }

    /// <summary><see cref="FileContentKind.RawPixelBuffer"/> 时使用：像素格式（如 "Mono8"/"RGB24"/"Mono16"）</summary>
    public string? PixelFormat { get; init; }
}
