namespace PF.Modules.SecsGem.ViewModels
{
    public class CeidRowViewModel : BaseParamRowViewModel
    {
        private string _linkReportIDs;
        public string LinkReportIDs { get => _linkReportIDs; set => SetProperty(ref _linkReportIDs, value); }
    }
}
