using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.Command
{
    /// <summary>
    /// SECS/GEM 命令管理器接口
    /// 提供对 SECS/GEM 协议中主动命令和应答命令的统一管理、验证及持久化等功能。
    /// </summary>
    public interface ICommandManager
    {
        /// <summary>
        /// 获取当前生效的配方配置信息。
        /// </summary>
        FormulaConfiguration FormulaConfiguration { get; }

        /// <summary>
        /// 主动命令管理器（通常对应 SECS/GEM 中的奇数 Function 命令）。
        /// </summary>
        ISFCommand IncentiveCommands { get; }

        /// <summary>
        /// 应答命令管理器（通常对应 SECS/GEM 中的偶数 Function 命令）。
        /// </summary>
        ISFCommand ResponseCommands { get; }

        /// <summary>
        /// 异步初始化命令管理器。
        /// </summary>
        /// <param name="formulaConfiguration">用于初始化的配方配置对象。</param>
        /// <returns>返回一个表示异步操作的任务。如果初始化成功，则结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> InitializeAsync(FormulaConfiguration formulaConfiguration);

        /// <summary>
        /// 异步更新命令集合。
        /// </summary>
        /// <param name="formulaConfiguration">包含最新参数设置的配方配置对象。</param>
        /// <returns>返回一个表示异步更新操作的任务。</returns>
        Task UPDataCommondCollection(FormulaConfiguration formulaConfiguration);

        /// <summary>
        /// 根据指定的 Key 异步获取对应的命令实例。
        /// </summary>
        /// <param name="key">命令的唯一标识键（通常由 Stream 和 Function 组合而成）。</param>
        /// <returns>返回一个表示异步操作的任务。任务结果包含匹配的 <see cref="SFCommand"/> 实例，若未找到则返回 <c>null</c>。</returns>
        Task<SFCommand> GetCommandAsync(string key);

        /// <summary>
        /// 异步添加一个新命令，管理器将根据命令的 Function 奇偶性自动判断并分配类型（主动或应答）。
        /// </summary>
        /// <param name="command">要添加的 <see cref="SFCommand"/> 命令实例。</param>
        /// <returns>返回一个表示异步操作的任务。如果添加成功，则结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> AddCommandAsync(SFCommand command);

        /// <summary>
        /// 异步移除指定的命令。
        /// </summary>
        /// <param name="stream">命令的 Stream 编号。</param>
        /// <param name="function">命令的 Function 编号。</param>
        /// <param name="key">命令的唯一标识键。</param>
        /// <returns>返回一个表示异步操作的任务。如果移除成功，则结果为 <c>true</c>；若命令不存在或移除失败则为 <c>false</c>。</returns>
        Task<bool> RemoveCommandAsync(uint stream, uint function, string key);

        /// <summary>
        /// 异步检查指定标识键的命令是否存在。
        /// </summary>
        /// <param name="key">要检查的命令唯一标识键。</param>
        /// <returns>返回一个表示异步操作的任务。如果命令存在，则结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> ContainsCommandAsync(string key);

        /// <summary>
        /// 异步获取所有已注册的命令集合。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。任务结果包含所有 <see cref="SFCommand"/> 的列表。</returns>
        Task<List<SFCommand>> GetAllCommandsAsync();

        /// <summary>
        /// 异步获取指定 Stream 编号下的所有命令。
        /// </summary>
        /// <param name="stream">指定的 Stream 编号。</param>
        /// <returns>返回一个表示异步操作的任务。任务结果包含属于该 Stream 的 <see cref="SFCommand"/> 列表。</returns>
        Task<List<SFCommand>> GetCommandsByStreamAsync(uint stream);

        /// <summary>
        /// 异步获取命令管理器的统计信息。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。任务结果包含当前的 <see cref="CommandManagerStatistics"/> 统计对象。</returns>
        Task<CommandManagerStatistics> GetStatisticsAsync();

        /// <summary>
        /// 根据给定的配方配置重新加载所有命令。
        /// </summary>
        /// <param name="formulaConfiguration">用于重新加载数据的配方配置对象。</param>
        /// <returns>返回一个表示异步操作的任务。</returns>
        Task ReloadAllCommandsAsync(FormulaConfiguration formulaConfiguration);

        /// <summary>
        /// 异步将所有命令（包括主动和应答）保存到 Excel 文件中。
        /// </summary>
        /// <returns>返回一个表示异步保存操作的任务。</returns>
        Task SaveAllCommandsToExcelAsync();

        /// <summary>
        /// 异步将主动命令保存到指定的 Excel 文件中。
        /// </summary>
        /// <param name="filePath">目标 Excel 文件的保存路径。如果为 <c>null</c>，则使用默认路径。</param>
        /// <returns>返回一个表示异步保存操作的任务。</returns>
        Task SaveIncentiveCommandsToExcelAsync(string filePath = null);

        /// <summary>
        /// 异步将应答命令保存到指定的 Excel 文件中。
        /// </summary>
        /// <param name="filePath">目标 Excel 文件的保存路径。如果为 <c>null</c>，则使用默认路径。</param>
        /// <returns>返回一个表示异步保存操作的任务。</returns>
        Task SaveResponseCommandsToExcelAsync(string filePath = null);

        /// <summary>
        /// 异步验证命令对（Primary/Secondary）的完整性。
        /// 检查是否存在只有主动没有应答，或只有应答没有主动的孤立命令情况。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。任务结果包含未配对或验证失败的 (Stream, Function) 元组列表。</returns>
        Task<List<(uint stream, uint function)>> ValidateCommandPairsAsync();

        /// <summary>
        /// 异步清除所有已加载的主动和应答命令。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。</returns>
        Task ClearAllCommandsAsync();

        /// <summary>
        /// 获取当前命令管理器的状态信息。
        /// </summary>
        /// <returns>返回当前管理器的 <see cref="CommandManagerStatus"/> 状态对象。</returns>
        CommandManagerStatus GetStatus();
    }

    /// <summary>
    /// 命令管理器统计信息
    /// 包含各类命令的数量统计及验证结果汇总。
    /// </summary>
    public class CommandManagerStatistics
    {
        /// <summary>
        /// 获取或设置系统中的命令总数。
        /// </summary>
        public int TotalCommands { get; set; }

        /// <summary>
        /// 获取或设置系统中的主动命令（奇数 Function）总数。
        /// </summary>
        public int IncentiveCommands { get; set; }

        /// <summary>
        /// 获取或设置系统中的应答命令（偶数 Function）总数。
        /// </summary>
        public int ResponseCommands { get; set; }

        /// <summary>
        /// 获取或设置按 Stream 编号分组的命令数量字典。
        /// 键为 Stream 编号，值为该 Stream 下的命令总数。
        /// </summary>
        public Dictionary<uint, int> CommandsByStream { get; set; } = new Dictionary<uint, int>();

        /// <summary>
        /// 获取或设置命令完整性验证结果的列表。
        /// </summary>
        public List<CommandValidationResult> ValidationResults { get; set; } = new List<CommandValidationResult>();

        /// <summary>
        /// 返回表示当前统计信息的字符串。
        /// </summary>
        /// <returns>格式化后的统计信息概览字符串。</returns>
        public override string ToString()
        {
            return $"命令统计: 总数={TotalCommands}, 主动={IncentiveCommands}, 应答={ResponseCommands}, 涉及Stream={CommandsByStream.Count}";
        }
    }

    /// <summary>
    /// 命令验证结果
    /// 记录单条命令或配对验证的具体结果及相关警告信息。
    /// </summary>
    public class CommandValidationResult
    {
        /// <summary>
        /// 获取或设置被验证命令的唯一标识键。
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 获取或设置命令类型（如"主动"、"应答"等）。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示命令的验证是否通过。
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 获取或设置验证产生的消息详情（通常为错误或成功说明）。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 获取或设置验证过程中产生的警告信息列表。
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 命令管理器状态
    /// 包含管理器实例的生命周期、文件路径状态等信息。
    /// </summary>
    public class CommandManagerStatus
    {
        /// <summary>
        /// 获取或设置一个值，指示命令管理器是否已完成初始化。
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// 获取或设置管理器最后一次成功初始化的时间戳。
        /// </summary>
        public DateTime LastInitialized { get; set; }

        /// <summary>
        /// 获取或设置主动命令 Excel 文件的绝对或相对路径。
        /// </summary>
        public string IncentiveExcelPath { get; set; }

        /// <summary>
        /// 获取或设置应答命令 Excel 文件的绝对或相对路径。
        /// </summary>
        public string ResponseExcelPath { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示主动命令对应的物理文件是否存在。
        /// </summary>
        public bool IncentiveFileExists { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示应答命令对应的物理文件是否存在。
        /// </summary>
        public bool ResponseFileExists { get; set; }

        /// <summary>
        /// 获取或设置管理器最后一次进行批量操作（如加载、保存）涉及的记录数量。
        /// </summary>
        public int LastOperationCount { get; set; }

        /// <summary>
        /// 返回表示当前管理器状态的字符串。
        /// </summary>
        /// <returns>包含初始化和文件存在状态等关键信息的字符串。</returns>
        public override string ToString()
        {
            return $"状态: 已初始化={IsInitialized}, 主动命令文件={IncentiveFileExists}, 应答命令文件={ResponseFileExists}";
        }
    }
}