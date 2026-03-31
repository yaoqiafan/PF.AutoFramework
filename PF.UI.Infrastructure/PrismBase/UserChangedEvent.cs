using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.PrismBase
{
    using PF.Core.Entities.Identity;
    using Prism.Events;

    public class UserChangedEvent : PubSubEvent<UserInfo?>
    {
    }
}
