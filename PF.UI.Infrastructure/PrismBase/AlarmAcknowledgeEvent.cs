using PF.Core.Models;
using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 报警确认事件（UI 操作员点击"确认/清除"特定报警记录时发布）。
    /// 仅清除 AlarmService 内存中的该条记录，不触发硬件复位或系统状态机跳转。
    /// </summary>
    public class AlarmAcknowledgeEvent : PubSubEvent<AlarmRecord>
    {
    }
}
