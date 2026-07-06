using PF.Modules.Parameter.Dialog.Editors;
using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 基恩士智能相机参数视图（ImplementationClassName = "KeyenceIntelligentCamera"）
    /// </summary>
    public class KeyenceIntelligentCameraParamView : BindableBase
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

        private string _commInstanceId;
        /// <summary>获取或设置触发通讯实例ID（Tcp/Client）</summary>
        [Category("连接参数")]
        [DisplayName("触发通讯实例")]
        [Editor(typeof(TcpClientInstancePropertyEditor), typeof(TcpClientInstancePropertyEditor))]
        public string CommInstanceId
        {
            get => _commInstanceId;
            set => SetProperty(ref _commInstanceId, value);
        }

        private string _timeOutms;
        /// <summary>获取或设置超时时间(ms)</summary>
        [Category("连接参数")]
        [DisplayName("超时(ms)")]
        public string TimeOutms
        {
            get => _timeOutms;
            set => SetProperty(ref _timeOutms, value);
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
