using System.ComponentModel;
using System.Configuration;

namespace PF.Common.Param.ViewModels.Models
{
    public  class StringParamView : BindableBase
    {
        private string _Value;
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
