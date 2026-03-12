using PF.Core.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Controls;

namespace PF.Modules.Production.Views
{
    [ModuleNavigationAttribute ("ProductionHistoryView","历史查询",
                                        GroupName = "生产数据",
                                        GroupOrder = 20,
                                        Order = 3,
                                        Icon = "History") ]
    public partial class ProductionHistoryView : UserControl
    {
        public ProductionHistoryView()
        {
            InitializeComponent();
        }
    }
}
