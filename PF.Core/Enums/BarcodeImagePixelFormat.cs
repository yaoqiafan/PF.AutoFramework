namespace PF.Core.Enums
{
    /// <summary>
    /// 条码扫描枪采集图像的像素/编码格式（与具体厂商 SDK 解耦的通用枚举）。
    /// </summary>
    public enum BarcodeImagePixelFormat
    {
        /// <summary> 未知/不支持图像采集 </summary>
        Unknown,
        /// <summary> Mono8 灰度原始数据 </summary>
        Mono8,
        /// <summary> 相机端压缩输出的 Jpeg 数据 </summary>
        Jpeg,
    }
}
