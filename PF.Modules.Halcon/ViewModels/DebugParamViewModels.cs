using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;

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

/// <summary>图像文件输入参数（文件选择器绑定）</summary>
public sealed class InputIconicParamVm : BindableBase
{
    public string Name { get; }

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public DelegateCommand BrowseCommand { get; }

    public InputIconicParamVm(string name)
    {
        Name          = name;
        BrowseCommand = new DelegateCommand(OnBrowse);
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
