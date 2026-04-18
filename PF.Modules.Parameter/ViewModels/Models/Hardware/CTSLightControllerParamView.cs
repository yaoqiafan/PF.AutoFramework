using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 康视达光源控制器参数视图（ImplementationClassName = "CTSLightController"）
    /// </summary>
    public class CTSLightControllerParamView : BindableBase
    {
        private string _deviceId;
        [Category("设备基本信息")]
        [DisplayName("设备ID")]
        /// <summary>获取或设置设备ID</summary>
        public string DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        private string _deviceName;
        [Category("设备基本信息")]
        [DisplayName("设备名称")]
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
        }

        private bool _isEnabled;
        [Category("设备基本信息")]
        [DisplayName("启用")]
        /// <summary>获取或设置是否启用</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSimulated;
        [Category("设备基本信息")]
        [DisplayName("仿真模式")]
        /// <summary>获取或设置是否仿真模式</summary>
        public bool IsSimulated
        {
            get => _isSimulated;
            set => SetProperty(ref _isSimulated, value);
        }

        private string _com;
        [Category("连接参数")]
        [DisplayName("串口号")]
        /// <summary>获取或设置串口号</summary>
        public string Com
        {
            get => _com;
            set => SetProperty(ref _com, value);
        }

        private string _remarks;
        [Category("设备基本信息")]
        [DisplayName("备注")]
        /// <summary>获取或设置备注</summary>
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }
    }
}
