using PF.Core.Attributes;
using PF.Core.Constants;

namespace PF.Application.Base.Views
{
    /// <summary>
    /// 框架主显界面：展示项目品牌信息与产品图片，提供"打开说明书"等常用操作入口。
    /// 各消费项目通过重写 <see cref="ViewModels.MainWindowViewModelBase"/> 的
    /// ProductImagePath / ProductDescription / UserManualPath 定制内容，无需重复实现页面。
    /// </summary>
    [ModuleNavigation(NavigationConstants.Views.MainView, "主显界面",
        GroupIcon = "/PF.UI.Resources;component/Images/PNG/11.png",
        GroupName = "开始", Order = 1, GroupOrder = 0,
        Icon = "MonitorDashboard")]
    public partial class MainView : System.Windows.Controls.UserControl
    {
        /// <summary>初始化 MainView 组件。</summary>
        public MainView()
        {
            InitializeComponent();
        }
    }
}
