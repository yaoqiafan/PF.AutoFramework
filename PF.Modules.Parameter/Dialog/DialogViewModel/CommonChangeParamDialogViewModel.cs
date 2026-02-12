using PF.UI.Infrastructure.PrismBase;
using PF.CommonTools.Reflection;
using PF.Modules.Parameter.Dialog.Base;
using PF.Modules.Parameter.ViewModels;
using System.Text.Json;

namespace PF.Modules.Parameter.Dialog.DialogViewModel
{
    public class CommonChangeParamDialogViewModel : PFDialogViewModelBase
    {
     
        public CommonChangeParamDialogViewModel()
        {
            Title = "通用参数修改";

            ConfirmCommand = new DelegateCommand(ONParamConfirmed);

            CancelCommand = new DelegateCommand(() => 
            {
                RequestClose.Invoke(new DialogResult()
                {
                    Result = ButtonResult.Cancel,
                });
            });
        }

       

        private ParamItemViewModel _selectedParameter;
        public ParamItemViewModel SelectedParameter
        {
            get => _selectedParameter;
            set => SetProperty(ref _selectedParameter, value);
        }

        private object _ValueInstance;
        public object ValueInstance
        {
            get { return _ValueInstance; }
            set { SetProperty(ref _ValueInstance, value); }
        }




        #region 接口实现
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

            try
            {
                // 从视图提取数据
                var data = ViewFactory.ExtractDataFromView(ValueInstance);

                if (data != null)
                {
                    // 序列化为 JSON
                   var JsonResult = JsonSerializer.Serialize(data);

                    // 更新原始参数
                    if (SelectedParameter != null)
                    {
                        SelectedParameter.JsonValue = JsonResult;
                    }

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存修改失败: {ex.Message}");
            }


            // 创建对话框参数，用于传递回调数据
            DialogParameters paras = new DialogParameters();
            paras.Add("CallBackParamItem", SelectedParameter);

            // 触发对话框关闭请求，返回确认结果和参数
            RequestClose.Invoke(new DialogResult()
            {
                Result = ButtonResult.Yes,
                Parameters = paras
            });

        }


        #endregion


    }
}
