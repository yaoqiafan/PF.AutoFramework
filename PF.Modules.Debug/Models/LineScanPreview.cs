using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using System.Windows.Media.Imaging;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 线扫帧 → 预览位图的转换。相机调试页与模组调试页共用，避免两处各写一份而慢慢走偏。
    ///
    /// <para><b>为什么一定要降采样</b>：线扫单帧可达 16K×上万行、数百 MB，
    /// 原样交给 WPF 会再复制一份显存/内存，几帧就把机器吃满。
    /// 即便界面用的是支持缩放的 ImageViewer 也一样——控件能缩放，不代表内存放得下。</para>
    /// </summary>
    internal static class LineScanPreview
    {
        /// <summary>预览图的最大边长（像素）。超出则整数倍降采样。</summary>
        public const int MaxEdge = 1200;

        /// <summary>
        /// 把一帧转成可直接绑定给 pf:ImageViewer 的位图。
        /// <para>返回 <see cref="BitmapFrame"/> 而非 BitmapSource：ImageViewer 的 ImageSource
        /// 依赖属性就是 BitmapFrame 类型，给普通 BitmapSource 会因类型不匹配被静默丢弃。</para>
        /// </summary>
        /// <param name="frame">线扫帧。</param>
        /// <param name="hint">无法预览时的原因说明；可预览时为空字符串。</param>
        /// <returns>预览位图；不支持预览或帧无效时返回 null。</returns>
        public static BitmapFrame? TryBuild(LineScanFrame? frame, out string hint)
        {
            hint = string.Empty;

            if (frame == null || frame.Width <= 0 || frame.Height <= 0)
            {
                hint = "尚未收到图像";
                return null;
            }

            if (frame.PixelFormat != ImagePixelFormat.Mono8)
            {
                // 只对 Mono8 做预览：10/12bit 打包格式与 Bayer 需要解包/插值，
                // 在调试面板里自造一套转换等于埋一个"看着对、其实错"的隐患，
                // 这类格式请用存盘功能交给 SDK 转换
                hint = $"当前像素格式 {frame.PixelFormat} 暂不支持面板预览，请存盘后查看。";
                return null;
            }

            int step = 1;
            while (frame.Width / step > MaxEdge || frame.Height / step > MaxEdge) step++;

            int w = frame.Width / step;
            int h = frame.Height / step;
            if (w <= 0 || h <= 0)
            {
                hint = "图像尺寸过小，无法预览";
                return null;
            }

            var pixels = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * step * frame.Width;
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    int srcIndex = srcRow + x * step;
                    pixels[dstRow + x] = srcIndex < frame.Data.Length ? frame.Data[srcIndex] : (byte)0;
                }
            }

            var bitmap = BitmapSource.Create(w, h, 96, 96,
                System.Windows.Media.PixelFormats.Gray8, null, pixels, w);
            bitmap.Freeze();   // 冻结后可跨线程安全使用，也省去 WPF 的变更通知开销

            var previewFrame = BitmapFrame.Create(bitmap);
            previewFrame.Freeze();
            return previewFrame;
        }
    }
}
