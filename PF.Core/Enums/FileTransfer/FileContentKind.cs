namespace PF.Core.Enums.FileTransfer;

/// <summary>
/// 传输字节数组的内容语义，决定接收端如何还原数据。
/// 传输通道本身不关心该字段，只透传，由业务层消费。
/// </summary>
public enum FileContentKind
{
    /// <summary>字节数组本身就是一个完整文件（bmp/tiff/png 等），接收端直接落盘即可</summary>
    RawFile,

    /// <summary>裸像素数据，无文件头，需配合 Width/Height/Channels/PixelFormat 才能重建图像</summary>
    RawPixelBuffer,

    /// <summary>纯字节数组透传，传输通道和业务层均不关心具体语义</summary>
    Opaque
}
