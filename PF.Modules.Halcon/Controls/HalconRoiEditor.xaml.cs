using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Vision.Halcon.Internal;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PF.Modules.Halcon.Controls;

// ── ROI 行 ViewModel ──────────────────────────────────────────────────────────

public sealed class RoiRowVm : BindableBase
{
    public VisionRoiConfig Config { get; }

    internal HTuple? DrawId { get; set; }

    public string Name
    {
        get => Config.Name;
        set { Config.Name = value; RaisePropertyChanged(); }
    }

    public string TypeLabel => Config.Type switch
    {
        RoiType.Rect          => "矩形",
        RoiType.Rect2         => "旋转矩",
        RoiType.Circle        => "圆",
        RoiType.Ellipse       => "椭圆",
        RoiType.EllipseSector => "扇形",
        RoiType.Polygon       => "多边形",
        _                     => Config.Type.ToString(),
    };

    private string _opLabel;
    public string OpLabel
    {
        get => _opLabel;
        set
        {
            if (SetProperty(ref _opLabel, value))
                Config.Op = value == "排除" ? RoiOp.Exclude : RoiOp.Include;
        }
    }

    public RoiRowVm(VisionRoiConfig config)
    {
        Config   = config;
        _opLabel = config.Op == RoiOp.Exclude ? "排除" : "包含";
    }
}

// ── HalconRoiEditor code-behind ───────────────────────────────────────────────

public partial class HalconRoiEditor : UserControl
{
    public static readonly IReadOnlyList<string> OpLabels = ["包含", "排除"];

    private readonly ObservableCollection<RoiRowVm> _rows = new();
    private RoiOp   _currentOp = RoiOp.Include;
    private int     _roiCounter;
    private bool    _isPreviewing;   // 预览检测范围时隐藏工具框，仅显示最终 Region

    // 拖拽画图工具状态：_armedType 非空 = 已武装某个工具，等待用户在图像上操作
    private RoiType? _armedType;
    private Button?  _armedButton;
    private bool          _panWasEnabled = true;
    private bool          _isDragging;
    private double        _dragStartRow, _dragStartCol;
    private readonly List<(double Row, double Col)> _polygonPoints = new();

