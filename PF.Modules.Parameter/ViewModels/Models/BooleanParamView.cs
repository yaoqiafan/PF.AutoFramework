using System.ComponentModel;
using System.Configuration;

namespace PF.Modules.Parameter.ViewModels.Models
{
    public class BooleanParamView:BindableBase
    {
        private bool _Value;
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
