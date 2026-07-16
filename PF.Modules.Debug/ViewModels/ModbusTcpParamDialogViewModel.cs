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
                $"[Modbus TCP 参数] 用户[{CurrentUserName}] 确认修改 | IP:{ParamInstance.IP} Port:{ParamInstance.Port}",
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
    }
}
