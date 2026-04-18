using PF.Core.Constants;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Infrastructure.SecsGem.Tools;
using System.Collections.Concurrent;


namespace PF.Infrastructure.SecsGem.Command.Response
{
   
    /// <summary>
    /// 应答命令管理器（偶数Function命令）
    /// 从Excel文件读取命令配置，Message为序列化字符串
    /// </summary>
    public class ResponseCommand : ISFCommand
    {
        private readonly string ResponseCommandExcelPath = Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemCommandConfig.xlsx");
        /// <summary>
        /// 命令字典
        /// </summary>
        public ConcurrentDictionary<string, SFCommand> _commandDictionary = new();
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);

        /// <summary>
        /// 获取命令数量
        /// </summary>
        public Task<int> CommandCount => Task.FromResult(_commandDictionary.Count);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="excelFilePath">Excel文件路径</param>
        public ResponseCommand(FormulaConfiguration formulaConfig)
        {
            _commandDictionary = formulaConfig.ResponseCommandDictionary;
        }

        /// <summary>
        /// 初始化命令字典（从Excel读取）
        /// </summary>
        public async Task<bool> InitializeCommands()
        {
            if (_isInitialized)
            {
                return true;
            }

            await _initSemaphore.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    return true;
                }

               
                _isInitialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
            return true;
        }

       
        /// <summary>
        /// 快速查找命令
        /// </summary>
        public Task<SFCommand> FindCommand(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return Task.FromResult<SFCommand>(null);
            }

            _commandDictionary.TryGetValue(key, out var command);
            return Task.FromResult(command);
        }

        /// <summary>
        /// 通过Stream和Function查找命令
        /// </summary>
        public Task<List<SFCommand>> FindCommands(uint stream, uint function)
        {
            var keyPattern = $"S{stream}F{function}";

            var results = _commandDictionary
                .Where(kvp => kvp.Key.Contains(keyPattern))
                .Select(kvp => kvp.Value)
                .ToList();

            return Task.FromResult(results);
        }

        /// <summary>
        /// 添加单个命令
        /// </summary>
        public async Task<bool> AddCommand(SFCommand command)
        {
            if (command == null)
            {
                return false;
            }

            if (!await ValidateCommand(command))
            {
                Console.WriteLine($"添加命令失败: 验证未通过 {command.Key}");
                return false;
            }

            var key = command.Key;
            if (_commandDictionary.ContainsKey(key))
            {
                Console.WriteLine($"添加命令失败: 命令 {key} 已存在");
                return false;
            }

            _commandDictionary[key] = command;
            Console.WriteLine($"添加应答命令成功: {key}");
            return true;
        }

        /// <summary>
        /// 移除命令（支持按 ID 或按 Key 查找）
        /// </summary>
        public Task<bool> RemoveCommand(string idOrKey)
        {
            // 先尝试直接按字典 Key (S{n}F{n}) 删除
            if (_commandDictionary.TryRemove(idOrKey, out _))
            {
                Console.WriteLine($"移除应答命令成功: {idOrKey}");
                return Task.FromResult(true);
            }

            // 回退：按命令 ID 扫描删除
            var pair = _commandDictionary.FirstOrDefault(kvp => kvp.Value.ID == idOrKey);
            if (pair.Value != null)
            {
                bool removed = _commandDictionary.TryRemove(pair.Key, out _);
                if (removed) Console.WriteLine($"移除应答命令成功: {pair.Key}");
                return Task.FromResult(removed);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// 检查命令是否存在
        /// </summary>
        public Task<bool> ContainsCommand(string key)
        {
            return Task.FromResult(_commandDictionary.ContainsKey(key));
        }

        /// <summary>
        /// 获取所有命令列表
        /// </summary>
        public Task<List<SFCommand>> GetAllCommands()
        {
            return Task.FromResult(_commandDictionary.Values.ToList());
        }

        /// <summary>
        /// 获取指定Stream的所有命令
        /// </summary>
        public Task<List<SFCommand>> GetCommandsByStream(uint stream)
        {
            var commands = _commandDictionary.Values
                .Where(c => c.Stream == stream)
                .OrderBy(c => c.Function)
                .ToList();
            return Task.FromResult(commands);
        }

        /// <summary>
        /// 验证命令格式
        /// </summary>
        public Task<bool> ValidateCommand(SFCommand command)
        {
            if (command == null)
            {
                return Task.FromResult(false);
            }

            // 验证Stream和Function的范围（SECS/GEM规范）
            bool isValid = command.Stream > 0 && command.Stream <= 128 &&
                          command.Function >= 0 && command.Function <= 255;

            // 验证偶数类应答指令
            bool isEvenFunction = (command.Function % 2) == 0;

            if (!isEvenFunction)
            {
                Console.WriteLine($"验证失败: Function {command.Function} 不是偶数（应答命令必须为偶数）");
            }

            return Task.FromResult(isValid && isEvenFunction);
        }

        /// <summary>
        /// 修改命令的基本信息
        /// </summary>
        public async Task<bool> UpdateCommandInfo(string oldKey,string newKey,SFCommand updatedCommand)
        {
            
            if (!_commandDictionary.TryGetValue(oldKey, out var command))
            {
                Console.WriteLine($"更新命令失败: 未找到命令 {oldKey}");
                return false;
            }
            // 验证新命令
            if (!await ValidateCommand(updatedCommand))
            {
                Console.WriteLine($"更新命令失败: 新命令格式验证失败 {updatedCommand.Key}");
                return false;
            }

            
           
            if (oldKey != newKey && _commandDictionary.ContainsKey(newKey))
            {
                Console.WriteLine($"更新命令失败: 新键 {newKey} 已存在");
                return false;
            }

            // 移除旧键（如果键改变了）
            if (oldKey != newKey)
            {
                _commandDictionary.TryRemove(oldKey, out _);
            }

            // 添加/更新命令
            _commandDictionary[newKey] = updatedCommand;
            Console.WriteLine($"更新命令成功: {oldKey} -> {newKey}");
            return true;
        }


        /// <summary>
        /// 重新加载Excel配置
        /// </summary>
        public async Task Reload()
        {
            await _initSemaphore.WaitAsync();
            try
            {
                _isInitialized = false;
                await InitializeCommands();
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// 将当前命令保存到Excel
        /// </summary>
        public async Task Save(string filePath = null)
        {
            await NPOIHelper.SaveResponseCommandToExcel(filePath, _commandDictionary);
        }

        
    }

  
}
