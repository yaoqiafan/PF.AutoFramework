using System.ComponentModel;

namespace PF.Modules.Parameter.ViewModels.Models.Hardware
{
    /// <summary>
    /// 辅助编码器参数视图（ImplementationClassName = "EtherCatAuxEncoder"）
    /// </summary>
    public class EtherCatAuxEncoderParamView : BindableBase
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
        /// 填运动控制卡 DeviceId → 挂在卡下单独使用；填某根轴的 DeviceId → 挂在该轴下随轴分组。
        /// </summary>
        [Category("设备基本信息")]
        [DisplayName("父设备ID（运动控制卡或轴）")]
        public string ParentDeviceId
        {
            get => _parentDeviceId;
            set => SetProperty(ref _parentDeviceId, value);
        }

        private string _channel;
        /// <summary>获取或设置辅助编码器在板卡内的物理通道号</summary>
        [Category("连接参数")]
        [DisplayName("通道号")]
        public string Channel
        {
            get => _channel;
            set => SetProperty(ref _channel, value);
        }

        private string _multiplier;
        /// <summary>获取或设置编码器倍率（脉冲当量换算系数，默认 2）</summary>
        [Category("连接参数")]
        [DisplayName("编码器倍率")]
        public string Multiplier
        {
            get => _multiplier;
            set => SetProperty(ref _multiplier, value);
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
