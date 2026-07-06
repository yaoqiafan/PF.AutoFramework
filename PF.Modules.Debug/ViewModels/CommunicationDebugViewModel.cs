using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Communication;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Reflection;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 通讯综合调试 ViewModel。
    /// 树的一级节点按 <see cref="ICommunication.Category"/> 分组，二级节点按
    /// <see cref="ICommunication.Role"/> 分组（None 时不建二级，实例直接扁平挂在分类下）。
    /// 点击叶子节点时，通过实例运行时类型上的 <see cref="CommunicationUIAttribute"/> 取得导航目标视图名。
    /// </summary>
    public class CommunicationDebugViewModel : RegionViewModelBase
    {
        private readonly ICommunicationManagerService _commManager;

        /// <summary>通讯实例分类树节点列表</summary>
        public ObservableCollection<DebugTreeNode> TreeNodes { get; } = new();

        private DebugTreeNode _selectedNode;
        /// <summary>获取或设置选中的树节点</summary>
        public DebugTreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        /// <summary>导航到通讯实例调试视图命令</summary>
        public DelegateCommand<object> NavigateToDebugCommand { get; }

        /// <summary>初始化通讯调试 ViewModel</summary>
        public CommunicationDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            NavigateToDebugCommand = new DelegateCommand<object>(ExecuteNavigateToDebug);

            BuildTree();
        }

        // ── 树构建 ────────────────────────────────────────────────────────────

        private void BuildTree()
        {
            foreach (var categoryGroup in _commManager.ActiveCommunications
                         .GroupBy(c => c.Category)
                         .OrderBy(g => g.Key))
            {
                var categoryNode = new DebugTreeNode { NodeName = categoryGroup.Key.ToString() };

                // Role=None：无服务端/客户端语义（如串口点对点），直接扁平挂在分类节点下
                foreach (var comm in categoryGroup.Where(c => c.Role == CommunicationRole.None))
                    categoryNode.Children.Add(new DebugTreeNode { NodeName = comm.DisplayName, Payload = comm });

                // Role!=None：按服务端/客户端再建一层分组
                foreach (var roleGroup in categoryGroup.Where(c => c.Role != CommunicationRole.None)
                             .GroupBy(c => c.Role)
                             .OrderBy(g => g.Key))
                {
                    var roleNode = new DebugTreeNode
                    {
                        NodeName = roleGroup.Key == CommunicationRole.Server ? "服务端" : "客户端"
                    };
                    foreach (var comm in roleGroup)
                        roleNode.Children.Add(new DebugTreeNode { NodeName = comm.DisplayName, Payload = comm });
                    categoryNode.Children.Add(roleNode);
                }

                TreeNodes.Add(categoryNode);
            }
        }

        // ── 导航 ──────────────────────────────────────────────────────────────

        private void ExecuteNavigateToDebug(object payload)
        {
            if (payload is not ICommunication comm) return;

            // 树节点的 Payload 是 BuildTree() 时的快照：任意一个通讯调试页触发 ReloadAllAsync() 后，
            // 全部实例都会被停止/释放并重建，这里必须按 InstanceId 重新从管理服务取一次当前存活的实例，
            // 否则会拿着已释放的旧实例导航过去，界面状态永远停在断开/默认值，也收不到任何后续事件。
            var current = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == comm.InstanceId) ?? comm;

            var viewName = current.GetType().GetCustomAttribute<CommunicationUIAttribute>()?.ViewName;
            if (string.IsNullOrEmpty(viewName)) return;

            var parameters = new NavigationParameters { { "Instance", current } };
            RegionManager.RequestNavigate(NavigationConstants.Regions.CommunicationViewRegion, viewName, parameters);
        }
    }
}
