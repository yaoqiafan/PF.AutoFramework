using PF.Core.Enums;
using Prism.Events;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 机台状态变更 Prism 事件（载体：新状态枚举值）。
    /// 由 App.xaml.cs 桥接 <see cref="PF.Core.Interfaces.Station.IMasterController.MasterStateChanged"/> 原生事件后发布，
    /// 供 TowerLightManager 等纯 UI/服务层订阅，无需直接引用 IMasterController。
    /// </summary>
    public class MachineStateChangedEvent : PubSubEvent<MachineState>
    {
    }
}
