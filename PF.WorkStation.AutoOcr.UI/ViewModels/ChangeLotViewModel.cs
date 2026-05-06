using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Recipe;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.CostParam;
using System.Collections.Generic;
using System.Windows.Input;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// ChangeLotViewModel
    /// </summary>
    public class ChangeLotViewModel : PFDialogViewModelBase
    {
        private readonly IUserService _userService;

        #region 参数

        private string _userid = "";
        public string UserId
        {
            get => _userid;
            set => SetProperty(ref _userid, value);
        }

        private string _lotid = "";
        public string LotId
        {
            get => _lotid;
            set => SetProperty(ref _lotid, value);
        }

        private string _recipe = "";
        /// <summary>
        /// 选择程式（仅超级用户可见，调试用）
        /// </summary>
        public string Recipe
        {
            get => _recipe;
            set => SetProperty(ref _recipe, value);
        }

        private bool _isSuperUser;
        /// <summary>
        /// 是否为超级用户，控制选择程式行的可见性
        /// </summary>
        public bool IsSuperUser
        {
            get => _isSuperUser;
            set => SetProperty(ref _isSuperUser, value);
        }

        /// <summary>
        /// 可选配方名称列表
        /// </summary>
        public List<string> RecipeNames { get; }

        #endregion

        #region 构造函数

        public ChangeLotViewModel(IUserService userService, IRecipeService<OCRRecipeParam> recipeService)
        {
            _userService = userService;
            IsSuperUser = _userService.IsAuthorized(UserLevel.SuperUser);
            RecipeNames = recipeService.RecipeNames;

            Title = "输入工单工号";
            ConfirmCommand = new DelegateCommand(OK);
            CancelCommand = new DelegateCommand(NG);
        }

        #endregion

        #region Dialog 生命周期

        public override void OnDialogOpened(IDialogParameters parameters)
        {
        }

        public override void OnDialogClosed()
        {
        }

        #endregion

        private void OK()
        {
            var param = new DialogParameters { { "Userid", this.UserId }, { "Lotid", this.LotId } };
            if (IsSuperUser)
                param.Add("Recipe", this.Recipe);
            RequestClose.Invoke(param, ButtonResult.OK);
        }

        private void NG()
        {
            RequestClose.Invoke(ButtonResult.Cancel);
        }
    }
}
