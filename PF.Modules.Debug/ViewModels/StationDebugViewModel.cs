using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Infrastructure.Station.Basic;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Reflection;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 工站调试主视图 ViewModel
    ///
    /// 发现机制：
    ///   通过注入的 IEnumerable&lt;StationBase&gt; 获取所有已注册工站实例，
    ///   对每个实例的运行时类型反射读取 [StationUIAttribute]，
    ///   提取 Title / ViewName / Order 后填充左侧导航列表。
    ///
    /// 导航机制：
    ///   选中列表项后调用 IRegionManager.RequestNavigate，
    ///   将对应 ViewName 加载到 StationContentRegion 右侧内容区域。
    /// </summary>
    public class StationDebugViewModel : RegionViewModelBase
    {
        private readonly IRegionManager _regionManager;

        /// <summary>左侧工站导航条目列表</summary>
        public ObservableCollection<StationNavItem> NavItems { get; } = new();

        private StationNavItem _selectedItem;
        public StationNavItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null)
                    NavigateTo(value.ViewName);
            }
        }

        public StationDebugViewModel(IEnumerable<StationBase> stations, IRegionManager regionManager)
        {
            _regionManager = regionManager;
            BuildNavItems(stations);
        }

        private void BuildNavItems(IEnumerable<StationBase> stations)
        {
            var items = new List<(StationNavItem Item, int Order)>();

            foreach (var station in stations)
            {
                var attr = station.GetType().GetCustomAttribute<StationUIAttribute>();
                if (attr == null) continue;

                items.Add((new StationNavItem
                {
                    Title    = attr.Title,
                    ViewName = attr.ViewName,
                    StationName = station.StationName
                }, attr.Order));
            }

            foreach (var (item, _) in items.OrderBy(x => x.Order))
                NavItems.Add(item);
        }

        private void NavigateTo(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName)) return;

            _regionManager.RequestNavigate(
                NavigationConstants.Regions.StationContentRegion,
                viewName,
                result =>
                {
                    if (result.Success == false)
                        System.Diagnostics.Debug.WriteLine(
                            $"[StationDebug] 导航失败: {result.Exception?.Message}");
                });
        }
    }

    /// <summary>工站导航条目数据模型</summary>
    public class StationNavItem
    {
        public string Title       { get; init; }
        public string ViewName    { get; init; }
        public string StationName { get; init; }
    }
}
