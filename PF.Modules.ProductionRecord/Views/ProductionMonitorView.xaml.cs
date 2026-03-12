using PF.Core.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Controls;

namespace PF.Modules.Production.Views
{
    [ 
        ModuleNavigationAttribute ("ProductionMonitorView",
                                         "生产监控",
                                        GroupName="生产数据",
                                        GroupOrder=20,
                                        Order=1,
                                        Icon="Monitor" )
   ]
    public partial class ProductionMonitorView : UserControl
    {
        public ProductionMonitorView()
        {
            InitializeComponent();
        }
    }
}
