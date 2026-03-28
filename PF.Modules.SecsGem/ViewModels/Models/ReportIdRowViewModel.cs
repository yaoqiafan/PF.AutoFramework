using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class ReportIdRowViewModel : BindableBase
    {
        private uint _code;
        public uint Code { get => _code; set => SetProperty(ref _code, value); }

        private string _description;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _linkVIDs;
        public string LinkVIDs { get => _linkVIDs; set => SetProperty(ref _linkVIDs, value); }

        private string _comment;
        public string Comment { get => _comment; set => SetProperty(ref _comment, value); }
    }
}
