using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>Modbus TCP 主站参数对话框 ViewModel，用法照抄 TcpClientParamDialogViewModel</summary>
    public class ModbusTcpParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化 Modbus TCP 参数对话框 ViewModel</summary>
        public ModbusTcpParamDialogViewModel()
        {
            Title = "Modbus TCP 参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[Modbus TCP 参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private ModbusTcpParamViewModel _paramInstance;
        /// <summary>获取或设置正在编辑的参数实例</summary>
        public ModbusTcpParamViewModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<ModbusTcpParamViewModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            LogService.Info(
                $"[Modbus TCP 参数] 用户[{CurrentUserName}] 确认修改 | IP:{ParamInstance.IP} Port:{ParamInstance.Port} " +
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

    /// <summary>Modbus TCP 主站可编辑参数，供 pf:PropertyGrid 绑定展示</summary>
    public class ModbusTcpParamViewModel : BindableBase
    {
        private string _ip = string.Empty;
        /// <summary>目标从站 IP</summary>
        [Category("Modbus TCP 参数")]
        [DisplayName("目标从站 IP")]
        [Browsable(true)]
        public string IP { get => _ip; set => SetProperty(ref _ip, value); }

        private int _port = 502;
        /// <summary>目标从站端口（Modbus TCP 标准端口 502）</summary>
        [Category("Modbus TCP 参数")]
        [DisplayName("目标从站端口")]
        [Browsable(true)]
        public int Port { get => _port; set => SetProperty(ref _port, value); }

        private int _timeoutMs = 1000;
        /// <summary>单次请求响应超时（毫秒），必须为正数</summary>
        [Category("Modbus TCP 参数")]
        [DisplayName("响应超时(ms)")]
        [Browsable(true)]
        public int TimeoutMs { get => _timeoutMs; set => SetProperty(ref _timeoutMs, value); }

        private bool _autoReconnect = true;
        /// <summary>连接意外断开/连接失败后是否自动重连（主动断开不触发）</summary>
        [Category("Modbus TCP 参数")]
        [DisplayName("断线自动重连")]
        [Browsable(true)]
        public bool AutoReconnect { get => _autoReconnect; set => SetProperty(ref _autoReconnect, value); }

        private int _reconnectIntervalMs = 5000;
        /// <summary>自动重连尝试间隔（毫秒），必须为正数</summary>
        [Category("Modbus TCP 参数")]
        [DisplayName("重连间隔(ms)")]
        [Browsable(true)]
        public int ReconnectIntervalMs { get => _reconnectIntervalMs; set => SetProperty(ref _reconnectIntervalMs, value); }
    }
}
