using HalconDotNet;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PF.Modules.Halcon.Controls;

/// <summary>
/// 通用 HALCON 图像显示控件。封装 HWindowControlWPF，提供简洁的显示 API。
/// 所有显示方法必须在 UI 线程调用。
/// </summary>
public partial class HalconImageViewer : UserControl
{
    // 当前底图（叠层清除后重绘用）
    private HObject? _currentImage;
    private bool     _hasImage;

    // 右键拖动平移：记录开始拖动时鼠标对应的图像坐标
    private Point? _panAnchor; // (X=col, Y=row)

    public HalconImageViewer()
    {
        InitializeComponent();
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

    /// <summary>清除叠层，只保留底图</summary>
    public void ClearOverlays()
    {
        var win = GetWindow();
        if (win is null) return;

        try
        {
            HOperatorSet.ClearWindow(win);
            if (_hasImage && _currentImage?.IsInitialized() == true)
            {
                AdaptPart(win, _currentImage);
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

    // ── 鼠标交互：滚轮缩放 / 右键拖动平移 / 双击自适应 ──────────────────────────

    private void HalconWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var win = GetWindow();
        if (win is null) return;
        try
        {
            var pos = e.GetPosition(HalconWindow);
            if (!TryToImageCoords(win, pos, out double imgR, out double imgC)) return;

            HOperatorSet.GetPart(win, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            double factor = e.Delta > 0 ? 0.75 : 1.333; // 缩小视口 = 放大图像

            HOperatorSet.SetPart(win,
                imgR - (imgR - r1.D) * factor,
                imgC - (imgC - c1.D) * factor,
                imgR + (r2.D - imgR) * factor,
                imgC + (c2.D - imgC) * factor);
        }
        catch { }
        e.Handled = true;
    }

    private void HalconWindow_PanStart(object sender, MouseButtonEventArgs e)
    {
        var win = GetWindow();
        if (win is null) return;
        var pos = e.GetPosition(HalconWindow);
        if (TryToImageCoords(win, pos, out double r, out double c))
        {
            _panAnchor = new Point(c, r);
            HalconWindow.CaptureMouse();
            HalconWindow.Cursor = Cursors.SizeAll;
        }
    }

    private void HalconWindow_PanMove(object sender, MouseEventArgs e)
    {
        if (_panAnchor is null) return;
        var win = GetWindow();
        if (win is null) return;
        try
        {
            var pos = e.GetPosition(HalconWindow);
            if (!TryToImageCoords(win, pos, out double r, out double c)) return;

            double dr = _panAnchor.Value.Y - r;
            double dc = _panAnchor.Value.X - c;
            if (Math.Abs(dr) < 0.001 && Math.Abs(dc) < 0.001) return;

            HOperatorSet.GetPart(win, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            HOperatorSet.SetPart(win, r1.D + dr, c1.D + dc, r2.D + dr, c2.D + dc);

            // 平移后重新采样锚点，使拖动跟随鼠标
            if (TryToImageCoords(win, pos, out double nr, out double nc))
                _panAnchor = new Point(nc, nr);
        }
        catch { }
    }

    private void HalconWindow_PanEnd(object sender, MouseButtonEventArgs e)
    {
        _panAnchor = null;
        HalconWindow.ReleaseMouseCapture();
        HalconWindow.Cursor = Cursors.Arrow;
    }

    private void HalconWindow_FitToWindow(object sender, MouseButtonEventArgs e)
    {
        var win = GetWindow();
        if (win is null || _currentImage is null || !_hasImage) return;
        try { AdaptPart(win, _currentImage); }
        catch { }
    }

    // 将 WPF 控件坐标转换为 HALCON 图像坐标
    private bool TryToImageCoords(HWindow win, Point wpfPos, out double row, out double col)
    {
        try
        {
            HOperatorSet.GetPart(win, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            double w = HalconWindow.ActualWidth;
            double h = HalconWindow.ActualHeight;
            if (w <= 0 || h <= 0) { row = col = 0; return false; }
            col = c1.D + (wpfPos.X / w) * (c2.D - c1.D);
            row = r1.D + (wpfPos.Y / h) * (r2.D - r1.D);
            return true;
        }
        catch { row = col = 0; return false; }
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
