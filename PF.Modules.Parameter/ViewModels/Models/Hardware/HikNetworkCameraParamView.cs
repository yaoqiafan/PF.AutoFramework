using System.ComponentModel;
using Prism.Mvvm;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 海康网络摄像机参数视图（ImplementationClassName = "HikNetworkCamera"）。
    ///
    /// <para>走参数管理页的保存流程：改完确认 → 存库 → 重载硬件生效。
    /// 与调试页的"保存配置"编辑的是同一份 HardwareConfig，只是入口与重载时机不同
    /// （调试页刻意不自动重载——全量重载会断开所有设备，见 NetworkCameraDebugViewModel 注释）。</para>
    /// </summary>
    public class HikNetworkCameraParamView : BindableBase
    {
        private string _deviceId;
        /// <summary>获取或设置设备ID</summary>
        [Category("设备基本信息")]
        [DisplayName("设备ID")]
        public string DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        private string _deviceName;
        /// <summary>获取或设置设备名称</summary>
        [Category("设备基本信息")]
        [DisplayName("设备名称")]
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
        }

        private bool _isEnabled;
        /// <summary>获取或设置是否启用</summary>
        [Category("设备基本信息")]
        [DisplayName("启用")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSimulated;
        /// <summary>获取或设置是否仿真</summary>
        [Category("设备基本信息")]
        [DisplayName("仿真模式")]
        public bool IsSimulated
        {
            get => _isSimulated;
            set => SetProperty(ref _isSimulated, value);
        }

        private string _parentDeviceId;
        /// <summary>获取或设置父设备ID（网络摄像机是自包含设备，正常留空）</summary>
        [Category("设备基本信息")]
        [DisplayName("父设备ID")]
        public string ParentDeviceId
        {
            get => _parentDeviceId;
            set => SetProperty(ref _parentDeviceId, value);
        }

        private string _remarks;
        /// <summary>获取或设置备注</summary>
        [Category("设备基本信息")]
        [DisplayName("备注")]
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        private string _ip;
        /// <summary>获取或设置设备 IP 地址（不含端口）</summary>
        [Category("连接参数")]
        [DisplayName("IP 地址")]
        public string Ip
        {
            get => _ip;
            set => SetProperty(ref _ip, value);
        }

        private string _port;
        /// <summary>获取或设置 SDK 私有协议端口（海康默认 8000）</summary>
        [Category("连接参数")]
        [DisplayName("端口")]
        public string Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        private string _userName;
        /// <summary>获取或设置登录用户名（默认 admin）</summary>
        [Category("连接参数")]
        [DisplayName("用户名")]
        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        private string _password;
        /// <summary>获取或设置登录密码</summary>
        [Category("连接参数")]
        [DisplayName("密码")]
        [Description("出厂密码见相机标签/机身二维码。")]
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private string _channel;
        /// <summary>
        /// 获取或设置通道号（0 = 登录后自动选第一个可用通道）。
        /// NVR 下接入的摄像机走数字通道（编号通常从 33 起），建议显式指定。
        /// </summary>
        [Category("采集")]
        [DisplayName("通道号")]
        public string Channel
        {
            get => _channel;
            set => SetProperty(ref _channel, value);
        }

        private string _captureBufferMb;
        /// <summary>
        /// 获取或设置抓图缓冲区起始大小（MB）。
        /// SDK 无"查询所需大小"接口，缓冲区不足（错误码 43）时设备层自动翻倍，上限 64MB。
        /// </summary>
        [Category("采集")]
        [DisplayName("抓图缓冲区(MB)")]
        public string CaptureBufferMb
        {
            get => _captureBufferMb;
            set => SetProperty(ref _captureBufferMb, value);
        }

        private string _saveFormat = "Jpeg";
        /// <summary>
        /// 获取或设置存图格式：Bmp / Jpeg / Tiff / Png。
        /// JPEG 时直接把 SDK 返回的字节写盘，不二次编码；其余格式经 System.Drawing 转码。
        /// </summary>
        [Category("存图")]
        [DisplayName("存图格式")]
        [Description("Bmp / Jpeg / Tiff / Png。留空或填错按 Jpeg。")]
        public string SaveFormat
        {
            get => _saveFormat;
            set => SetProperty(ref _saveFormat, value);
        }

        private string _saveDirectory = string.Empty;
        /// <summary>
        /// 获取或设置存图根目录；留空用 <c>{ConfigPath}\NetworkCamera\{DeviceId}</c>。
        /// 文件名格式 <c>{时间戳}.{扩展名}</c>。
        /// </summary>
        [Category("存图")]
        [DisplayName("存图目录")]
        [Description("留空则用 {配置目录}\\NetworkCamera\\{设备ID}。")]
        public string SaveDirectory
        {
            get => _saveDirectory;
            set => SetProperty(ref _saveDirectory, value);
        }
    }
}
