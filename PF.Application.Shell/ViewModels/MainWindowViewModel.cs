using PF.Application.Base.Configuration;
using PF.Application.Base.Models;
using PF.Application.Base.ViewModels;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Station;
using PF.UI.Infrastructure.Navigation;

namespace PF.Application.Shell.ViewModels
{
    public class MainWindowViewModel : MainWindowViewModelBase
    {
        private readonly DeviceStatusItem _scanner1;
        private readonly DeviceStatusItem _scanner2;
        private readonly DeviceStatusItem _camera;
        private readonly DeviceStatusItem _secGem;

        public MainWindowViewModel(
            INavigationMenuService navigationMenuService,
            IContainerProvider containerProvider,
            CommonSettings commonSettings,
            IAlarmService alarmService,
            IMasterController masterController)
            : base(navigationMenuService, containerProvider, commonSettings, alarmService, masterController)
        {
            _scanner1 = new DeviceStatusItem { Label = "工位1扫码枪：" };
            _scanner2 = new DeviceStatusItem { Label = "工位2扫码枪：" };
            _camera   = new DeviceStatusItem { Label = "智能相机：" };
            _secGem   = new DeviceStatusItem { Label = "SECS/GEM：" };

            DeviceStatusItems.Add(_scanner1);
            DeviceStatusItems.Add(_scanner2);
            DeviceStatusItems.Add(_camera);
            DeviceStatusItems.Add(_secGem);
        }

        protected override void PollDeviceStatuses()
        {
            if (HardwareManager != null)
            {
                var scanners = HardwareManager.ActiveDevices
                    .Where(d => d.Category == HardwareCategory.Scanner)
                    .ToList();

                _scanner1.IsConnected = scanners.Count > 0 && scanners[0].IsConnected;
                _scanner2.IsConnected = scanners.Count > 1 && scanners[1].IsConnected;

                var camera = HardwareManager.ActiveDevices
                    .FirstOrDefault(d => d.Category == HardwareCategory.Camera);
                _camera.IsConnected = camera?.IsConnected ?? false;
            }

            if (SecsGemManager != null)
                _secGem.IsConnected = SecsGemManager.IsConnected;
        }
    }
}
