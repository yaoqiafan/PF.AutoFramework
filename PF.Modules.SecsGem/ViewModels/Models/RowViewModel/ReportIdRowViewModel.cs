namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    /// <summary>报告ID行视图模型</summary>
    public class ReportIdRowViewModel : BaseParamRowViewModel
    {
        private string _linkVIDs;
        /// <summary>获取或设置关联VID列表</summary>
        public string LinkVIDs { get => _linkVIDs; set => SetProperty(ref _linkVIDs, value); }
    }
}
