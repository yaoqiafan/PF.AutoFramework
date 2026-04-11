using PF.Core.Models;
using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 报警清除事件（由 PrismAlarmEventPublisher 发布，AlarmCenterViewModel 订阅）。
    /// </summary>
    public class AlarmClearedEvent : PubSubEvent<AlarmRecord>
    {
    }
}
