using Microsoft.Win32;
using PF.Core.Interfaces.Vision.Pipeline;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>可编辑控制量输入参数（TextBox 绑定）</summary>
public sealed class InputControlParamVm : BindableBase
{
    public string Name { get; }

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public InputControlParamVm(string name) => Name = name;
}

/// <summary>
/// 图像/ROI 输入参数。
/// IsRoiMode=false：文件路径模式（原有行为）。
/// IsRoiMode=true ：ROI 绘制模式，CurrentRois 由 HalconRoiEditor 填充。
/// </summary>
public sealed class InputIconicParamVm : BindableBase
{
    public string Name { get; }

    // ── 文件路径模式 ──────────────────────────────────────────────────────────

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public DelegateCommand BrowseCommand { get; }

    // ── ROI 绘制模式 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 切换到 ROI 绘制模式 或 在 ROI 模式下重新打开编辑弹窗时触发。
    /// 外部订阅此事件以弹出 ROI 编辑对话框。
    /// </summary>
    public event Action<InputIconicParamVm>? RoiEditorRequested;

    private bool _isRoiMode;
    public bool IsRoiMode
    {
        get => _isRoiMode;
        set
        {
            if (SetProperty(ref _isRoiMode, value))
            {
                RaisePropertyChanged(nameof(IsFileMode));
                RaisePropertyChanged(nameof(ModeLabelText));
                BrowseCommand.RaiseCanExecuteChanged();
                EditRoisCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool   IsFileMode     => !_isRoiMode;
    public string ModeLabelText  => _isRoiMode ? "ROI 绘制" : "图像文件";

    /// <summary>ROI 绘制模式下的配置列表（弹窗关闭后写回）</summary>
    public ObservableCollection<VisionRoiConfig> CurrentRois { get; } = new();

    public DelegateCommand ToggleModeCommand  { get; }
    public DelegateCommand EditRoisCommand    { get; }

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public InputIconicParamVm(string name)
    {
        Name          = name;
        BrowseCommand = new DelegateCommand(OnBrowse, () => IsFileMode);

        // 文件模式 → 切换到 ROI 模式并打开弹窗；ROI 模式 → 切回文件模式
        ToggleModeCommand = new DelegateCommand(() =>
        {
            if (IsFileMode)
            {
                IsRoiMode = true;
                RoiEditorRequested?.Invoke(this);
            }
            else
            {
                IsRoiMode = false;
                CurrentRois.Clear();
            }
        });

        // ROI 模式下再次打开弹窗编辑
        EditRoisCommand = new DelegateCommand(
            () => RoiEditorRequested?.Invoke(this),
            () => IsRoiMode);
    }

    private void OnBrowse()
    {
        var dlg = new OpenFileDialog
        {
            Title  = $"选择图像文件 [{Name}]",
            Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tiff;*.tif;*.hobj;*.himage|所有文件|*.*",
        };
        if (dlg.ShowDialog() == true)
            FilePath = dlg.FileName;
    }
}

/// <summary>控制量输出参数（执行后填充，只读显示）</summary>
public sealed class OutputCtrlParamVm : BindableBase
{
    public string Name { get; }

    private string? _value;
    public string? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public OutputCtrlParamVm(string name) => Name = name;
}
