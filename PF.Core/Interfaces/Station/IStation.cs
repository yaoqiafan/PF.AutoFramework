using PF.Core.Enums;
using PF.Core.Events;

using System.ComponentModel;

/// <summary>
/// 工站非泛型契约。
/// 供 MasterController 以统一视图管理异构 StationBase&lt;TMemory&gt; 子类，
/// 屏蔽底层具体共享内存类型的差异。
/// </summary>
public interface IStation : INotifyPropertyChanged, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 获取工站的唯一识别名称。
    /// </summary>
    string StationName { get; }

    /// <summary>
    /// 获取当前工站的生命周期状态（如：运行中、停止、故障等）。
    /// </summary>
    MachineState CurrentState { get; }

    /// <summary>
    /// 获取或设置当前工站的操作模式（如：自动、手动、步进等）。
    /// </summary>
    OperationMode CurrentMode { get; set; }

    /// <summary>
    /// 当工站触发异常报警时引发的事件。
    /// </summary>
    event EventHandler<StationAlarmEventArgs> StationAlarmTriggered;

    /// <summary>
    /// 当报警被系统自动清除（非人工干预）时引发的事件。
    /// </summary>
    event EventHandler StationAlarmAutoCleared;

    /// <summary>
    /// 当工站状态 <see cref="CurrentState"/> 发生变更时引发的事件。
    /// </summary>
    event EventHandler<StationStateChangedEventArgs> StationStateChanged;

    /// <summary>
    /// 异步启动工站逻辑。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    Task StartAsync();

    /// <summary>
    /// 异步停止工站逻辑。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    Task StopAsync();

    /// <summary>
    /// 异步恢复已暂停的工站。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    Task ResumeAsync();

    /// <summary>
    /// 暂停当前工站的执行逻辑（非阻塞式请求）。
    /// </summary>
    void Pause();

    /// <summary>
    /// 触发一个通用报警。
    /// </summary>
    void TriggerAlarm();

    /// <summary>
    /// 根据指定的错误代码触发报警。
    /// </summary>
    /// <param name="errorCode">预定义的错误码。</param>
    void TriggerAlarm(string errorCode);

    /// <summary>
    /// 根据错误代码和自定义运行时详细信息触发报警。
    /// </summary>
    /// <param name="errorCode">预定义的错误码。</param>
    /// <param name="runtimeMessage">附加的实时错误描述信息。</param>
    void TriggerAlarm(string errorCode, string? runtimeMessage);

    /// <summary>
    /// 重置/清除当前工站的所有报警状态。
    /// </summary>
    void ResetAlarm();

    /// <summary>
    /// 执行工站初始化流程（如硬件自检、参数加载）。
    /// </summary>
    /// <param name="token">用于取消初始化操作的任务令牌。</param>
    /// <returns>表示初始化过程的任务。</returns>
    Task ExecuteInitializeAsync(CancellationToken token);

    /// <summary>
    /// 执行工站复位流程，将其归位至初始运行点。
    /// </summary>
    /// <param name="token">用于取消复位操作的任务令牌。</param>
    /// <returns>表示复位过程的任务。</returns>
    Task ExecuteResetAsync(CancellationToken token);
}