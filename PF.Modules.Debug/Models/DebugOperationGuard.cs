using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
using PF.UI.Infrastructure.Dialog.Basic;
using Prism.Ioc;
using System.Windows;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 调试页直接操作硬件前的机台状态守卫。
    /// <para>为什么需要：主窗口只在机台进入运行/初始化/复位时把<b>非超级用户</b>踢出调试页，
    /// 超级用户可以留在页面上；暂停、报警状态下所有人都能进入。而各调试页的命令本身不看机台状态——
    /// 运行中点动轴、翻转气缸输出、断开板卡、切仿真，都会和工站线程同时操作同一台硬件。</para>
    /// <para>受限状态下：非超级用户直接拒绝；超级用户弹二次确认，确认后放行并写警告级操作日志。</para>
    /// <para>Stop / 急停 / 写安全值这类"让硬件停下来"的操作不经过守卫，任何时候都必须能执行。</para>
    /// </summary>
    internal static class DebugOperationGuard
    {
        private static readonly MachineState[] HardwareBlockedStates =
            { MachineState.Running, MachineState.Initializing, MachineState.Resetting };

        private static readonly MachineState[] SimulationBlockedStates =
            { MachineState.Running, MachineState.Initializing, MachineState.Resetting, MachineState.Paused };

        /// <summary>
        /// 运动、输出、连接类操作：运行中 / 初始化中 / 复位中受限（超级用户确认后放行）。
        /// 暂停和报警状态下允许（现场需要手动处理卡料、退回轴），恢复前由人确认位置。
        /// </summary>
        public static bool EnsureHardwareOperationAllowed(IMessageService messageService, string action)
            => Check(messageService, action, HardwareBlockedStates, allowSuperUserOverride: true);

        /// <summary>
        /// 点动：受限状态下一律拒绝，超级用户也不放行。
        /// 点动是"按住才走、松开即停"，在按下的瞬间弹确认框，鼠标松开事件会落在对话框上，
        /// 确认后轴开始走却已经没有"按住"这个动作——等于启动了一次不会自己停的连续运动。
        /// </summary>
        public static bool EnsureJogAllowed(IMessageService messageService, string action)
            => Check(messageService, action, HardwareBlockedStates, allowSuperUserOverride: false);

        /// <summary>
        /// 切换仿真模式：除上述状态外，暂停中也受限（超级用户确认后放行）。
        /// 仿真切换立即改变设备读写行为（仿真 IO 读入恒为 false、安全门扫描跳过），
        /// 暂停后恢复运行时工站会在"一半真机一半仿真"的硬件上继续跑。
        /// </summary>
        public static bool EnsureSimulationSwitchAllowed(IMessageService messageService, string action)
            => Check(messageService, action, SimulationBlockedStates, allowSuperUserOverride: true);

        private static bool Check(IMessageService messageService, string action,
            MachineState[] blocked, bool allowSuperUserOverride)
        {
            var container = ContainerLocator.Container;
            if (container == null || !container.IsRegistered<IMasterController>()) return true;

            var state = container.Resolve<IMasterController>().CurrentState;
            if (Array.IndexOf(blocked, state) < 0) return true;

            var userService = container.IsRegistered<IUserService>() ? container.Resolve<IUserService>() : null;
            bool isSuperUser = userService?.IsAuthorized(UserLevel.SuperUser) == true;

            if (!allowSuperUserOverride || !isSuperUser)
            {
                messageService?.ShowMessage(
                    allowSuperUserOverride
                        ? $"设备当前处于【{state}】状态，禁止在调试页{action}。\n请先停止设备后再操作。"
                        : $"设备当前处于【{state}】状态，禁止{action}（超级用户也不能在此状态下点动）。\n请先停止或暂停设备后再操作。",
                    "操作受限", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // 在 UI 线程上 ShowMessage 是同步模态的，回调在返回前执行；
            // 万一从非 UI 线程调用，回调来不及执行，confirmed 保持 false，按拒绝处理（安全侧）
            bool confirmed = false;
            messageService?.ShowMessage(
                $"设备当前处于【{state}】状态，工站正在使用这些硬件。\n\n" +
                $"此时{action}会与工站同时操作同一台设备，可能导致撞机、掉料或流程错乱。\n\n确定继续吗？",
                "超级用户确认", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                result => confirmed = result == ButtonResult.Yes);

            if (confirmed && container.IsRegistered<ILogService>())
            {
                var user = userService?.CurrentUser?.UserName ?? "未知用户";
                container.Resolve<ILogService>().Warn(
                    $"[调试页] 超级用户[{user}] 在机台【{state}】状态下确认执行：{action}", "操作日志");
            }
            return confirmed;
        }
    }
}
