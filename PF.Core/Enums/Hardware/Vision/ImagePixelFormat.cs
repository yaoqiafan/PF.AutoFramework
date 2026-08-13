namespace PF.Core.Enums.Hardware.Vision
{
    /// <summary>
    /// 图像像素/编码格式。
    /// <para>与相机 SDK 的 PixelType 解耦：设备层负责把厂商枚举翻译成本枚举，
    /// 上层（调试面板渲染、视觉算法、存盘）只认本枚举。</para>
    /// </summary>
    public enum ImagePixelFormat
    {
        /// <summary>未知/未支持的格式（原始数据仍可透传，但上层无法解析）</summary>
        Unknown = 0,

        /// <summary>8 位灰度，每像素 1 字节</summary>
        Mono8,

        /// <summary>10 位灰度，每像素 2 字节（低位对齐）</summary>
        Mono10,

        /// <summary>12 位灰度，每像素 2 字节（低位对齐）</summary>
        Mono12,

        /// <summary>16 位灰度，每像素 2 字节</summary>
        Mono16,

        /// <summary>Bayer RG 8 位</summary>
        BayerRG8,

        /// <summary>Bayer GB 8 位</summary>
        BayerGB8,

        /// <summary>Bayer GR 8 位</summary>
        BayerGR8,

        /// <summary>Bayer BG 8 位</summary>
        BayerBG8,

        /// <summary>RGB 8 位打包（每像素 3 字节）</summary>
        Rgb8Packed,

        /// <summary>JPEG 编码数据（非裸像素，需解码后才能按宽高解析）</summary>
        Jpeg,
    }
}
