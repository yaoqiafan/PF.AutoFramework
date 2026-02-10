using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.UI.Infrastructure.Tools.Helper
{
    public class SplashScreenHelper
    {
        private static Window splashWindow;
        /// <summary>
        /// 显示启动画面
        /// </summary>
        /// <param name="imagePath">
        /// 图片路径：
        /// 1. "splash.png" (项目根目录，构建操作为 SplashScreen)
        /// 2. "Resources/splash.png" (相对路径)
        /// 3. 完整 pack URI
        /// </param>
        /// <param name="autoClose">是否自动关闭（显示主窗口时自动关闭）</param>
        /// <param name="fadeOutTime">淡出时间（秒）</param>
        public static void OpenSplashScreen(
            string imagePath = "Loading_Default",
            bool autoClose = true,
            double fadeOutTime = 1.0)
        {
            try
            {
                ShowFallbackSplash();
            }
            catch (Exception ex) { }
        }

        /// <summary>
        /// 备用方案：使用自定义窗口作为启动画面
        /// </summary>
        private static void ShowFallbackSplash()
        {
            try
            {
                // 创建一个简单的自定义启动窗口
                splashWindow = new Window()
                {
                    WindowStyle = WindowStyle.None,
                    WindowState = WindowState.Normal,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = false,
                    Topmost = true,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Background = System.Windows.Media.Brushes.White,
                    Content = new System.Windows.Controls.StackPanel()
                    {
                        Children =
                        {
                            new System.Windows.Controls.Image()
                            {
                                Source = new System.Windows.Media.Imaging.BitmapImage(
                                    new Uri("pack://application:,,,/PF.Resources;component/Images/JPG/Loading_Default.jpg")),
                                Width = 400,
                                Height = 300
                            },
                            new System.Windows.Controls.TextBlock()
                            {
                                Text = "正在启动...",
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                Margin = new Thickness(0, 10, 0, 0)
                            }
                        }
                    }
                };

                splashWindow.Show();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"备用启动画面也失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭自定义启动画面
        /// </summary>
        public static void CloseCustomSplash()
        {
            splashWindow.Close();
        }
    }
}

