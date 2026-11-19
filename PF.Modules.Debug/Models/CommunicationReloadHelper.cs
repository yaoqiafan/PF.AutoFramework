using PF.Core.Enums;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Station;
using PF.UI.Infrastructure.Dialog.Basic;
using System.Windows;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 通讯调试页"改参数 → 存库"之后的统一重载入口。
    ///
    /// <para>为什么要连硬件一起重载：扫码枪、相机、串口光源等设备在创建时就从通讯管理服务拿走了通讯实例的引用；
    /// 只重载通讯实例的话，这些设备手里仍是已释放的旧实例，直到硬件也重载前都无法通讯。</para>
    /// <para>重载全部硬件会断开所有设备、机构里的设备引用随之失效，因此只允许在机台未初始化时进行，并先确认。</para>
    /// </summary>
    internal static class CommunicationReloadHelper
    {
        /// <summary>
        /// 按上述规则重载；真正执行了重载返回 true（调用方随后重新绑定刷新后的通讯实例），
        /// 未重载（机台未停止 / 用户取消）返回 false，已保存的参数在下次重载或重启后生效。
        /// </summary>
        public static async Task<bool> ReloadAsync(ICommunicationManagerService commManager, IMessageService messageService, Action<string> log)
        {
            var container = ContainerLocator.Container;
            var hardwareManager = container.IsRegistered<IHardwareManagerService>() ? container.Resolve<IHardwareManagerService>() : null;
            var master = container.IsRegistered<IMasterController>() ? container.Resolve<IMasterController>() : null;

            if (master != null && master.CurrentState != MachineState.Uninitialized)
            {
                log($"[参数] 已保存。机台当前为 {master.CurrentState}，本次未重新加载，参数暂不生效。");
                await messageService.ShowMessageAsync(
                    $"参数已保存，但机台未停止（当前状态：{master.CurrentState}），本次不重新加载。\n\n" +
                    "通讯参数需要连同硬件设备一起重新加载才会生效：请停止机台（回到未初始化）后再次保存，或重启软件。",
                    "通讯参数", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var confirm = await messageService.ShowMessageAsync(
                "参数已保存。生效需要重新加载【全部】通讯实例和【全部】硬件设备\n" +
                "（运动轴、IO、相机、扫码枪等都会断开重连，之后需重新初始化机台）。\n\n确认现在重新加载？",
                "重新加载", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != ButtonResult.Yes)
            {
                log("[参数] 已保存，未重新加载（下次重新加载或重启软件后生效）。");
                return false;
            }

            log("[参数] 正在重新加载全部通讯实例...");
            await commManager.ReloadAllAsync();

            if (hardwareManager != null)
            {
                // 设备持有通讯实例引用，必须在通讯实例重建之后重建
                log("[参数] 正在重新加载全部硬件设备...");
                await hardwareManager.ReloadAllAsync();
            }
            return true;
        }
    }
}
