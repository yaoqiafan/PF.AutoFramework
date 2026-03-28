using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class VidRowViewModel : BindableBase
    {
        private uint _code;
        public uint Code { get => _code; set => SetProperty(ref _code, value); }

        private string _description;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _dataType;
        public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }

        private string _value;
        public string Value { get => _value; set => SetProperty(ref _value, value); }

        private string _comment;
        public string Comment { get => _comment; set => SetProperty(ref _comment, value); }
    }
}
