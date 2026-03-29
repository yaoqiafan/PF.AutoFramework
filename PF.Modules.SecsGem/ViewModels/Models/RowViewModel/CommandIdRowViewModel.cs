namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    public class CommandIdRowViewModel : BaseParamRowViewModel
    {
        private string _rcmd;
        public string RCMD { get => _rcmd; set => SetProperty(ref _rcmd, value); }

        private string _linkVIDs;
        public string LinkVIDs { get => _linkVIDs; set => SetProperty(ref _linkVIDs, value); }
    }
}
