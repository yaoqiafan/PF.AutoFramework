using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// 右侧参数配置面板的 DataGrid 行 ViewModel
    /// </summary>
    public class ParamRowViewModel : BindableBase
    {
        private string _value;

        public string Name { get; set; }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public string DataType { get; set; }

        public string Description { get; set; }
    }
}
