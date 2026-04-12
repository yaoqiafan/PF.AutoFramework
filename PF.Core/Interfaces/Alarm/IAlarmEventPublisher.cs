using PF.Core.Models;

namespace PF.Core.Interfaces.Alarm
{
    /// <summary>
    /// 报警事件发布器桥接接口。
    /// <para>
    /// <c>AlarmService</c> 通过此接口向 UI 层发布报警状态变更事件，
    /// 使 PF.Services 无需直接依赖 Prism。
    /// </para>
    /// <para>
    /// Shell 层的 <c>PrismAlarmEventPublisher</c> 实现该接口，通过 Prism
    /// <c>IEventAggregator</c> 广播 <c>AlarmTriggeredEvent</c>、
    /// <c>AlarmClearedEvent</c>、<c>HardwareResetRequestedEvent</c>。
    /// </para>
    /// </summary>
    public interface IAlarmEventPublisher
    {
        /// <summary>有新报警触发时调用</summary>
        void PublishAlarmTriggered(AlarmRecord record);

        /// <summary>报警被清除时调用</summary>
        void PublishAlarmCleared(AlarmRecord record);

        /// <summary>报警清除后请求执行硬件物理复位时调用</summary>
        void PublishHardwareResetRequested(HardwareResetRequest request);
    }
}
