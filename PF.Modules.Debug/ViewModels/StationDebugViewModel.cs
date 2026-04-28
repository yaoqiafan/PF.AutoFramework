using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Core.Interfaces.Station;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 工站调试主视图 ViewModel
    ///
    /// 左侧面板集成主控状态（状态、模式、报警、控制指令）与可点击的工站导航列表；
    /// 点击工站条目后，右侧 StationContentRegion 会动态加载对应的工站调试视图。
    /// 包含后台轮询定时器，用于实时同步主控和各子工站的运行状态及信号量。
    /// </summary>
    public class StationDebugViewModel : RegionViewModelBase, IDisposable
    {
        /// <summary>Prism 区域管理器，用于处理视图导航</summary>
        private readonly IRegionManager _regionManager;

        /// <summary>全局主控调度器，负责全线启停和状态管理</summary>
        private readonly IMasterController _controller;

        /// <summary>工站信号同步服务，管理流水线中上下游交互的信号量</summary>
        private readonly IStationSyncService _syncService;

        /// <summary>UI 轮询定时器，以固定频率刷新绑定的状态属性</summary>
        private readonly DispatcherTimer _pollTimer;

        // ── 主控状态属性 ─────────────────────────────────────────────────────

        /// <summary>主控器状态对应的界面颜色画刷映射字典</summary>
        private static readonly Dictionary<MachineState, Brush> _stateBrushMap = new()
        {
            // 运行状态采用高对比度翠绿色 (#02AD8B)
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x02, 0xad, 0x8b)) },
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) },
            { MachineState.InitAlarm,    new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)) },
            { MachineState.RunAlarm,     new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) },
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) },
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
        };

        /// <summary>未知或异常状态下的默认颜色画刷</summary>
        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        private MachineState _controllerState;
        /// <summary>获取主控器当前所处的运行状态</summary>
        public MachineState ControllerState
        {
            get => _controllerState;
            private set
            {
                SetProperty(ref _controllerState, value);
                RaisePropertyChanged(nameof(StatusBrush));
            }
        }

        private OperationMode _currentMode;
        /// <summary>获取当前的设备操作模式（如：正常、空跑、维修等）</summary>
        public OperationMode CurrentMode
        {
            get => _currentMode;
            private set => SetProperty(ref _currentMode, value);
        }

        private string _lastAlarmMessage = "无";
        /// <summary>获取主控或子工站最近一次触发的报警消息内容</summary>
        public string LastAlarmMessage
        {
            get => _lastAlarmMessage;
            private set => SetProperty(ref _lastAlarmMessage, value);
        }

        /// <summary>获取用于界面绑定的当前状态颜色画刷</summary>
        public Brush StatusBrush =>
            _stateBrushMap.TryGetValue(_controllerState, out var b) ? b : _defaultBrush;

        // ── 主控指令 ─────────────────────────────────────────────────────────

        /// <summary>触发全线初始化的命令</summary>
        public DelegateCommand InitializeAllCommand { get; }

        /// <summary>触发全线启动的命令</summary>
        public DelegateCommand StartAllCommand { get; }

        /// <summary>触发全线暂停的命令</summary>
        public DelegateCommand PauseAllCommand { get; }

        /// <summary>触发全线从暂停中恢复运行的命令</summary>
        public DelegateCommand ResumeAllCommand { get; }

        /// <summary>触发全线停止的命令</summary>
        public DelegateCommand StopAllCommand { get; }

        /// <summary>触发全线硬件复位与清警的命令</summary>
        public DelegateCommand ResetAllCommand { get; }

        // ── 流水线信号量树（Scope → Signal）──────────────────────────────────

        /// <summary>获取信号量作用域树节点列表，用于在界面展示当前的互锁或防呆信号状态</summary>
        public ObservableCollection<ScopeTreeNode> ScopeNodes { get; } = new();

        /// <summary>在 ContextMenu 中强制释放指定的单个信号量（允许计数 +1）的命令</summary>
        public DelegateCommand<SignalTreeNode> ReleaseSignalCommand { get; }

        /// <summary>在 ContextMenu 中强制将单个信号量复位至初始计数的命令</summary>
        public DelegateCommand<SignalTreeNode> ResetSignalCommand { get; }

        // ── 工站导航列表 ─────────────────────────────────────────────────────

        /// <summary>获取左侧栏的工站导航项列表</summary>
        public ObservableCollection<StationNavItem> NavItems { get; } = new();

        private StationNavItem _selectedItem;
        /// <summary>获取或设置当前在列表中被选中的工站导航项，赋值时会自动触发视图导航</summary>
        public StationNavItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null)
                    NavigateTo(value.ViewName);
            }
        }

        /// <summary>
        /// 实例化 <see cref="StationDebugViewModel"/>
        /// </summary>
        /// <param name="stations">所有已注册的子工站实例集合</param>
        /// <param name="controller">全局主控调度器实例</param>
        /// <param name="syncService">全局信号同步服务</param>
        /// <param name="regionManager">Prism 区域导航管理器</param>
        public StationDebugViewModel(
            IEnumerable<IStation> stations,
            IMasterController controller,
            IStationSyncService syncService,
            IRegionManager regionManager)
        {
            _regionManager = regionManager;
            _controller = controller;
            _syncService = syncService;

            // 订阅主控报警事件，用于在界面底部或状态栏更新报警文本
            _controller.MasterAlarmTriggered += OnMasterAlarmTriggered;

            BuildNavItems(stations);

            // 初始化全线控制命令及其可执行状态判断逻辑
            InitializeAllCommand = new DelegateCommand(
                async () => { try { await _controller.InitializeAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Uninitialized
                   || _controller.CurrentState == MachineState.Idle);

            StartAllCommand = new DelegateCommand(
                async () => { try { await _controller.StartAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Idle);

            PauseAllCommand = new DelegateCommand(
                () => { try { _controller.PauseAll(); } catch { } },
                () => _controller.CurrentState == MachineState.Running);

            ResumeAllCommand = new DelegateCommand(
                async () => { try { await _controller.ResumeAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Paused);

            StopAllCommand = new DelegateCommand(
                async () => { try { await _controller.StopAllAsync(); } catch { } },
                () => _controller.CurrentState == MachineState.Idle
                   || _controller.CurrentState == MachineState.Running
                   || _controller.CurrentState == MachineState.Paused);

            ResetAllCommand = new DelegateCommand(
                async () => { try { await _controller.ResetAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.InitAlarm
                   || _controller.CurrentState == MachineState.RunAlarm);

            // 信号量操作命令
            ReleaseSignalCommand = new DelegateCommand<SignalTreeNode>(node =>
            {
                if (node == null) return;
                try { _syncService.Release(node.SignalName, node.ParentScope); }
                catch (Exception ex) { Log(ex); }
            });

            ResetSignalCommand = new DelegateCommand<SignalTreeNode>(node =>
            {
                if (node == null) return;
                try { _syncService.ResetSingleSignal(node.SignalName, node.ParentScope); }
                catch (Exception ex) { Log(ex); }
            });

            // 初始化并启动 UI 定时轮询器 (数据绑定优先级)
            _pollTimer = new DispatcherTimer(DispatcherPriority.DataBind)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        /// <summary>
        /// 利用反射读取每个工站的 <see cref="StationUIAttribute"/> 特性，并构建排序后的导航列表
        /// </summary>
        private void BuildNavItems(IEnumerable<IStation> stations)
        {
            var items = new List<(StationNavItem Item, int Order)>();

            foreach (var station in stations)
            {
                var attr = station.GetType().GetCustomAttribute<StationUIAttribute>();
                if (attr == null) continue;

                items.Add((new StationNavItem
                {
                    Title = attr.Title,
                    ViewName = attr.ViewName,
                    StationName = station.StationName,
                    Station = station,
                }, attr.Order));
            }

            foreach (var (item, _) in items.OrderBy(x => x.Order))
                NavItems.Add(item);
        }

        /// <summary>
        /// 将指定的视图名称请求导航到主界面右侧的 StationContentRegion
        /// </summary>
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

        /// <summary>
        /// 定时器滴答事件：同步设备状态、命令的可执行性以及局部导航条目的状态
        /// </summary>
        private void OnPollTick(object sender, EventArgs e)
        {
            ControllerState = _controller.CurrentState;
            CurrentMode = _controller.CurrentMode;

            foreach (var item in NavItems)
                item.Refresh();

            RefreshSignals();

            // 强制重新评估所有的按钮/命令的可执行状态
            InitializeAllCommand.RaiseCanExecuteChanged();
            StartAllCommand.RaiseCanExecuteChanged();
            PauseAllCommand.RaiseCanExecuteChanged();
            ResumeAllCommand.RaiseCanExecuteChanged();
            StopAllCommand.RaiseCanExecuteChanged();
            ResetAllCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 刷新信号量树集合（增量更新策略，防止 UI 频繁重绘导致闪烁）
        /// </summary>
        private void RefreshSignals()
        {
            var snapshot = _syncService.GetSnapshot();

            // 按 scope 分组快照条目（key 格式："scope/name"）
            var grouped = new Dictionary<string, List<(string SignalName, int Initial, int Current)>>();
            foreach (var kv in snapshot)
            {
                var slash = kv.Key.IndexOf('/');
                if (slash < 0) continue;
                var scope = kv.Key[..slash];
                var signalName = kv.Key[(slash + 1)..];
                if (!grouped.TryGetValue(scope, out var list))
                    grouped[scope] = list = new List<(string, int, int)>();
                list.Add((signalName, kv.Value.InitialCount, kv.Value.CurrentCount));
            }

            // 同步 ScopeNodes
            var existingScopes = ScopeNodes.ToDictionary(n => n.ScopeName);

            foreach (var (scopeName, signals) in grouped)
            {
                if (!existingScopes.TryGetValue(scopeName, out var scopeNode))
                {
                    scopeNode = new ScopeTreeNode(scopeName);
                    ScopeNodes.Add(scopeNode);
                }

                var existingSignals = scopeNode.Signals.ToDictionary(s => s.SignalName);
                var snapshotNames = new HashSet<string>();

                foreach (var (signalName, initial, current) in signals)
                {
                    snapshotNames.Add(signalName);
                    if (existingSignals.TryGetValue(signalName, out var node))
                        node.Update(current);
                    else
                        scopeNode.Signals.Add(new SignalTreeNode(signalName, scopeName, initial, current));
                }

                // 移除已消失的信号量叶子节点
                for (int i = scopeNode.Signals.Count - 1; i >= 0; i--)
                {
                    if (!snapshotNames.Contains(scopeNode.Signals[i].SignalName))
                        scopeNode.Signals.RemoveAt(i);
                }
            }

            // 移除快照中不再存在的 scope 根节点
            for (int i = ScopeNodes.Count - 1; i >= 0; i--)
            {
                if (!grouped.ContainsKey(ScopeNodes[i].ScopeName))
                    ScopeNodes.RemoveAt(i);
            }
        }

        /// <summary>
        /// 主控器报警触发时的事件处理方法，更新最新报警信息展示
        /// </summary>
        private void OnMasterAlarmTriggered(object sender, StationAlarmEventArgs e) =>
            LastAlarmMessage = string.IsNullOrEmpty(e.RuntimeMessage)
                ? e.ErrorCode
                : $"{e.ErrorCode}: {e.RuntimeMessage}";

        /// <summary>
        /// 日志输出辅助方法
        /// </summary>
        private static void Log(Exception ex) =>
            System.Diagnostics.Debug.WriteLine($"[StationDebug] 指令执行失败: {ex.Message}");

        /// <summary>
        /// 停止定时器并注销全局事件监听，防止内存泄漏
        /// </summary>
        public void Dispose()
        {
            _pollTimer.Stop();
            _controller.MasterAlarmTriggered -= OnMasterAlarmTriggered;
        }

        /// <summary>
        /// 重写基类的方法以在 ViewModel 销毁时清理资源
        /// </summary>
        public override void Destroy() => Dispose();
    }

    /// <summary>
    /// 工站导航条目模型，持有对应工站的实时状态，并向左侧 UI 列表提供数据绑定。
    /// </summary>
    public class StationNavItem : BindableBase
    {
        private static readonly Dictionary<MachineState, Brush> _brushMap = new()
        {
            // 保持与主控相同的深色高对比度配色
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x02, 0xad, 0x8b)) },
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) },
            { MachineState.InitAlarm,    new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)) },
            { MachineState.RunAlarm,     new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) },
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) },
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
        };
        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        /// <summary>获取或设置工站在导航列表中的显示标题</summary>
        public string Title { get; init; }

        /// <summary>获取或设置用于 Prism 区域导航的对应视图名称</summary>
        public string ViewName { get; init; }

        /// <summary>获取或设置后台子工站实例的唯一名称</summary>
        public string StationName { get; init; }

        /// <summary>内部持有的底层子工站实例引用</summary>
        internal IStation Station { get; init; }

        private MachineState _state;
        /// <summary>获取该导航条目对应的子工站当前状态</summary>
        public MachineState State
        {
            get => _state;
            private set
            {
                SetProperty(ref _state, value);
                RaisePropertyChanged(nameof(StateBrush));
            }
        }

        private string _stepDescription = string.Empty;
        /// <summary>获取该导航条目对应的子工站当前流程步进的文本描述</summary>
        public string StepDescription
        {
            get => _stepDescription;
            private set => SetProperty(ref _stepDescription, value);
        }

        /// <summary>获取工站状态所对应的颜色画刷，供前端指示灯绑定使用</summary>
        public Brush StateBrush =>
            _brushMap.TryGetValue(_state, out var b) ? b : _defaultBrush;

        /// <summary>
        /// 由主控 ViewModel 的定时器统一调用，用于从底层的 <see cref="StationBase{T}"/> 拉取并刷新最新状态。
        /// </summary>
        internal void Refresh()
        {
            if (Station == null) return;
            State = Station.CurrentState;
            StepDescription = Station.CurrentStepDescription;
        }
    }
}