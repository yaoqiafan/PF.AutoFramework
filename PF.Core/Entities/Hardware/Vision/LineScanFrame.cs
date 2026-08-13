using PF.Core.Enums.Hardware.Vision;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 线阵相机采集到的一帧完整图像（裸数据 + 元信息）。
    ///
    /// <para>设计约定：设备层只负责把图像原样交出来，不做任何算法处理、不依赖任何视觉库。
    /// 后续要转 HALCON HObject、转 Bitmap、存档还是丢弃，全部由调用方自行决定，
    /// 从而使 PF.Infrastructure 不必引用 halcondotnet 等第三方视觉库（保持七层单向依赖）。</para>
    ///
    /// <para>⚠️ 线扫图通常很大（16K 宽 × 上万行可达数百 MB），
    /// <see cref="Data"/> 是深拷贝后的托管数组，调用方应尽快消费并释放引用，避免长期持有导致 LOH 膨胀。</para>
    /// </summary>
    public sealed class LineScanFrame
    {
        /// <summary>图像原始字节数据（已从 SDK 缓冲区深拷贝，可安全长期持有）。</summary>
        public byte[] Data { get; init; } = Array.Empty<byte>();

        /// <summary>图像宽度（像素）。线扫相机即传感器像元数。</summary>
        public int Width { get; init; }

        /// <summary>图像高度（像素）。线扫相机即本帧累计扫描的行数。</summary>
        public int Height { get; init; }

        /// <summary>像素格式。</summary>
        public ImagePixelFormat PixelFormat { get; init; } = ImagePixelFormat.Unknown;

        /// <summary>SDK 上报的帧序号，可用于检测丢帧。</summary>
        public uint FrameNumber { get; init; }

        /// <summary>本帧在上位机侧被接收到的时间。</summary>
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <summary>图像数据字节数。</summary>
        public long SizeBytes => Data.LongLength;

        /// <summary>是否为可直接按宽高解析的裸像素数据（JPEG 等编码格式为 false）。</summary>
        public bool IsRawPixelData => PixelFormat != ImagePixelFormat.Jpeg
                                   && PixelFormat != ImagePixelFormat.Unknown;
    }
}
