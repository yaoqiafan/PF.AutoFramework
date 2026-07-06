using System.Windows.Controls;

namespace PF.Modules.Debug.Views.Communication
{
    /// <summary>
    /// FileTransfer 通道调试视图（无侧边栏入口）。
    /// 由 CommunicationDebugViewModel 在用户点击通讯实例树的 FileTransfer 叶子节点时导航至此，
    /// 通过 NavigationParameter("Instance") 接收 IFileTransferChannel 实例。
    /// </summary>
    public partial class FileTransferDebugView : UserControl
    {
        /// <summary>初始化 FileTransfer 通道调试视图</summary>
        public FileTransferDebugView()
        {
            InitializeComponent();
        }
    }
}
