using PF.Modules.Parameter.Dialog.Editors;
using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 海康条码扫描枪参数视图（ImplementationClassName = "HKBarcodeScan"）
    /// </summary>
    public class HKBarcodeScanParamView : BindableBase
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

        private string _triggerCommInstanceId;
        /// <summary>获取或设置触发通讯实例ID（Tcp/Client）</summary>
        [Category("连接参数")]
        [DisplayName("触发通讯实例")]
        [Editor(typeof(TcpClientInstancePropertyEditor), typeof(TcpClientInstancePropertyEditor))]
        public string TriggerCommInstanceId
        {
            get => _triggerCommInstanceId;
            set => SetProperty(ref _triggerCommInstanceId, value);
        }

        private string _userPowerCommInstanceId;
        /// <summary>获取或设置用户电源通讯实例ID（Tcp/Client）</summary>
        [Category("连接参数")]
        [DisplayName("用户电源通讯实例")]
        [Editor(typeof(TcpClientInstancePropertyEditor), typeof(TcpClientInstancePropertyEditor))]
        public string UserPowerCommInstanceId
        {
            get => _userPowerCommInstanceId;
            set => SetProperty(ref _userPowerCommInstanceId, value);
        }

        private string _timeOutMs;
        /// <summary>获取或设置超时时间(ms)</summary>
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
