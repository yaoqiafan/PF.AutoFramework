using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>Modbus RTU 主站参数对话框 ViewModel，用法照抄 TcpClientParamDialogViewModel</summary>
    public class ModbusRtuParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化 Modbus RTU 参数对话框 ViewModel</summary>
        public ModbusRtuParamDialogViewModel()
        {
            Title = "Modbus RTU 参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[Modbus RTU 参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private ModbusRtuParamViewModel _paramInstance;
        /// <summary>获取或设置正在编辑的参数实例</summary>
        public ModbusRtuParamViewModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<ModbusRtuParamViewModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            LogService.Info(
                $"[Modbus RTU 参数] 用户[{CurrentUserName}] 确认修改 | PortName:{ParamInstance.PortName} BaudRate:{ParamInstance.BaudRate}",
                "操作日志");

            var paras = new DialogParameters();
            paras.Add("CallBackParamItem", ParamInstance);

            RequestClose.Invoke(new DialogResult
            {
                Result = ButtonResult.Yes,
                Parameters = paras
            });
        }
    }

    /// <summary>Modbus RTU 主站可编辑参数，供 pf:PropertyGrid 绑定展示</summary>
    public class ModbusRtuParamViewModel : BindableBase
    {
        private string _portName = string.Empty;
        /// <summary>串口名称（如 "COM3"）</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("串口名称")]
        [Browsable(true)]
        public string PortName { get => _portName; set => SetProperty(ref _portName, value); }

        private int _baudRate = 9600;
        /// <summary>波特率</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("波特率")]
        [Browsable(true)]
        public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }
    }
}
