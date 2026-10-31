using System.IO;
using System.Windows.Media.Imaging;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 网络摄像机 JPEG 字节 → 预览位图的转换。与 <see cref="LineScanPreview"/> 同一角色：
    /// 页面只管拿结果绑定，解码细节集中在这里，避免多页面各写一份慢慢走偏。
    /// </summary>
    internal static class NetworkCameraPreview
    {
        /// <summary>
        /// 把一帧 JPEG 字节转成可直接绑定给 pf:ImageViewer 的位图。
        /// <para>返回 <see cref="BitmapFrame"/> 而非 BitmapSource：ImageViewer 的 ImageSource
        /// 依赖属性就是 BitmapFrame 类型，给普通 BitmapSource 会因类型不匹配被静默丢弃。</para>
        /// </summary>
        /// <param name="jpeg">JPEG 字节（含完整文件头尾）。</param>
        /// <param name="hint">无法预览时的原因说明；可预览时为空字符串。</param>
        /// <returns>预览位图；数据无效时返回 null。</returns>
        public static BitmapFrame? TryBuild(byte[]? jpeg, out string hint)
        {
            hint = string.Empty;

            if (jpeg is null || jpeg.Length == 0)
            {
                hint = "尚未收到图像";
                return null;
            }

            try
            {
                using var ms = new MemoryStream(jpeg);
                var decoder = BitmapDecoder.Create(ms,
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                // OnLoad 是关键：不写的话解码器懒加载，MemoryStream 被 using 释放后再渲染就是一片空白
                var frame = BitmapFrame.Create(decoder.Frames[0]);
                frame.Freeze();   // 冻结后可跨线程安全使用
                return frame;
            }
            catch (Exception ex)
            {
                hint = $"图像解码失败: {ex.Message}";
                return null;
            }
        }
    }
}
