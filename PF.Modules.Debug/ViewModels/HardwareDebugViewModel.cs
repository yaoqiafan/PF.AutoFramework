using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
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

        public DelegateCommand<object> NavigateToDebugCommand { get; }

        public HardwareDebugViewModel(IHardwareManagerService hardwareManager)
        {
            NavigateToDebugCommand = new DelegateCommand<object>(ExecuteNavigateToDebug);
            BuildTree(hardwareManager);
        }

        /// <summary>
        /// 按硬件物理层级构建设备树：
        ///   第一层 — IMotionCard（板卡节点，可点击跳转板卡调试）
        ///   第二层 — IAttachedDevice（挂载在板卡上的轴 / IO 控制器，继承自 IAttachedDevice.ParentCard）
        ///   兜底组 — 既非板卡又无父板卡的独立设备（相机、仪器等）
        /// </summary>
        private void BuildTree(IHardwareManagerService hw)
        {
            var allDevices = hw.ActiveDevices.ToList();

            // ── 第一层：板卡节点 ──────────────────────────────────────────────
            var cards = allDevices.OfType<IMotionCard>().OrderBy(c => c.CardIndex).ToList();

            // 建立 cardId → 子设备列表 的映射（通过 IAttachedDevice.ParentCard）
            var childMap = allDevices
                .OfType<IAttachedDevice>()
                .Where(d => d.ParentCard != null)
                .GroupBy(d => d.ParentCard!.DeviceId)
                .ToDictionary(g => g.Key, g => g.Cast<IHardwareDevice>().ToList());

            // 所有已归入某张卡的子设备 ID 集合（用于筛选孤立设备）
            var attachedIds = childMap.Values
                .SelectMany(list => list)
                .Select(d => d.DeviceId)
                .ToHashSet();

            foreach (var card in cards)
            {
                var cardNode = new DebugTreeNode
                {
                    NodeName = $"[卡{card.CardIndex}] {card.DeviceName}",
                    Payload = card
                };

                if (childMap.TryGetValue(card.DeviceId, out var children))
                {
                    // 子设备按 Category 排序，同 Category 按设备名排序
                    foreach (var child in children.OrderBy(c => c.Category.ToString()).ThenBy(c => c.DeviceName))
                    {
                        cardNode.Children.Add(new DebugTreeNode
                        {
                            NodeName = child.DeviceName,
                            Payload  = child
                        });
                    }
                }

                TreeNodes.Add(cardNode);
            }

            // ── 兜底：既非板卡、又没有父板卡的独立设备 ─────────────────────
            var orphans = allDevices
                .Where(d => d is not IMotionCard && !attachedIds.Contains(d.DeviceId))
                .OrderBy(d => d.Category.ToString())
                .ThenBy(d => d.DeviceName)
                .ToList();

            if (orphans.Any())
            {
                var orphanGroup = new DebugTreeNode { NodeName = "独立设备" };
                foreach (var d in orphans)
                    orphanGroup.Children.Add(new DebugTreeNode { NodeName = d.DeviceName, Payload = d });
                TreeNodes.Add(orphanGroup);
            }
        }

        private void ExecuteNavigateToDebug(object payload)
        {
            if (payload == null) return;

            var parameters = new NavigationParameters();

            // 板卡节点：优先判断，避免子接口误匹配
            if (payload is IMotionCard card)
            {
                parameters.Add("Device", card);
                RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion,
                    NavigationConstants.Views.CardDebugView, parameters);
            }
            else if (payload is IAxis axis)
            {
                parameters.Add("Device", axis);
                RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion,
                    NavigationConstants.Views.AxisDebugView, parameters);
            }
            else if (payload is IIOController io)
            {
                parameters.Add("Device", io);
                RegionManager.RequestNavigate(NavigationConstants.Regions.DebugViewRegion,
                    NavigationConstants.Views.IODebugView, parameters);
            }
        }
    }
}
