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
                $"[Modbus RTU 参数] 用户[{CurrentUserName}] 确认修改 | PortName:{ParamInstance.PortName} BaudRate:{ParamInstance.BaudRate} " +
                $"Parity:{ParamInstance.Parity} DataBits:{ParamInstance.DataBits} StopBits:{ParamInstance.StopBits} " +
                $"TimeoutMs:{ParamInstance.TimeoutMs} AutoReconnect:{ParamInstance.AutoReconnect} ReconnectIntervalMs:{ParamInstance.ReconnectIntervalMs}",
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

        private string _parity = "None";
        /// <summary>校验位，工厂按 System.IO.Ports.Parity 枚举名解析，非法值回退 None</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("校验位(None/Odd/Even/Mark/Space)")]
        [Browsable(true)]
        public string Parity { get => _parity; set => SetProperty(ref _parity, value); }

        private int _dataBits = 8;
        /// <summary>数据位（5~8）</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("数据位(5~8)")]
        [Browsable(true)]
        public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }

        private string _stopBits = "One";
        /// <summary>停止位，工厂按 System.IO.Ports.StopBits 枚举名解析，非法值回退 One</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("停止位(One/Two/OnePointFive)")]
        [Browsable(true)]
        public string StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }

        private int _timeoutMs = 1000;
        /// <summary>单次请求响应超时（毫秒），必须为正数</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("响应超时(ms)")]
        [Browsable(true)]
        public int TimeoutMs { get => _timeoutMs; set => SetProperty(ref _timeoutMs, value); }

        private bool _autoReconnect = true;
        /// <summary>串口意外失效后是否自动重连（主动关闭不触发）</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("断线自动重连")]
        [Browsable(true)]
        public bool AutoReconnect { get => _autoReconnect; set => SetProperty(ref _autoReconnect, value); }

        private int _reconnectIntervalMs = 5000;
        /// <summary>自动重连尝试间隔（毫秒），必须为正数</summary>
        [Category("Modbus RTU 参数")]
        [DisplayName("重连间隔(ms)")]
        [Browsable(true)]
        public int ReconnectIntervalMs { get => _reconnectIntervalMs; set => SetProperty(ref _reconnectIntervalMs, value); }
    }
}
