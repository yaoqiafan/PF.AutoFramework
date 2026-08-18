using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 海康串口光源控制器参数视图（ImplementationClassName = "HikComLightController"）。
    ///
    /// <para>与康视达那款的差别：串口不在这里配。本设备复用通讯层的串口实例，
    /// 这里只填通讯实例ID，波特率/校验位等串口参数在「通讯配置」里改——
    /// 好处是同一个串口能在通讯调试面板里单独收发验证。</para>
    /// </summary>
    public class HikComLightControllerParamView : BindableBase
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
        /// <summary>获取或设置串口通讯实例ID（指向一条 ImplementationClassName = "SerialPort" 的通讯配置）</summary>
        [Category("连接参数")]
        [DisplayName("串口通讯实例ID")]
        public string CommInstanceId
        {
            get => _commInstanceId;
            set => SetProperty(ref _commInstanceId, value);
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
