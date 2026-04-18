using PF.Core.Entities.Identity;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// 用户改变事件
    /// </summary>
    public class UserChangedEvent : PubSubEvent<UserInfo?>
    {
    }
}