    public HalconRoiEditor()
    {
        InitializeComponent();
        RoiGrid.ItemsSource = _rows;
        // HALCON 窗口（重新）创建后重挂载 DrawingObject（切 tab/导航返回时窗口会重建）
        ImageViewer.WindowInitialized += OnWindowInitialized;
        ImageViewer.ImageMouseDown    += OnImageMouseDown;
        ImageViewer.ImageMouseMove    += OnImageMouseMove;
        ImageViewer.ImageMouseUp      += OnImageMouseUp;
        PreviewKeyDown += OnRoiEditorPreviewKeyDown;
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── 公共 API ──────────────────────────────────────────────────────────────

    public void LoadImage(HObject image) => ImageViewer.DisplayImage(image);

    public IReadOnlyList<VisionRoiConfig> GetCurrentRois()
    {
        SyncAllFromDrawObjects();
        return _rows.Select(r => r.Config).ToList();
    }

    public void LoadRois(IEnumerable<VisionRoiConfig> rois)
    {
        DisarmTool();
        ClearAllDrawObjects();
        _rows.Clear();
        foreach (var cfg in rois)
        {
            var row = new RoiRowVm(cfg);
            _rows.Add(row);
            AttachDrawObject(row);
        }
    }

    public void ClearRois()
    {
        DisarmTool();
        _isPreviewing = false;
        PreviewBtn.Visibility     = Visibility.Visible;
        ExitPreviewBtn.Visibility = Visibility.Collapsed;
        ClearAllDrawObjects();
        _rows.Clear();
        ImageViewer.ClearOverlays();
    }

    // ── 生命周期 ──────────────────────────────────────────────────────────────

    private void OnWindowInitialized(object? sender, EventArgs e)
    {
        // 窗口重建后：预览态重绘最终 Region（工具框保持隐藏），否则重挂工具框
        if (_isPreviewing) RedrawPreviewRegion();
        else               EnsureAllAttached();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DisarmTool();
        // 窗口即将释放：先把 DrawingObject 当前坐标同步回 Config，再销毁 drawId
        // 这样窗口重建后能按 Config.RoiParams 重新创建 DrawingObject，位置不丢
        SyncAllFromDrawObjects();
        ClearAllDrawObjects();
    }

    /// <summary>挂载所有行的 DrawingObject（drawId 为空则新建，否则重新 attach 已有）</summary>
    private void EnsureAllAttached()
    {
        var win = GetHWindow();
        foreach (var row in _rows)
        {
            if (row.DrawId is null)
                AttachDrawObject(row);
            else if (win is not null)
            {
                try { HOperatorSet.AttachDrawingObjectToWindow(win, row.DrawId); } catch { }
            }
        }
    }

    /// <summary>从窗口卸载所有 DrawingObject（保留 drawId，退出预览时重新 attach）</summary>
    private void DetachAllDrawObjects()
    {
        var win = GetHWindow();
        if (win is null) return;
        foreach (var row in _rows)
            if (row.DrawId is not null)
                try { HOperatorSet.DetachDrawingObjectFromWindow(win, row.DrawId); } catch { }
    }

    private HWindow? GetHWindow()
    {
        var win = ImageViewer.HalconWindow.HalconWindow;
        if (win is null) return null;
        try { HOperatorSet.GetWindowExtents(win, out _, out _, out _, out _); return win; }
        catch { return null; }
    }

    // ── 工具栏事件 ────────────────────────────────────────────────────────────

    private void IncludeBtn_Checked(object sender, RoutedEventArgs e)
    {
        _currentOp = RoiOp.Include;
        if (ExcludeBtn is not null) ExcludeBtn.IsChecked = false;
    }

    private void ExcludeBtn_Checked(object sender, RoutedEventArgs e)
    {
        _currentOp = RoiOp.Exclude;
        if (IncludeBtn is not null) IncludeBtn.IsChecked = false;
    }

    private void AddRect_Click(object sender, RoutedEventArgs e)    => ArmTool(RoiType.Rect,          (Button)sender);
    private void AddRect2_Click(object sender, RoutedEventArgs e)   => ArmTool(RoiType.Rect2,         (Button)sender);
    private void AddCircle_Click(object sender, RoutedEventArgs e)  => ArmTool(RoiType.Circle,        (Button)sender);
    private void AddEllipse_Click(object sender, RoutedEventArgs e) => ArmTool(RoiType.Ellipse,       (Button)sender);
    private void AddSector_Click(object sender, RoutedEventArgs e)  => ArmTool(RoiType.EllipseSector, (Button)sender);
    private void AddPolygon_Click(object sender, RoutedEventArgs e) => ArmTool(RoiType.Polygon,       (Button)sender);

    private void PreviewRegion_Click(object sender, RoutedEventArgs e) => EnterPreview();

    private void ExitPreview_Click(object sender, RoutedEventArgs e) => ExitPreview();

    /// <summary>进入预览：隐藏工具框，显示 Include − Exclude 最终检测区域</summary>
    private void EnterPreview()
    {
        if (_rows.Count == 0) return;
        _isPreviewing = true;
        DetachAllDrawObjects();
        RedrawPreviewRegion();
        PreviewBtn.Visibility     = Visibility.Collapsed;
        ExitPreviewBtn.Visibility = Visibility.Visible;
    }

    /// <summary>退出预览：清除预览区域，恢复显示工具框</summary>
    private void ExitPreview()
    {
        _isPreviewing = false;
        ImageViewer.ClearOverlays();
        EnsureAllAttached();
        PreviewBtn.Visibility     = Visibility.Visible;
        ExitPreviewBtn.Visibility = Visibility.Collapsed;
    }

    private void RedrawPreviewRegion()
    {
        SyncAllFromDrawObjects();
        var region = RoiRegionBuilder.Build(_rows.Select(r => r.Config));
        ImageViewer.ClearOverlays();
        if (region.IsInitialized())
            ImageViewer.DisplayOverlay(region, color: "lime green", lineWidth: 2);
        region.Dispose();
    }

    /// <summary>任何 ROI 编辑操作前调用：若处于预览态先退出，恢复工具框</summary>
    private void BeginEdit()
    {
        if (_isPreviewing) ExitPreview();
    }

    private void ClearRois_Click(object sender, RoutedEventArgs e) => ClearRois();

    private void DeleteRoi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RoiRowVm row)
            RemoveRow(row);
    }

    private void OpComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BeginEdit();
        if (sender is ComboBox cb && cb.DataContext is RoiRowVm row && row.DrawId is not null)
        {
            try
            {
                var color = row.Config.Op == RoiOp.Include ? "green" : "red";
                HOperatorSet.SetDrawingObjectParams(row.DrawId, "color", color);
            }
            catch { }
        }
    }

    private void RoiGrid_SelectionChanged(object sender, SelectedCellsChangedEventArgs e) { }

    // ── 拖拽画图工具（武装/取消） ─────────────────────────────────────────────

    /// <summary>
    /// 点击形状按钮：武装对应工具（按钮加高亮边框，光标变十字，关闭内置左键平移），
    /// 等待用户在图像上拖拽（多边形为逐点点击）完成绘制；再次点击同一按钮取消武装。
    /// </summary>
    private void ArmTool(RoiType type, Button btn)
    {
        BeginEdit();

        if (_armedButton == btn)
        {
            DisarmTool();
            return;
        }

        // 只在"从无工具切到有工具"时记录原始平移状态；A 工具切到 B 工具时不能重新采样
        // （此时平移已被 A 关掉，会把"关闭"误当作原始状态存下来，导致最终恢复不回去）
        if (_armedType is null)
            _panWasEnabled = ImageViewer.PanEnabled;

        DisarmTool(restorePan: false);

        _armedType   = type;
        _armedButton = btn;
        ImageViewer.PanEnabled = false;
        ImageViewer.Cursor     = Cursors.Cross;
        SetArmedVisual(btn, true);
    }

    /// <summary>取消武装：还原按钮高亮/光标/平移，清掉未完成的拖拽或多边形预览</summary>
    private void DisarmTool(bool restorePan = true)
    {
        if (_armedButton is not null) SetArmedVisual(_armedButton, false);
        _armedButton = null;
        _armedType   = null;
        _isDragging  = false;
        _polygonPoints.Clear();
        ImageViewer.Cursor = Cursors.Arrow;
        if (restorePan) ImageViewer.PanEnabled = _panWasEnabled;
        if (!_isPreviewing) ImageViewer.ClearOverlays();
    }

    private static void SetArmedVisual(Button btn, bool armed)
    {
        if (armed)
        {
            btn.BorderBrush     = btn.TryFindResource("PrimaryBrush") as Brush ?? Brushes.DodgerBlue;
            btn.BorderThickness = new Thickness(2);
        }
        else
        {
            btn.ClearValue(Button.BorderBrushProperty);
            btn.ClearValue(Button.BorderThicknessProperty);
        }
    }

    private void OnRoiEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _armedType is null) return;
        DisarmTool();
        e.Handled = true;
    }

    // ── 拖拽画图（矩形/旋转矩形/圆/椭圆/扇形） ───────────────────────────────

    private void OnImageMouseDown(object? sender, ImageMouseEventArgs e)
    {
        if (_armedType is not { } type) return;

        if (type == RoiType.Polygon)
        {
            if (e.Button == MouseButton.Right) { FinishPolygon(); return; }
            if (e.Button != MouseButton.Left)  return;
            _polygonPoints.Add((e.Row, e.Column));
            RedrawPolygonPreview(e.Row, e.Column);
            return;
        }

        if (e.Button != MouseButton.Left) return;
        _isDragging   = true;
        _dragStartRow = e.Row;
        _dragStartCol = e.Column;
    }

    private void OnImageMouseMove(object? sender, ImageMouseEventArgs e)
    {
        if (_armedType is not { } type) return;

        if (type == RoiType.Polygon)
        {
            if (_polygonPoints.Count > 0) RedrawPolygonPreview(e.Row, e.Column);
            return;
        }

        if (!_isDragging) return;
        DrawDragPreview(type, _dragStartRow, _dragStartCol, e.Row, e.Column);
    }

    private void OnImageMouseUp(object? sender, ImageMouseEventArgs e)
    {
        if (_armedType is not { } type || type == RoiType.Polygon) return;
        if (!_isDragging) return;

        // 只按 _isDragging 判断是否收尾：HALCON 转发的 HMouseUp.Button 取值不完全可靠
        // （曾观察到抬起时不等于 Left），用它做强校验会导致永远走不到这一步，
        // 工具卡在"武装"状态、松手后仍在无限重绘拖拽预览。_isDragging 只在按下时
        // 校验过左键才会置真，抬起时不用再重复校验按钮种类。
        _isDragging = false;
        CommitDraggedShape(type, _dragStartRow, _dragStartCol, e.Row, e.Column);
        DisarmTool();
    }

    /// <summary>矩形/旋转矩形＝对角拖拽定边界；圆/椭圆/扇形＝起点为中心、当前点定半径（旋转矩形先按水平框创建，画完再用手柄旋转）</summary>
    private static double[] ComputeDragParams(RoiType type, double r0, double c0, double r1, double c1)
    {
        var centerRow = (r0 + r1) / 2.0;
        var centerCol = (c0 + c1) / 2.0;
        var halfH     = Math.Max(Math.Abs(r1 - r0) / 2.0, 0.5);
        var halfW     = Math.Max(Math.Abs(c1 - c0) / 2.0, 0.5);
        var radius    = Math.Max(Math.Sqrt((r1 - r0) * (r1 - r0) + (c1 - c0) * (c1 - c0)), 0.5);

        return type switch
        {
            RoiType.Rect          => [Math.Min(r0, r1), Math.Min(c0, c1), Math.Max(r0, r1), Math.Max(c0, c1)],
            RoiType.Rect2         => [centerRow, centerCol, 0.0, halfW, halfH],
            RoiType.Circle        => [r0, c0, radius],
            RoiType.Ellipse       => [r0, c0, 0.0, Math.Max(Math.Abs(c1 - c0), 0.5), Math.Max(Math.Abs(r1 - r0), 0.5)],
            RoiType.EllipseSector => [r0, c0, 0.0, Math.Max(Math.Abs(c1 - c0), 0.5), Math.Max(Math.Abs(r1 - r0), 0.5), 0.0, 1.57],
            _                     => [],
        };
    }

    private void DrawDragPreview(RoiType type, double r0, double c0, double r1, double c1)
    {
        var p = ComputeDragParams(type, r0, c0, r1, c1);
        using var region = RoiRegionBuilder.GetRegion(new VisionRoiConfig { Type = type, RoiParams = p });
        ImageViewer.ClearOverlays();
        if (region.IsInitialized())
            ImageViewer.DisplayOverlay(region, color: _currentOp == RoiOp.Include ? "green" : "red", lineWidth: 2);
    }

    private void CommitDraggedShape(RoiType type, double r0, double c0, double r1, double c1)
    {
        _roiCounter++;
        var cfg = new VisionRoiConfig
        {
            Name      = $"ROI_{_roiCounter}",
            Type      = type,
            Op        = _currentOp,
            RoiParams = ComputeDragParams(type, r0, c0, r1, c1),
        };
        var row = new RoiRowVm(cfg);
        _rows.Add(row);
        AttachDrawObject(row);
    }

    // ── 多边形（逐点点击，右键完成，Esc 取消） ───────────────────────────────

    private void RedrawPolygonPreview(double liveRow, double liveCol)
    {
        ImageViewer.ClearOverlays();
        if (_polygonPoints.Count == 0) return;

        var rows = _polygonPoints.Select(p => p.Row).Append(liveRow).ToArray();
        var cols = _polygonPoints.Select(p => p.Col).Append(liveCol).ToArray();
        if (rows.Length < 2) return;

        HOperatorSet.GenContourPolygonXld(out HObject xld, new HTuple(rows), new HTuple(cols));
        ImageViewer.DisplayOverlay(xld, color: _currentOp == RoiOp.Include ? "green" : "red", lineWidth: 2);
        xld.Dispose();
    }

    /// <summary>右键结束多边形：顶点数不足 3 个视为取消，不生成 ROI</summary>
    private void FinishPolygon()
    {
        var points = _polygonPoints.ToList(); // DisarmTool 会清空 _polygonPoints，先拷贝一份
        DisarmTool();
        if (points.Count < 3) return;

        var p = new double[points.Count * 2];
        for (int i = 0; i < points.Count; i++)
        {
            p[i * 2]     = points[i].Row;
            p[i * 2 + 1] = points[i].Col;
        }

        _roiCounter++;
        var cfg = new VisionRoiConfig
        {
            Name      = $"ROI_{_roiCounter}",
            Type      = RoiType.Polygon,
            Op        = _currentOp,
            RoiParams = p,
        };
        var row = new RoiRowVm(cfg);
        _rows.Add(row);
        AttachDrawObject(row);
    }

    // ── ROI 删除 ──────────────────────────────────────────────────────────────

    private void RemoveRow(RoiRowVm row)
    {
        BeginEdit();
        if (row.DrawId is not null)
        {
            try { HOperatorSet.ClearDrawingObject(row.DrawId); } catch { }
            row.DrawId = null;
        }
        _rows.Remove(row);
    }

    // ── DrawingObject 联动 ────────────────────────────────────────────────────

    private void AttachDrawObject(RoiRowVm row)
    {
        var win = GetHWindow();
        if (win is null) return;

        var color = row.Config.Op == RoiOp.Include ? "green" : "red";
        var p     = row.Config.RoiParams;
        HTuple drawId;

        try
        {
            switch (row.Config.Type)
            {
                case RoiType.Rect:
                    HOperatorSet.CreateDrawingObjectRectangle1(p[0], p[1], p[2], p[3], out drawId);
                    break;
                case RoiType.Rect2:
                    HOperatorSet.CreateDrawingObjectRectangle2(p[0], p[1], p[2], p[3], p[4], out drawId);
                    break;
                case RoiType.Circle:
                    HOperatorSet.CreateDrawingObjectCircle(p[0], p[1], p[2], out drawId);
                    break;
                case RoiType.Ellipse:
                    HOperatorSet.CreateDrawingObjectEllipse(p[0], p[1], p[2], p[3], p[4], out drawId);
                    break;
                case RoiType.EllipseSector:
                    // row, col, phi, radius1, radius2, startAngle, endAngle
                    HOperatorSet.CreateDrawingObjectEllipseSector(
                        p[0], p[1], p[2], p[3], p[4], p[5], p[6], out drawId);
                    break;
                case RoiType.Polygon when p.Length >= 6:
                {
                    int count = p.Length / 2;
                    var rows  = new HTuple(Enumerable.Range(0, count).Select(i => p[i * 2]).ToArray());
                    var cols  = new HTuple(Enumerable.Range(0, count).Select(i => p[i * 2 + 1]).ToArray());
                    HOperatorSet.CreateDrawingObjectXld(rows, cols, out drawId);
                    break;
                }
                default:
                    return;
            }

            HOperatorSet.SetDrawingObjectParams(drawId, "color",      color);
            HOperatorSet.SetDrawingObjectParams(drawId, "line_width",  2);
            HOperatorSet.AttachDrawingObjectToWindow(win, drawId);
            row.DrawId = drawId;
        }
        catch { }
    }

    // 从 DrawObject 读回当前坐标，更新 RoiParams（按需调用，不依赖回调）
    private static void SyncFromDrawObject(RoiRowVm row)
    {
        if (row.DrawId is null) return;
        var drawId = row.DrawId;

        try
        {
            switch (row.Config.Type)
            {
                case RoiType.Rect:
                {
                    HOperatorSet.GetDrawingObjectIconic(out HObject region, drawId);
                    HOperatorSet.SmallestRectangle1(region,
                        out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                    row.Config.RoiParams = [r1.D, c1.D, r2.D, c2.D];
                    region.Dispose();
                    break;
                }
                case RoiType.Rect2:
                {
                    HOperatorSet.GetDrawingObjectParams(drawId, "row",     out HTuple r);
                    HOperatorSet.GetDrawingObjectParams(drawId, "column",  out HTuple c);
                    HOperatorSet.GetDrawingObjectParams(drawId, "phi",     out HTuple phi);
                    HOperatorSet.GetDrawingObjectParams(drawId, "length1", out HTuple l1);
                    HOperatorSet.GetDrawingObjectParams(drawId, "length2", out HTuple l2);
                    row.Config.RoiParams = [r.D, c.D, phi.D, l1.D, l2.D];
                    break;
                }
                case RoiType.Circle:
                {
                    HOperatorSet.GetDrawingObjectParams(drawId, "row",    out HTuple r);
                    HOperatorSet.GetDrawingObjectParams(drawId, "column", out HTuple c);
                    HOperatorSet.GetDrawingObjectParams(drawId, "radius", out HTuple rad);
                    row.Config.RoiParams = [r.D, c.D, rad.D];
                    break;
                }
                case RoiType.Ellipse:
                {
                    HOperatorSet.GetDrawingObjectParams(drawId, "row",     out HTuple r);
                    HOperatorSet.GetDrawingObjectParams(drawId, "column",  out HTuple c);
                    HOperatorSet.GetDrawingObjectParams(drawId, "phi",     out HTuple phi);
                    HOperatorSet.GetDrawingObjectParams(drawId, "radius1", out HTuple r1);
                    HOperatorSet.GetDrawingObjectParams(drawId, "radius2", out HTuple r2);
                    row.Config.RoiParams = [r.D, c.D, phi.D, r1.D, r2.D];
                    break;
                }
                case RoiType.EllipseSector:
                {
                    HOperatorSet.GetDrawingObjectParams(drawId, "row",         out HTuple r);
                    HOperatorSet.GetDrawingObjectParams(drawId, "column",      out HTuple c);
                    HOperatorSet.GetDrawingObjectParams(drawId, "phi",         out HTuple phi);
                    HOperatorSet.GetDrawingObjectParams(drawId, "radius1",     out HTuple r1);
                    HOperatorSet.GetDrawingObjectParams(drawId, "radius2",     out HTuple r2);
                    HOperatorSet.GetDrawingObjectParams(drawId, "start_angle", out HTuple sa);
                    HOperatorSet.GetDrawingObjectParams(drawId, "end_angle",   out HTuple ea);
                    row.Config.RoiParams = [r.D, c.D, phi.D, r1.D, r2.D, sa.D, ea.D];
                    break;
                }
                case RoiType.Polygon:
                {
                    HOperatorSet.GetDrawingObjectIconic(out HObject xld, drawId);
                    HOperatorSet.GetContourXld(xld, out HTuple rows, out HTuple cols);
                    var p = new double[rows.Length * 2];
                    for (int i = 0; i < rows.Length; i++)
                    {
                        p[i * 2]     = rows.DArr[i];
                        p[i * 2 + 1] = cols.DArr[i];
                    }
                    row.Config.RoiParams = p;
                    xld.Dispose();
                    break;
                }
            }
        }
        catch { }
    }

    private void SyncAllFromDrawObjects()
    {
        foreach (var row in _rows)
            SyncFromDrawObject(row);
    }

    private void ClearAllDrawObjects()
    {
        foreach (var row in _rows)
        {
            if (row.DrawId is not null)
            {
                try { HOperatorSet.ClearDrawingObject(row.DrawId); } catch { }
                row.DrawId = null;
            }
        }
    }
}
