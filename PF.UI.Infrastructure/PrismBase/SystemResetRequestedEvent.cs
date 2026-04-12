using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 系统复位请求事件（操作员点击"系统复位"按钮时发布）。
    /// 触发全局复位：清除 AlarmService 所有活跃报警 + 执行 ResetAllAsync 状态机跳转。
    /// 与 <see cref="AlarmAcknowledgeEvent"/> 的区别：本事件驱动系统级复位，包含硬件清警与回待机位。
    /// </summary>
    public class SystemResetRequestedEvent : PubSubEvent
    {
    }
}
