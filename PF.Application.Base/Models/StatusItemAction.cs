using PF.Core.Enums;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.SecsGem;
using System.Windows.Input;

namespace PF.Application.Base.Models;

/// <summary>
/// 状态栏条目上的一个可点击操作（如“重新连接”“断开”“清除报警”）。
/// <para>通过 <see cref="DeviceStatusItem.AddAction"/> 挂到条目上后，状态栏点击该条目会弹出操作菜单。
/// 执行时由 MainWindowViewModelBase 统一做权限检查、机台状态检查、二次确认、超时控制、结果提示与操作日志，
/// 业务方只需提供 <see cref="Execute"/>。</para>
/// </summary>
public sealed class StatusItemAction
{
    /// <summary>创建状态栏操作。</summary>
    /// <param name="header">菜单显示文本。</param>
    /// <param name="execute">具体动作；返回 true 表示成功。抛出异常按失败处理。</param>
    public StatusItemAction(string header, Func<CancellationToken, Task<bool>> execute)
    {
        Header = header;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>菜单显示文本。</summary>
    public string Header { get; }

    /// <summary>具体动作；返回 true 表示成功。</summary>
    public Func<CancellationToken, Task<bool>> Execute { get; }

    /// <summary>执行所需的最低权限，默认工程师。</summary>
    public UserLevel MinUserLevel { get; init; } = UserLevel.Engineer;

    /// <summary>
    /// 是否允许在机台 Running / Initializing / Resetting 时执行，默认不允许：
    /// 运行中断开或重连设备会打断正在进行的工站动作（如扫码、拍照）。
    /// </summary>
    public bool AllowWhileMachineBusy { get; init; }

    /// <summary>执行前的二次确认提示；为空则不弹确认框。</summary>
    public string? ConfirmText { get; init; }

    /// <summary>执行超时，默认 30 秒。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>绑定到菜单项的命令（由 <see cref="DeviceStatusItem.AddAction"/> 创建）。</summary>
    public ICommand? Command { get; internal set; }
}

/// <summary>
/// 常用状态栏操作的工厂方法。设备通过委托延迟获取：状态栏条目通常在主窗口 ViewModel 构造时创建，
/// 此时硬件管理服务尚未完成初始化，执行时再解析设备实例。
/// </summary>
public static class StatusItemActions
{
    /// <summary>硬件设备：先断开再重新连接。</summary>
    public static StatusItemAction Reconnect(Func<IHardwareDevice?> device) =>
        new("重新连接", async token =>
        {
            var dev = device() ?? throw new InvalidOperationException("设备未找到或未启用");
            await dev.DisconnectAsync(token).ConfigureAwait(false);
            return await dev.ConnectAsync(token).ConfigureAwait(false);
        })
        { ConfirmText = "将先断开再重新连接该设备，期间设备不可用。" };

    /// <summary>硬件设备：断开连接。</summary>
    public static StatusItemAction Disconnect(Func<IHardwareDevice?> device) =>
        new("断开连接", async token =>
        {
            var dev = device() ?? throw new InvalidOperationException("设备未找到或未启用");
            await dev.DisconnectAsync(token).ConfigureAwait(false);
            return !dev.IsConnected;
        })
        { ConfirmText = "断开后设备将不可用，需手动重新连接或重新初始化。" };

    /// <summary>硬件设备：清除硬件报警标志（不回原点）。</summary>
    public static StatusItemAction ClearAlarm(Func<IHardwareDevice?> device) =>
        new("清除报警", async token =>
        {
            var dev = device() ?? throw new InvalidOperationException("设备未找到或未启用");
            return await dev.ResetHardwareAlarmAsync(token).ConfigureAwait(false);
        });

    /// <summary>SECS/GEM：先断开再重新连接 Host。</summary>
    public static StatusItemAction SecsReconnect(Func<ISecsGemManager?> manager) =>
        new("重新连接", async _ =>
        {
            var mgr = manager() ?? throw new InvalidOperationException("SECS/GEM 服务未注册");
            await mgr.DisconnectAsync().ConfigureAwait(false);
            return await mgr.ConnectAsync().ConfigureAwait(false);
        })
        { ConfirmText = "将先断开再重新连接 SECS/GEM 服务，期间与主机的通讯会中断。" };

    /// <summary>SECS/GEM：断开与 Host 的连接。</summary>
    public static StatusItemAction SecsDisconnect(Func<ISecsGemManager?> manager) =>
        new("断开连接", async _ =>
        {
            var mgr = manager() ?? throw new InvalidOperationException("SECS/GEM 服务未注册");
            await mgr.DisconnectAsync().ConfigureAwait(false);
            return !mgr.IsConnected;
        })
        { ConfirmText = "断开后将无法与主机通讯（换批、上报均会失败），确认断开？" };
}
