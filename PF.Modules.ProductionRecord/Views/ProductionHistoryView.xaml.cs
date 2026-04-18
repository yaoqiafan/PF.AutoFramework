using PF.Core.Attributes;
using PF.Core.Constants;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Controls;

namespace PF.Modules.Production.Views
{

    [ModuleNavigation(NavigationConstants.Views.ProductionHistoryView, "��ʷ��ѯ",
      GroupName = "��ʷ����", GroupOrder = 4, Order = 1,
      GroupIcon = "/PF.UI.Resources;component/Images/PNG/7.png", // ���ڵ� PNG ͼ��
      Icon = "RunInfoIcon")]
   
    public partial class ProductionHistoryView : UserControl
    {
        /// <summary>初始化生产历史视图</summary>
        public ProductionHistoryView()
        {
            InitializeComponent();
        }
    }
}
