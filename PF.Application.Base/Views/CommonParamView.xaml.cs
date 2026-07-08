using PF.Core.Attributes;
using PF.Core.Constants;

namespace PF.Application.Base.Views
{
    [ModuleNavigation(NavigationConstants.Views.CommonParamView, "公共参数",
        GroupName = "设备参数设置", Order = 2,
        Icon = "ViewListOutline")]
    public partial class CommonParamView : System.Windows.Controls.UserControl
    {
        /// <summary>初始化 CommonParamView 组件。</summary>
        public CommonParamView()
        {
            InitializeComponent();
        }
    }
}
