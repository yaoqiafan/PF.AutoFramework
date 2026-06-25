using HalconDotNet;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Controls;

/// <summary>
/// 通用 HALCON 图像显示控件。封装 HSmartWindowControlWPF。
/// 平移（左键拖动）/ 缩放（滚轮）/ 自适应（双击）由控件内置，
/// 无需手写鼠标交互。所有显示方法必须在 UI 线程调用。
/// </summary>
public partial class HalconImageViewer : UserControl
{
    // 当前底图（叠层清除后重绘用）
    private HObject? _currentImage;
    private bool     _hasImage;

    public HalconImageViewer()
    {
        InitializeComponent();
        // 窗口（重新）创建时重绘底图；TabControl 切换 tab / 导航离开再返回会释放 HALCON 窗口
        HalconWindow.HInitWindow += OnHInitWindow;
    }

    /// <summary>HALCON 窗口（重新）初始化完成时触发，供宿主（如 ROI 编辑器）重挂载 DrawingObject</summary>
    public event EventHandler? WindowInitialized;

    private void OnHInitWindow(object sender, EventArgs e)
    {
        // 窗口重建后内容丢失，重绘底图（保持自适应视口）
        if (_hasImage && _currentImage?.IsInitialized() == true)
        {
            var win = GetWindow();
            if (win is not null)
            {
                try
                {
                    HOperatorSet.ClearWindow(win);
                    AdaptPart(win, _currentImage);
                    HOperatorSet.SetDraw(win, "fill");
                    HOperatorSet.DispObj(_currentImage, win);
                }
                catch { }
            }
        }
        WindowInitialized?.Invoke(this, EventArgs.Empty);
    }

    // ── 公共 API ──────────────────────────────────────────────────────────────

    /// <summary>显示底图，自动适应窗口大小</summary>
    public void DisplayImage(HObject image)
    {
        var win = GetWindow();
        if (win is null || !image.IsInitialized()) return;

        _currentImage?.Dispose();
        _currentImage = image;
        _hasImage     = true;

        PlaceholderText.Visibility = Visibility.Collapsed;

        try
        {
            HOperatorSet.ClearWindow(win);
            AdaptPart(win, image);
            HOperatorSet.SetDraw(win, "fill");
            HOperatorSet.DispObj(image, win);
        }
        catch { }
    }

    /// <summary>在底图上叠加图标量（Region / XLD / image），不清除底图</summary>
    public void DisplayOverlay(HObject obj, string color = "lime green", double lineWidth = 2)
    {
        var win = GetWindow();
        if (win is null || !obj.IsInitialized()) return;

        try
        {
            HOperatorSet.GetObjClass(obj, out HTuple cls);
            bool isImage = cls.S == "image";

            if (isImage)
            {
                HOperatorSet.SetDraw(win, "fill");
            }
            else
            {
                HOperatorSet.SetColor(win, color);
                HOperatorSet.SetDraw(win, "margin");
                HOperatorSet.SetLineWidth(win, lineWidth);
            }
            HOperatorSet.DispObj(obj, win);
        }
        catch { }
    }

    /// <summary>清除叠层，只保留底图（保持当前缩放/平移视口）</summary>
    public void ClearOverlays()
    {
        var win = GetWindow();
        if (win is null) return;

        try
        {
            HOperatorSet.ClearWindow(win);
            if (_hasImage && _currentImage?.IsInitialized() == true)
            {
                HOperatorSet.SetDraw(win, "fill");
                HOperatorSet.DispObj(_currentImage, win);
            }
        }
        catch { }
    }

    /// <summary>渲染一组图标量（自动先渲染 image 再渲染 region/XLD）</summary>
    public void DisplayIconics(IReadOnlyDictionary<string, object?> iconics,
                               bool clearFirst = true)
    {
        var win = GetWindow();
        if (win is null || iconics.Count == 0) return;

        PlaceholderText.Visibility = Visibility.Collapsed;

        if (clearFirst)
        {
            try { HOperatorSet.ClearWindow(win); } catch { return; }
        }

        // 先找图像尺寸，自动适应
        foreach (var (_, v) in iconics)
        {
            if (v is not HObject img || !img.IsInitialized()) continue;
            try
            {
                HOperatorSet.GetObjClass(img, out HTuple cls);
                if (cls.S != "image") continue;
                AdaptPart(win, img);
                _currentImage?.Dispose();
                _currentImage = img;
                _hasImage     = true;
                break;
            }
            catch { }
        }

        // 渲染 image 先，region/XLD 后
        RenderPass(win, iconics, imageFirst: true);
        RenderPass(win, iconics, imageFirst: false);
    }

    /// <summary>完全清空窗口</summary>
    public void Clear()
    {
        var win = GetWindow();
        if (win is null) return;
        try { HOperatorSet.ClearWindow(win); } catch { }
        _hasImage = false;
        PlaceholderText.Visibility = Visibility.Visible;
    }

    // ── 内部 ──────────────────────────────────────────────────────────────────

    private HWindow? GetWindow()
    {
        var win = HalconWindow.HalconWindow;
        if (win is null) return null;
        try { HOperatorSet.GetWindowExtents(win, out _, out _, out _, out _); return win; }
        catch { return null; }
    }

    private static void AdaptPart(HWindow win, HObject image)
    {
        try
        {
            HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
            HOperatorSet.SetPart(win, 0, 0, h.I - 1, w.I - 1);
        }
        catch { }
    }

    private static void RenderPass(HWindow win,
                                   IReadOnlyDictionary<string, object?> iconics,
                                   bool imageFirst)
    {
        foreach (var (key, value) in iconics)
        {
            if (value is not HObject hobj || !hobj.IsInitialized()) continue;

            int cnt = 0;
            try { HOperatorSet.CountObj(hobj, out HTuple c); cnt = c.I; } catch { continue; }
            if (cnt <= 0) continue;

            bool isImage;
            try { HOperatorSet.GetObjClass(hobj, out HTuple cls); isImage = cls.S == "image"; }
            catch { isImage = false; }

            if (imageFirst != isImage) continue;

            try
            {
                if (isImage)
                {
                    HOperatorSet.SetDraw(win, "fill");
                }
                else
                {
                    var color = key.Contains("Defect", StringComparison.OrdinalIgnoreCase) ? "red" : "lime green";
                    HOperatorSet.SetColor(win, color);
                    HOperatorSet.SetDraw(win, "margin");
                    HOperatorSet.SetLineWidth(win, 2);
                }
                HOperatorSet.DispObj(hobj, win);
            }
            catch { }
        }
    }
}
