using PF.Core.Constants;
using PF.Core.Interfaces.Hardware;
using PF.Core.Interfaces.Hardware.IO.Basic;
using PF.Core.Interfaces.Hardware.Motor.Basic;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;

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

        // 导航到独立调试页面的命令
        public DelegateCommand<object> NavigateToDebugCommand { get; }

        public HardwareDebugViewModel(IEnumerable<IHardwareDevice> hardwareDevices)
        {
            NavigateToDebugCommand = new DelegateCommand<object>(ExecuteNavigateToDebug);

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

        private void ExecuteNavigateToDebug(object payload)
        {
            if (payload == null) return;

            var parameters = new NavigationParameters();

            // 根据设备类型，导航到对应的独立调试视图，并传递设备实例
            if (payload is IAxis axis)
            {
                parameters.Add("Device", axis);
                RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion, NavigationConstants.Views.AxisDebugView, parameters);
            }
            else if (payload is IIOController io)
            {
                parameters.Add("Device", io);
                RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion, NavigationConstants.Views.IODebugView, parameters);
            }
        }
    }
}