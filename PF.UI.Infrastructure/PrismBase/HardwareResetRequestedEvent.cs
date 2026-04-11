using PF.Core.Models;
using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 硬件物理复位请求事件（由 PrismAlarmEventPublisher 在报警清除后发布）。
    /// Shell 中订阅并路由到 IMasterController.RegisterHardwareResetHandler 委托（Phase 4）。
    /// </summary>
    public class HardwareResetRequestedEvent : PubSubEvent<HardwareResetRequest>
    {
    }
}
