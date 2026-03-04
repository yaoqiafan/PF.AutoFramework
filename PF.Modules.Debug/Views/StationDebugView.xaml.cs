using PF.Core.Attributes;
using PF.Core.Constants;
using System.Windows.Controls;

namespace PF.Modules.Debug.Views
{
    /// <summary>
    /// 工站调试主视图（容器），承载左侧工站列表 + 右侧动态子工站调试面板。
    /// </summary>
    [ModuleNavigation(NavigationConstants.Views.StationDebugView, "工站调试", GroupName = "系统调试", Icon = "DebugIcon", GroupOrder = 2, Order = 3)]
    public partial class StationDebugView : UserControl
    {
        public StationDebugView(IRegionManager regionManager)
        {
            // 防止嵌套 Region 重复注册导致 RegionAlreadyRegisteredException
            string regionName = NavigationConstants.Regions.StationContentRegion;
            if (regionManager.Regions.ContainsRegionWithName(regionName))
                regionManager.Regions.Remove(regionName);

            InitializeComponent();
        }
    }
}
