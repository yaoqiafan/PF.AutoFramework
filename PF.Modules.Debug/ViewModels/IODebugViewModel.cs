using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// IO 调试视图模型
    /// 通过 IIOMappingService 实现业务层与通用 UI 的解耦
    /// </summary>
    public class IODebugViewModel : RegionViewModelBase
    {
        private IIOController _ioController;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;
        private readonly IIOMappingService _ioMappingService;

        // 构造函数注入映射服务
        /// <summary>初始化 IO 调试 ViewModel</summary>
        public IODebugViewModel(IIOMappingService ioMappingService)
        {
            _ioMappingService = ioMappingService;

            // 初始化命令
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(System.Threading.CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(System.Threading.CancellationToken.None); });

            // 工控常用的 IO 刷新率通常在 50ms - 100ms
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        /// <summary>导航进入时加载 IO 设备数据</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _ioController = navigationContext.Parameters.GetValue<IIOController>("Device");
                _baseDevice = _ioController as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知 IO 设备";
                    DeviceDescription = "无法获取底层设备信息";
                }

                if (_ioController != null)
                {
                    InitializePorts();
                    _pollingTimer.Start();
                }
            }
        }

        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop(); // 离开页面务必停止轮询，防止内存泄漏
        }

        #endregion

        #region 【设备基础属性】

        private string _deviceName = "未选中IO板卡";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }

        #endregion

        #region 【IO 端口集合与初始化】

        // 输入输出端口集合，绑定到 UI 的 ItemsControl
        /// <summary>获取输入端口列表</summary>
        public ObservableCollection<IOPortModel> InputPorts { get; } = new ObservableCollection<IOPortModel>();
        /// <summary>获取输出端口列表</summary>
        public ObservableCollection<IOPortModel> OutputPorts { get; } = new ObservableCollection<IOPortModel>();

        private void InitializePorts()
        {
            InputPorts.Clear();
            OutputPorts.Clear();

            // 1. 获取动态引脚数量
            int inCount = _ioController.InputCount;
            int outCount = _ioController.OutputCount;

            // 2. 获取当前设备 ID
            string deviceId = _baseDevice?.DeviceId ?? "Default";

            // 3. 初始化输入端口（过滤 [Browsable(false)] 的引脚）
            for (int i = 0; i < inCount; i++)
            {
                var ioInfo = _ioMappingService.GetInputInfo(deviceId, i);

                // 【核心过滤逻辑】：如果明确标记了不可见，则直接跳过该引脚
                if (ioInfo != null && !ioInfo.IsBrowsable) continue;

                string showName = ioInfo?.Name ?? $"DI {i:D2}";
                InputPorts.Add(new IOPortModel { Index = i, PortName = showName, IsOutput = false });
            }

            // 4. 初始化输出端口（过滤 [Browsable(false)] 的引脚）
            for (int i = 0; i < outCount; i++)
            {
                var ioInfo = _ioMappingService.GetOutputInfo(deviceId, i);

                // 【核心过滤逻辑】：如果明确标记了不可见，则直接跳过该引脚
                if (ioInfo != null && !ioInfo.IsBrowsable) continue;

                string showName = ioInfo?.Name ?? $"DO {i:D2}";
                var outPort = new IOPortModel { Index = i, PortName = showName, IsOutput = true };
                outPort.ToggleCommand = new DelegateCommand<IOPortModel>(ToggleOutputPort);
                OutputPorts.Add(outPort);
            }
        }

        private void ToggleOutputPort(IOPortModel port)
        {
            if (port == null || _ioController == null) return;

            bool targetState = !port.State; // 取反

            // =========================================================================
            // ⚠️ TODO: 替换为你实际的 IO 写入方法 (例如 WriteDO, SetOutput 等)
            // =========================================================================
             _ioController.WriteOutput(port.Index, targetState);

            
        }

        #endregion

        #region 【定时器轮询状态】

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_ioController == null) return;

            // =========================================================================
            // ⚠️ TODO: 替换为你实际的读取状态方法 (例如 ReadDI, ReadDO 等)
            // 如果底层方法比较耗时，建议在底层维护好缓存，这里只读缓存状态以防阻塞 UI
            // =========================================================================

            // 1. 刷新输入端口 (DI)
            foreach (var port in InputPorts)
            {
                 port.State =Convert.ToBoolean(_ioController.ReadInput(port.Index));
            }

            // 2. 刷新输出端口 (DO) 的反馈状态
            foreach (var port in OutputPorts)
            {
                port.State = Convert.ToBoolean(_ioController.ReadOutput(port.Index));
            }

            // 3. 刷新连接状态
             if (_baseDevice != null) IsConnected = _baseDevice.IsConnected;
        }

        #endregion
    }

    /// <summary>
    /// 单个 IO 端口的视图模型
    /// </summary>
    public class IOPortModel : BindableBase
    {
        /// <summary>获取或设置IO端口索引</summary>
        public int Index { get; set; }
        /// <summary>获取或设置端口名称</summary>
        public string PortName { get; set; }
        /// <summary>获取或设置是否为输出端口</summary>
        public bool IsOutput { get; set; }

        private bool _state;
        /// <summary>获取或设置端口状态</summary>
        public bool State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        /// <summary>切换端口状态命令</summary>
        public DelegateCommand<IOPortModel> ToggleCommand { get; set; }
    }
}
