using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Card;
using System.Collections.Generic;
using System.Linq;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware;
using PF.Infrastructure.Logging;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 图像采集卡调试 ViewModel。
    ///
    /// <para>采集卡在框架里是独立的顶级设备而非相机的附属品：CameraLink 相机没有自己的网络栈，
    /// 流的切帧（一帧多少行、帧超时、从哪起帧）全部发生在卡上，因此需要一个能单独下发这些
    /// 节点并单独发软触发的调试面板。</para>
    /// </summary>
    public class FrameGrabberDebugViewModel : RegionViewModelBase
    {
        private IFrameGrabberCard _card;
        private BaseDevice _baseDevice;

        /// <summary>
        /// 硬件分类日志。面板不再自建日志列表——操作结果统一发到 ILogService，
        /// 由主窗体底部的日志栏统一展示，与设备本身写的硬件日志混排在同一条时间线上，
        /// 排查时不必在面板日志和系统日志之间来回对照。
        /// </summary>
        private readonly CategoryLogger _logger;

        private readonly DispatcherTimer _statusTimer;

        /// <summary>初始化采集卡调试 ViewModel</summary>
        public FrameGrabberDebugViewModel(ILogService logService)
        {
            _logger = CategoryLoggerFactory.Hardware(logService);

            InitializeCommands();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _statusTimer.Tick += OnStatusTimerTick;
        }

        #region 【Prism 导航生命周期】

        /// <summary>
        /// 同一张卡复用已有实例，换卡才新建。
        /// <para>基类默认恒为 false（每次导航都新建实例），调试面板不适用：
        /// 填好的帧长、触发源、节点名一离开页面就没了。</para>
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext)
            => navigationContext.Parameters.ContainsKey("Device")
               && ReferenceEquals(navigationContext.Parameters.GetValue<object>("Device"), _card);

        /// <summary>配合 <see cref="IsNavigationTarget"/>：实例要留在 Region 中才谈得上复用。</summary>
        public override bool KeepAlive => true;

        /// <summary>导航进入时绑定采集卡设备</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Device")) return;

            var card = navigationContext.Parameters.GetValue<IFrameGrabberCard>("Device");

            // 复用实例时是同一张卡，不重置任何已填参数
            if (!ReferenceEquals(card, _card))
            {
                _card = card;
                _baseDevice = _card as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知采集卡设备";
                    DeviceDescription = "无法获取底层设备信息";
                }
            }

            // 进页面就把设备真实状态读回来。
            // 不依赖"ViewModel 实例能活到下次导航"——DebugViewRegion 嵌在硬件调试页里，
            // 外层页面一旦被回收，内层实例连同填写的值一起没了；而且显示设备的实际值
            // 本来就比显示"上次谁填了什么"更可信。
            RunAsync("加载采集卡状态", LoadFromCardAsync);

            _statusTimer.Start();
        }

        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _statusTimer.Stop();
        }

        #endregion

        #region 【设备信息与状态】

        private string _deviceName = "未选中采集卡";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _isAlarm;
        /// <summary>获取或设置是否报警</summary>
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        private string _modelName = "-";
        /// <summary>获取或设置采集卡型号</summary>
        public string ModelName { get => _modelName; set => SetProperty(ref _modelName, value); }

        private string _serialNumber = "-";
        /// <summary>获取或设置采集卡序列号</summary>
        public string SerialNumber { get => _serialNumber; set => SetProperty(ref _serialNumber, value); }

        private string _currentStreamDevice = "-";
        /// <summary>
        /// 当前流绑定的相机（卡节点 CurrentStreamDevice）。
        /// 为空说明流上没有相机，此时 ImageHeight 等流参数不可写。
        /// </summary>
        public string CurrentStreamDevice { get => _currentStreamDevice; set => SetProperty(ref _currentStreamDevice, value); }

        #endregion

        #region 【帧控制参数】

        private string _imageHeight = "1000";
        /// <summary>获取或设置帧长（一帧累计多少行）</summary>
        public string ImageHeight { get => _imageHeight; set => SetProperty(ref _imageHeight, value); }

        private string _frameTimeoutMs = "3000";
        /// <summary>获取或设置帧超时（毫秒，行数攒不满时的兜底出图）</summary>
        public string FrameTimeoutMs { get => _frameTimeoutMs; set => SetProperty(ref _frameTimeoutMs, value); }

        private bool _triggerEnable;
        /// <summary>获取或设置是否启用帧触发（false = 连续模式，攒满帧长即出图）</summary>
        public bool TriggerEnable { get => _triggerEnable; set => SetProperty(ref _triggerEnable, value); }

        private string _triggerSource = "SoftwareSignal0";
        /// <summary>获取或设置帧触发源</summary>
        public string TriggerSource { get => _triggerSource; set => SetProperty(ref _triggerSource, value); }

        private string _triggerActivation = "RisingEdge";
        /// <summary>获取或设置帧触发有效边沿</summary>
        public string TriggerActivation { get => _triggerActivation; set => SetProperty(ref _triggerActivation, value); }

        private string _streamSelector = string.Empty;
        /// <summary>获取或设置流选择器（留空则不下发）</summary>
        public string StreamSelector { get => _streamSelector; set => SetProperty(ref _streamSelector, value); }

        private string _cameraType = string.Empty;
        /// <summary>获取或设置相机类型匹配（CameraLink 位宽，留空则不下发）</summary>
        public string CameraType { get => _cameraType; set => SetProperty(ref _cameraType, value); }

        private string _partialImageControl = string.Empty;
        /// <summary>获取或设置残帧处理策略（留空则不下发）</summary>
        public string PartialImageControl { get => _partialImageControl; set => SetProperty(ref _partialImageControl, value); }

        #endregion

        #region 【属性树】

        /// <summary>
        /// 设备属性树的全部节点（名称/分类/类型/权限/当前值）。
        /// 从卡的 GenICam XML 枚举而来，等价于 MVS 客户端的属性树，不必再手填节点名。
        /// </summary>
        public ObservableCollection<GenICamNode> Nodes { get; } = new();

        private GenICamNode _selectedNode;
        /// <summary>当前选中的节点。选中即把节点名/当前值/可选项填进编辑区。</summary>
        public GenICamNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (!SetProperty(ref _selectedNode, value) || value == null) return;

                NodeName = value.Name;
                NodeValue = value.Value ?? string.Empty;

                NodeEnumEntries.Clear();
                foreach (var e in value.EnumEntries) NodeEnumEntries.Add(e);
            }
        }

        private string _nodeFilter = string.Empty;
        /// <summary>节点过滤关键字（按名称/显示名/分类匹配）。</summary>
        public string NodeFilter
        {
            get => _nodeFilter;
            set { if (SetProperty(ref _nodeFilter, value)) ApplyNodeFilter(); }
        }

        private bool _writableNodesOnly;
        /// <summary>只看当前可写的节点。排查"为什么改不了"时特别有用。</summary>
        public bool WritableNodesOnly
        {
            get => _writableNodesOnly;
            set { if (SetProperty(ref _writableNodesOnly, value)) ApplyNodeFilter(); }
        }

        private string _nodeSummary = "尚未枚举";
        /// <summary>属性树统计说明。</summary>
        public string NodeSummary { get => _nodeSummary; set => SetProperty(ref _nodeSummary, value); }

        private string _nodeName = string.Empty;
        /// <summary>当前操作的 GenICam 节点名（由属性树选中填入，也可手工修改）</summary>
        public string NodeName { get => _nodeName; set => SetProperty(ref _nodeName, value); }

        private string _nodeValue = string.Empty;
        /// <summary>获取或设置节点值</summary>
        public string NodeValue { get => _nodeValue; set => SetProperty(ref _nodeValue, value); }

        /// <summary>选中节点的枚举可选项（绑到可编辑下拉框）</summary>
        public ObservableCollection<string> NodeEnumEntries { get; } = new();

        /// <summary>枚举到的本卡相机列表</summary>
        public ObservableCollection<string> DiscoveredCameras { get; } = new();

        #endregion

        #region 【命令】

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>模拟报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; private set; }
        /// <summary>下发帧控制配置命令</summary>
        public DelegateCommand ApplyFrameControlCommand { get; private set; }
        /// <summary>帧软触发命令</summary>
        public DelegateCommand SoftwareTriggerCommand { get; private set; }
        /// <summary>枚举本卡相机命令</summary>
        public DelegateCommand DiscoverCamerasCommand { get; private set; }
        /// <summary>读取节点命令</summary>
        public DelegateCommand ReadNodeCommand { get; private set; }
        /// <summary>写入节点命令</summary>
        public DelegateCommand WriteNodeCommand { get; private set; }
        /// <summary>执行命令节点命令</summary>
        public DelegateCommand ExecuteNodeCommand { get; private set; }
        /// <summary>刷新采集卡状态命令（型号/SN/流参数/相机列表）</summary>
        public DelegateCommand RefreshStateCommand { get; private set; }
        /// <summary>枚举设备属性树命令</summary>
        public DelegateCommand EnumerateNodesCommand { get; private set; }

        private void InitializeCommands()
        {
            // DelegateCommand 的 async lambda 是 async void：未捕获异常会击穿到 Dispatcher 直接崩进程，
            // 因此所有异步命令一律经 RunAsync 包一层
            ConnectCommand = new DelegateCommand(() => RunAsync("连接", async () =>
            {
                if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None);
                await LoadFromCardAsync();
            }));

            RefreshStateCommand = new DelegateCommand(() => RunAsync("刷新采集卡状态", LoadFromCardAsync));

            DisconnectCommand = new DelegateCommand(() => RunAsync("断开连接", async () =>
            {
                if (_baseDevice != null) await _baseDevice.DisconnectAsync();
            }));

            ResetCommand = new DelegateCommand(() => RunAsync("复位", async () =>
            {
                if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None);
                await LoadFromCardAsync();
            }));

            SimulateAlarmCommand = new DelegateCommand(() =>
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.FrameGrabberDisconnected, "调试页面手动模拟采集卡报警"));

            ApplyFrameControlCommand = new DelegateCommand(() => RunAsync("下发帧控制", async () =>
            {
                if (_card == null) return;
                await _card.ApplyFrameControlAsync(BuildFrameControlConfig());
                Log("帧控制配置已下发（逐节点结果见上条硬件日志）。");
            }));

            SoftwareTriggerCommand = new DelegateCommand(() => RunAsync("帧软触发", async () =>
            {
                if (_card == null) return;
                bool ok = await _card.SoftwareTriggerFrameAsync();
                if (ok) Log("帧软触发已发送。");
                else LogWarn("帧软触发失败（节点不存在或不可执行）。");
            }));

            DiscoverCamerasCommand = new DelegateCommand(() => RunAsync("枚举相机", async () =>
            {
                DiscoveredCameras.Clear();
                if (_card == null) return;

                var list = await _card.DiscoverCamerasAsync();
                foreach (var d in list) DiscoveredCameras.Add(d.DisplayName);

                Log($"本卡下发现 {list.Count} 台相机。");
            }));

            ReadNodeCommand = new DelegateCommand(() => RunAsync("读取节点", async () =>
            {
                if (_card == null || string.IsNullOrWhiteSpace(NodeName)) return;

                NodeEnumEntries.Clear();

                string value = await _card.GetNodeAsync(NodeName);
                if (value == null)
                {
                    LogWarn($"节点 '{NodeName}' 读取失败（不存在或不可读）。");
                    return;
                }

                NodeValue = value;
                foreach (var e in await _card.GetEnumEntriesAsync(NodeName)) NodeEnumEntries.Add(e);

                Log($"节点 '{NodeName}' = '{value}'"
                    + (NodeEnumEntries.Count > 0 ? $"（可选项 {NodeEnumEntries.Count} 个）" : string.Empty));
            }));

            WriteNodeCommand = new DelegateCommand(() => RunAsync("写入节点", async () =>
            {
                if (_card == null || string.IsNullOrWhiteSpace(NodeName)) return;

                bool ok = await _card.SetNodeAsync(NodeName, NodeValue ?? string.Empty);
                if (ok) Log($"节点 '{NodeName}' 已写入 '{NodeValue}'。");
                else LogWarn($"节点 '{NodeName}' 写入失败（不可写或值非法）。");
            }));

            ExecuteNodeCommand = new DelegateCommand(() => RunAsync("执行节点", async () =>
            {
                if (_card == null || string.IsNullOrWhiteSpace(NodeName)) return;

                bool ok = await _card.ExecuteCommandAsync(NodeName);
                if (ok) Log($"命令节点 '{NodeName}' 已执行。");
                else LogWarn($"命令节点 '{NodeName}' 执行失败。");
            }));

            EnumerateNodesCommand = new DelegateCommand(() => RunAsync("枚举属性树", async () =>
            {
                if (_card == null) return;

                NodeSummary = "正在枚举...";
                _allNodes = await _card.EnumerateNodesAsync();
                ApplyNodeFilter();
            }));
        }

        #endregion

        #region 【私有辅助】

        /// <summary>按界面填写值组装帧控制配置。空字符串一律保持为空（语义为不下发）。</summary>
        private FrameControlConfig BuildFrameControlConfig() => new()
        {
            ImageHeight = int.TryParse(ImageHeight, out var h) ? h : 0,
            FrameTimeoutMs = int.TryParse(FrameTimeoutMs, out var t) ? t : 0,
            TriggerEnable = TriggerEnable,
            TriggerSource = TriggerSource ?? string.Empty,
            TriggerActivation = TriggerActivation,
            StreamSelector = StreamSelector,
            CameraType = CameraType,
            PartialImageControl = PartialImageControl,
        };

        /// <summary>属性树枚举结果的全集，界面上按 <see cref="NodeFilter"/> / <see cref="WritableNodesOnly"/> 过滤后展示。</summary>
        private IReadOnlyList<GenICamNode> _allNodes = Array.Empty<GenICamNode>();

        /// <summary>按关键字与"只看可写"过滤属性树。</summary>
        private void ApplyNodeFilter()
        {
            Nodes.Clear();

            IEnumerable<GenICamNode> query = _allNodes;

            if (WritableNodesOnly)
                query = query.Where(n => n.IsWritable);

            if (!string.IsNullOrWhiteSpace(NodeFilter))
            {
                string key = NodeFilter.Trim();
                query = query.Where(n =>
                    n.Name.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || n.DisplayName.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || n.Category.Contains(key, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var n in query.OrderBy(n => n.Category).ThenBy(n => n.Name))
                Nodes.Add(n);

            NodeSummary = _allNodes.Count == 0
                ? "尚未枚举"
                : $"共 {_allNodes.Count} 个可用节点，其中可写 {_allNodes.Count(n => n.IsWritable)} 个；当前显示 {Nodes.Count} 个";
        }

        private void RefreshCardInfo()
        {
            ModelName = string.IsNullOrEmpty(_card?.ModelName) ? "-" : _card.ModelName;
            SerialNumber = string.IsNullOrEmpty(_card?.SerialNumber) ? "-" : _card.SerialNumber;
        }

        /// <summary>
        /// 从卡上读回型号/序列号、当前流参数与本卡相机列表，回填到界面。
        /// 未连接时只刷新型号/SN（都会是 "-"），不去读节点。
        /// </summary>
        private async Task LoadFromCardAsync()
        {
            RefreshCardInfo();

            if (_card == null || !_card.IsConnected) return;

            string? v;
            if ((v = await _card.GetNodeAsync("ImageHeight")) != null) ImageHeight = v;
            if ((v = await _card.GetNodeAsync("FrameTimeoutTime")) != null) FrameTimeoutMs = v;
            if ((v = await _card.GetNodeAsync("StreamSelector")) != null) StreamSelector = v;
            if ((v = await _card.GetNodeAsync("CameraType")) != null) CameraType = v;
            if ((v = await _card.GetNodeAsync("StreamTriggerSource")) != null) TriggerSource = v;
            if ((v = await _card.GetNodeAsync("StreamTriggerActivation")) != null) TriggerActivation = v;

            if ((v = await _card.GetNodeAsync("StreamTriggerEnable")) != null)
                TriggerEnable = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

            // 残帧策略：CameraLink 卡叫 StreamPartialImageControl，XoF 卡叫 PartialImageOutputMode
            v = await _card.GetNodeAsync("StreamPartialImageControl")
                ?? await _card.GetNodeAsync("PartialImageOutputMode");
            if (v != null) PartialImageControl = v;

            CurrentStreamDevice = await _card.GetNodeAsync("CurrentStreamDevice") ?? "（未绑定相机）";

            // 相机列表也一并刷新：打开这个页面十有八九就是想看卡上挂了什么
            DiscoveredCameras.Clear();
            foreach (var d in await _card.DiscoverCamerasAsync()) DiscoveredCameras.Add(d.DisplayName);
        }

        /// <summary>统一的异步命令外壳：吞掉异常并落到日志栏，不让 async void 击穿进程。</summary>
        private async void RunAsync(string opName, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.Error($"[{DeviceName}] {opName}失败：{ex.Message}", ex);
            }
        }

        private void Log(string message) => _logger.Info($"[{DeviceName}] {message}");

        private void LogWarn(string message) => _logger.Warn($"[{DeviceName}] {message}");

        private void OnStatusTimerTick(object sender, EventArgs e)
        {
            if (_baseDevice == null) return;

            IsConnected = _baseDevice.IsConnected;
            IsAlarm = _baseDevice.HasAlarm;
        }

        #endregion
    }
}
