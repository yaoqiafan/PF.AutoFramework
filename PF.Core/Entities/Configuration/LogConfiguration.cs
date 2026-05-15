using PF.Core.Constants;
using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Core.Entities.Configuration
{
    /// <summary>
    /// 日志配置
    /// </summary>
    public class LogConfiguration
    {
        /// <summary>
        /// 全局最低日志记录级别
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 是否启用UI界面日志显示
        /// </summary>
        public bool EnableUiLogging { get; set; } = true;

        /// <summary>
        /// 是否启用文件日志记录
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// 是否启用控制台日志输出
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// 是否启用自动删除旧日志文件功能
        /// </summary>
        public bool AutoDeleteLogs { get; set; } = true;

        /// <summary>
        /// 自动删除日志文件的时间间隔（天数）
        /// </summary>
        public int AutoDeleteIntervalDays { get; set; } = 30;

        /// <summary>
        /// 是否按小时分割日志文件
        /// </summary>
        public bool SplitByHour { get; set; } = false;

        /// <summary>
        /// UI界面最多显示的日志条目数
        /// </summary>
        public int MaxUiEntries { get; set; } = 1000;

        /// <summary>
        /// 日志文件的基础存储路径
        /// </summary>
        public string BasePath { get; set; } = "Logs";

        /// <summary>
        /// 历史日志文件路径（用于查询）
        /// </summary>
        public string HistoricalLogPath { get; set; } = "Logs";


        /// <summary>
        /// 按分类配置的日志设置
        /// </summary>
        public Dictionary<string, CategoryConfig> Categories { get; set; } =
            new Dictionary<string, CategoryConfig>();

        /// <summary>
        /// 默认配置文件名
        /// </summary>
        public const string DefaultConfigFileName = "log_config.json";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        /// <summary>
        /// 保存配置到本地 JSON 文件
        /// </summary>
        public void Save(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 从本地 JSON 文件加载配置，文件不存在时返回默认配置
        /// </summary>
        public static LogConfiguration LoadOrDefault(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath, Encoding.UTF8);
                    var config = JsonSerializer.Deserialize<LogConfiguration>(json, _jsonOptions);
                    if (config != null && config.Categories.Count > 0)
                        return config;
                }
                else
                {
                    var configdefault = CreateDefaultLogConfiguration().ConfigureDefaultCategories();

                    string strjson = JsonSerializer.Serialize<LogConfiguration>(configdefault, _jsonOptions);

                    File.WriteAllText(filePath, strjson);

                    if (configdefault != null && configdefault.Categories.Count > 0)
                        return configdefault;
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogConfiguration] 加载配置文件失败，使用默认配置: {ex.Message}");
            }
                return CreateDefaultLogConfiguration().ConfigureDefaultCategories();
        }



        private static LogConfiguration CreateDefaultLogConfiguration()
        {
            //var appBasePath = AppDomain.CurrentDomain.BaseDirectory;
            var logBasePath = "D://PF_Logs";

            var config = new LogConfiguration
            {
                BasePath = logBasePath,
                HistoricalLogPath = logBasePath,
                EnableConsoleLogging = true,
                EnableFileLogging = true,
                EnableUiLogging = true,
                MinimumLevel = LogLevel.Debug,
                AutoDeleteLogs = true,
                AutoDeleteIntervalDays = 30,
                MaxUiEntries = 1000,
                SplitByHour = false
            };
            config.ConfigureDefaultCategories();
            config.AddCategory(LogCategories.Custom, LogLevel.Warn, LogCategories.Custom);
            return config;
        }

        /// <summary>
        /// 获取默认配置文件路径（应用程序根目录下）
        /// </summary>
        public static string GetDefaultFilePath() =>
            Path.Combine(ConstGlobalParam.ConfigPath, DefaultConfigFileName);

        /// <summary>
        /// 配置默认分类
        /// </summary>
        public LogConfiguration ConfigureDefaultCategories()
        {
            // 系统日志 - 记录系统运行状态
            AddCategory(LogCategories.System, LogLevel.Debug, LogCategories.System);

            // 数据库日志 - 记录数据库操作
            AddCategory(LogCategories.Database, LogLevel.Info, LogCategories.Database);

            // UI日志 - 记录用户界面操作
            AddCategory(LogCategories.UI, LogLevel.Info, LogCategories.UI);

            // 通信日志 - 记录网络通信
            AddCategory(LogCategories.Communication, LogLevel.Info, LogCategories.Communication);

            // 默认分类 - 未明确分类的日志
            AddCategory("Default", LogLevel.Info, "General");

            //硬件日志 - 记录硬件调试
            AddCategory(LogCategories.HaraWare, LogLevel.Debug, LogCategories.HaraWare);

            AddCategory(LogCategories.Recipe, LogLevel.Debug, LogCategories.Recipe);

            AddCategory(LogCategories.SecsGem, LogLevel.Debug, LogCategories.SecsGem);

            return this;
        }

        /// <summary>
        /// 添加或更新分类配置
        /// </summary>
        public LogConfiguration AddCategory(string category, LogLevel minLevel = LogLevel.Info,
            string? fileNamePrefix = null, bool enableFileLog = true)
        {
            Categories[category] = new CategoryConfig
            {
                MinLevel = minLevel,
                EnableFileLog = enableFileLog,
                FileNamePrefix = fileNamePrefix ?? category
            };
            return this;
        }

        /// <summary>
        /// 移除分类配置
        /// </summary>
        public LogConfiguration RemoveCategory(string category)
        {
            Categories.Remove(category);
            return this;
        }

        /// <summary>
        /// 获取分类配置，如果不存在则返回默认配置
        /// </summary>
        public CategoryConfig GetCategoryConfig(string category)
        {
            if (Categories.TryGetValue(category, out var config))
            {
                return config;
            }

            // 返回默认配置
            return new CategoryConfig
            {
                MinLevel = MinimumLevel,
                EnableFileLog = EnableFileLogging,
                FileNamePrefix = "General"
            };
        }

        /// <summary>
        /// 获取所有启用了文件日志的分类
        /// </summary>
        public IEnumerable<string> GetFileLogCategories()
        {
            foreach (var kvp in Categories)
            {
                if (kvp.Value.EnableFileLog)
                {
                    yield return kvp.Key;
                }
            }
        }
    }
}
