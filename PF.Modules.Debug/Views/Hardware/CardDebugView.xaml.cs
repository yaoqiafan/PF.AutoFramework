using System.Windows.Controls;

namespace PF.Modules.Debug.Views
{
    /// <summary>
    /// 板卡调试视图（无侧边栏入口）。
    /// 由 HardwareDebugViewModel 在用户点击设备树的板卡节点时导航至此，
    /// 通过 NavigationParameter("Device") 接收 IMotionCard 实例。
    /// </summary>
    public partial class CardDebugView : UserControl
    {
        public CardDebugView()
        {
            InitializeComponent();
        }
    }
}
