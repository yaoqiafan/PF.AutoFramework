using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.Application.Shell.Converters
{
    /// <summary>
    /// 设备连接状态文本转换器：将 bool 值转换为“已连接”或“未连接”字符串
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))] // 指明源类型为 bool，目标类型为 string
    public class DeviceConnectedTextConverter : IValueConverter
    {
        /// <summary>
        /// 正向转换：将布尔值转换为显示文本
        /// </summary>
        /// <param name="value">绑定源传来的值（期待为 bool）</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
       
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            // 模式匹配：如果 value 是 bool 且为 true，返回“已连接”，否则返回“未连接”
            => value is bool b && b ? "已连接" : "未连接";

        /// <summary>
        /// 反向转换：从 UI 文本转回布尔值（此处不支持，通常用于只读显示）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 设备连接状态画刷转换器：将 bool 值转换为对应的 UI 颜色（Brush）
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))] // 指明源类型为 bool，目标类型为 Brush
    public class DeviceConnectedBrushConverter : IValueConverter
    {
        // 预定义连接态的颜色：一种偏绿色的色调 (R:2, G:173, B:139)
        private static readonly SolidColorBrush _connected = MakeBrush(0x02, 0xad, 0x8b);

        // 预定义未连接态的颜色：灰色 (R:117, G:117, B:117)
        private static readonly SolidColorBrush _disconnected = MakeBrush(0x75, 0x75, 0x75);

        /// <summary>
        /// 正向转换：将布尔值转换为颜色画刷
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            // 如果已连接返回绿色画刷，否则返回灰色画刷
            => value is bool b && b ? _connected : _disconnected;

        /// <summary>
        /// 反向转换：不支持从颜色转回布尔值
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        /// <summary>
        /// 辅助方法：根据 RGB 值创建并冻结画刷
        /// </summary>
        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            // Freeze() 调用非常重要，它使对象变为只读，
            // 从而提高性能并允许在多个线程间安全共享 UI 资源。
            brush.Freeze();
            return brush;
        }
    }
}
