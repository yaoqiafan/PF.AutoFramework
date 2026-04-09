using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 基恩士智能相机参数视图（ImplementationClassName = "KeyenceIntelligentCamera"）
    /// </summary>
    public class KeyenceIntelligentCameraParamView : BindableBase
    {
        private string _deviceId;
        [Category("设备基本信息")]
        [DisplayName("设备ID")]
        public string DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        private string _deviceName;
        [Category("设备基本信息")]
        [DisplayName("设备名称")]
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
        }

        private bool _isEnabled;
        [Category("设备基本信息")]
        [DisplayName("启用")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSimulated;
        [Category("设备基本信息")]
        [DisplayName("仿真模式")]
        public bool IsSimulated
        {
            get => _isSimulated;
            set => SetProperty(ref _isSimulated, value);
        }

        private string _ip;
        [Category("连接参数")]
        [DisplayName("IP地址")]
        public string IP
        {
            get => _ip;
            set => SetProperty(ref _ip, value);
        }

        private string _tiggerPort;
        [Category("连接参数")]
        [DisplayName("触发端口")]
        public string TiggerPort
        {
            get => _tiggerPort;
            set => SetProperty(ref _tiggerPort, value);
        }

        private string _timeOutms;
        [Category("连接参数")]
        [DisplayName("超时(ms)")]
        public string TimeOutms
        {
            get => _timeOutms;
            set => SetProperty(ref _timeOutms, value);
        }

        private string _remarks;
        [Category("设备基本信息")]
        [DisplayName("备注")]
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }
    }
}
