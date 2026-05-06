using Prism.Events;

namespace PF.WorkStation.AutoOcr
{
    /// <summary>
    /// 操作员下料请求事件（工站通知操作员下料时发布）。
    /// 载荷为工位标识字符串，UI 层弹出确认弹窗，操作员确认后释放对应的人工下料完成信号。
    /// </summary>
    public class OperatorUnloadRequestedEvent : PubSubEvent<string>
    {
    }
}
