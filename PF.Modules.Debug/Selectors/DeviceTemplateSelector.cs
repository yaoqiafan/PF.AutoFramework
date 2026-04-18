using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Debug.Selectors
{
    /// <summary>
    /// 根据设备或模组的具体接口，动态选择对应的 UI 模板
    /// </summary>
    public class DeviceTemplateSelector : DataTemplateSelector
    {
        /// <summary>空节点模板</summary>
        public DataTemplate NullTemplate { get; set; }
        /// <summary>轴设备模板</summary>
        public DataTemplate AxisTemplate { get; set; }
        /// <summary>IO 设备模板</summary>
        public DataTemplate IOTemplate { get; set; }
        /// <summary>默认硬件模板</summary>
        public DataTemplate DefaultHardwareTemplate { get; set; }

        /// <summary>根据设备类型选择对应的 UI 模板</summary>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // 1. 如果没有选中任何节点 (Payload 为空)
            if (item == null)
                return NullTemplate;

            // 2. 如果选中的是 运动控制轴
            if (item is IAxis)
                return AxisTemplate;

            // 3. 如果选中的是 IO控制器
            if (item is IIOController)
                return IOTemplate;

            // 4. 如果是其他普通硬件
            if (item is IHardwareDevice)
                return DefaultHardwareTemplate;

            // 默认返回基础的模板
            return base.SelectTemplate(item, container);
        }
    }
}