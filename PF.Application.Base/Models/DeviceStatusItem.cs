namespace PF.Application.Base.Models;

/// <summary>
/// 状态栏设备状态条目，供 MainWindow 数据驱动绑定
/// </summary>
public class DeviceStatusItem : BindableBase
{
    public string Label { get; init; } = string.Empty;

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }
}
