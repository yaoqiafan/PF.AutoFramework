using System.ComponentModel;
using System.Configuration;
using System.Numerics;

namespace PF.Modules.Parameter.ViewModels.Models
{
    public class NumericParamView<T> : BindableBase
    where T : struct, INumber<T>
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
