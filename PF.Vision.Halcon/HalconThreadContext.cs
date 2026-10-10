using HalconDotNet;

namespace PF.Vision.Halcon;

/// <summary>
/// HALCON 区域裁剪的线程坑：<c>clip_region=true</c>（默认）时，新生成的区域会被裁剪到"当前图像尺寸"以内，
/// 而这个尺寸<b>按线程缓存</b>——一个线程第一次用 HALCON 时记下当时的全局 <c>width/height</c>（未读过图时是 128×128），
/// 之后别的线程读了大图、全局尺寸变了，这个线程也不会跟着变，只有它自己读图或设置 <c>width/height</c> 才更新。
///
/// <para>实测后果（11000×16384 线扫图）：图在后台线程读，UI 线程上画 ROI 时 <c>gen_rectangle1(9590,…)</c>
/// 面积为 0——ROI 编辑器看不到拖拽预览、算外接矩形报"区域为空"；视觉引擎工作线程初始化时缓存了 128×128，
/// 进程启动后第一次执行过程，<c>reduce_domain</c> 拿到空区域、匹配必失败，执行过一次后才正常。</para>
///
/// <para>两种处理：</para>
/// <list type="bullet">
/// <item><see cref="EnsureRegionClipOff"/>：工具类代码（ROI 区域生成、建模板、资产包会话）在当前线程关掉区域裁剪，
/// ROI 画多大就是多大，与线程缓存无关。</item>
/// <item><see cref="SyncClipSize"/>：视觉引擎执行过程前，把工作线程的裁剪尺寸设成输入图像的尺寸，
/// 保留裁剪语义，与 HDevelop 里"读了图再处理"的行为一致。</item>
/// </list>
/// </summary>
public static class HalconThreadContext
{
    [ThreadStatic] private static bool _clipOff;

    /// <summary>在当前线程关闭区域裁剪（每个线程只设一次）。</summary>
    public static void EnsureRegionClipOff()
    {
        if (_clipOff) return;
        HOperatorSet.SetSystem("clip_region", "false");
        _clipOff = true;
    }

    /// <summary>
    /// 把当前线程的区域裁剪尺寸设成 <paramref name="images"/> 中最大的宽、高（非图像对象忽略）。
    /// 没有图像时不改。用在视觉引擎执行过程之前。
    /// </summary>
    public static void SyncClipSize(IEnumerable<HObject> images)
    {
        int w = 0, h = 0;
        foreach (var obj in images)
        {
            try
            {
                if (!obj.IsInitialized()) continue;
                HOperatorSet.GetObjClass(obj, out HTuple cls);
                if (cls.Length == 0 || cls[0].S != "image") continue;
                HOperatorSet.GetImageSize(obj, out HTuple iw, out HTuple ih);
                for (int i = 0; i < iw.Length; i++)
                {
                    w = Math.Max(w, iw[i].I);
                    h = Math.Max(h, ih[i].I);
                }
            }
            catch (HalconException) { /* 不是图像或已失效，跳过 */ }
        }
        if (w <= 0 || h <= 0) return;
        HOperatorSet.SetSystem("width", w);
        HOperatorSet.SetSystem("height", h);
    }
}
