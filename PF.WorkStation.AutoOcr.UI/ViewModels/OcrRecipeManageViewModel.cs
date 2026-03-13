using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.SecsGem;
using PF.Infrastructure.SecsGem;
using PF.UI.Controls;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.UI.UserControls;
using PF.WorkStation.AutoOcr.UI.ViewModels.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    public class OcrRecipeManageViewModel : RegionViewModelBase
    {
        // 核心修改：改用接口抽象，不依赖具体实现类
        private readonly IRecipeService<OCRRecipeParam> _recipeService;
        private readonly ISecsGemManger _secsGemManger;
        // 通过 Prism 容器注入接口
        public OcrRecipeManageViewModel(IRecipeService<OCRRecipeParam> recipeService,ISecsGemManger secsGemManger)
        {
            _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
            _secsGemManger = secsGemManger ?? throw new ArgumentNullException(nameof(secsGemManger));

            Parameters = new ObservableCollection<OcrRecipeParamEntity>();

            // 初始化命令
            LoadRecipesCommand = new DelegateCommand(ExecuteLoadRecipes);
            NewRecipeCommand = new DelegateCommand(ExecuteNewRecipe);
            SaveRecipeCommand = new DelegateCommand(ExecuteSaveRecipe, CanExecuteSaveRecipe).ObservesProperty(() => SelectedParameter);
            DeleteRecipeCommand = new DelegateCommand(ExecuteDeleteRecipe, CanExecuteDeleteRecipe).ObservesProperty(() => SelectedParameter);

            UPLoadRecipeCommand = new DelegateCommand(ExecuteUPLoadRecipe, CanExecuteUPLoadRecipe).ObservesProperty(() => SelectedParameter);

            ChangeRecipeNameCommand = new DelegateCommand(ExecuteChangeRecipeName, CanExecuteSaveRecipe).ObservesProperty(() => SelectedParameter);

            CloneRecipeCommand = new DelegateCommand(ExecuteCloneRecipe, CanExecuteCloneRecipe).ObservesProperty(() => SelectedParameter);

            // 初始加载数据
            ExecuteLoadRecipes();
        }

        public ObservableCollection<OcrRecipeParamEntity> Parameters { get; set; }

        private OcrRecipeParamEntity _SelectedParameter;
        public OcrRecipeParamEntity SelectedParameter
        {
            get { return _SelectedParameter; }
            set { SetProperty(ref _SelectedParameter, value); }
        }

        #region Commands

        public DelegateCommand LoadRecipesCommand { get; private set; }
        public DelegateCommand NewRecipeCommand { get; private set; }
        public DelegateCommand SaveRecipeCommand { get; private set; }
        public DelegateCommand DeleteRecipeCommand { get; private set; }

        public DelegateCommand UPLoadRecipeCommand { get; private set; }

        public DelegateCommand ChangeRecipeNameCommand { get; private set; }

        public DelegateCommand CloneRecipeCommand { get; private set; }

        private async void ExecuteLoadRecipes()
        {
            Parameters.Clear();

            // 调用接口：获取所有配方
            var recipes = await _recipeService.GetAllRecipes();

            if (recipes != null && recipes.Any())
            {
                foreach (var recipe in recipes)
                {
                    Parameters.Add(MapToEntity(recipe));
                }
                SelectedParameter = Parameters.FirstOrDefault();
            }
        }

        private void ExecuteNewRecipe()
        {
            // 新建配方 Entity
            var newEntity = new OcrRecipeParamEntity()
            {
                RecipeName = $"New_Recipe_{DateTime.Now:HHmmss}",
                CodeCount = 2,
                WafeSize = E_WafeSize._12寸
            };

            Parameters.Add(newEntity);
            SelectedParameter = newEntity;
        }

        private bool CanExecuteSaveRecipe()
        {
            return SelectedParameter != null && !string.IsNullOrWhiteSpace(SelectedParameter.RecipeName);
        }

        private async void ExecuteSaveRecipe()
        {
            if (SelectedParameter == null) return;

            try
            {
                var paramToSave = MapToParam(SelectedParameter);

                if (!paramToSave.Validate( out string err))
                {
                    MessageService.ShowMessage($"参数验证失败: {err}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return ;
                }

                // 调用接口：写入配方参数 (IsCover = true)
                bool isSuccess = await _recipeService.RecipeParamWriteAsync(paramToSave, true);

                if (isSuccess)
                {
                    MessageService.ShowMessage($"配方 [{SelectedParameter.RecipeName}] 已成功保存到本地！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageService.ShowMessage($"配方 [{SelectedParameter.RecipeName}] 保存失败，请查看日志。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"保存发生异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteDeleteRecipe()
        {
            return SelectedParameter != null;
        }

        private async void ExecuteDeleteRecipe()
        {
            if (SelectedParameter == null) return;

            var result = await MessageService.ShowMessageAsync($"确定要彻底删除配方 [{SelectedParameter.RecipeName}] 吗？\n该操作将删除本地文件。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == ButtonResult.Yes)
            {
                // 调用接口：删除指定配方
                bool isSuccess = await _recipeService.RecipeDeleteAsync(SelectedParameter.RecipeName);

                if (isSuccess)
                {
                    Parameters.Remove(SelectedParameter);
                    SelectedParameter = Parameters.FirstOrDefault();
                }
                else
                {
                    // 未落盘的临时数据直接移除
                    Parameters.Remove(SelectedParameter);
                    SelectedParameter = Parameters.FirstOrDefault();
                }
            }
        }


        private async void ExecuteChangeRecipeName()
        {
            if (SelectedParameter == null) return;
           var name = await  MessageService.ShowInputAsync("请输入程式名称：","重命名", " ");

            if (!string.IsNullOrWhiteSpace(name))
            {
                var paramToSave = MapToParam(SelectedParameter);

                if (!paramToSave.Validate(out string err))
                {
                    MessageService.ShowMessage($"参数验证失败: {err}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var isSuccess = await _recipeService.ChangeRecipeNameAsync(paramToSave, name);

                if (isSuccess)
                {
                    ExecuteLoadRecipes();
                    MessageService.ShowMessage($"配方 [{name}] 已成功保存到本地！", "重命名成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageService.ShowMessage($"配方 [{SelectedParameter.RecipeName}] 重命名失败，请查看日志。", "重命名失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageService.ShowMessage($"未输入有效的程式名称！\r\n 重命名失败，请查看日志。", "重命名失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private async void ExecuteUPLoadRecipe()
        {
            if (SelectedParameter == null) return;
            //RecipeUpdateAsync

            var paramToSave = MapToParam(SelectedParameter);

            if (!paramToSave.Validate(out string err))
            {
                MessageService.ShowMessage($"参数验证失败: {err}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var isSuccess = await _recipeService.RecipeUpdateAsync(paramToSave);

            if (isSuccess)
            {
                MessageService.ShowMessage($"程式上传成功！", "上传成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageService.ShowMessage($"程式上传失败！", "上传失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private bool CanExecuteUPLoadRecipe()
        {
            return SelectedParameter != null&& _secsGemManger!=null && _secsGemManger.IsConnected;
        }


        private async void ExecuteCloneRecipe()
        {
            if (SelectedParameter == null) return;
            var name = await MessageService.ShowInputAsync("请输入程式名称：", "复制程式", " ");
            if (string.IsNullOrWhiteSpace(name)) return;

            var paramToSave = MapToParam(SelectedParameter);

            if (!paramToSave.Validate(out string err))
            {
                MessageService.ShowMessage($"参数验证失败: {err}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cloneparam = await _recipeService.CopyRecipeAsync(name, paramToSave);

            var newEntity = MapToEntity(cloneparam);

            Parameters.Add(newEntity);



        }

        private bool CanExecuteCloneRecipe()
        {
            return SelectedParameter != null;
        }


        #endregion

        #region 数据映射 (Mapping)

        private OcrRecipeParamEntity MapToEntity(OCRRecipeParam param)
        {
            if (param == null) return null;

            return new OcrRecipeParamEntity
            {
                RecipeName = param.RecipeName,
                CodeCount = param.CodeCount,
                WafeSize = param.WafeSize,
                OCRRecipeName = param.OCRRecipeName,
                PosX_1 = param._1PosX,
                PosY_1 = param._1PosY,
                PosZ_1 = param._1PosZ,
                PosX_2 = param._1PosX,
                PosY_2 = param._1PosY,
                PosZ_2 = param._1PosZ,
                GuestStartIndex = param.GuestStartIndex,
                GuestLength = param.GuestLength,
                IsOCRCodePate = param.IsOCRCodePate,
                AssociateProduct = param.AssociateProduct != null ? new List<string>(param.AssociateProduct) : new List<string>()
            };
        }

        private OCRRecipeParam MapToParam(OcrRecipeParamEntity entity)
        {
            if (entity == null) return null;

            return new OCRRecipeParam
            {
                RecipeName = entity.RecipeName,
                CodeCount = entity.CodeCount,
                WafeSize = entity.WafeSize,
                OCRRecipeName = entity.OCRRecipeName ?? string.Empty,
                _1PosX = entity.PosX_1,
                _1PosY = entity.PosY_1,
                _1PosZ = entity.PosZ_1,
                _2PosX = entity.PosX_1,
                _2PosY = entity.PosY_1,
                _2PosZ = entity.PosZ_1,
                GuestStartIndex = entity.GuestStartIndex,
                GuestLength = entity.GuestLength,
                IsOCRCodePate = entity.IsOCRCodePate,
                AssociateProduct = entity.AssociateProduct != null ? new List<string>(entity.AssociateProduct) : new List<string>()
            };
        }

        #endregion
    }
}