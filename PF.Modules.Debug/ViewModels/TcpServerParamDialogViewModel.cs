using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>TCP服务端参数对话框 ViewModel，用法照抄 AxisParamDialogViewModel</summary>
    public class TcpServerParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化 TCP 服务端参数对话框 ViewModel</summary>
        public TcpServerParamDialogViewModel()
        {
            Title = "TCP服务端参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[TCP服务端参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private TcpServerParamViewModel _paramInstance;
        /// <summary>获取或设置正在编辑的参数实例</summary>
        public TcpServerParamViewModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<TcpServerParamViewModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            LogService.Info(
                $"[TCP服务端参数] 用户[{CurrentUserName}] 确认修改 | IP:{ParamInstance.IP} Port:{ParamInstance.Port} Backlog:{ParamInstance.Backlog}",
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

    /// <summary>TCP服务端可编辑参数，供 pf:PropertyGrid 绑定展示</summary>
    public class TcpServerParamViewModel : BindableBase
    {
        private string _ip = "0.0.0.0";
        /// <summary>监听IP</summary>
        [Category("TCP服务端参数")]
        [DisplayName("监听IP")]
        [Browsable(true)]
        public string IP { get => _ip; set => SetProperty(ref _ip, value); }

        private int _port;
        /// <summary>监听端口</summary>
        [Category("TCP服务端参数")]
        [DisplayName("监听端口")]
        [Browsable(true)]
        public int Port { get => _port; set => SetProperty(ref _port, value); }

        private int _backlog = 10;
        /// <summary>挂起连接队列长度</summary>
        [Category("TCP服务端参数")]
        [DisplayName("挂起连接队列长度")]
        [Browsable(true)]
        public int Backlog { get => _backlog; set => SetProperty(ref _backlog, value); }
    }
}
