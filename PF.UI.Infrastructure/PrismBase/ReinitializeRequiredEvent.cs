using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 工位屏蔽参数变更后需要重新初始化设备的 Prism 通知事件。
    /// 由 App.xaml.cs 桥接 <see cref="PF.Core.Interfaces.Station.IMasterController.ReinitializationRequired"/> 原生事件后发布。
    /// </summary>
    public class ReinitializeRequiredEvent : PubSubEvent
    {
    }
}
