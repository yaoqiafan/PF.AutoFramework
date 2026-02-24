using PF.Core.Interfaces.Hardware;
using PF.Core.Interfaces.Hardware.IO.Basic;
using PF.Core.Interfaces.Hardware.Motor.Basic;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Debug.Selectors
{
    /// <summary>
    /// 根据设备或模组的具体接口，动态选择对应的 UI 模板
    /// </summary>
    public class DeviceTemplateSelector : DataTemplateSelector
    {
        // 定义可供选择的各种模板属性
        public DataTemplate NullTemplate { get; set; }
        public DataTemplate AxisTemplate { get; set; }
        public DataTemplate IOTemplate { get; set; }
        public DataTemplate DefaultHardwareTemplate { get; set; }

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