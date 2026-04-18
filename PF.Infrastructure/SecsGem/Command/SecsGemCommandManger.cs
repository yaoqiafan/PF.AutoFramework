using PF.Core.Constants;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Infrastructure.SecsGem.Command.Interaction;
using PF.Infrastructure.SecsGem.Command.Response;
using PF.Infrastructure.SecsGem.Tools;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Infrastructure.SecsGem.Command
{
    /// <summary>
    /// SECS/GEM 命令管理器实现
    /// </summary>
    public class SecsGemCommandManger : ICommandManager
    {
        private readonly string CommandExcelPath = Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemCommandConfig.xlsx");
       
        private readonly ConcurrentDictionary<string, SFCommand> _allCommandsCache = new();
        private bool _isInitialized = false;
        private DateTime _lastInitialized = DateTime.MinValue;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;
        private int _lastOperationCount = 0;

        private IncentiveCommand _incentiveCommands;
        private ResponseCommand _responseCommands;

        /// <summary>
        /// 主动命令管理器
        /// </summary>
        public ISFCommand IncentiveCommands => _incentiveCommands;
        /// <summary>
        /// 应答命令管理器
        /// </summary>
        public ISFCommand ResponseCommands => _responseCommands;

        /// <summary>
        /// 配方配置
        /// </summary>
        public FormulaConfiguration FormulaConfiguration { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SecsGemCommandManger()
        {

          
           
           

            // 配置JSON序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
        }

        /// <summary>
        /// 初始化命令管理器
        /// </summary>
        public async Task<bool> InitializeAsync(FormulaConfiguration formulaConfiguration)
        {
            bool res1=false, res2=false;
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

                Console.WriteLine($"开始初始化SECS/GEM命令管理器...");
                Console.WriteLine($"主动命令文件: {CommandExcelPath}");
                _allCommandsCache.Clear();
                FormulaConfiguration = formulaConfiguration;

                // 创建命令管理器实例
                _incentiveCommands = new IncentiveCommand(FormulaConfiguration);
                if (_incentiveCommands is IncentiveCommand incentive)
                {
                    res1 = await incentive.InitializeCommands();
                    Console.WriteLine($"主动命令初始化完成: {await incentive.CommandCount} 个命令");
                }

                await Task.Delay(500);

                _responseCommands = new ResponseCommand(FormulaConfiguration);
                if (_responseCommands is ResponseCommand response)
                {
                   res2= await response.InitializeCommands();
                    Console.WriteLine($"应答命令初始化完成: {await response.CommandCount} 个命令");
                }

                // 重建缓存
                await RebuildCacheAsync();

                _isInitialized = true;
                _lastInitialized = DateTime.Now;

                Console.WriteLine($"命令管理器初始化完成，总共 {_allCommandsCache.Count} 个命令");
            }
            catch (Exception ex)
            {
               
                Console.WriteLine($"初始化命令管理器失败: {ex.Message}");
                return false;
            }
            finally
            {
                _initSemaphore.Release();
            }
            return res2 & res1;
        }

        /// <summary>
        /// 根据Stream和Function获取命令
        /// </summary>
        public async Task<SFCommand> GetCommandAsync(string key)
        {
           
            if (_allCommandsCache.TryGetValue(key, out var command))
            {
                return command;
            }

            command = await _incentiveCommands.FindCommand(key);
            if (command!=null)
            {
                await UpdateCacheAsync(command);
                return command;
            }

            command = await _responseCommands.FindCommand(key);
            if (command != null)
            {
                await UpdateCacheAsync(command);
                return command;
            }
            return null;
        }

        /// <summary>
        /// 添加命令（自动判断类型）
        /// </summary>
        public async Task<bool> AddCommandAsync(SFCommand command)
        {
            if (command == null)
            {
                return false;
            }

           
            bool added = false;
            var key = command.Key;

            try
            {
                if (command.Function % 2 == 1) // 奇数，主动命令
                {
                    added = await _incentiveCommands.AddCommand(command);
                }
                else // 偶数，应答命令
                {
                    added = await _responseCommands.AddCommand(command);
                }

                if (added)
                {
                    await UpdateCacheAsync(command);
                    _lastOperationCount++;
                    Console.WriteLine($"添加命令成功: {key}");
                }

                return added;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"添加命令失败 {key}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移除命令
        /// </summary>
        public async Task<bool> RemoveCommandAsync(uint stream, uint function,string key)
        {
           
            bool removed = false;
            try
            {
                if (function % 2 == 1) // 奇数，主动命令
                {
                    removed = await _incentiveCommands.RemoveCommand(key);
                }
                else // 偶数，应答命令
                {
                    removed = await _responseCommands.RemoveCommand(key);
                }

                if (removed)
                {
                    _allCommandsCache.TryRemove(key, out _);
                    _lastOperationCount++;
                    Console.WriteLine($"移除命令成功: {key}");
                }

                return removed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移除命令失败 {key}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查命令是否存在
        /// </summary>
        public async Task<bool> ContainsCommandAsync(string key)
        {
          
            // 先检查缓存
            if (_allCommandsCache.ContainsKey(key))
            {
                return true;
            }

            if (!await _incentiveCommands.ContainsCommand(key))
            {
                return await _responseCommands.ContainsCommand(key);
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// 获取所有命令
        /// </summary>
        public async Task<List<SFCommand>> GetAllCommandsAsync()
        {
            
            await _cacheSemaphore.WaitAsync();
            try
            {
                // 确保缓存是最新的
                await RebuildCacheAsync();
                return _allCommandsCache.Values.ToList();
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取指定Stream的所有命令
        /// </summary>
        public async Task<List<SFCommand>> GetCommandsByStreamAsync(uint stream)
        {
            var allCommands = await GetAllCommandsAsync();
            return allCommands
                .Where(c => c.Stream == stream)
                .OrderBy(c => c.Function)
                .ToList();
        }

        /// <summary>
        /// 获取命令统计信息
        /// </summary>
        public async Task<CommandManagerStatistics> GetStatisticsAsync()
        {
           
            var stats = new CommandManagerStatistics();

            // 获取所有命令
            var allCommands = await GetAllCommandsAsync();
            stats.TotalCommands = allCommands.Count;

            // 分类统计
            stats.IncentiveCommands = allCommands.Count(c => c.Function % 2 == 1);
            stats.ResponseCommands = allCommands.Count(c => c.Function % 2 == 0);

            // 按Stream统计
            stats.CommandsByStream = allCommands
                .GroupBy(c => c.Stream)
                .ToDictionary(g => g.Key, g => g.Count());

            // 验证命令
            stats.ValidationResults = await ValidateCommandsAsync(allCommands);

            return stats;
        }

        /// <summary>
        /// 重新加载所有命令
        /// </summary>
        public async Task ReloadAllCommandsAsync(FormulaConfiguration formulaConfiguration)
        {
            await _initSemaphore.WaitAsync();
            try
            {
                Console.WriteLine("重新加载所有命令...");

                // 重置状态
                _isInitialized = false;
                _allCommandsCache.Clear();

                // 重新初始化
                await InitializeAsync(formulaConfiguration);

                Console.WriteLine("重新加载完成");
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// 保存所有命令到Excel
        /// </summary>
        public async Task SaveAllCommandsToExcelAsync()
        {
            try
            {
                Console.WriteLine("保存所有命令到Excel...");

                // 保存主动命令
                if (_incentiveCommands is IncentiveCommand incentive)
                {
                    await NPOIHelper.SaveIncentiveCommandToExcel(CommandExcelPath, _incentiveCommands._commandDictionary);
                    Console.WriteLine($"主动命令已保存到: {CommandExcelPath}");
                }

                // 保存应答命令
                if (_responseCommands is ResponseCommand response)
                {
                    await NPOIHelper.SaveResponseCommandToExcel(CommandExcelPath, _responseCommands._commandDictionary);
                    Console.WriteLine($"应答命令已保存到: {CommandExcelPath}");
                }

                Console.WriteLine("所有命令保存完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存命令到Excel失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存主动命令到Excel
        /// </summary>
        public async Task SaveIncentiveCommandsToExcelAsync(string filePath = null)
        {
            
            if (_incentiveCommands is IncentiveCommand incentive)
            {
                await incentive.Save(filePath);
                Console.WriteLine($"主动命令已保存到: {filePath ?? CommandExcelPath}");
            }
        }

        /// <summary>
        /// 保存应答命令到Excel
        /// </summary>
        public async Task SaveResponseCommandsToExcelAsync(string filePath = null)
        {
            if (_responseCommands is ResponseCommand response)
            {
                await response.Save(filePath);
                Console.WriteLine($"应答命令已保存到: {filePath ?? CommandExcelPath}");
            }
        }

      
        
        /// <summary>
        /// 验证命令对完整性
        /// </summary>
        public async Task<List<(uint stream, uint function)>> ValidateCommandPairsAsync()
        {
           
            var missingPairs = new List<(uint stream, uint function)>();

            // 获取所有主动命令
            var incentiveCommands = await _incentiveCommands.GetAllCommands();

            foreach (var incentive in incentiveCommands)
            {
                uint responseFunction = incentive.Function + 1;

                // 检查对应的应答命令是否存在
                var response = await _responseCommands.FindCommands(incentive.Stream, responseFunction);
                if (response == null)
                {
                    missingPairs.Add((incentive.Stream, incentive.Function));
                }
            }

            return missingPairs;
        }

        /// <summary>
        /// 清除所有命令
        /// </summary>
        public async Task ClearAllCommandsAsync()
        {
            if (!_isInitialized)
            {
                return;
            }

            Console.WriteLine("清除所有命令...");

            // 清除主动命令
            var incentiveCommands = await _incentiveCommands.GetAllCommands();
            foreach (var command in incentiveCommands)
            {
                await _incentiveCommands.RemoveCommand(command.Name);
            }

            // 清除应答命令
            var responseCommands = await _responseCommands.GetAllCommands();
            foreach (var command in responseCommands)
            {
                await _responseCommands.RemoveCommand(command.Name);
            }

            // 清除缓存
            _allCommandsCache.Clear();

            Console.WriteLine("所有命令已清除");
        }

        /// <summary>
        /// 获取命令管理器状态
        /// </summary>
        public CommandManagerStatus GetStatus()
        {
            return new CommandManagerStatus
            {
                IsInitialized = _isInitialized,
                LastInitialized = _lastInitialized,
                IncentiveExcelPath = CommandExcelPath,
                ResponseExcelPath = CommandExcelPath,
                IncentiveFileExists = File.Exists(CommandExcelPath),
                ResponseFileExists = File.Exists(CommandExcelPath),
                LastOperationCount = _lastOperationCount
            };
        }

        #region 私有辅助方法

        /// <summary>
        /// 重建缓存
        /// </summary>
        private async Task RebuildCacheAsync()
        {
            await _cacheSemaphore.WaitAsync();
            try
            {
                _allCommandsCache.Clear();

                // 合并主动命令和应答命令
                var incentiveCommands = await _incentiveCommands.GetAllCommands();
                var responseCommands = await _responseCommands.GetAllCommands();

                foreach (var command in incentiveCommands)
                {
                    _allCommandsCache[command.Key] = command;
                }

                foreach (var command in responseCommands)
                {
                    _allCommandsCache[command.Key] = command;
                }
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }

        /// <summary>
        /// 更新缓存
        /// </summary>
        private async Task UpdateCacheAsync(SFCommand command)
        {
            await _cacheSemaphore.WaitAsync();
            try
            {
                _allCommandsCache[command.Key] = command;
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }

        /// <summary>
        /// 验证命令列表
        /// </summary>
        private async Task<List<CommandValidationResult>> ValidateCommandsAsync(List<SFCommand> commands)
        {
            var results = new List<CommandValidationResult>();

            foreach (var command in commands)
            {
                var result = new CommandValidationResult
                {
                    Key = command.Key,
                    Type = command.Function % 2 == 1 ? "Incentive" : "Response"
                };

                // 基本验证
                if (command.Stream < 1 || command.Stream > 127)
                {
                    result.IsValid = false;
                    result.Message = $"Stream {command.Stream} 超出范围(1-127)";
                }
                else if (command.Function > 255)
                {
                    result.IsValid = false;
                    result.Message = $"Function {command.Function} 超出范围(0-255)";
                }
                else
                {
                    result.IsValid = true;
                    result.Message = "命令格式有效";

                    // 检查类型是否正确
                    if (command.Function % 2 == 1 && result.Type != "Incentive")
                    {
                        result.Warnings.Add($"Function {command.Function} 是奇数，但被标记为应答命令");
                    }
                    else if (command.Function % 2 == 0 && result.Type != "Response")
                    {
                        result.Warnings.Add($"Function {command.Function} 是偶数，但被标记为主动命令");
                    }
                }

                results.Add(result);
            }

            return results;
        }

        #endregion

        #region 其他辅助方法

        /// <summary>
        /// 导出命令对到CSV文件
        /// </summary>
        public async Task<bool> ExportCommandPairsToCsvAsync(string filePath)
        {
            try
            {
                var allCommands = await GetAllCommandsAsync();
                var csvLines = new List<string>
            {
                "Stream,Function,Name,Type,WBit,HasMessage,ID"
            };

                foreach (var command in allCommands.OrderBy(c => c.Stream).ThenBy(c => c.Function))
                {
                    var type = command.Function % 2 == 1 ? "Incentive" : "Response";
                    var wbit = command.Message?.WBit.ToString() ?? "false";
                    var hasMessage = command.Message != null ? "Yes" : "No";

                    csvLines.Add($"{command.Stream},{command.Function},\"{command.Name}\",{type},{wbit},{hasMessage},{command.ID}");
                }

                await File.WriteAllLinesAsync(filePath, csvLines);
                Console.WriteLine($"命令对已导出到CSV: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"导出命令对到CSV失败: {ex.Message}");
                return false;
            }
        }

       

        /// <summary>
        /// 搜索命令（根据名称或ID）
        /// </summary>
        public async Task<List<SFCommand>> SearchCommandsAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<SFCommand>();
            }

            var allCommands = await GetAllCommandsAsync();

            return allCommands
                .Where(c =>
                    (c.Name != null && c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (c.ID != null && c.ID.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    c.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// 更新命令集合
        /// </summary>
        public async Task UPDataCommondCollection(FormulaConfiguration formulaConfiguration)
        {
            _incentiveCommands._commandDictionary = formulaConfiguration.IncentiveCommandDictionary;

            _responseCommands._commandDictionary = formulaConfiguration.ResponseCommandDictionary;
        }



        #endregion
    }


}
