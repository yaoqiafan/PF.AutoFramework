namespace PF.Modules.SecsGem.ViewModels.Models.RowViewModel
{
    /// <summary>命令ID行视图模型</summary>
    public class CommandIdRowViewModel : BaseParamRowViewModel
    {
        private string _rcmd;
        /// <summary>获取或设置远程命令</summary>
        public string RCMD { get => _rcmd; set => SetProperty(ref _rcmd, value); }

        private string _linkVIDs;
        /// <summary>获取或设置关联VID列表</summary>
        public string LinkVIDs { get => _linkVIDs; set => SetProperty(ref _linkVIDs, value); }
    }
}
