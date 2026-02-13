using PF.Application.Shell.ViewModels;
using PF.UI.Controls;
using PF.UI.Infrastructure.Navigation;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PF.Application.Shell.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : PF.UI.Controls.Window
    {

        public MainWindow()
        {

            InitializeComponent();
            this.Loaded += MainWindow_Loaded; // 订阅 Loaded 事件
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                RenderSideMenu(vm.MenuItems);

                vm.MenuItems.CollectionChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() => RenderSideMenu(vm.MenuItems));
                };
            }
        }

        private void RenderSideMenu(ObservableCollection<NavigationItem> menuItems)
        {
            MainSideMenu.Items.Clear();

            foreach (var group in menuItems)
            {
                var groupItem = new SideMenuItem
                {
                    FontSize = 15,
                    Header = group.Title,
                    DataContext = group,
                    Background = (Brush)FindResource("LightPrimaryBrush"), // 还原原有的背景色
                    Margin = new Thickness(0, 0, 0, 10),
                    Icon = CreateIconElement(group.Icon) // 渲染组图标
                };

                foreach (var child in group.Children)
                {
                    var childItem = new SideMenuItem
                    {
                        Header = $" {child.Title}",
                        Tag = child,
                        DataContext = child,
                        Icon = CreateIconElement(child.Icon) // 渲染子节点图标
                    };

                    groupItem.Items.Add(childItem);
                }

                MainSideMenu.Items.Add(groupItem);
            }
        }

        /// <summary>
        /// 智能解析图标：包含 .png 则生成 Image，否则视为 StaticResource 的 Geometry Path
        /// </summary>
        private object CreateIconElement(string iconStr)
        {
            if (string.IsNullOrEmpty(iconStr)) return null;

            if (iconStr.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var image = new Image();
                // 补全 Pack URI 协议，防止找不到图片
                string packUri = iconStr.StartsWith("/") ? $"pack://application:,,,{iconStr}" : iconStr;
                image.Source = new BitmapImage(new Uri(packUri, UriKind.Absolute));
                image.SetResourceReference(FrameworkElement.StyleProperty, "IconStyle");
                return image;
            }
            else
            {
                var path = new Path
                {
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Fill
                };

                // 绑定 Geometry 资源 (如 SettingIcon, AudioGeometry)
                path.SetResourceReference(Path.DataProperty, iconStr);
                // 绑定颜色资源
                path.SetResourceReference(Path.FillProperty, "TextIconBrush");

                return path;
            }
        }

        private void ButtonSkins_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private void ButtonConfig_OnClick(object sender, RoutedEventArgs e) => PopupConfig.IsOpen = true;




        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

    }
}
