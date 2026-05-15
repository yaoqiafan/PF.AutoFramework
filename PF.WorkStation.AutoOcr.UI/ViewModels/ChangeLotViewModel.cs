using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Recipe;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.CostParam;
using PF.Workstation.AutoOcr.CostParam;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        /// <summary>选择程式（仅超级用户可见，调试用）</summary>
        public string Recipe
        {
            get => _recipe;
            set => SetProperty(ref _recipe, value);
        }

        private bool _isSuperUser;
        /// <summary>是否为超级用户，控制选择程式行的可见性</summary>
        public bool IsSuperUser
        {
            get => _isSuperUser;
            set => SetProperty(ref _isSuperUser, value);
        }

        /// <summary>可选配方名称列表</summary>
        public List<string> RecipeNames { get; }

        // ── 层检测模式 ──────────────────────────────────────────────────

        private E_LayerProcessMode _layerMode = E_LayerProcessMode.全做;

        /// <summary>RadioButton 双向绑定：全做模式</summary>
        public bool IsLayerModeAll
        {
            get => _layerMode == E_LayerProcessMode.全做;
            set { if (value) SetLayerModeInternal(E_LayerProcessMode.全做); }
        }

        /// <summary>RadioButton 双向绑定：指定层模式</summary>
        public bool IsLayerModeSpecified
        {
            get => _layerMode == E_LayerProcessMode.指定层;
            set { if (value) SetLayerModeInternal(E_LayerProcessMode.指定层); }
        }

        /// <summary>控制层选择面板的可见性</summary>
        public bool IsSpecifiedLayerPanelVisible => _layerMode == E_LayerProcessMode.指定层;

        private void SetLayerModeInternal(E_LayerProcessMode mode)
        {
            _layerMode = mode;
            RaisePropertyChanged(nameof(IsLayerModeAll));
            RaisePropertyChanged(nameof(IsLayerModeSpecified));
            RaisePropertyChanged(nameof(IsSpecifiedLayerPanelVisible));
        }

        /// <summary>13个槽位勾选项（与 CassetteSlotCount = 13 对齐，索引 0-based）</summary>
        public ObservableCollection<LayerSelectionItem> SpecifiedLayerItems { get; } =
            new(Enumerable.Range(0, 13).Select(i => new LayerSelectionItem { LayerIndex = i }));

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
            if (parameters.TryGetValue("InitialLayerMode", out E_LayerProcessMode mode))
                SetLayerModeInternal(mode);

            if (parameters.TryGetValue("InitialSpecifiedLayers", out List<int> layers) && layers != null)
                foreach (var item in SpecifiedLayerItems)
                    item.IsSelected = layers.Contains(item.LayerIndex);
        }

        public override void OnDialogClosed()
        {
        }

        #endregion

        private void OK()
        {
            if (_layerMode == E_LayerProcessMode.指定层 && !SpecifiedLayerItems.Any(x => x.IsSelected))
                return;

            var param = new DialogParameters
            {
                { "Userid", UserId },
                { "Lotid", LotId },
                { "LayerMode", _layerMode },
                { "SpecifiedLayers", SpecifiedLayerItems.Where(x => x.IsSelected).Select(x => x.LayerIndex).ToList() }
            };
            if (IsSuperUser)
                param.Add("Recipe", Recipe);
            RequestClose.Invoke(param, ButtonResult.OK);
        }

        private void NG()
        {
            RequestClose.Invoke(ButtonResult.Cancel);
        }
    }

    /// <summary>层选择项：用于 ChangeLotView 中指定层模式的复选框列表绑定</summary>
    public class LayerSelectionItem : INotifyPropertyChanged
    {
        /// <summary>0-based 物理层索引，与 _layersToProcess 对齐</summary>
        public int LayerIndex { get; init; }

        /// <summary>1-based 界面显示编号</summary>
        public int DisplayIndex => LayerIndex + 1;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
