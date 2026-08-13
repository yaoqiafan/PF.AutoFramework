using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 海康线阵相机参数视图（ImplementationClassName = "HikLineScanCamera"）。
    ///
    /// <para><b>父设备ID决定链路类型</b>：填采集卡的设备ID = 经采集卡取流（CameraLink/CXP/XoF），
    /// 帧长与帧触发下发到采集卡节点树；留空 = GigE/USB 直连，下发到相机自身节点树。
    /// 无需另设"链路类型"字段，配置拓扑本身就表达了链路。</para>
    ///
    /// <para>曝光、增益、行触发、编码器等运行期参数不在这里配置——它们随配方变化，
    /// 由机构层按扫描任务下发，调试阶段用线阵相机调试面板逐项试。</para>
    /// </summary>
    public class HikLineScanCameraParamView : BindableBase
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
        /// <summary>获取或设置父设备ID（采集卡设备ID；留空表示 GigE/USB 直连）</summary>
        [Category("设备基本信息")]
        [DisplayName("父设备ID(采集卡)")]
        public string ParentDeviceId
        {
            get => _parentDeviceId;
            set => SetProperty(ref _parentDeviceId, value);
        }

        private string _serialNumber;
        /// <summary>获取或设置相机序列号（推荐填写，枚举顺序变化不受影响）</summary>
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

        private string _imageNodeNum;
        /// <summary>
        /// 获取或设置 SDK 图像缓存节点数。
        /// 占用 ≈ 单帧字节数 × 本值，线扫大帧（16K 宽 × 上万行可达数百 MB）下必须下调，
        /// 小于 2 会被设备层按 2 处理。
        /// </summary>
        [Category("采集")]
        [DisplayName("图像缓存节点数")]
        public string ImageNodeNum
        {
            get => _imageNodeNum;
            set => SetProperty(ref _imageNodeNum, value);
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
