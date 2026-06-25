using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Vision.Halcon.Internal;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

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

    public HalconRoiEditor()
    {
        InitializeComponent();
        RoiGrid.ItemsSource = _rows;
        // HALCON 窗口（重新）创建后重挂载 DrawingObject（切 tab/导航返回时窗口会重建）
        ImageViewer.WindowInitialized += OnWindowInitialized;
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

    private void AddRect_Click(object sender, RoutedEventArgs e)    => AddRoi(RoiType.Rect);
    private void AddRect2_Click(object sender, RoutedEventArgs e)   => AddRoi(RoiType.Rect2);
    private void AddCircle_Click(object sender, RoutedEventArgs e)  => AddRoi(RoiType.Circle);
    private void AddEllipse_Click(object sender, RoutedEventArgs e) => AddRoi(RoiType.Ellipse);
    private void AddSector_Click(object sender, RoutedEventArgs e)  => AddRoi(RoiType.EllipseSector);

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

    // ── ROI 添加/删除 ─────────────────────────────────────────────────────────

    private void AddRoi(RoiType type)
    {
        BeginEdit();
        _roiCounter++;
        var cfg = new VisionRoiConfig
        {
            Name      = $"ROI_{_roiCounter}",
            Type      = type,
            Op        = _currentOp,
            RoiParams = DefaultParams(type),
        };
        var row = new RoiRowVm(cfg);
        _rows.Add(row);
        AttachDrawObject(row);
    }

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

    // ── 默认参数 ──────────────────────────────────────────────────────────────

    private static double[] DefaultParams(RoiType type) => type switch
    {
        RoiType.Rect          => [100, 100, 300, 400],
        RoiType.Rect2         => [200, 300, 0.0, 100, 50],
        RoiType.Circle        => [200, 300, 80],
        RoiType.Ellipse       => [200, 300, 0.0, 100, 50],
        RoiType.EllipseSector => [200, 300, 0.0, 100, 60, 0.0, 1.57],
        _                     => [],
    };
}
