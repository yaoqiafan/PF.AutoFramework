using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>TCP客户端参数对话框 ViewModel，用法照抄 AxisParamDialogViewModel</summary>
    public class TcpClientParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化 TCP 客户端参数对话框 ViewModel</summary>
        public TcpClientParamDialogViewModel()
        {
            Title = "TCP客户端参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[TCP客户端参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private TcpClientParamViewModel _paramInstance;
        /// <summary>获取或设置正在编辑的参数实例</summary>
        public TcpClientParamViewModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<TcpClientParamViewModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            LogService.Info(
                $"[TCP客户端参数] 用户[{CurrentUserName}] 确认修改 | ServerIp:{ParamInstance.ServerIp} ServerPort:{ParamInstance.ServerPort}",
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

    /// <summary>TCP客户端可编辑参数，供 pf:PropertyGrid 绑定展示</summary>
    public class TcpClientParamViewModel : BindableBase
    {
        private string _serverIp = string.Empty;
        /// <summary>目标服务端IP</summary>
        [Category("TCP客户端参数")]
        [DisplayName("目标服务端IP")]
        [Browsable(true)]
        public string ServerIp { get => _serverIp; set => SetProperty(ref _serverIp, value); }

        private int _serverPort;
        /// <summary>目标服务端端口</summary>
        [Category("TCP客户端参数")]
        [DisplayName("目标服务端端口")]
        [Browsable(true)]
        public int ServerPort { get => _serverPort; set => SetProperty(ref _serverPort, value); }
    }
}
