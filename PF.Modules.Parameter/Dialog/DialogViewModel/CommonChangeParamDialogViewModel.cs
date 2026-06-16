using PF.UI.Infrastructure.PrismBase;
using PF.CommonTools.Reflection;
using PF.Modules.Parameter.Dialog.Base;
using PF.Modules.Parameter.ViewModels;
using System.Text.Json;

namespace PF.Modules.Parameter.Dialog.DialogViewModel
{
    /// <summary>通用参数修改对话框 ViewModel</summary>
    public class CommonChangeParamDialogViewModel : PFDialogViewModelBase
    {
     
        /// <summary>初始化通用参数修改对话框 ViewModel</summary>
        public CommonChangeParamDialogViewModel()
        {
            Title = "通用参数修改";

            ConfirmCommand = new DelegateCommand(ONParamConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[参数修改] 用户[{CurrentUserName}] 取消修改参数「{SelectedParameter?.Name}」", "操作日志");
                RequestClose.Invoke(new DialogResult()
                {
                    Result = ButtonResult.Cancel,
                });
            });
        }

       

        private ParamItemViewModel _selectedParameter;
        /// <summary>获取或设置选中的参数项</summary>
        public ParamItemViewModel SelectedParameter
        {
            get => _selectedParameter;
            set => SetProperty(ref _selectedParameter, value);
        }

        private object _ValueInstance;
        /// <summary>获取或设置参数值实例</summary>
        public object ValueInstance
        {
            get { return _ValueInstance; }
            set { SetProperty(ref _ValueInstance, value); }
        }




        #region 接口实现
        /// <summary>对话框打开时加载参数数据</summary>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
            {
                var paramItem = parameters.GetValue<ParamItemViewModel>("Data");
                Title = $"参数：{paramItem.Name} 修改";
                SelectedParameter = paramItem;

                // 获取参数类型
                Type paramType = TypeClassExtensions.GetTypeFromAnyAssembly(SelectedParameter.TypeFullName);
                if (paramType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"无法解析类型: {SelectedParameter.TypeFullName}");
                    return;
                }

                // 反序列化 JSON 数据
                object data = null;
                if (!string.IsNullOrEmpty(SelectedParameter.JsonValue))
                {
                    try
                    {
                        data = JsonSerializer.Deserialize(SelectedParameter.JsonValue, paramType);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"反序列化 JSON 失败: {ex.Message}");
                    }
                }

                // 创建视图实例并绑定数据
                ValueInstance = ViewFactory.GetViewInstanceWithData(paramType, data);
            }
        }


        private void ONParamConfirmed()
        {
            if (ValueInstance == null)
                return;

            string oldValue = SelectedParameter?.JsonValue ?? string.Empty;

            try
            {
                var data = ViewFactory.ExtractDataFromView(ValueInstance);

                if (data != null)
                {
                    var JsonResult = JsonSerializer.Serialize(data);

                    if (SelectedParameter != null)
                    {
                        SelectedParameter.JsonValue = JsonResult;
                    }

                    LogService.Info(
                        $"[参数修改] 用户[{CurrentUserName}] 确认修改参数「{SelectedParameter?.Name}」 | " +
                        $"类型：{SelectedParameter?.TypeFullName} | 旧值：{oldValue} | 新值：{JsonResult}",
                        "操作日志");
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"[参数修改] 用户[{CurrentUserName}] 修改参数「{SelectedParameter?.Name}」失败", "操作日志", ex);
                System.Diagnostics.Debug.WriteLine($"保存修改失败: {ex.Message}");
            }

            DialogParameters paras = new DialogParameters();
            paras.Add("CallBackParamItem", SelectedParameter);

            RequestClose.Invoke(new DialogResult()
            {
                Result = ButtonResult.Yes,
                Parameters = paras
            });

        }


        #endregion


    }
}
