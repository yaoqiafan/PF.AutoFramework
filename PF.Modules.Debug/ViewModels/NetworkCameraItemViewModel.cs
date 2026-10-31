using PF.Core.Entities.Hardware;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera;
using Prism.Mvvm;
using System.ComponentModel;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 网络摄像机调试页左侧列表的单台摄像机项：设备状态转发 + 连接参数编辑态。
    /// <para>编辑字段从 <see cref="HardwareConfig.ConnectionParameters"/> 初始化，
    /// 保存动作在页面 ViewModel 里（需要 IHardwareManagerService）。</para>
    /// </summary>
    public class NetworkCameraItemViewModel : BindableBase, IDisposable
    {
        private readonly INotifyPropertyChanged? _notifier;

        /// <summary>以设备实例与其配置创建列表项。</summary>
        public NetworkCameraItemViewModel(INetworkCamera camera, HardwareConfig? config)
        {
            Camera = camera;
            _notifier = camera as INotifyPropertyChanged;
            if (_notifier != null)
                _notifier.PropertyChanged += OnCameraPropertyChanged;

            LoadEditFields(config);
        }

        /// <summary>设备实例。</summary>
        public INetworkCamera Camera { get; }

        /// <summary>设备显示名。</summary>
        public string Name => Camera.DeviceName;

        /// <summary>是否仿真模式。</summary>
        public bool IsSimulated => Camera.IsSimulated;

        /// <summary>是否已连接（已登录）。</summary>
        public bool IsConnected => Camera.IsConnected;

        /// <summary>列表项连接状态文本。</summary>
        public string ConnectionStateText => Camera.IsConnected ? "● 已登录" : "○ 未登录";

        /// <summary>设备端点（IP:端口）。</summary>
        public string Endpoint => $"{Camera.IPAddress}:{Camera.Port}";

        /// <summary>设备序列号（登录后填充）。</summary>
        public string SerialNumber => Camera.SerialNumber;

        /// <summary>可用通道（登录后填充；未登录为空）。</summary>
        public IReadOnlyList<NetworkCameraChannel> Channels => Camera.Channels;

        /// <summary>当前抓图通道号。</summary>
        public int ActiveChannel => Camera.ActiveChannel;

        private NetworkCameraChannel? _selectedChannel;
        /// <summary>抓图通道下拉选择；null = 使用设备当前通道（ActiveChannel）。</summary>
        public NetworkCameraChannel? SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }

        /// <summary>传给 CaptureJpegAsync 的通道号：null = 用当前通道。</summary>
        public int? CaptureChannel => SelectedChannel?.ChannelNo;

        // ── 连接参数编辑态（保存后写入 HardwareConfig，重载硬件才生效） ──────────

        private string _editIp = string.Empty;
        /// <summary>编辑中的 IP 地址。</summary>
        public string EditIp { get => _editIp; set => SetProperty(ref _editIp, value); }

        private string _editPort = "8000";
        /// <summary>编辑中的端口。</summary>
        public string EditPort { get => _editPort; set => SetProperty(ref _editPort, value); }

        private string _editUser = string.Empty;
        /// <summary>编辑中的用户名。</summary>
        public string EditUser { get => _editUser; set => SetProperty(ref _editUser, value); }

        private string _editPassword = string.Empty;
        /// <summary>编辑中的密码。</summary>
        public string EditPassword { get => _editPassword; set => SetProperty(ref _editPassword, value); }

        private string _editChannel = "0";
        /// <summary>编辑中的通道号（0 = 自动选第一个）。</summary>
        public string EditChannel { get => _editChannel; set => SetProperty(ref _editChannel, value); }

        private string _editBufferMb = "8";
        /// <summary>编辑中的抓图缓冲区大小（MB）。</summary>
        public string EditBufferMb { get => _editBufferMb; set => SetProperty(ref _editBufferMb, value); }

        /// <summary>从硬件配置初始化编辑字段。</summary>
        public void LoadEditFields(HardwareConfig? config)
        {
            if (config?.ConnectionParameters == null) return;

            var p = config.ConnectionParameters;
            p.TryGetValue("IP", out var ip);
            p.TryGetValue("Port", out var port);
            p.TryGetValue("UserName", out var user);
            p.TryGetValue("Password", out var password);
            p.TryGetValue("Channel", out var channel);
            p.TryGetValue("CaptureBufferMB", out var bufferMb);

            EditIp       = ip ?? Camera.IPAddress;
            EditPort     = port ?? "8000";
            EditUser     = user ?? "admin";
            EditPassword = password ?? string.Empty;
            EditChannel  = channel ?? "0";
            EditBufferMb = bufferMb ?? "8";
        }

        /// <summary>把设备侧状态变化转发给页面绑定（编辑字段与设备状态无关，不转发）。</summary>
        private void OnCameraPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Camera.IsConnected) or nameof(Camera.HasAlarm)
                or nameof(Camera.SerialNumber) or nameof(Camera.Channels) or nameof(Camera.ActiveChannel))
            {
                RaisePropertyChanged(e.PropertyName);
                RaisePropertyChanged(nameof(ConnectionStateText));
            }
        }

        /// <summary>解除设备事件订阅（列表重建/页面离开时调用，避免设备实例被页面持有）。</summary>
        public void Dispose()
        {
            if (_notifier != null)
                _notifier.PropertyChanged -= OnCameraPropertyChanged;
        }
    }
}
