using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 海康 MvCodeReaderSDK 条码扫描枪参数视图（ImplementationClassName = "MvCodeReaderBarcodeScan"）。
    /// <para>与 HKBarcodeScanParamView（TCP 版，已弃用）的区别：SDK 版不使用端口，
    /// 仅需 IP（设备发现/匹配）与超时时间。读码参数通过标准 GenICam 用户参数组由
    /// ChangeUserParam 加载。</para>
    /// </summary>
    public class MvCodeReaderBarcodeScanParamView : BindableBase
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

        private string _ip;
        /// <summary>获取或设置读码器IP地址</summary>
        [Category("连接参数")]
        [DisplayName("IP地址")]
        public string IP
        {
            get => _ip;
            set => SetProperty(ref _ip, value);
        }

        private string _timeOutMs;
        /// <summary>获取或设置触发取码超时时间(ms)</summary>
        [Category("连接参数")]
        [DisplayName("超时(ms)")]
        public string TimeOutMs
        {
            get => _timeOutMs;
            set => SetProperty(ref _timeOutMs, value);
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
    }
}
