using PF.UI.Infrastructure.PrismBase;
using PF.CommonTools.Reflection;
using PF.Core.Entities.Hardware;
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
                        // 此前只写调试输出然后照常打开：编辑视图按空白默认值创建，用户点确认就把原参数整个覆盖成默认值
                        // （例如一个用户账号的密码、权限全部被清空）。解析不了就不让在这里改，原值保持不动。
                        LogService.Error($"[参数修改] 参数「{paramItem.Name}」的值无法按 {SelectedParameter.TypeFullName} 解析，已拒绝编辑", "操作日志", ex);
                        MessageService.ShowMessage(
                            $"参数「{paramItem.Name}」的现有值无法解析（{ex.Message}）。\n\n" +
                            "为避免用默认值覆盖原数据，此处不允许编辑。请联系工程人员检查该参数。",
                            "无法修改", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        ValueInstance = null;
                        return;
                    }
                }

                _originalData = data;

                // 创建视图实例并绑定数据
                ValueInstance = ViewFactory.GetViewInstanceWithData(paramType, data);
            }
        }

        // 打开对话框时反序列化出的原值，确认时用来补回编辑视图不认识的字段
        private object _originalData;

        private void ONParamConfirmed()
        {
            if (ValueInstance == null)
            {
                // 未注册编辑视图（或原值反序列化失败）：此前点确认毫无反应，看起来像按钮坏了
                MessageService.ShowMessage(
                    $"参数「{SelectedParameter?.Name}」（{SelectedParameter?.TypeFullName}）没有可用的编辑界面，无法在此修改。",
                    "无法修改", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            string oldValue = SelectedParameter?.JsonValue ?? string.Empty;

            try
            {
                var data = ViewFactory.ExtractDataFromView(ValueInstance);
                if (data == null)
                {
                    // 此前提取失败也按"确认"关闭，界面标记为有修改，实际什么都没改
                    MessageService.ShowMessage("未能从编辑界面取得参数值，未做任何修改。", "无法修改",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                PreserveUnknownConnectionParameters(data);

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
            catch (Exception ex)
            {
                // 失败时保持对话框打开，不能按"确认"关闭（否则调用方会把它当成已修改）
                LogService.Error($"[参数修改] 用户[{CurrentUserName}] 修改参数「{SelectedParameter?.Name}」失败", "操作日志", ex);
                MessageService.ShowMessage($"修改失败：{ex.Message}", "无法修改",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            DialogParameters paras = new DialogParameters();
            paras.Add("CallBackParamItem", SelectedParameter);

            RequestClose.Invoke(new DialogResult()
            {
                Result = ButtonResult.Yes,
                Parameters = paras
            });

        }


        /// <summary>
        /// 硬件配置的编辑视图只认识自己那几个连接参数键（如轴视图只有 AxisIndex），
        /// 各 Mapper 提取时会新建 ConnectionParameters，原配置里视图不编辑的键（AxisParam、Channels 等）
        /// 会被整个丢掉——在参数页改一下轴号，调试页配好的轴参数就没了。
        /// 这里把"原来有、提取结果里没有"的键原样补回；视图编辑过的键（Mapper 总会写出，即使是空串）不受影响。
        /// </summary>
        private void PreserveUnknownConnectionParameters(object extracted)
        {
            if (extracted is not HardwareConfig newConfig || _originalData is not HardwareConfig oldConfig)
                return;
            if (oldConfig.ConnectionParameters == null || oldConfig.ConnectionParameters.Count == 0)
                return;

            newConfig.ConnectionParameters ??= new Dictionary<string, string>();
            foreach (var kv in oldConfig.ConnectionParameters)
            {
                if (!newConfig.ConnectionParameters.ContainsKey(kv.Key))
                    newConfig.ConnectionParameters[kv.Key] = kv.Value;
            }
        }

        #endregion


    }
}
