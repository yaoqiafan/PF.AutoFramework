using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Identity;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PF.Modules.Debug.ViewModels
{
    public class HardwareDebugViewModel : RegionViewModelBase
    {
        private readonly IHardwareManagerService _hardwareManager;
        private readonly IUserService _userService;

        public ObservableCollection<DebugTreeNode> TreeNodes { get; } = new();

        private DebugTreeNode _selectedNode;
        public DebugTreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        private bool _isSuperUser;
        /// <summary>当前用户是否为 SuperUser（控制全局模拟切换按钮可见性）</summary>
        public bool IsSuperUser
        {
            get => _isSuperUser;
            private set => SetProperty(ref _isSuperUser, value);
        }

        private bool _isGlobalSimulated;
        /// <summary>全局模拟模式开关状态（所有配置均为模拟时为 true）</summary>
        public bool IsGlobalSimulated
        {
            get => _isGlobalSimulated;
            set => SetProperty(ref _isGlobalSimulated, value);
        }

        private bool _isBusy;
        /// <summary>正在执行异步硬件操作（防止重复点击）</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                SetProperty(ref _isBusy, value);
                ToggleGlobalSimulationCommand.RaiseCanExecuteChanged();
                ToggleDeviceSimulationCommand.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand<object> NavigateToDebugCommand { get; }

        /// <summary>一键切换全局模拟模式（仅 SuperUser 可见）</summary>
        public DelegateCommand ToggleGlobalSimulationCommand { get; }

        /// <summary>切换单个设备的模拟模式</summary>
        public DelegateCommand<DebugTreeNode> ToggleDeviceSimulationCommand { get; }

        public HardwareDebugViewModel(IHardwareManagerService hardwareManager, IUserService userService)
        {
            _hardwareManager = hardwareManager;
            _userService = userService;

            NavigateToDebugCommand        = new DelegateCommand<object>(ExecuteNavigateToDebug);
            ToggleGlobalSimulationCommand = new DelegateCommand(async () => await ExecuteToggleGlobalAsync(), () => !IsBusy);
            ToggleDeviceSimulationCommand = new DelegateCommand<DebugTreeNode>(async node => await ExecuteToggleDeviceAsync(node), _ => !IsBusy);

            UpdateSuperUserState();
            _userService.CurrentUserChanged += (_, _) => UpdateSuperUserState();

            BuildTree();
            UpdateGlobalSimulatedState();
        }

        // ── 全局模拟切换 ──────────────────────────────────────────────────────

        private async Task ExecuteToggleGlobalAsync()
        {
            IsBusy = true;
            try
            {
                // 仅持久化配置，不触发热重载；用户需手动点击重连按钮使配置生效
                await _hardwareManager.SetGlobalSimulationModeAsync(!IsGlobalSimulated);
                UpdateGlobalSimulatedState();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── 单设备模拟切换 ────────────────────────────────────────────────────

        private async Task ExecuteToggleDeviceAsync(DebugTreeNode node)
        {
            if (node?.Payload is not IHardwareDevice device) return;

            IsBusy = true;
            try
            {
                // ToggleButton 双向绑定已预先翻转 node.IsSimulated，此处读取即为目标值
                device.IsSimulated = node.IsSimulated;

                await device.DisconnectAsync();
                await device.ConnectAsync();

                // 回写真实状态（ConnectAsync 失败时修正 UI）
                node.IsSimulated = device.IsSimulated;

                // 持久化，使下次 ReloadAllAsync 后仍生效
                var config = _hardwareManager.GetConfig(device.DeviceId);
                if (config != null)
                {
                    config.IsSimulated = device.IsSimulated;
                    await _hardwareManager.SaveConfigAsync(config);
                }

                UpdateGlobalSimulatedState();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── 树构建 ────────────────────────────────────────────────────────────

        private void BuildTree()
        {
            var allDevices = _hardwareManager.ActiveDevices.ToList();
            var cards      = allDevices.OfType<IMotionCard>().OrderBy(c => c.CardIndex).ToList();

            var childMap = allDevices
                .OfType<IAttachedDevice>()
                .Where(d => d.ParentCard != null)
                .GroupBy(d => d.ParentCard!.DeviceId)
                .ToDictionary(g => g.Key, g => g.Cast<IHardwareDevice>().ToList());

            var attachedIds = childMap.Values
                .SelectMany(list => list)
                .Select(d => d.DeviceId)
                .ToHashSet();

            foreach (var card in cards)
            {
                var cardNode = new DebugTreeNode
                {
                    NodeName = $"[卡{card.CardIndex}] {card.DeviceName}",
                    Payload  = card
                };

                if (childMap.TryGetValue(card.DeviceId, out var children))
                {
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

        // ── 导航 ──────────────────────────────────────────────────────────────

        private void ExecuteNavigateToDebug(object payload)
        {
            if (payload == null) return;
            var parameters = new NavigationParameters();

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

        // ── 辅助 ──────────────────────────────────────────────────────────────

        private void UpdateSuperUserState()
            => IsSuperUser = _userService.IsAuthorized(UserLevel.SuperUser);

        private void UpdateGlobalSimulatedState()
        {
            var configs = _hardwareManager.GetAllConfigs().ToList();
            IsGlobalSimulated = configs.Any() && configs.All(c => c.IsSimulated);
        }
    }
}
