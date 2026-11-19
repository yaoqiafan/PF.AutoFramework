using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.Station;
using PF.Infrastructure.SecsGem.Tools;
using PF.UI.Infrastructure.Dialog.Basic;
using System.Collections.Concurrent;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels.SubViewModels
{
    /// <summary>
    /// SECS/GEM 配置修改的公共安全措施：修改前的状态检查，以及"先加载到副本、成功后再替换"的导入。
    /// </summary>
    internal static class SecsConfigSafety
    {
        /// <summary>
        /// 修改 SECS 配置（导入/保存/覆盖/删除命令）前的检查。
        /// <list type="bullet">
        /// <item>机台运行中/初始化中/复位中：工站正按这份配置与 Host 交互（上报事件、应答远程命令），
        /// 非超级用户直接拒绝，超级用户二次确认后放行。</item>
        /// <item>已与 Host 建链：替换 VID/CEID/命令会立刻影响正在进行的通讯，二次确认。</item>
        /// </list>
        /// 此前这些操作没有任何限制，运行中、已建链时也能整表替换配置。
        /// </summary>
        public static async Task<bool> EnsureCanModifyAsync(IMessageService messageService, ISecsGemManager manager, string action)
        {
            var container = ContainerLocator.Container;
            if (container != null && container.IsRegistered<IMasterController>())
            {
                var state = container.Resolve<IMasterController>().CurrentState;
                if (state is MachineState.Running or MachineState.Initializing or MachineState.Resetting)
                {
                    var userService = container.IsRegistered<IUserService>() ? container.Resolve<IUserService>() : null;
                    if (userService?.IsAuthorized(UserLevel.SuperUser) != true)
                    {
                        await messageService.ShowMessageAsync(
                            $"设备当前处于【{state}】状态，禁止{action}。\n请先停止设备后再修改 SECS/GEM 配置。",
                            "操作受限", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    var superConfirm = await messageService.ShowMessageAsync(
                        $"设备当前处于【{state}】状态，工站正在按现有配置与 Host 交互。\n\n此时{action}可能导致上报内容错乱或远程命令无法应答。\n\n确定继续吗？",
                        "超级用户确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (superConfirm != ButtonResult.Yes) return false;
                    Log(container, $"[SECS配置] 超级用户在机台【{state}】状态下确认执行：{action}");
                    return true;
                }
            }

            if (manager?.IsConnected == true)
            {
                var confirm = await messageService.ShowMessageAsync(
                    $"当前已与 Host 建立连接，{action}会立刻影响正在进行的通讯。\n\n确定继续吗？",
                    "SECS/GEM 配置修改确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != ButtonResult.Yes) return false;
                Log(container, $"[SECS配置] 已与 Host 建链状态下确认执行：{action}");
            }
            return true;
        }

        /// <summary>
        /// 把 Excel 里的变量配置合并到 <paramref name="baseConfig"/> 的<b>副本</b>上；读取失败返回 null，现有配置不受影响。
        /// <para>此前直接在正在使用的配置对象上原地加载：Excel 读到一半失败时内存配置已被部分改写，
        /// 且不检查返回值，失败也照常写库。</para>
        /// </summary>
        public static async Task<ValidateConfiguration?> LoadValidateStagedAsync(string filePath, ValidateConfiguration? baseConfig)
        {
            var staged = new ValidateConfiguration
            {
                CEIDS      = new ConcurrentDictionary<string, CEID>(baseConfig?.CEIDS ?? new()),
                ReportIDS  = new ConcurrentDictionary<string, ReportID>(baseConfig?.ReportIDS ?? new()),
                VIDS       = new ConcurrentDictionary<string, VID>(baseConfig?.VIDS ?? new()),
                CommandIDS = new ConcurrentDictionary<string, CommandID>(baseConfig?.CommandIDS ?? new()),
            };
            return await NPOIHelper.LoadValidateFromExcel(filePath, staged) ? staged : null;
        }

        /// <summary>
        /// 从 Excel 读取主动/应答命令到<b>新的</b>字典；任一类读取失败返回 null，现有命令不受影响。
        /// <para>此前直接传入正在使用的命令字典：LoadXxxCommandFromExcel 第一步就 Clear()，选错文件（缺工作表、
        /// 表头不符）返回 false 时现有命令已被清空；调用方不看返回值，接着把空集合写进数据库——一次误导入清掉全部命令。</para>
        /// </summary>
        public static async Task<(ConcurrentDictionary<string, Core.Entities.SecsGem.Command.SFCommand> Incentive,
                                  ConcurrentDictionary<string, Core.Entities.SecsGem.Command.SFCommand> Response)?>
            LoadFormulaStagedAsync(string filePath)
        {
            var incentive = new ConcurrentDictionary<string, Core.Entities.SecsGem.Command.SFCommand>();
            var response = new ConcurrentDictionary<string, Core.Entities.SecsGem.Command.SFCommand>();
            if (!await NPOIHelper.LoadIncentiveCommandFromExcel(filePath, incentive)) return null;
            if (!await NPOIHelper.LoadResponseCommandFromExcel(filePath, response)) return null;
            return (incentive, response);
        }

        /// <summary>把读取成功的命令集整体换入正在使用的配方配置，并同步到命令管理器。</summary>
        public static async Task ApplyFormulaAsync(ISecsGemManager manager,
            ConcurrentDictionary<string, Core.Entities.SecsGem.Command.SFCommand> incentive,
            ConcurrentDictionary<string, Core.Entities.SecsGem.Command.SFCommand> response)
        {
            var live = manager.CommandManager.FormulaConfiguration ?? new FormulaConfiguration();
            live.IncentiveCommandDictionary = incentive;
            live.ResponseCommandDictionary = response;
            await manager.CommandManager.UPDataCommondCollection(live);
        }

        private static void Log(IContainerProvider? container, string message)
        {
            if (container != null && container.IsRegistered<ILogService>())
                container.Resolve<ILogService>().Warn(message, "操作日志");
        }
    }
}
