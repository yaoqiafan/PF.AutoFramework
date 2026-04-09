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
    public  class LightControllerDebugViewModel : RegionViewModelBase
    {


        public LightControllerDebugViewModel ()
        {
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }
        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_baseDevice == null) return;
            IsConnected = _baseDevice.IsConnected;
            HasAlarm = _baseDevice.HasAlarm;
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
            _cts?.Cancel();
        }


        private ILightController _lightController;

        private BaseDevice _baseDevice;

        private readonly DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;
        #region 【设备信息与状态属性】

        private string _deviceName = "未选中扫码枪";
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _hasAlarm;
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }


        public string COMAdress => _lightController.ComName;

        #endregion


        #region 自定义数据

        private int _lightValue1;

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

        #endregion 自定义数据

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

    }
}
