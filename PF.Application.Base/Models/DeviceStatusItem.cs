namespace PF.Application.Base.Models;

/// <summary>
/// 状态栏设备状态条目，供 MainWindow 数据驱动绑定
/// </summary>
public class DeviceStatusItem : BindableBase
{
    /// <summary>状态栏显示的设备名称标签。</summary>
    public string Label { get; init; } = string.Empty;

    private bool _isConnected;
    /// <summary>设备当前是否已连接。</summary>
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }
}
