using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 海康图像采集卡参数视图（ImplementationClassName = "HikFrameGrabberCard"）。
    ///
    /// <para>采集卡是顶级设备（无父设备），线阵相机把 ParentDeviceId 指向本卡的设备ID 挂在其下。
    /// 帧长、帧超时、帧触发等运行期节点不在这里配置——它们随配方变化，由机构层按扫描任务下发。</para>
    /// </summary>
    public class HikFrameGrabberCardParamView : BindableBase
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

        private string _serialNumber;
        /// <summary>获取或设置采集卡序列号（推荐填写，插拔顺序变化不受影响）</summary>
        [Category("设备选定")]
        [DisplayName("序列号")]
        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        private string _index;
        /// <summary>获取或设置枚举索引（序列号为空时按此索引选定）</summary>
        [Category("设备选定")]
        [DisplayName("枚举索引")]
        public string Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
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
