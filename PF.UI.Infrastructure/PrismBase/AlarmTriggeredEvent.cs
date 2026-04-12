using PF.Core.Models;
using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 报警触发事件（由 PrismAlarmEventPublisher 发布，AlarmCenterViewModel / MainWindowViewModel 订阅）。
    /// </summary>
    public class AlarmTriggeredEvent : PubSubEvent<AlarmRecord>
    {
    }
}
