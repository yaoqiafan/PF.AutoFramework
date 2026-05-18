using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.WorkStation.AutoOcr.UI.Converters
{
    /// <summary>
    /// 将 WaferSlotStatus 枚举转换为对应的背景画刷，供晶圆盒槽位可视化使用。
    /// Empty=透明, HasMaterial=白色, Inspecting=蓝, OK=绿, NG=红
    /// </summary>
    public class WaferSlotStatusToColorConverter : IValueConverter
    {
        private static readonly Brush _transparent = Brushes.Transparent;
        private static readonly Brush _white       = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        private static readonly Brush _blue        = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
        private static readonly Brush _green       = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly Brush _red         = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));

        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WaferSlotStatus status)
            {
                return status switch
                {
                    WaferSlotStatus.HasMaterial => _white,
                    WaferSlotStatus.Inspecting  => _blue,
                    WaferSlotStatus.OK          => _green,
                    WaferSlotStatus.NG          => _red,
                    _                           => _transparent
                };
            }
            return _transparent;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
