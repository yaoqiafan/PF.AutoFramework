using PF.Core.Attributes;
using PF.Core.Constants;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Controls;

namespace PF.Modules.Production.Views
{

    [ModuleNavigation(NavigationConstants.Views.ProductionHistoryView, "历史查询",
      GroupName = "历史表单", GroupOrder = 3, Order = 1,
      GroupIcon = "/PF.UI.Resources;component/Images/PNG/7.png", // 父节点 PNG 图标
      Icon = "RunInfoIcon")]
   
    public partial class ProductionHistoryView : UserControl
    {
        public ProductionHistoryView()
        {
            InitializeComponent();
        }
    }
}
