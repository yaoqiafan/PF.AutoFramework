using PF.Core.Interfaces.Alarm;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Events;

namespace PF.Application.Shell.Services
{
    /// <summary>
    /// <see cref="IAlarmEventPublisher"/> 的 Prism 实现。
    /// 在 Shell 中注册，通过 <see cref="IEventAggregator"/> 将报警状态变更广播到全应用。
    /// <para>
    /// 此实现位于 Shell 层，使 <c>PF.Services</c> 的 <c>AlarmService</c> 无需直接依赖 Prism，
    /// 从而保持基础设施层的依赖隔离。
    /// </para>
    /// </summary>
    internal sealed class PrismAlarmEventPublisher : IAlarmEventPublisher
    {
        private readonly IEventAggregator _ea;

        public PrismAlarmEventPublisher(IEventAggregator ea)
        {
            _ea = ea;
        }

        public void PublishAlarmTriggered(AlarmRecord record)
            => _ea.GetEvent<AlarmTriggeredEvent>().Publish(record);

        public void PublishAlarmCleared(AlarmRecord record)
            => _ea.GetEvent<AlarmClearedEvent>().Publish(record);

        public void PublishHardwareResetRequested(HardwareResetRequest request)
            => _ea.GetEvent<HardwareResetRequestedEvent>().Publish(request);
    }
}
