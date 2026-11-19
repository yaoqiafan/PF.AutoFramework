using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Enums.Hardware;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Analog;
using PF.Core.Models.Device.Hardware.IO.Analog;
using PF.Infrastructure.Hardware;
using PF.Infrastructure.Hardware.IO.Analog;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 模拟量 IO 调试视图模型：AI 实时读数、AO 写出、仿真注入、通道配置。
    /// <para>只认 <see cref="IAnalogIOController"/> 契约，不区分设备挂在板卡下还是 IO 模块下。
    /// 同步读由接口契约保证不阻塞，因此定时器直接在 UI 线程读取；写出统一走异步版本。</para>
    /// </summary>
    public class AnalogIODebugViewModel : RegionViewModelBase
    {
        private readonly IHardwareManagerService _hardwareManager;
        private readonly DispatcherTimer _pollingTimer;

        private IAnalogIOController? _device;
        private BaseDevice? _baseDevice;
        private IAnalogSimulationControl? _sim;

        /// <summary>方向下拉项（通道配置表用）</summary>
        public static AnalogDirection[] DirectionValues { get; } = Enum.GetValues<AnalogDirection>();
        /// <summary>信号类型下拉项</summary>
        public static AnalogSignalType[] SignalTypeValues { get; } = Enum.GetValues<AnalogSignalType>();
        /// <summary>仿真模式下拉项</summary>
        public static AnalogSimMode[] SimModeValues { get; } = Enum.GetValues<AnalogSimMode>();

        /// <summary>初始化模拟量调试 ViewModel</summary>
        public AnalogIODebugViewModel(IHardwareManagerService hardwareManager)
        {
            _hardwareManager = hardwareManager;

            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null && Allowed("连接模拟量设备")) await _baseDevice.ConnectAsync(); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null && Allowed("断开模拟量设备")) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null && Allowed("复位模拟量设备")) await _baseDevice.ResetAsync(); });
            // 写安全值是"让输出回到安全态"的动作，任何状态下都允许，不过守卫
            SimulateAlarmCommand = new DelegateCommand(() =>
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.AnalogModuleUnavailable, "调试页面手动模拟模拟量模块不可用"));
            ApplySafeOutputsCommand = new DelegateCommand(async () =>
            {
                if (_device == null) return;
                bool ok = await _device.ApplySafeOutputsAsync();
                StatusText = ok ? "已将配置了安全值的 AO 全部写回安全值" : "部分 AO 未能写回安全值，详见日志";
            });

            AddInputCommand = new DelegateCommand(() => AddEditRow(AnalogDirection.Input));
            AddOutputCommand = new DelegateCommand(() => AddEditRow(AnalogDirection.Output));
            RemoveChannelCommand = new DelegateCommand(() =>
            {
                if (SelectedEditChannel != null) EditChannels.Remove(SelectedEditChannel);
            });
            ApplyPresetCommand = new DelegateCommand(ApplyPresetToSelected);
            ApplySignalDefaultsCommand = new DelegateCommand(ApplySignalDefaultsToSelected);
            SaveAsPresetCommand = new DelegateCommand(SaveSelectedAsPreset);
            RevertChannelsCommand = new DelegateCommand(LoadEditChannels);
            SaveChannelsCommand = new DelegateCommand(async () => await SaveChannelsAsync());

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        /// <summary>导航进入：绑定设备、构建通道行、启动轮询</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            if (!navigationContext.Parameters.ContainsKey("Device")) return;

            _device = navigationContext.Parameters.GetValue<IAnalogIOController>("Device");
            _baseDevice = _device as BaseDevice;
            _sim = _device as IAnalogSimulationControl;

            DeviceName = _device?.DeviceName ?? "未知模拟量设备";
            RefreshDescription();

            Presets.Clear();
            foreach (var p in AnalogPresetStore.Load(msg => StatusText = msg)) Presets.Add(p);
            SelectedPreset = Presets.FirstOrDefault();

            RebuildRows();
            LoadEditChannels();
            OnPollingTimerTick(this, EventArgs.Empty);
            _pollingTimer.Start();
        }

        /// <summary>导航离开：停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
        }

        #endregion

        #region 【设备基础属性】

        private string _deviceName = "未选中模拟量设备";
        /// <summary>设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _isSimulated;
        /// <summary>是否仿真</summary>
        public bool IsSimulated { get => _isSimulated; set => SetProperty(ref _isSimulated, value); }

        private string _statusText = string.Empty;
        /// <summary>页面底部状态提示</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; }
        /// <summary>断开命令</summary>
        public DelegateCommand DisconnectCommand { get; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; }
        /// <summary>模拟报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; }
        /// <summary>全部 AO 回安全值命令</summary>
        public DelegateCommand ApplySafeOutputsCommand { get; }

        private void RefreshDescription()
        {
            if (_baseDevice == null)
            {
                DeviceDescription = "无法获取底层设备信息";
                return;
            }

            string node = _device is CardAnalogIODevice card
                ? (card.NodeId?.ToString() ?? "板卡本体")
                : "-";
            var parent = (_device as IAttachedDevice)?.ParentDevice;
            string parentText = parent == null ? "未挂载" : parent.DeviceName;
            string cardText = parent is IAnalogCarrier carrier
                ? (carrier.AnalogCard?.DeviceName ?? "不支持模拟量")
                : "-";
            DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated} | 节点: {node} | 父设备: {parentText} | 落点板卡: {cardText}";
        }

        #endregion

        #region 【实时监控】

        /// <summary>AI 行</summary>
        public ObservableCollection<AnalogInputRowViewModel> Inputs { get; } = new();

        /// <summary>AO 行</summary>
        public ObservableCollection<AnalogOutputRowViewModel> Outputs { get; } = new();

        private bool _hasNoChannels;
        /// <summary>通道表为空（提示到「通道配置」页添加）</summary>
        public bool HasNoChannels { get => _hasNoChannels; set => SetProperty(ref _hasNoChannels, value); }

        private void RebuildRows()
        {
            Inputs.Clear();
            Outputs.Clear();
            if (_device == null) return;

            foreach (var ai in _device.InputChannels)
            {
                bool canFollow = _sim?.CanFollowOutput(ai.Channel) == true;
                Inputs.Add(new AnalogInputRowViewModel(ai, canFollow,
                    (ch, mode) => _sim?.SetSimMode(ch, mode),
                    ApplySimValue));
            }
            foreach (var ao in _device.OutputChannels)
                Outputs.Add(new AnalogOutputRowViewModel(ao, WriteOutputAsync));

            HasNoChannels = Inputs.Count == 0 && Outputs.Count == 0;
        }

        private void ApplySimValue(AnalogInputRowViewModel row)
        {
            if (_sim == null) return;
            if (!double.TryParse(row.SimValueText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) && !double.TryParse(row.SimValueText, out v))
            {
                StatusText = $"AI{row.Channel} 仿真值不是有效数字";
                return;
            }
            _sim.SetSimInput(row.Channel, v);
            StatusText = $"AI{row.Channel}【{row.Name}】已注入仿真值 {v}（只在内存，重连后恢复配置值）";
        }

        private bool Allowed(string action) => DebugOperationGuard.EnsureHardwareOperationAllowed(MessageService, action);

        private async Task WriteOutputAsync(AnalogOutputRowViewModel row, double value)
        {
            if (_device == null) return;
            if (!Allowed($"写出 AO{row.Channel}"))
            {
                row.ResultText = "设备运行中，未写出";
                return;
            }
            bool ok = await _device.WriteAnalogOutputAsync(row.Channel, value);
            row.ResultText = ok
                ? $"已写出 {value:G6}{row.Unit}（{DateTime.Now:HH:mm:ss}）"
                : (value < row.OutputMin || value > row.OutputMax
                    ? $"已拒绝：{value:G6} 超出限幅 [{row.OutputMin:G6}, {row.OutputMax:G6}]"
                    : "写出失败，详见硬件日志");
        }

        private void OnPollingTimerTick(object? sender, EventArgs e)
        {
            if (_device == null) return;

            if (_baseDevice != null)
            {
                IsConnected = _baseDevice.IsConnected;
                if (IsSimulated != _baseDevice.IsSimulated)
                {
                    IsSimulated = _baseDevice.IsSimulated;
                    RefreshDescription();
                }
            }

            foreach (var row in Inputs)
            {
                var mode = _sim?.GetSimMode(row.Channel) ?? AnalogSimMode.AlwaysPass;
                row.Apply(_device.ReadAnalogInputDetail(row.Channel), IsSimulated, mode);
            }
            foreach (var row in Outputs)
                row.Apply(_device.ReadAnalogOutput(row.Channel));
        }

        #endregion

        #region 【通道配置】

        /// <summary>正在编辑的通道表副本（保存前不影响设备）</summary>
        public ObservableCollection<AnalogChannelConfig> EditChannels { get; } = new();

        private AnalogChannelConfig? _selectedEditChannel;
        /// <summary>选中的编辑行</summary>
        public AnalogChannelConfig? SelectedEditChannel { get => _selectedEditChannel; set => SetProperty(ref _selectedEditChannel, value); }

        /// <summary>传感器预设库</summary>
        public ObservableCollection<AnalogPreset> Presets { get; } = new();

        private AnalogPreset? _selectedPreset;
        /// <summary>选中的预设</summary>
        public AnalogPreset? SelectedPreset { get => _selectedPreset; set => SetProperty(ref _selectedPreset, value); }

        private string _configStatusText = string.Empty;
        /// <summary>通道配置页状态提示</summary>
        public string ConfigStatusText { get => _configStatusText; set => SetProperty(ref _configStatusText, value); }

        /// <summary>新增 AI 行</summary>
        public DelegateCommand AddInputCommand { get; }
        /// <summary>新增 AO 行</summary>
        public DelegateCommand AddOutputCommand { get; }
        /// <summary>删除选中行</summary>
        public DelegateCommand RemoveChannelCommand { get; }
        /// <summary>选中预设套用到选中行</summary>
        public DelegateCommand ApplyPresetCommand { get; }
        /// <summary>按信号类型填原始量程</summary>
        public DelegateCommand ApplySignalDefaultsCommand { get; }
        /// <summary>选中行存为预设</summary>
        public DelegateCommand SaveAsPresetCommand { get; }
        /// <summary>撤销未保存的修改</summary>
        public DelegateCommand RevertChannelsCommand { get; }
        /// <summary>校验并保存通道表</summary>
        public DelegateCommand SaveChannelsCommand { get; }

        private void LoadEditChannels()
        {
            EditChannels.Clear();
            if (_device == null) return;
            foreach (var c in _device.InputChannels.Concat(_device.OutputChannels)) EditChannels.Add(c);
            ConfigStatusText = "已加载设备当前通道表";
        }

        private void AddEditRow(AnalogDirection direction)
        {
            int next = EditChannels.Where(c => c.Direction == direction).Select(c => c.Channel).DefaultIfEmpty(-1).Max() + 1;
            var row = new AnalogChannelConfig
            {
                Direction = direction,
                Channel = next,
                Name = $"{(direction == AnalogDirection.Input ? "AI" : "AO")}{next}",
            };
            row.ApplySignalTypeDefaults();
            EditChannels.Add(row);
            SelectedEditChannel = row;
        }

        // DataGrid 绑的是 POCO（无变更通知）：整行替换一次，界面才会显示新值
        private void ReplaceSelected(Action<AnalogChannelConfig> change)
        {
            var sel = SelectedEditChannel;
            if (sel == null) { ConfigStatusText = "请先选中一行"; return; }
            int index = EditChannels.IndexOf(sel);
            var copy = sel.Clone();
            change(copy);
            EditChannels[index] = copy;
            SelectedEditChannel = copy;
        }

        private void ApplyPresetToSelected()
        {
            if (SelectedPreset == null) { ConfigStatusText = "请先选择预设"; return; }
            ReplaceSelected(c => SelectedPreset.ApplyTo(c));
            ConfigStatusText = $"已套用预设【{SelectedPreset.Name}】（未保存）";
        }

        private void ApplySignalDefaultsToSelected()
        {
            ReplaceSelected(c => c.ApplySignalTypeDefaults());
            ConfigStatusText = "已按信号类型填入原始量程（未保存）";
        }

        private void SaveSelectedAsPreset()
        {
            var sel = SelectedEditChannel;
            if (sel == null) { ConfigStatusText = "请先选中一行"; return; }

            string name = $"{sel.Name} {AnalogEnumText.Of(sel.SignalType)} {sel.EngMin:G6}~{sel.EngMax:G6}{sel.Unit}";
            var preset = AnalogPreset.From(sel, name);
            var existing = Presets.FirstOrDefault(p => p.Name == name);
            if (existing != null) Presets[Presets.IndexOf(existing)] = preset;
            else Presets.Add(preset);

            try
            {
                AnalogPresetStore.Save(Presets);
                SelectedPreset = preset;
                ConfigStatusText = $"已存为预设【{name}】";
            }
            catch (Exception ex)
            {
                ConfigStatusText = $"保存预设失败：{ex.Message}";
            }
        }

        private async Task SaveChannelsAsync()
        {
            if (_device == null) return;

            if (UserService?.IsAuthorized(UserLevel.Engineer) != true)
            {
                MessageService.ShowMessage("修改模拟量通道配置需要工程师及以上权限。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rows = EditChannels.Select(c => c.Clone()).ToList();
            var result = _device.UpdateChannels(rows);
            if (!result.Success)
            {
                ConfigStatusText = "校验未通过，未保存";
                MessageService.ShowMessage("通道配置校验未通过，未做任何修改：\n" + string.Join("\n", result.Errors),
                    "模拟量通道配置", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RebuildRows();

            var config = _hardwareManager.GetConfig(_device.DeviceId);
            if (config == null)
            {
                ConfigStatusText = "已在内存生效，但未找到硬件配置记录，重启后会丢失";
                MessageService.ShowMessage($"未找到设备 {_device.DeviceId} 的硬件配置记录。\n新通道表已在内存生效，但未能保存，重启后会丢失。",
                    "模拟量通道配置", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                config.ConnectionParameters[AnalogChannelJson.ChannelsKey] = AnalogChannelJson.Serialize(rows);
                await _hardwareManager.SaveConfigAsync(config);
                ConfigStatusText = $"已保存并生效（{DateTime.Now:HH:mm:ss}）";
            }
            catch (Exception ex)
            {
                ConfigStatusText = $"已在内存生效，但保存失败：{ex.Message}";
                MessageService.ShowMessage($"新通道表已在内存生效，但写入配置失败，重启后会丢失：\n{ex.Message}",
                    "模拟量通道配置", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
