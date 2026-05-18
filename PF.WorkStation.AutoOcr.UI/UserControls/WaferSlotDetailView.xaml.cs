using Microsoft.Win32;
using PF.WorkStation.AutoOcr.UI.ViewModels;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    /// <summary>
    /// WaferSlotDetailView.xaml 的交互逻辑
    /// </summary>
    public partial class WaferSlotDetailView : UserControl
    {
        /// <summary>Initializes a new instance.</summary>
        public WaferSlotDetailView()
        {
            InitializeComponent();
        }

        private void OnExportScreenshot(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as WaferSlotDetailViewModel;
            string slotLabel = vm != null ? $"第{vm.DisplayIndex}层" : "槽位";

            var dialog = new SaveFileDialog
            {
                Title    = "保存截图",
                Filter   = "PNG 图片 (*.png)|*.png",
                FileName = $"{slotLabel}_检测详情_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var dpi    = VisualTreeHelper.GetDpi(this);
                var bitmap = new RenderTargetBitmap(
                    (int)(ActualWidth  * dpi.DpiScaleX),
                    (int)(ActualHeight * dpi.DpiScaleY),
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Pbgra32);
                bitmap.Render(this);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var stream = File.OpenWrite(dialog.FileName);
                encoder.Save(stream);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"截图保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
