using System.Windows.Controls;

namespace PF.Modules.Debug.Views.Communication
{
    /// <summary>
    /// Modbus RTU 主站调试视图（无侧边栏入口）。
    /// 由 CommunicationDebugViewModel 在用户点击通讯实例树的 Modbus RTU 叶子节点时导航至此，
    /// 通过 NavigationParameter("Instance") 接收 IModbusRtuMaster 实例。
    /// </summary>
    public partial class ModbusRtuDebugView : UserControl
    {
        /// <summary>初始化 Modbus RTU 主站调试视图</summary>
        public ModbusRtuDebugView()
        {
            InitializeComponent();
        }
    }
}
