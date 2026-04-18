using System.ComponentModel;
using System.Configuration;

namespace PF.Modules.Parameter.ViewModels.Models
{
    /// <summary>字符串参数视图模型</summary>
    public  class StringParamView : BindableBase
    {
        private string _Value;
        /// <summary>获取或设置字符串值</summary>
        [DefaultSettingValue("")]
        [CategoryAttribute("参数属性")]
        [DisplayNameAttribute("参数值")]
        [BrowsableAttribute(true)]
        public string Value
        {
            get { return _Value; }
            set { SetProperty(ref _Value, value); }
        }
    }
}
