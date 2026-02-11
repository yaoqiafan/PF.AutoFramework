using System.ComponentModel;
using System.Configuration;

namespace PF.Modules.Parameter.ViewModels.Models
{
    public class EnumParamView<T>: BindableBase where T : struct, Enum
    {
        private T _Value;
        [DefaultSettingValue("")]
        [CategoryAttribute("参数属性")]
        [DisplayNameAttribute("参数值")]
        [BrowsableAttribute(true)]
        public T Value
        {
            get { return _Value; }
            set { SetProperty(ref _Value, value); }
        }
    }
}
