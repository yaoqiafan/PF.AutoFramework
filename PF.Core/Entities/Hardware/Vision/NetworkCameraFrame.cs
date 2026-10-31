namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// 网络摄像机抓取的一帧图片。
    ///
    /// <para>设计约定与 <see cref="LineScanFrame"/> 一致：设备层只负责把图像原样交出来，
    /// 不做算法处理、不依赖任何视觉库。转位图 / 存盘 / 送 HALCON 全由调用方决定。</para>
    /// </summary>
    public sealed class NetworkCameraFrame
    {
        /// <summary>图片字节（JPEG 编码，含完整文件头尾，可直接写盘或交位图解码）。</summary>
        public byte[] Data { get; init; } = Array.Empty<byte>();

        /// <summary>实际采集使用的通道号。</summary>
        public int Channel { get; init; }

        /// <summary>所在设备的序列号（未获取到时为空字符串）。</summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>采集时间。</summary>
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <summary>图片字节数。</summary>
        public long SizeBytes => Data.LongLength;
    }
}
