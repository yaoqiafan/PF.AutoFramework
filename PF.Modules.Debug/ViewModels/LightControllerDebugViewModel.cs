using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Infrastructure.Hardware;
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
    /// <summary>光源控制器调试 ViewModel</summary>
    public  class LightControllerDebugViewModel : RegionViewModelBase
    {


        /// <summary>初始化光源控制器调试 ViewModel</summary>
        public LightControllerDebugViewModel ()
        {
            InitializeCommands();
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }
        private ILightController _lightController;

        private BaseDevice _baseDevice;

        private readonly DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;


        #region 【Prism 导航生命周期】
        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
            _cts?.Cancel();
        }
        /// <summary>导航进入时加载光源控制器设备数据</summary>
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
            }
        }
        #endregion 【Prism 导航生命周期】

        #region 【设备信息与状态属性】

        private string _deviceName = "未选中扫码枪";
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


        /// <summary>获取串口地址</summary>
        public string COMAdress => _lightController.ComName;

        #endregion





        #region 【控制命令定义】

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }


        private void InitializeCommands()
        {
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None); });
        }

        #endregion 【控制命令定义】


        #region 光源控制器特有属性

        private int _lightValue1;

        /// <summary>获取或设置光源通道1亮度值</summary>
        public int LightValue1
        {
            get => _lightValue1;
            set
            {
                if (SetProperty(ref _lightValue1, value))
                {
                    if (_lightController != null)
                    {
                        _lightController.SetLightValue(1, value);
                    }
                }
            }
        }


        private int _lightValue2;

        /// <summary>获取或设置光源通道2亮度值</summary>
        public int LightValue2
        {
            get => _lightValue2;
            set
            {
                if (SetProperty(ref _lightValue2, value))
                {
                    if (_lightController != null)
                    {
                        _lightController.SetLightValue(2, value);
                    }
                }
            }
        }



        private int _lightValue3;

        /// <summary>获取或设置光源通道3亮度值</summary>
        public int LightValue3
        {
            get => _lightValue3;
            set
            {
                if (SetProperty(ref _lightValue3, value))
                {
                    if (_lightController != null)
                    {
                        _lightController.SetLightValue(3, value);
                    }
                }
            }
        }


        private int _lightValue4;

        /// <summary>获取或设置光源通道4亮度值</summary>
        public int LightValue4
        {
            get => _lightValue4;
            set
            {
                if (SetProperty(ref _lightValue4, value))
                {
                    if (_lightController != null)
                    {
                        _lightController.SetLightValue(4, value);
                    }
                }
            }
        }

        #endregion 光源控制器特有属性

     



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
