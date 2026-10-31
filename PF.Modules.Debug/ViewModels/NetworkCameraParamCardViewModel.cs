using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera;
using Prism.Mvvm;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 网络摄像机分组参数页的一张参数卡片（一台摄像机）。
    ///
    /// <para><b>编辑与设备实例分离</b>：卡片上的字段是 <see cref="HardwareConfig.ConnectionParameters"/>
    /// 的可编辑副本，改完统一保存写库，<b>不</b>触碰运行中的设备实例——新参数要重载硬件才生效
    /// （<c>ReloadAllAsync</c> 会断开全部设备，不能自动触发）。</para>
    ///
    /// <para><b>脏标记</b>：每个字段 setter 都与 <see cref="_baseline"/> 里的快照比对，
    /// 用于顶部"有未保存改动"横幅与「保存全部」按钮的可用性。</para>
    /// </summary>
    public class NetworkCameraParamCardViewModel : BindableBase
    {
        /// <summary>原始配置快照（撤销与脏判定的基准）。</summary>
        private readonly Snapshot _baseline;

        /// <summary>以设备实例与其硬件配置创建参数卡片。</summary>
        /// <param name="camera">摄像机实例（只读取设备名与运行态信息）。</param>
        /// <param name="config">该设备的硬件配置；为 null 时字段取设备实例上的默认值。</param>
        public NetworkCameraParamCardViewModel(INetworkCamera camera, HardwareConfig? config)
        {
            Camera = camera;
            _baseline = Snapshot.FromConfig(config, camera);

            _deviceName = _baseline.DeviceName;
            _isEnabled   = _baseline.IsEnabled;
            _isSimulated = _baseline.IsSimulated;
            _ip         = _baseline.Ip;
            _port       = _baseline.Port;
            _userName   = _baseline.UserName;
            _password   = _baseline.Password;
            _channel    = _baseline.Channel;
            _bufferMb   = _baseline.BufferMb;
            _saveFormat = _baseline.SaveFormat;
            _saveDirectory = _baseline.SaveDirectory;
        }

        /// <summary>对应的摄像机实例（只读，供展示运行态）。</summary>
        public INetworkCamera Camera { get; }

        /// <summary>设备唯一标识（保存时定位配置行）。</summary>
        public string DeviceId => Camera.DeviceId;

        /// <summary>运行态摘要（已登录/未登录 + 端点），保存后不会因编辑而变。</summary>
        public string RuntimeInfo
            => $"{Camera.IPAddress}:{Camera.Port} · {(Camera.IsConnected ? "已登录" : "未登录")}";

        // ── 可编辑字段 ───────────────────────────────────────────────────────────

        private string _deviceName;
        /// <summary>设备显示名称。</summary>
        public string DeviceName { get => _deviceName; set => SetEdited(ref _deviceName, value); }

        private bool _isEnabled;
        /// <summary>是否启用。</summary>
        public bool IsEnabled { get => _isEnabled; set => SetEdited(ref _isEnabled, value); }

        private bool _isSimulated;
        /// <summary>是否仿真模式。</summary>
        public bool IsSimulated { get => _isSimulated; set => SetEdited(ref _isSimulated, value); }

        private string _ip;
        /// <summary>IP 地址（不含端口）。</summary>
        public string Ip { get => _ip; set => SetEdited(ref _ip, value ?? string.Empty); }

        private string _port;
        /// <summary>SDK 私有协议端口（海康默认 8000）。</summary>
        public string Port { get => _port; set => SetEdited(ref _port, value ?? string.Empty); }

        private string _userName;
        /// <summary>登录用户名。</summary>
        public string UserName { get => _userName; set => SetEdited(ref _userName, value ?? string.Empty); }

        private string _password;
        /// <summary>登录密码。</summary>
        public string Password { get => _password; set => SetEdited(ref _password, value ?? string.Empty); }

        private string _channel;
        /// <summary>通道号（0 = 登录后自动选第一个可用通道）。</summary>
        public string Channel { get => _channel; set => SetEdited(ref _channel, value ?? string.Empty); }

        private string _bufferMb;
        /// <summary>抓图缓冲区起始大小（MB）。</summary>
        public string BufferMb { get => _bufferMb; set => SetEdited(ref _bufferMb, value ?? string.Empty); }

        private string _saveFormat;
        /// <summary>存图格式（Bmp / Jpeg / Tiff / Png），设备侧解析失败回退 Jpeg。</summary>
        public string SaveFormat { get => _saveFormat; set => SetEdited(ref _saveFormat, value ?? string.Empty); }

        private string _saveDirectory;
        /// <summary>存图目录，留空用设备默认目录。</summary>
        public string SaveDirectory { get => _saveDirectory; set => SetEdited(ref _saveDirectory, value ?? string.Empty); }

        // ── 脏标记 ───────────────────────────────────────────────────────────────

        private bool _isDirty;
        /// <summary>本卡片是否有未保存的改动。</summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        /// <summary>字段变更后重算脏标记（任一字段与基准不同即为脏）。</summary>
        private void SetEdited<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            if (!SetProperty(ref field, value, name)) return;

            IsDirty = !Matches(_baseline);
        }

        /// <summary>当前编辑值与基准快照逐字段比对。</summary>
        private bool Matches(Snapshot b)
            => DeviceName == b.DeviceName
            && IsEnabled   == b.IsEnabled
            && IsSimulated == b.IsSimulated
            && Ip          == b.Ip
            && Port        == b.Port
            && UserName    == b.UserName
            && Password    == b.Password
            && Channel      == b.Channel
            && BufferMb     == b.BufferMb
            && SaveFormat   == b.SaveFormat
            && SaveDirectory == b.SaveDirectory;

        /// <summary>撤销本卡片的全部未保存改动（回到最近一次加载/保存时的值）。</summary>
        public void Revert()
        {
            DeviceName  = _baseline.DeviceName;
            IsEnabled   = _baseline.IsEnabled;
            IsSimulated = _baseline.IsSimulated;
            Ip          = _baseline.Ip;
            Port        = _baseline.Port;
            UserName    = _baseline.UserName;
            Password    = _baseline.Password;
            Channel     = _baseline.Channel;
            BufferMb    = _baseline.BufferMb;
            SaveFormat  = _baseline.SaveFormat;
            SaveDirectory = _baseline.SaveDirectory;
            IsDirty     = false;
        }

        /// <summary>
        /// 把卡片上的编辑值写入硬件配置（<b>只改内存对象，不写库</b>——写库由页面统一做）。
        /// </summary>
        /// <returns>true = 已写入；false = 配置对象为 null（找不到配置行）。</returns>
        public bool ApplyTo(HardwareConfig? config)
        {
            if (config == null) return false;

            config.DeviceName = DeviceName ?? string.Empty;
            config.IsEnabled  = IsEnabled;
            config.IsSimulated = IsSimulated;
            config.ConnectionParameters["IP"]              = (Ip ?? string.Empty).Trim();
            config.ConnectionParameters["Port"]            = (Port ?? string.Empty).Trim();
            config.ConnectionParameters["UserName"]        = (UserName ?? string.Empty).Trim();
            config.ConnectionParameters["Password"]        = Password ?? string.Empty;
            config.ConnectionParameters["Channel"]         = (Channel ?? string.Empty).Trim();
            config.ConnectionParameters["CaptureBufferMB"] = (BufferMb ?? string.Empty).Trim();
            config.ConnectionParameters["SaveFormat"]      = (SaveFormat ?? string.Empty).Trim();
            config.ConnectionParameters["SaveDirectory"]   = (SaveDirectory ?? string.Empty).Trim();
            return true;
        }

        /// <summary>保存成功后刷新基准，脏标记归零。</summary>
        public void MarkSaved()
        {
            _baseline.CopyFrom(this);
            IsDirty = false;
        }

        /// <summary>字段快照。用可变字段而非 record，便于保存后原地刷新基准。</summary>
        private sealed class Snapshot
        {
            public string DeviceName = string.Empty;
            public bool IsEnabled;
            public bool IsSimulated;
            public string Ip = string.Empty;
            public string Port = "8000";
            public string UserName = "admin";
            public string Password = string.Empty;
            public string Channel = "0";
            public string BufferMb = "8";
            public string SaveFormat = "Jpeg";
            public string SaveDirectory = string.Empty;

            /// <summary>从硬件配置取基准值；配置缺失（或某键缺失）时回退到设备实例上的值。</summary>
            public static Snapshot FromConfig(HardwareConfig? config, INetworkCamera camera)
            {
                var s = new Snapshot
                {
                    DeviceName  = config?.DeviceName ?? camera.DeviceName,
                    IsEnabled   = config?.IsEnabled  ?? true,
                    IsSimulated = config?.IsSimulated ?? camera.IsSimulated,
                    Ip          = camera.IPAddress,
                    Port        = camera.Port.ToString(),
                    UserName    = camera.UserName,
                };

                if (config?.ConnectionParameters is { } p)
                {
                    if (p.TryGetValue("IP", out var ip) && !string.IsNullOrWhiteSpace(ip)) s.Ip = ip;
                    if (p.TryGetValue("Port", out var port) && !string.IsNullOrWhiteSpace(port)) s.Port = port;
                    if (p.TryGetValue("UserName", out var user) && !string.IsNullOrWhiteSpace(user)) s.UserName = user;
                    if (p.TryGetValue("Password", out var pwd) && !string.IsNullOrEmpty(pwd)) s.Password = pwd;
                    if (p.TryGetValue("Channel", out var ch) && !string.IsNullOrWhiteSpace(ch)) s.Channel = ch;
                    if (p.TryGetValue("CaptureBufferMB", out var bm) && !string.IsNullOrWhiteSpace(bm)) s.BufferMb = bm;
                    if (p.TryGetValue("SaveFormat", out var fmt) && !string.IsNullOrWhiteSpace(fmt)) s.SaveFormat = fmt;
                    // 存图目录允许为空（= 用设备默认目录），空串也要如实回填，否则撤销会残留旧值
                    if (p.TryGetValue("SaveDirectory", out var dir)) s.SaveDirectory = dir;
                }

                return s;
            }

            /// <summary>用卡片当前值刷新基准（保存成功后调用）。</summary>
            public void CopyFrom(NetworkCameraParamCardViewModel c)
            {
                DeviceName  = c.DeviceName;
                IsEnabled   = c.IsEnabled;
                IsSimulated = c.IsSimulated;
                Ip          = c.Ip;
                Port        = c.Port;
                UserName    = c.UserName;
                Password    = c.Password;
                Channel     = c.Channel;
                BufferMb    = c.BufferMb;
                SaveFormat  = c.SaveFormat;
                SaveDirectory = c.SaveDirectory;
            }
        }
    }
}
