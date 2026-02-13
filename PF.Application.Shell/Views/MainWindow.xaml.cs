using PF.Application.Shell.ViewModels;
using PF.UI.Controls;
using PF.UI.Infrastructure.Navigation;
using System.Collections.ObjectModel;
using System.Windows;

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
                // 1. 初始渲染一次
                RenderSideMenu(vm.MenuItems);

                // 2. 监听集合的变化（应对模块异步加载或在 OnLoading 中的刷新）
                vm.MenuItems.CollectionChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() => RenderSideMenu(vm.MenuItems));
                };
            }
        }
        /// <summary>
        /// 动态将数据模型转换为控件支持的 SideMenuItem
        /// </summary>
        private void RenderSideMenu(ObservableCollection<NavigationItem> menuItems)
        {
            MainSideMenu.Items.Clear();

            foreach (var group in menuItems)
            {
                // 创建父级分组节点
                var groupItem = new SideMenuItem
                {
                    Header = group.Title,
                    DataContext = group
                };

                // 创建子页面节点
                foreach (var child in group.Children)
                {
                    var childItem = new SideMenuItem
                    {
                        Header = $" {child.Title}", // 稍微加个空格缩进美化
                        Tag = child,                 // 💡 关键：把整个数据模型塞进 Tag 里，传给点击事件
                        DataContext = child
                    };

                    // 将子节点加入分组
                    groupItem.Items.Add(childItem);
                }

                // 将分组加入左侧菜单
                MainSideMenu.Items.Add(groupItem);
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
