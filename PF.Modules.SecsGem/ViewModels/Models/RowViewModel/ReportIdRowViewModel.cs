namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    public class ReportIdRowViewModel : BaseParamRowViewModel
    {
        private string _linkVIDs;
        public string LinkVIDs { get => _linkVIDs; set => SetProperty(ref _linkVIDs, value); }
    }
}
