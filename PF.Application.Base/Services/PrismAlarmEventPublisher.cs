using PF.Core.Interfaces.Alarm;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Application.Base.Services
{
    /// <summary>
    /// IAlarmEventPublisher 的 Prism 实现。
    /// 位于 Base 层，使 PF.Services 的 AlarmService 无需直接依赖 Prism。
    /// </summary>
    internal sealed class PrismAlarmEventPublisher : IAlarmEventPublisher
    {
        private readonly IEventAggregator _ea;

        public PrismAlarmEventPublisher(IEventAggregator ea) => _ea = ea;

        public void PublishAlarmTriggered(AlarmRecord record)
            => _ea.GetEvent<AlarmTriggeredEvent>().Publish(record);

        public void PublishAlarmCleared(AlarmRecord record)
            => _ea.GetEvent<AlarmClearedEvent>().Publish(record);

        public void PublishHardwareResetRequested(HardwareResetRequest request)
            => _ea.GetEvent<HardwareResetRequestedEvent>().Publish(request);
    }
}
