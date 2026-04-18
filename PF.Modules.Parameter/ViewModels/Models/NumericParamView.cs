using System.ComponentModel;
using System.Configuration;
using System.Numerics;

namespace PF.Modules.Parameter.ViewModels.Models
{
    /// <summary>数值参数视图模型</summary>
    public class NumericParamView<T> : BindableBase
    where T : struct, INumber<T>
    {
        private T _Value;
        [DefaultSettingValue("")]
        [CategoryAttribute("参数属性")]
        [DisplayNameAttribute("参数值")]
        [BrowsableAttribute(true)]
        /// <summary>获取或设置数值</summary>
        public T Value
        {
            get { return _Value; }
            set { SetProperty(ref _Value, value); }
        }
    }
}
