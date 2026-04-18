namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    /// <summary>CEID行视图模型</summary>
    public class CeidRowViewModel : BaseParamRowViewModel
    {
        private string _linkReportIDs;
        /// <summary>获取或设置关联报告ID列表</summary>
        public string LinkReportIDs { get => _linkReportIDs; set => SetProperty(ref _linkReportIDs, value); }
    }
}
