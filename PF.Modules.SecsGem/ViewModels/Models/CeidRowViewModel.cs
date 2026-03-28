using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class CeidRowViewModel : BindableBase
    {
        private uint _code;
        public uint Code { get => _code; set => SetProperty(ref _code, value); }

        private string _description;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _linkReportIDs;
        public string LinkReportIDs { get => _linkReportIDs; set => SetProperty(ref _linkReportIDs, value); }

        private string _comment;
        public string Comment { get => _comment; set => SetProperty(ref _comment, value); }
    }
}
