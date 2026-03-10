using PF.Core.Attributes;
using PF.Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PF.WorkStation.AutoOcr.UI.Views
{
    [ModuleNavigation(nameof(OcrRecipeManageView), "配方", GroupIcon = "/PF.UI.Resources;component/Images/PNG/10.png",
       GroupName = "配方管理", Order = 1, GroupOrder = 3,
       Icon = "NailGeometry"
     )]
    public partial class OcrRecipeManageView : UserControl
    {
        public OcrRecipeManageView()
        {
            InitializeComponent();
        }
    }
}
