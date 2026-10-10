using HalconDotNet;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PF.Modules.Halcon.Controls;

/// <summary>鼠标事件转发（已换算为图像坐标系的 Row/Column）</summary>
public sealed class ImageMouseEventArgs : EventArgs
{
    public double      Row    { get; }
    public double      Column { get; }
    public MouseButton? Button { get; }

    public ImageMouseEventArgs(double row, double column, MouseButton? button)
    {
        Row = row; Column = column; Button = button;
    }
}

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
        HalconWindow.HMouseDown  += (_, e) => ImageMouseDown?.Invoke(this, new ImageMouseEventArgs(e.Row, e.Column, e.Button));
        HalconWindow.HMouseMove  += (_, e) => ImageMouseMove?.Invoke(this, new ImageMouseEventArgs(e.Row, e.Column, e.Button));
        HalconWindow.HMouseUp    += (_, e) => ImageMouseUp?.Invoke(this, new ImageMouseEventArgs(e.Row, e.Column, e.Button));
        Unloaded += (_, _) => { _currentImage?.Dispose(); _currentImage = null; _hasImage = false; };
    }

    /// <summary>HALCON 窗口（重新）初始化完成时触发，供宿主（如 ROI 编辑器）重挂载 DrawingObject</summary>
    public event EventHandler? WindowInitialized;

    /// <summary>鼠标在 HALCON 窗口内按下/移动/抬起，坐标已换算为图像坐标系</summary>
    public event EventHandler<ImageMouseEventArgs>? ImageMouseDown;
    public event EventHandler<ImageMouseEventArgs>? ImageMouseMove;
    public event EventHandler<ImageMouseEventArgs>? ImageMouseUp;

    /// <summary>内置左键拖动平移的开关。交互式画图（拖拽新建 ROI）期间需要临时关闭，避免和平移抢左键</summary>
    public bool PanEnabled
    {
        get => HalconWindow.HMoveContent;
        set => HalconWindow.HMoveContent = value;
    }

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

    /// <summary>
    /// 自适应：按窗口宽高比算显示范围，整张图完整显示且居中、不拉伸。此前直接 SetPart 成整张图，
    /// 图与窗口宽高比不同时（如 11000×16384 竖图放进横向窗口）第一次显示是拉伸的，双击自适应后才正常。
    /// </summary>
    private static void AdaptPart(HWindow win, HObject image)
    {
        try
        {
            HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
            double iw = w.I, ih = h.I;
            HOperatorSet.GetWindowExtents(win, out _, out _, out HTuple ww, out HTuple wh);
            if (ww.I <= 1 || wh.I <= 1)
            {
                HOperatorSet.SetPart(win, 0, 0, ih - 1, iw - 1);
                return;
            }
            double scale = Math.Max(iw / ww.I, ih / wh.I);   // 每个窗口像素对应多少图像像素，取较大者保证整图放得下
            double pw = ww.I * scale, ph = wh.I * scale;
            double r1 = (ih - ph) / 2, c1 = (iw - pw) / 2;
            HOperatorSet.SetPart(win, r1, c1, r1 + ph - 1, c1 + pw - 1);
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
