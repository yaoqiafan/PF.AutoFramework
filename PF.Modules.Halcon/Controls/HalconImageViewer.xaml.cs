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
        // WPF 路由事件无法穿透 HwndHost，改用 HALCON 原生事件
        HalconWindow.HMouseWheel += OnHMouseWheel;
        HalconWindow.HMouseDown  += OnHMouseDown;
        HalconWindow.HMouseMove  += OnHMouseMove;
        HalconWindow.HMouseUp    += OnHMouseUp;
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
    // 使用 HALCON 原生事件，e.Row/e.Column 已由 HALCON 转换为图像坐标

    private void OnHMouseWheel(object sender, HMouseEventArgsWPF e)
    {
        var win = GetWindow();
        if (win is null) return;
        try
        {
            HOperatorSet.GetPart(win, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            double factor = e.Delta > 0 ? 0.75 : 1.333; // 缩小视口 = 放大图像
            double imgR = e.Row, imgC = e.Column;
            HOperatorSet.SetPart(win,
                imgR - (imgR - r1.D) * factor,
                imgC - (imgC - c1.D) * factor,
                imgR + (r2.D - imgR) * factor,
                imgC + (c2.D - imgC) * factor);
        }
        catch { }
    }

    private void OnHMouseDown(object sender, HMouseEventArgsWPF e)
    {
        if (e.Button != System.Windows.Input.MouseButton.Right) return;
        _panAnchor = new Point(e.Column, e.Row);
        HalconWindow.Cursor = Cursors.SizeAll;
    }

    private void OnHMouseMove(object sender, HMouseEventArgsWPF e)
    {
        if (_panAnchor is null) return;
        var win = GetWindow();
        if (win is null) return;
        try
        {
            // _panAnchor 是锚点的图像坐标，e.Row/Column 是当前鼠标的图像坐标
            // 平移量 = 锚点 − 当前位置（锚点需跟随鼠标）
            double dr = _panAnchor.Value.Y - e.Row;
            double dc = _panAnchor.Value.X - e.Column;
            if (Math.Abs(dr) < 0.001 && Math.Abs(dc) < 0.001) return;

            HOperatorSet.GetPart(win, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            HOperatorSet.SetPart(win, r1.D + dr, c1.D + dc, r2.D + dr, c2.D + dc);
            // 平移后锚点随鼠标，不需要更新 _panAnchor（锚点始终在鼠标下方）
        }
        catch { }
    }

    private void OnHMouseUp(object sender, HMouseEventArgsWPF e)
    {
        if (e.Button != System.Windows.Input.MouseButton.Right) return;
        _panAnchor = null;
        HalconWindow.Cursor = Cursors.Arrow;
    }

    // MouseDoubleClick 是 WPF 特殊事件，HWindowControlWPF 支持
    private void HalconWindow_FitToWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var win = GetWindow();
        if (win is null || _currentImage is null || !_hasImage) return;
        try { AdaptPart(win, _currentImage); }
        catch { }
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
