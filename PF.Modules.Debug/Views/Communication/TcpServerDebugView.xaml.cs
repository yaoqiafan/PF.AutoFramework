using System.Windows.Controls;

namespace PF.Modules.Debug.Views.Communication
{
    /// <summary>
    /// TCP 服务端调试视图（无侧边栏入口）。
    /// 由 CommunicationDebugViewModel 在用户点击通讯实例树的 TCP 服务端叶子节点时导航至此，
    /// 通过 NavigationParameter("Instance") 接收 IServer 实例。
    /// </summary>
    public partial class TcpServerDebugView : UserControl
    {
        /// <summary>初始化 TCP 服务端调试视图</summary>
        public TcpServerDebugView()
        {
            InitializeComponent();
        }
    }
}
