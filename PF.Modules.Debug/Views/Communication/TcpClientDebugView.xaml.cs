using System.Windows.Controls;

namespace PF.Modules.Debug.Views.Communication
{
    /// <summary>
    /// TCP 客户端调试视图（无侧边栏入口）。
    /// 由 CommunicationDebugViewModel 在用户点击通讯实例树的 TCP 客户端叶子节点时导航至此，
    /// 通过 NavigationParameter("Instance") 接收 IClient 实例。
    /// </summary>
    public partial class TcpClientDebugView : UserControl
    {
        /// <summary>初始化 TCP 客户端调试视图</summary>
        public TcpClientDebugView()
        {
            InitializeComponent();
        }
    }
}
