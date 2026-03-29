namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    public class VidRowViewModel : BaseParamRowViewModel
    {
        private string _dataType;
        public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }

        private string _value;
        public string Value { get => _value; set => SetProperty(ref _value, value); }
    }
}
