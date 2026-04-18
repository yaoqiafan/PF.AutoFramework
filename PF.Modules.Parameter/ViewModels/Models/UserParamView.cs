using PF.Core.Enums;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;

namespace PF.Modules.Parameter.ViewModels.Models
{
    /// <summary>用户参数视图</summary>
    public class UserParamView : BindableBase
    {
        private string _UserName;

        /// <summary>获取或设置用户名</summary>
        [DefaultSettingValue("")]
        [CategoryAttribute("用户名称")]
        [DisplayNameAttribute("参数值")]
        [BrowsableAttribute(true)]
        public string UserName
        {
            get { return _UserName; }
            set { SetProperty(ref _UserName, value); }
        }


        private string _UserId;
        /// <summary>获取或设置用户ID</summary>
        public string UserId
        {
            get { return _UserId; }
            set { SetProperty(ref _UserId, value); }
        }

        private UserLevel _Root;
        /// <summary>获取或设置用户等级</summary>
        public UserLevel Root
        {
            get { return _Root; }
            set { SetProperty(ref _Root, value); }
        }

        private string _Password;
        /// <summary>获取或设置密码</summary>
        public string Password
        {
            get { return _Password; }
            set { SetProperty(ref _Password, value); }
        }

        private List<string> _accessibleViews;
        /// <summary>获取或设置可访问视图列表</summary>
        [Browsable(false)] // 隐藏该属性，不在参数属性网格中显示
        public List<string> AccessibleViews
        {
            get { return _accessibleViews; }
            set { SetProperty(ref _accessibleViews, value); }
        }
    }
}