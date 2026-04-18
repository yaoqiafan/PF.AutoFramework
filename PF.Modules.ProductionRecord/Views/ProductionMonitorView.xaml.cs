using PF.Core.Attributes;
using PF.Core.Constants;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Controls;

namespace PF.Modules.Production.Views
{

    [ModuleNavigation(NavigationConstants.Views.ProductionMonitorView, "�������",
      GroupName = "��ʷ����", GroupOrder = 4, Order = 1,
      GroupIcon = "/PF.UI.Resources;component/Images/PNG/7.png", // ���ڵ� PNG ͼ��
      Icon = "RunInfoIcon")]

   
    public partial class ProductionMonitorView : UserControl
    {
        /// <summary>初始化生产监控视图</summary>
        public ProductionMonitorView()
        {
            InitializeComponent();
        }
    }
}
