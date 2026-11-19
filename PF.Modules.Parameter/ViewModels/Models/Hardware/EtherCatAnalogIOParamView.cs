using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 总线卡模拟量模块参数视图（ImplementationClassName = "EtherCatAnalogIO"）。
    /// <para>这里只放模块级参数；通道表（量程/限幅/安全值/仿真）在「硬件调试 → 模拟量设备 → 通道配置」页编辑，
    /// 与 AxisParam 在轴调试页编辑同一做法。</para>
    /// </summary>
    public class EtherCatAnalogIOParamView : BindableBase
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
        /// <summary>
        /// 获取或设置父设备ID。
        /// 填运动控制卡 DeviceId → 挂在卡下单独使用；填某个 IO 模块的 DeviceId → 挂在该 IO 模块下随其分组（不继承节点号）。
        /// </summary>
        [Category("设备基本信息")]
        [DisplayName("父设备ID（运动控制卡或IO模块）")]
        public string ParentDeviceId
        {
            get => _parentDeviceId;
            set => SetProperty(ref _parentDeviceId, value);
        }

        private string _nodeId;
        /// <summary>获取或设置节点号（留空 = 板卡本体 AD/DA；挂在 IO 模块下也需单独填写）</summary>
        [Category("连接参数")]
        [DisplayName("节点号（留空=板卡本体）")]
        public string NodeId
        {
            get => _nodeId;
            set => SetProperty(ref _nodeId, value);
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
