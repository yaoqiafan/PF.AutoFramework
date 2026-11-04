using PF.Core.Interfaces.Device.Hardware;
using Prism.Commands;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace PF.Application.Base.Models;

/// <summary>
/// 状态栏设备状态条目，供 MainWindow 数据驱动绑定。
/// <para>默认以 <see cref="IsConnected"/> 二值灯显示；当设置了 <see cref="StatusText"/> 时，
/// 自动切换为多态显示（自定义文本 + 颜色），用于承载三态及以上的枚举状态
/// （如 SECS 控制状态：离线 / 本地 / 远程）。</para>
/// <para>枚举 → 文本/颜色的映射由消费方（外接转换器）负责，本模型只承载转换后的结果，
/// 因此框架不感知任何具体业务枚举，保持通用。</para>
/// </summary>
public class DeviceStatusItem : BindableBase
{
    /// <summary>状态栏显示的设备名称标签。</summary>
    public string Label { get; init; } = string.Empty;

    private bool _isConnected;
    /// <summary>设备当前是否已连接（二值灯模式，<see cref="UseCustomStatus"/> 为 false 时生效）。</summary>
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private string? _statusText;
    /// <summary>
    /// 自定义状态文本。非空时状态栏改用多态显示（<see cref="StatusText"/> + <see cref="StatusBrush"/>），
    /// 替代 <see cref="IsConnected"/> 二值灯，用于表达三态及以上的状态。
    /// </summary>
    public string? StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
                RaisePropertyChanged(nameof(UseCustomStatus));
        }
    }

    private Brush? _statusBrush;
    /// <summary>多态显示模式下灯与文本的颜色（由消费方外接转换器提供）。</summary>
    public Brush? StatusBrush
    {
        get => _statusBrush;
        set => SetProperty(ref _statusBrush, value);
    }

    /// <summary>
    /// 是否启用多态状态显示：当 <see cref="StatusText"/> 非空时为真，
    /// 状态栏显示 <see cref="StatusText"/> / <see cref="StatusBrush"/>；
    /// 否则回退到 <see cref="IsConnected"/> 二值灯。
    /// </summary>
    public bool UseCustomStatus => !string.IsNullOrEmpty(_statusText);

    #region 可点击操作

    /// <summary>条目上的可点击操作列表；为空时条目只做显示。通过 <see cref="AddAction"/> 添加。</summary>
    public ObservableCollection<StatusItemAction> Actions { get; } = new();

    /// <summary>是否挂有可点击操作（状态栏据此切换为可点击的菜单样式）。</summary>
    public bool HasActions => Actions.Count > 0;

    private bool _isBusy;
    /// <summary>是否有操作正在执行（执行期间菜单项禁用，防止重复点击）。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        internal set
        {
            if (SetProperty(ref _isBusy, value))
            {
                foreach (var action in Actions)
                    (action.Command as DelegateCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// 操作执行器，由 MainWindowViewModelBase 在条目加入状态栏时注入
    /// （统一做权限、机台状态、确认、超时、提示与日志）。未注入时直接执行动作。
    /// </summary>
    internal Func<DeviceStatusItem, StatusItemAction, Task>? Executor { get; set; }

    /// <summary>添加一个可点击操作，返回自身以便链式调用。</summary>
    public DeviceStatusItem AddAction(StatusItemAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.Command = new DelegateCommand(async () => await RunAsync(action), () => !IsBusy);
        Actions.Add(action);
        RaisePropertyChanged(nameof(HasActions));
        return this;
    }

    /// <summary>添加硬件设备常用操作：重新连接 / 断开连接 / 清除报警。</summary>
    public DeviceStatusItem AddDeviceActions(Func<IHardwareDevice?> device) =>
        AddAction(StatusItemActions.Reconnect(device))
            .AddAction(StatusItemActions.Disconnect(device))
            .AddAction(StatusItemActions.ClearAlarm(device));

    private async Task RunAsync(StatusItemAction action)
    {
        if (Executor != null)
        {
            await Executor(this, action);
            return;
        }

        IsBusy = true;
        try { await action.Execute(CancellationToken.None); }
        catch { /* 未注入执行器时无提示通道，静默失败 */ }
        finally { IsBusy = false; }
    }

    #endregion
}
