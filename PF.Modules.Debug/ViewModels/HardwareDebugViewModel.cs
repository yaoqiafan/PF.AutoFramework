using PF.Core.Interfaces.Hardware;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PF.Modules.Debug.ViewModels
{
    public class HardwareDebugViewModel : RegionViewModelBase
    {
        public ObservableCollection<DebugTreeNode> TreeNodes { get; } = new ObservableCollection<DebugTreeNode>();

        private DebugTreeNode _selectedNode;
        public DebugTreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        // 这里只注入 IHardwareDevice
        public HardwareDebugViewModel(IEnumerable<IHardwareDevice> hardwareDevices)
        {
            BuildTree(hardwareDevices);
        }

        private void BuildTree(IEnumerable<IHardwareDevice> hardwares)
        {
            var groups = hardwares.GroupBy(d => d.Category).OrderBy(g => g.Key.ToString());
            foreach (var group in groups)
            {
                var categoryNode = new DebugTreeNode { NodeName = group.Key.ToString() };
                foreach (var device in group)
                {
                    categoryNode.Children.Add(new DebugTreeNode { NodeName = device.DeviceName, Payload = device });
                }
                TreeNodes.Add(categoryNode);
            }
        }
    }
}