using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>模组调试 ViewModel</summary>
    public class MechanismDebugViewModel : RegionViewModelBase
    {
        private readonly IRegionManager _regionManager;
        private readonly DispatcherTimer _pollTimer;

        /// <summary>获取模组导航条目列表</summary>
        public ObservableCollection<MechanismNavItem> NavItems { get; } = new ObservableCollection<MechanismNavItem>();

        private MechanismNavItem? _selectedItem;
        /// <summary>获取或设置选中的导航条目</summary>
        public MechanismNavItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    NavigateToSelectedMechanism();
                }
            }
        }

        /// <summary>初始化模组调试 ViewModel</summary>
        public MechanismDebugViewModel(IEnumerable<IMechanism> mechanisms, IRegionManager regionManager)
        {
            _regionManager = regionManager;
            BuildNavItems(mechanisms);

            _pollTimer = new DispatcherTimer(DispatcherPriority.DataBind)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        /// <summary>
        /// 利用反射读取每个模组的 <see cref="MechanismUIAttribute"/> 特性，并构建排序后的导航列表
        /// </summary>
        private void BuildNavItems(IEnumerable<IMechanism> mechanisms)
        {
            var items = new List<(MechanismNavItem Item, int Order)>();

            foreach (var mech in mechanisms)
            {
                string nodeName = mech.GetType().Name;
                string viewName = string.Empty;
                int order = 99;

                var attr = mech.GetType().GetCustomAttribute<MechanismUIAttribute>();
                if (attr != null)
                {
                    nodeName = attr.Title;
                    viewName = attr.MechanismViewName;
                    order = attr.Order;
                }

                var navItem = new MechanismNavItem
                {
                    Title = nodeName,
                    ViewName = viewName,
                    Mechanism = mech
                };

                items.Add((navItem, order));
            }

            foreach (var item in items.OrderBy(x => x.Order))
            {
                NavItems.Add(item.Item);
            }
        }

        /// <summary>
        /// 当选中的导航条目变化时，通过 Prism RegionManager 导航到对应的视图
        /// </summary>
        private void NavigateToSelectedMechanism()
        {
            if (_selectedItem == null || string.IsNullOrEmpty(_selectedItem.ViewName))
                return;

            _regionManager.RequestNavigate(NavigationConstants.Regions.MechanismContentRegion, _selectedItem.ViewName, result =>
            {
                if (result.Success == false)
                {
                    MessageService.ShowMessage($"导航失败: {result?.Exception?.Message}");
                }
            });
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            foreach (var item in NavItems)
            {
                item.Refresh();
            }
        }

        /// <summary>
        /// 重写基类方法，在 ViewModel 销毁时停止定时器
        /// </summary>
        public override void Destroy()
        {
            _pollTimer.Stop();
        }
    }
}
