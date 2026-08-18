using System.Windows.Controls;

namespace PF.Modules.Debug.Views.Communication
{
    /// <summary>
    /// 串口调试视图（无侧边栏入口）。
    /// 由 CommunicationDebugViewModel 在用户点击通讯实例树的串口叶子节点时导航至此，
    /// 通过 NavigationParameter("Instance") 接收 ISerialCommunication 实例。
    /// </summary>
    public partial class SerialPortDebugView : UserControl
    {
        /// <summary>初始化串口调试视图</summary>
        public SerialPortDebugView()
        {
            InitializeComponent();
        }
    }
}
