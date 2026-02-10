using PF.Core.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    /// <summary>
    /// 参数更改事件参数
    /// </summary>
    public class ParamChangedEventArgs : EventArgs
    {
        public string Category { get; }
        public string ParamName { get; }
        public object NewValue { get; }
        public object? OldValue { get; }
        public UserInfo UserInfo { get; }
        public DateTime ChangeTime { get; }

        public ParamChangedEventArgs(string category, string paramName, object newValue,
            object? oldValue = null, UserInfo? userInfo = null)
        {
            Category = category;
            ParamName = paramName;
            NewValue = newValue;
            OldValue = oldValue;
            UserInfo = userInfo ?? UserInfo.SystemUser;
            ChangeTime = DateTime.Now;
        }
    }
}
