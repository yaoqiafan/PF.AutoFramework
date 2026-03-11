using PF.Core.Entities.SecsGem.Command;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.Command
{
    /// <summary>
    /// SECS/GEM 命令管理接口
    /// </summary>
    public interface ISFCommand
    {
        /// <summary>
        /// 命令数量
        /// </summary>
        Task<int> CommandCount { get; }

        /// <summary>
        /// 初始化命令集合
        /// </summary>
        Task<bool> InitializeCommands();

        /// <summary>
        /// 保存命令集合到数据源
        /// </summary>
        Task Save(string filePath = null);

        /// <summary>
        /// 重新加载命令集合
        /// </summary>
        Task Reload();

        /// <summary>
        /// 查找命令（精确匹配）
        /// </summary>
        Task<SFCommand> FindCommand(string key);

        /// <summary>
        /// 通过Stream和Function查找命令
        /// </summary>
        Task<List<SFCommand>> FindCommands(uint stream, uint function);

        /// <summary>
        /// 通过Stream查找所有命令
        /// </summary>
        Task<List<SFCommand>> GetCommandsByStream(uint stream);

        /// <summary>
        /// 添加命令
        /// </summary>
        Task<bool> AddCommand(SFCommand command);

        /// <summary>
        /// 更新命令
        /// </summary>
        Task<bool> UpdateCommandInfo(string oldKey, string newKey, SFCommand command);

        /// <summary>
        /// 删除命令
        /// </summary>
        Task<bool> RemoveCommand(string key);

        /// <summary>
        /// 检查命令是否存在
        /// </summary>
        Task<bool> ContainsCommand(string key);

        /// <summary>
        /// 获取所有命令
        /// </summary>
        Task<List<SFCommand>> GetAllCommands();

        /// <summary>
        /// 验证命令格式
        /// </summary>
        Task<bool> ValidateCommand(SFCommand command);
    }
}
