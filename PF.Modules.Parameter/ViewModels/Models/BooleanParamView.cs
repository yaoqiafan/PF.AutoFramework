using System.ComponentModel;
using System.Configuration;

namespace PF.Modules.Parameter.ViewModels.Models
{
    /// <summary>布尔参数视图模型</summary>
    public class BooleanParamView:BindableBase
    {
        private bool _Value;
        /// <summary>获取或设置布尔值</summary>
        [DefaultSettingValue("")]
        [CategoryAttribute("参数属性")]
        [DisplayNameAttribute("参数值")]
        [BrowsableAttribute(true)]
        public bool Value
        {
            get { return _Value; }
            set { SetProperty(ref _Value, value); }
        }
    }
}
