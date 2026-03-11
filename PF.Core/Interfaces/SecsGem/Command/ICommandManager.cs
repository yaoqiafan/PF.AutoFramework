using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params.FormulaParam;

namespace PF.Core.Interfaces.SecsGem.Command
{
    /// <summary>
    /// SECS/GEM 命令管理器接口
    /// </summary>
    public interface ICommandManager
    {
        FormulaConfiguration FormulaConfiguration { get; }

        /// <summary>
        /// 主动命令管理器（奇数Function命令）
        /// </summary>
        ISFCommand IncentiveCommands { get; }

        /// <summary>
        /// 应答命令管理器（偶数Function命令）
        /// </summary>
        ISFCommand ResponseCommands { get; }

        /// <summary>
        /// 初始化命令管理器
        /// </summary>
        Task<bool> InitializeAsync(FormulaConfiguration formulaConfiguration);

        Task UPDataCommondCollection(FormulaConfiguration formulaConfiguration);

        /// <summary>
        /// 根据Stream和Function获取命令
        /// </summary>
        Task<SFCommand> GetCommandAsync(string key);

        /// <summary>
        /// 添加命令（自动判断类型）
        /// </summary>
        Task<bool> AddCommandAsync(SFCommand command);

        /// <summary>
        /// 移除命令
        /// </summary>
        Task<bool> RemoveCommandAsync(uint stream, uint function, string key);

        /// <summary>
        /// 检查命令是否存在
        /// </summary>
        Task<bool> ContainsCommandAsync(string key);

        /// <summary>
        /// 获取所有命令
        /// </summary>
        Task<List<SFCommand>> GetAllCommandsAsync();

        /// <summary>
        /// 获取指定Stream的所有命令
        /// </summary>
        Task<List<SFCommand>> GetCommandsByStreamAsync(uint stream);

        /// <summary>
        /// 获取命令统计信息
        /// </summary>
        Task<CommandManagerStatistics> GetStatisticsAsync();

        /// <summary>
        /// 重新加载所有命令
        /// </summary>
        Task ReloadAllCommandsAsync(FormulaConfiguration formulaConfiguration);

        /// <summary>
        /// 保存所有命令到Excel
        /// </summary>
        Task SaveAllCommandsToExcelAsync();

        /// <summary>
        /// 保存主动命令到Excel
        /// </summary>
        Task SaveIncentiveCommandsToExcelAsync(string filePath = null);

        /// <summary>
        /// 保存应答命令到Excel
        /// </summary>
        Task SaveResponseCommandsToExcelAsync(string filePath = null);

        /// <summary>
        /// 验证命令对完整性
        /// </summary>
        Task<List<(uint stream, uint function)>> ValidateCommandPairsAsync();

        /// <summary>
        /// 清除所有命令
        /// </summary>
        Task ClearAllCommandsAsync();

        /// <summary>
        /// 获取命令管理器状态
        /// </summary>
        CommandManagerStatus GetStatus();
    }

    /// <summary>
    /// 命令管理器统计信息
    /// </summary>
    public class CommandManagerStatistics
    {
        public int TotalCommands { get; set; }
        public int IncentiveCommands { get; set; }
        public int ResponseCommands { get; set; }
        public Dictionary<uint, int> CommandsByStream { get; set; } = new Dictionary<uint, int>();
        public List<CommandValidationResult> ValidationResults { get; set; } = new List<CommandValidationResult>();

        public override string ToString()
        {
            return $"命令统计: 总数={TotalCommands}, 主动={IncentiveCommands}, 应答={ResponseCommands}, 涉及Stream={CommandsByStream.Count}";
        }
    }

    /// <summary>
    /// 命令验证结果
    /// </summary>
    public class CommandValidationResult
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 命令管理器状态
    /// </summary>
    public class CommandManagerStatus
    {
        public bool IsInitialized { get; set; }
        public DateTime LastInitialized { get; set; }
        public string IncentiveExcelPath { get; set; }
        public string ResponseExcelPath { get; set; }
        public bool IncentiveFileExists { get; set; }
        public bool ResponseFileExists { get; set; }
        public int LastOperationCount { get; set; }

        public override string ToString()
        {
            return $"状态: 已初始化={IsInitialized}, 主动命令文件={IncentiveFileExists}, 应答命令文件={ResponseFileExists}";
        }
    }
}
