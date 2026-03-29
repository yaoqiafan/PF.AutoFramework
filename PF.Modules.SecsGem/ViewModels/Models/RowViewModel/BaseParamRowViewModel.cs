using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    /// <summary>
    /// VID / CEID / ReportID / CommandID 行视图模型的公共基类。
    /// </summary>
    public abstract class BaseParamRowViewModel : BindableBase
    {
        private uint _code;
        public uint Code { get => _code; set => SetProperty(ref _code, value); }

        private string _description;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _comment;
        public string Comment { get => _comment; set => SetProperty(ref _comment, value); }
    }
}
