using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware;
using PF.Infrastructure.Logging;
using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 光源控制器调试 ViewModel。按 <see cref="ILightController"/> 接口工作，
    /// 康视达（串口号写在硬件配置里）与海康（串口由通讯层持有）两种实现共用本页。
    ///
    /// <para><b>亮度为什么要防抖</b>：界面上是滑块且 UpdateSourceTrigger=PropertyChanged，
    /// 拖一次会连发几十个 setter。而下发一次亮度是一笔串口事务（海康那款还要等应答，
    /// 最长 3 秒），逐个下发会把串口打满、界面看着像卡死。这里改为"记下最新值 →
    /// 停手 150ms 后只发最后一个值"，并用信号量保证同一时刻只有一笔在途。</para>
    ///
    /// <para><b>亮度读回是按钮触发、不是轮询</b>：<see cref="ILightController.GetLightValue"/> 每读一个
    /// 通道就是一笔串口问答（海康那款等应答最长 3 秒），通道多的设备轮询一遍最坏要十几秒；
    /// 若按 200ms 轮询，串口会被读指令占满、下发亮度根本挤不进去，而且读回值会不停把用户
    /// 正在拖的滑块拽回去。因此改为：进页面时（且设备已连接）自动读一次对齐初值，
    /// 之后由"读取亮度"按钮显式触发。读与写共用同一把信号量，不会交叉占用链路。</para>
    /// </summary>
    public  class LightControllerDebugViewModel : RegionViewModelBase
    {
        /// <summary>状态轮询间隔（毫秒）。</summary>
        private const int PollingIntervalMs = 200;

        /// <summary>亮度下发防抖窗口（毫秒）：停止拖动这么久之后才真正下发。</summary>
        private const int WriteDebounceMs = 150;

        /// <summary>未选中设备时的兜底通道数，仅用于导航前的占位显示。</summary>
        private const int DefaultChannelCount = 4;

        /// <summary>初始化光源控制器调试 ViewModel</summary>
        public LightControllerDebugViewModel(ILogService logService)
        {
            _logger = CategoryLoggerFactory.Hardware(logService);

            InitializeCommands();
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PollingIntervalMs)
            };
            _pollingTimer.Tick += OnPollingTimerTick;

            _writeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(WriteDebounceMs)
            };
            _writeTimer.Tick += OnWriteTimerTick;

            RebuildChannels(DefaultChannelCount);
        }

        private ILightController _lightController;

        private BaseDevice _baseDevice;

        /// <summary>
        /// 硬件分类日志。本页没有自己的消息栏，下发失败等结果统一发到 ILogService，
        /// 由主窗体底部的日志栏与设备自身的硬件日志混排展示。
        /// </summary>
        private readonly CategoryLogger _logger;

        private readonly DispatcherTimer _pollingTimer;
        private readonly DispatcherTimer _writeTimer;

        /// <summary>页面级取消源：离开页面时取消尚未完成的亮度下发。</summary>
        private CancellationTokenSource _cts;

        /// <summary>各通道待下发的最新值（索引 0 对应通道 1）。随 <see cref="RebuildChannels"/> 重建。</summary>
        private int[] _pendingValues = new int[DefaultChannelCount];

        /// <summary>各通道是否有未下发的改动。随 <see cref="RebuildChannels"/> 重建。</summary>
        private bool[] _pendingDirty = new bool[DefaultChannelCount];

        /// <summary>串行化链路：同一时刻只允许一笔亮度事务（下发或读回）在途。</summary>
        private readonly SemaphoreSlim _writeGate = new(1, 1);

        #region 【Prism 导航生命周期】
        /// <summary>导航离开时停止轮询、取消在途下发</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
            _writeTimer.Stop();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        /// <summary>导航进入时加载光源控制器设备数据并启动状态轮询</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _lightController = navigationContext.Parameters.GetValue<ILightController>("Device");
                _baseDevice = _lightController as BaseDevice;
                if (_baseDevice != null)
                {
                    DeviceName = _lightController.DeviceName;
                    DeviceDescription = $"设备类别: {_lightController.Category} | 模拟状态: {_lightController.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知光源控制器设备";
                    DeviceDescription = "无法获取底层设备信息";
                }

                // 串口地址是取自设备实例的只读值，实例要到这一刻才有，必须显式通知界面刷新
                RaisePropertyChanged(nameof(COMAdress));

                // 通道数按设备实例注册时的配置来，每台光源控制器可以不一样，故每次导航都重建
                RebuildChannels(_lightController?.ChannelCount ?? DefaultChannelCount);
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            // 连接状态/报警灯靠这个定时器刷新。此前只挂了 Tick 从未 Start，
            // 两盏灯一直停在初始值，界面与设备实际状态完全脱节。
            if (_baseDevice != null)
            {
                OnPollingTimerTick(this, EventArgs.Empty);
                _pollingTimer.Start();

                // 已连接才自动读：没连接时读四个通道只会连抛四次"未连接"，
                // 白白在日志里刷一屏红字
                if (_baseDevice.IsConnected)
                {
                    RunAsync("读取亮度", () => ReadAllChannelsAsync());
                }
            }
        }
        #endregion 【Prism 导航生命周期】

        #region 【设备信息与状态属性】

        private string _deviceName = "未选中光源控制器";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _hasAlarm;
        /// <summary>获取或设置是否报警</summary>
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }

        private bool _isReading;
        /// <summary>获取或设置是否正在读回亮度（读回期间置灰按钮，避免重复排队）</summary>
        public bool IsReading
        {
            get => _isReading;
            set
            {
                if (SetProperty(ref _isReading, value))
                {
                    ReadLightValueCommand?.RaiseCanExecuteChanged();
                }
            }
        }


        /// <summary>
        /// 获取串口地址。设备未选中时为空；
        /// 串口由通讯层持有的实现（如海康串口光源）自身不知道端口号，此时给出提示而非空白。
        /// </summary>
        public string COMAdress
        {
            get
            {
                if (_lightController == null) return string.Empty;
                var com = _lightController.ComName;
                return string.IsNullOrWhiteSpace(com) ? "—（串口由通讯配置持有）" : com;
            }
        }

        #endregion




        #region 【控制命令定义】

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>模拟硬件报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; private set; }
        /// <summary>读回各通道当前亮度命令</summary>
        public DelegateCommand ReadLightValueCommand { get; private set; }


        private void InitializeCommands()
        {
            // DelegateCommand 的 async lambda 是 async void：未捕获异常会击穿到 Dispatcher 直接崩进程，
            // 故一律经 RunAsync 兜住并落日志
            ConnectCommand = new DelegateCommand(() => RunAsync("连接", async () =>
            {
                if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None);
            }));
            DisconnectCommand = new DelegateCommand(() => RunAsync("断开连接", async () =>
            {
                if (_baseDevice != null) await _baseDevice.DisconnectAsync();
            }));
            ResetCommand = new DelegateCommand(() => RunAsync("复位", async () =>
            {
                if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None);
            }));
            SimulateAlarmCommand = new DelegateCommand(() =>
            {
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.LightControllerError, "调试页面手动模拟光源控制器报警");
            });
            ReadLightValueCommand = new DelegateCommand(
                () => RunAsync("读取亮度", () => ReadAllChannelsAsync()),
                () => !IsReading);
        }

        #endregion 【控制命令定义】


        #region 光源控制器特有属性

        /// <summary>
        /// 各通道的滑块/文本框数据项，界面用 ItemsControl 渲染。数量随设备 <see cref="ILightController.ChannelCount"/> 变化，
        /// 每次导航进页面（拿到新的 Device 参数）都会按当前设备的通道数重建。
        /// </summary>
        public ObservableCollection<LightChannelViewModel> Channels { get; } = new();

        /// <summary>
        /// 按给定通道数重建 <see cref="Channels"/> 及配套的待发值/脏标记数组。
        /// 旧通道项要先摘掉事件订阅，否则重建后旧项仍会被回调（虽然已经从集合里移除，但委托链不会自动断开）。
        /// </summary>
        private void RebuildChannels(int channelCount)
        {
            foreach (var old in Channels)
            {
                old.ValueChangedByUser -= OnChannelValueChanged;
            }
            Channels.Clear();

            for (int i = 1; i <= channelCount; i++)
            {
                var item = new LightChannelViewModel(i);
                item.ValueChangedByUser += OnChannelValueChanged;
                Channels.Add(item);
            }

            _pendingValues = new int[channelCount];
            _pendingDirty = new bool[channelCount];
        }

        #endregion 光源控制器特有属性


        #region 【亮度下发】

        /// <summary>通道滑块/文本框的用户改动回调，转发给 <see cref="RequestWrite"/>。</summary>
        private void OnChannelValueChanged(int channel, int value) => RequestWrite(channel, value);

        /// <summary>
        /// 登记一次亮度改动并重置防抖计时：拖动过程中只更新待发值，停手后才真正下发。
        /// </summary>
        private void RequestWrite(int channel, int value)
        {
            if (_lightController == null) return;
            if (channel < 1 || channel > _pendingValues.Length) return;

            _pendingValues[channel - 1] = value;
            _pendingDirty[channel - 1] = true;

            // Stop+Start 才是重置计时，只 Start 对已在跑的 DispatcherTimer 无效果
            _writeTimer.Stop();
            _writeTimer.Start();
        }

        private void OnWriteTimerTick(object sender, EventArgs e)
        {
            _writeTimer.Stop();
            FlushPendingWrites();
        }

        /// <summary>
        /// 把各通道待发值逐一下发。取快照后立即清脏标记，
        /// 下发期间用户继续拖动会重新置脏并触发下一轮，不会丢最后一次改动。
        /// </summary>
        private void FlushPendingWrites()
        {
            var light = _lightController;
            if (light == null) return;

            var snapshot = new List<(int Channel, int Value)>();
            for (int i = 0; i < _pendingValues.Length; i++)
            {
                if (!_pendingDirty[i]) continue;
                _pendingDirty[i] = false;
                snapshot.Add((i + 1, _pendingValues[i]));
            }

            if (snapshot.Count == 0) return;

            var token = _cts?.Token ?? CancellationToken.None;

            RunAsync("设置亮度", async () =>
            {
                await _writeGate.WaitAsync(token);
                try
                {
                    foreach (var (channel, value) in snapshot)
                    {
                        token.ThrowIfCancellationRequested();
                        await light.SetLightValue(channel, value, token);
                        _logger.Debug($"[{DeviceName}] 通道{channel} 亮度已下发：{value}");
                    }
                }
                finally
                {
                    _writeGate.Release();
                }
            });
        }

        #endregion 【亮度下发】


        #region 【亮度读回】

        /// <summary>
        /// 逐通道读回当前亮度并刷新界面。
        /// 单通道失败只记日志、继续读后面的通道——某一路没接灯不该让整次读取作废。
        /// </summary>
        private async Task ReadAllChannelsAsync()
        {
            var light = _lightController;
            if (light == null) return;

            var token = _cts?.Token ?? CancellationToken.None;

            await _writeGate.WaitAsync(token);
            IsReading = true;
            try
            {
                for (int idx = 0; idx < Channels.Count; idx++)
                {
                    var item = Channels[idx];
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        int value = await light.GetLightValue(item.Channel, token);
                        item.ApplyReadValue(value);
                        _logger.Debug($"[{DeviceName}] 通道{item.Channel} 亮度读回：{value}");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{DeviceName}] 通道{item.Channel} 亮度读取失败：{ex.Message}", ex);
                    }
                }
            }
            finally
            {
                IsReading = false;
                _writeGate.Release();
            }
        }

        #endregion 【亮度读回】


        #region 【辅助】

        /// <summary>
        /// 统一的异步执行包装：吞掉取消（离开页面属正常路径），其余异常落 Error 日志。
        /// </summary>
        private async void RunAsync(string opName, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                // 离开调试页时取消在途下发，属正常路径
            }
            catch (Exception ex)
            {
                _logger.Error($"[{DeviceName}] {opName}失败：{ex.Message}", ex);
            }
        }

        #endregion 【辅助】


        #region 【定时器轮询更新】

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_baseDevice == null) return;
            IsConnected = _baseDevice.IsConnected;
            HasAlarm = _baseDevice.HasAlarm;
        }
        #endregion【定时器轮询更新】

    }
}
