using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>FileTransfer 通道参数对话框 ViewModel，用法照抄 AxisParamDialogViewModel</summary>
    public class FileTransferParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化 FileTransfer 通道参数对话框 ViewModel</summary>
        public FileTransferParamDialogViewModel()
        {
            Title = "FileTransfer通道参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[FileTransfer参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private FileTransferParamViewModel _paramInstance;
        /// <summary>获取或设置正在编辑的参数实例</summary>
        public FileTransferParamViewModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<FileTransferParamViewModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            LogService.Info(
                $"[FileTransfer参数] 用户[{CurrentUserName}] 确认修改 | Role:{ParamInstance.RoleText} " +
                $"LaneId:{ParamInstance.LaneId} LocalIp:{ParamInstance.LocalIp} Port:{ParamInstance.Port} RemoteIp:{ParamInstance.RemoteIp}",
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

    /// <summary>
    /// FileTransfer 通道可编辑参数，供 pf:PropertyGrid 绑定展示。
    /// 简化为单 Lane 编辑（默认配置目前也都只有一条 Lane），多 Lane 场景暂不在此弹窗覆盖范围内。
    /// </summary>
    public class FileTransferParamViewModel : BindableBase
    {
        private string _roleText = "Server";
        /// <summary>角色，填 Server 或 Client</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("角色(Server/Client)")]
        [Browsable(true)]
        public string RoleText { get => _roleText; set => SetProperty(ref _roleText, value); }

        private int _laneId;
        /// <summary>Lane编号</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("LaneId")]
        [Browsable(true)]
        public int LaneId { get => _laneId; set => SetProperty(ref _laneId, value); }

        private string _localIp = "0.0.0.0";
        /// <summary>本地绑定IP（Server=监听网口IP；Client=出口网口IP）</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("本地IP")]
        [Browsable(true)]
        public string LocalIp { get => _localIp; set => SetProperty(ref _localIp, value); }

        private int _port;
        /// <summary>端口</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("端口")]
        [Browsable(true)]
        public int Port { get => _port; set => SetProperty(ref _port, value); }

        private string _remoteIp = string.Empty;
        /// <summary>对端IP，仅 Client 角色需要</summary>
        [Category("FileTransfer通道参数")]
        [DisplayName("对端IP(仅Client需要)")]
        [Browsable(true)]
        public string RemoteIp { get => _remoteIp; set => SetProperty(ref _remoteIp, value); }
    }
}
