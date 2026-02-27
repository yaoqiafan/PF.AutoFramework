using PF.Core.Attributes;
using PF.Core.Constants;
using System.Windows.Controls;

namespace PF.Modules.Debug.Views
{
    /// <summary>
    /// 控制卡级调试视图：展示系统顶级板卡（IMotionCard）的连接状态、报警状态及基础测试命令。
    /// </summary>
    [ModuleNavigation(NavigationConstants.Views.CardDebugView, "控制卡调试", GroupName = "系统调试", Icon = "DebugIcon", Order = 5, GroupIcon = "/PF.UI.Resources;component/Images/PNG/4.png")]
    public partial class CardDebugView : UserControl
    {
        public CardDebugView()
        {
            InitializeComponent();
        }
    }
}
