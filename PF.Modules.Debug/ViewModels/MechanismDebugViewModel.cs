using PF.Core.Interfaces.Mechanisms;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PF.Modules.Debug.ViewModels
{
    public class MechanismDebugViewModel : RegionViewModelBase
    {
        public ObservableCollection<DebugTreeNode> TreeNodes { get; } = new ObservableCollection<DebugTreeNode>();

        private DebugTreeNode _selectedNode;
        public DebugTreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        // 这里只注入 你的模组基类或接口 (比如 IEnumerable<IMechanism> 或 IEnumerable<BaseMechanism>)
        // 如果没有提取接口，可以先用 object，然后在构建时使用反射获取名称
        public MechanismDebugViewModel(IEnumerable<IMechanism> mechanisms)
        {
            BuildTree(mechanisms);
        }

        private void BuildTree(IEnumerable<IMechanism> mechanisms)
        {
            foreach (var mech in mechanisms)
            {
                // 如果是已知类型，可以强转后获取名称；这里暂时用类的名称展示
                string name = mech.GetType().Name;
                TreeNodes.Add(new DebugTreeNode { NodeName = name, Payload = mech });
            }
        }
    }
}