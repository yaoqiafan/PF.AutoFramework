using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// EtherCAT IO模块参数视图（ImplementationClassName = "EtherCatIO"）
    /// </summary>
    public class EtherCatIOParamView : BindableBase
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

        private string _parentDeviceId;
        [Category("设备基本信息")]
        [DisplayName("父设备ID")]
        public string ParentDeviceId
        {
            get => _parentDeviceId;
            set => SetProperty(ref _parentDeviceId, value);
        }

        private string _inPutCount;
        [Category("连接参数")]
        [DisplayName("输入点数")]
        public string InPutCount
        {
            get => _inPutCount;
            set => SetProperty(ref _inPutCount, value);
        }

        private string _outPutCount;
        [Category("连接参数")]
        [DisplayName("输出点数")]
        public string OutPutCount
        {
            get => _outPutCount;
            set => SetProperty(ref _outPutCount, value);
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
