using PF.Core.Enums;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;

namespace PF.Modules.Parameter.ViewModels.Models
{
    public class UserParamView : BindableBase
    {
        private string _UserName;

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
        public string UserId
        {
            get { return _UserId; }
            set { SetProperty(ref _UserId, value); }
        }

        private UserLevel _Root;
        public UserLevel Root
        {
            get { return _Root; }
            set { SetProperty(ref _Root, value); }
        }

        private string _Password;
        public string Password
        {
            get { return _Password; }
            set { SetProperty(ref _Password, value); }
        }

        private List<string> _accessibleViews;
        [Browsable(false)] // 隐藏该属性，不在参数属性网格中显示
        public List<string> AccessibleViews
        {
            get { return _accessibleViews; }
            set { SetProperty(ref _accessibleViews, value); }
        }
    }
}