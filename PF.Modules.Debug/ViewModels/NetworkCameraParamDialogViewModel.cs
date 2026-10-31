using PF.UI.Infrastructure.PrismBase;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 网络摄像机参数修改对话框 ViewModel（用法照抄 SerialPortParamDialogViewModel）。
    ///
    /// <para>从单台实例调试页顶部「参数设置」按钮弹出。字段与分组参数页一致，
    /// 确认后由页面负责写库，<b>不自动重载</b>——全量重载会断开所有设备。</para>
    /// </summary>
    public class NetworkCameraParamDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化网络摄像机参数对话框 ViewModel。</summary>
        public NetworkCameraParamDialogViewModel()
        {
            Title = "网络摄像机参数修改";

            ConfirmCommand = new DelegateCommand(OnConfirmed);

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info($"[网络摄像机参数] 用户[{CurrentUserName}] 取消修改", "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
            });
        }

        private NetworkCameraParamEditModel _paramInstance = new();
        /// <summary>获取或设置正在编辑的参数实例。</summary>
        public NetworkCameraParamEditModel ParamInstance
        {
            get => _paramInstance;
            set => SetProperty(ref _paramInstance, value);
        }

        /// <inheritdoc/>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
                ParamInstance = parameters.GetValue<NetworkCameraParamEditModel>("Data");
        }

        private void OnConfirmed()
        {
            if (ParamInstance == null) return;

            // 密码不写进操作日志
            LogService.Info(
                $"[网络摄像机参数] 用户[{CurrentUserName}] 确认修改 | 设备:{ParamInstance.DeviceName} " +
                $"IP:{ParamInstance.Ip} 端口:{ParamInstance.Port} 用户名:{ParamInstance.UserName} " +
                $"通道:{ParamInstance.Channel} 缓冲:{ParamInstance.BufferMb}MB",
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
    /// 网络摄像机可编辑参数，供 pf:PropertyGrid 绑定展示。
    /// <para>与 <see cref="ViewModels.NetworkCameraParamCardViewModel"/> 字段一致，
    /// 但这是弹窗专用的轻量模型（PropertyGrid 按 [Category]/[DisplayName] 反射排版）。</para>
    /// </summary>
    public class NetworkCameraParamEditModel : BindableBase
    {
        private string _deviceName = string.Empty;
        /// <summary>设备显示名称。</summary>
        [Category("设备")]
        [DisplayName("设备名称")]
        [Browsable(true)]
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private bool _isEnabled = true;
        /// <summary>是否启用。</summary>
        [Category("设备")]
        [DisplayName("启用")]
        [Browsable(true)]
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private bool _isSimulated;
        /// <summary>是否仿真模式。</summary>
        [Category("设备")]
        [DisplayName("仿真模式")]
        [Browsable(true)]
        public bool IsSimulated { get => _isSimulated; set => SetProperty(ref _isSimulated, value); }

        private string _ip = string.Empty;
        /// <summary>IP 地址（不含端口）。</summary>
        [Category("连接参数")]
        [DisplayName("IP 地址")]
        [Browsable(true)]
        public string Ip { get => _ip; set => SetProperty(ref _ip, value); }

        private string _port = "8000";
        /// <summary>SDK 私有协议端口（海康默认 8000）。</summary>
        [Category("连接参数")]
        [DisplayName("端口")]
        [Browsable(true)]
        public string Port { get => _port; set => SetProperty(ref _port, value); }

        private string _userName = "admin";
        /// <summary>登录用户名。</summary>
        [Category("连接参数")]
        [DisplayName("用户名")]
        [Browsable(true)]
        public string UserName { get => _userName; set => SetProperty(ref _userName, value); }

        private string _password = string.Empty;
        /// <summary>登录密码。</summary>
        [Category("连接参数")]
        [DisplayName("密码")]
        [Browsable(true)]
        public string Password { get => _password; set => SetProperty(ref _password, value); }

        private string _channel = "0";
        /// <summary>通道号（0 = 登录后自动选第一个可用通道）。</summary>
        [Category("采集")]
        [DisplayName("通道号(0=自动)")]
        [Browsable(true)]
        public string Channel { get => _channel; set => SetProperty(ref _channel, value); }

        private string _bufferMb = "8";
        /// <summary>抓图缓冲区起始大小（MB），不足时按错误码 43 自动翻倍。</summary>
        [Category("采集")]
        [DisplayName("抓图缓冲区(MB)")]
        [Browsable(true)]
        public string BufferMb { get => _bufferMb; set => SetProperty(ref _bufferMb, value); }

        private string _saveFormat = "Jpeg";
        /// <summary>存图格式：Bmp / Jpeg / Tiff / Png，留空或填错按 Jpeg。</summary>
        [Category("存图")]
        [DisplayName("存图格式")]
        [Browsable(true)]
        public string SaveFormat { get => _saveFormat; set => SetProperty(ref _saveFormat, value); }

        private string _saveDirectory = string.Empty;
        /// <summary>存图目录，留空则用 {配置目录}\NetworkCamera\{设备ID}。</summary>
        [Category("存图")]
        [DisplayName("存图目录")]
        [Browsable(true)]
        public string SaveDirectory { get => _saveDirectory; set => SetProperty(ref _saveDirectory, value); }
    }
}
