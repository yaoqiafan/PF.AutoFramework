using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    /// <summary>
    /// 右侧参数配置面板的 DataGrid 行 ViewModel
    /// </summary>
    public class ParamRowViewModel : BindableBase
    {
        private string _value;

        /// <summary>获取或设置名称</summary>
        public string Name { get; set; }

        /// <summary>获取或设置值</summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>获取或设置数据类型</summary>
        public string DataType { get; set; }

        /// <summary>获取或设置描述</summary>
        public string Description { get; set; }
    }
}
