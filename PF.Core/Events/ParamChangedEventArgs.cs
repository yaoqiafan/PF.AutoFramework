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
        /// <summary>参数分类</summary>
        public string Category { get; }
        /// <summary>参数名称</summary>
        public string ParamName { get; }
        /// <summary>新值</summary>
        public object NewValue { get; }
        /// <summary>旧值</summary>
        public object? OldValue { get; }
        /// <summary>操作用户信息</summary>
        public UserInfo UserInfo { get; }
        /// <summary>变更时间</summary>
        public DateTime ChangeTime { get; }

        /// <summary>
        /// 初始化参数变更事件参数
        /// </summary>
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
