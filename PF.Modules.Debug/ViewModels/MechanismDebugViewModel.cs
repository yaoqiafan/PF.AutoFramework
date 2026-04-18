using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Reflection;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>模组调试 ViewModel</summary>
    public class MechanismDebugViewModel : RegionViewModelBase
    {
        private readonly IRegionManager _regionManager;

        

        // 缓存：模组实例 -> 对应的视图名称 (ViewName)
        private readonly Dictionary<object, string> _mechanismViewMap = new Dictionary<object, string>();

        /// <summary>获取模组树节点列表</summary>
        public ObservableCollection<DebugTreeNode> TreeNodes { get; } = new ObservableCollection<DebugTreeNode>();

        private DebugTreeNode _selectedNode;
        /// <summary>获取或设置选中的树节点</summary>
        public DebugTreeNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    NavigateToSelectedMechanism();
                }
            }
        }

        // 注入 IRegionManager 用于动态加载页面
        /// <summary>初始化模组调试 ViewModel</summary>
        public MechanismDebugViewModel(IEnumerable<IMechanism> mechanisms, IRegionManager regionManager)
        {
            _regionManager = regionManager;
            BuildTree(mechanisms);
        }

        private void BuildTree(IEnumerable<IMechanism> mechanisms)
        {
            // 用于临时存储以便进行排序
            var tempList = new List<(DebugTreeNode Node, int Order)>();

            foreach (var mech in mechanisms)
            {
                // 默认值
                string nodeName = mech.GetType().Name;
                string viewName = string.Empty;
                int order = 99; // 默认排序号

                // 反射获取 MechanismUIAttribute 特性
                var attr = mech.GetType().GetCustomAttribute<MechanismUIAttribute>();
                if (attr != null)
                {
                    nodeName = attr.Title; // 使用特性中定义的中文名称
                    viewName = attr.MechanismViewName; // 获取对应的前端视图注册名
                    order = attr.Order; // 获取排序号
                }

                // 构建树节点
                var node = new DebugTreeNode { NodeName = nodeName, Payload = mech };
                tempList.Add((node, order));

                // 如果特性中定义了视图名称，则加入路由缓存字典
                if (!string.IsNullOrEmpty(viewName))
                {
                    _mechanismViewMap[mech] = viewName;
                }
            }

            // 按 Order 升序排序并填充到绑定的集合中
            foreach (var item in tempList.OrderBy(x => x.Order))
            {
                TreeNodes.Add(item.Node);
            }
        }

        /// <summary>
        /// 当选中的节点发生变化时，通过 Prism RegionManager 导航到对应的视图
        /// </summary>
        private void NavigateToSelectedMechanism()
        {
            if (_selectedNode?.Payload != null &&
                _mechanismViewMap.TryGetValue(_selectedNode.Payload, out var viewName))
            {
                // 加上回调函数，捕获并显示导航失败的真正原因
                _regionManager.RequestNavigate("MechanismContentRegion", viewName, result =>
                {
                    if (result.Success == false)
                    {
                        // 这里打个断点，或者用 MessageBox 弹出来看看具体报什么错！
                        MessageService.ShowMessage($"导航失败: {result?.Exception?.Message}");
                    }
                });
            }
        }
    }
}