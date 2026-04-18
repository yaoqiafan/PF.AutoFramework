using PF.Core.Attributes;
using PF.Core.Constants;
using Prism.Navigation.Regions;

namespace PF.Modules.Debug.Views
{
    /// <summary>
    /// 业务模组调试视图
    /// </summary>
    [ModuleNavigation(NavigationConstants.Views.MechanismDebugView, "业务模组调试", GroupName = "系统调试", Icon = "DebugIcon", Order = 2)]
    public partial class MechanismDebugView
    {
        /// <summary>初始化模组调试视图</summary>
        public MechanismDebugView(IRegionManager regionManager)
        {
            string regionName = NavigationConstants.Regions.MechanismContentRegion;
            if (regionManager.Regions.ContainsRegionWithName(regionName))
            {
                regionManager.Regions.Remove(regionName);
            }

            InitializeComponent();
        }
    }
}
