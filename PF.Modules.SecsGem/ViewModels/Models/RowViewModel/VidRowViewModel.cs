namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    /// <summary>VID行视图模型</summary>
    public class VidRowViewModel : BaseParamRowViewModel
    {
        private string _dataType;
        /// <summary>获取或设置数据类型</summary>
        public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }

        private string _value;
        /// <summary>获取或设置值</summary>
        public string Value { get => _value; set => SetProperty(ref _value, value); }
    }
}
