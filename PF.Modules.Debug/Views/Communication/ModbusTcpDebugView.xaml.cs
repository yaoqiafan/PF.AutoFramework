using System.Windows.Controls;

namespace PF.Modules.Debug.Views.Communication
{
    /// <summary>
    /// Modbus TCP 主站调试视图（无侧边栏入口）。
    /// 由 CommunicationDebugViewModel 在用户点击通讯实例树的 Modbus TCP 叶子节点时导航至此，
    /// 通过 NavigationParameter("Instance") 接收 IModbusTcpMaster 实例。
    /// </summary>
    public partial class ModbusTcpDebugView : UserControl
    {
        /// <summary>初始化 Modbus TCP 主站调试视图</summary>
        public ModbusTcpDebugView()
        {
            InitializeComponent();
        }
    }
}
