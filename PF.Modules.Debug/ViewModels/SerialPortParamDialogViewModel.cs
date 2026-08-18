using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>串口参数对话框 ViewModel，用法照抄 ModbusRtuParamDialogViewModel</summary>
    public class SerialPortParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化串口参数对话框 ViewModel</summary>
        public SerialPortParamDialogViewModel()
        {
            Title = "串口参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[串口参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private SerialPortParamViewModel _paramInstance;
        /// <summary>获取或设置正在编辑的参数实例</summary>
        public SerialPortParamViewModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<SerialPortParamViewModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            LogService.Info(
                $"[串口参数] 用户[{CurrentUserName}] 确认修改 | PortName:{ParamInstance.PortName} BaudRate:{ParamInstance.BaudRate} " +
                $"Parity:{ParamInstance.Parity} DataBits:{ParamInstance.DataBits} StopBits:{ParamInstance.StopBits}",
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

    /// <summary>串口可编辑参数，供 pf:PropertyGrid 绑定展示</summary>
    public class SerialPortParamViewModel : BindableBase
    {
        private string _portName = string.Empty;
        /// <summary>串口名称（如 "COM3"）</summary>
        [Category("串口参数")]
        [DisplayName("串口名称")]
        [Browsable(true)]
        public string PortName { get => _portName; set => SetProperty(ref _portName, value); }

        private int _baudRate = 9600;
        /// <summary>波特率</summary>
        [Category("串口参数")]
        [DisplayName("波特率")]
        [Browsable(true)]
        public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }

        private string _parity = "None";
        /// <summary>校验位，工厂按 System.IO.Ports.Parity 枚举名解析，非法值回退 None</summary>
        [Category("串口参数")]
        [DisplayName("校验位(None/Odd/Even/Mark/Space)")]
        [Browsable(true)]
        public string Parity { get => _parity; set => SetProperty(ref _parity, value); }

        private int _dataBits = 8;
        /// <summary>数据位（5~8）</summary>
        [Category("串口参数")]
        [DisplayName("数据位(5~8)")]
        [Browsable(true)]
        public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }

        private string _stopBits = "One";
        /// <summary>停止位，工厂按 System.IO.Ports.StopBits 枚举名解析，非法值回退 One</summary>
        [Category("串口参数")]
        [DisplayName("停止位(One/Two/OnePointFive)")]
        [Browsable(true)]
        public string StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }
    }
}
