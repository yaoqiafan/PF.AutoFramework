using PF.Application.Base.Configuration;
using PF.Application.Base.Models;
using PF.Application.Base.ViewModels;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Station;
using PF.UI.Infrastructure.Navigation;

namespace PF.Application.Shell.ViewModels
{
    /// <summary>AutoOCR 主窗口 ViewModel：扩展状态栏以显示扫码枪、相机和 SECS/GEM 连接状态。</summary>
    public class MainWindowViewModel : MainWindowViewModelBase
    {
        private readonly DeviceStatusItem _scanner1;
        private readonly DeviceStatusItem _scanner2;
        private readonly DeviceStatusItem _camera;
        private readonly DeviceStatusItem _secGem;

        /// <summary>初始化并向 DeviceStatusItems 添加工位1/2扫码枪、智能相机和 SECS/GEM 状态条目。</summary>
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

        /// <summary>轮询硬件管理器和 SECS/GEM 管理器，更新各设备连接状态指示。</summary>
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
