using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

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
        /// 配置默认分类
        /// </summary>
        public LogConfiguration ConfigureDefaultCategories()
        {
            // 系统日志 - 记录系统运行状态
            AddCategory("System", LogLevel.Debug, "System");

            // 数据库日志 - 记录数据库操作
            AddCategory("Database", LogLevel.Info, "Database");

            // UI日志 - 记录用户界面操作
            AddCategory("UI", LogLevel.Info, "UI");

            // 通信日志 - 记录网络通信
            AddCategory("Communication", LogLevel.Info, "Communication");

            // 默认分类 - 未明确分类的日志
            AddCategory("Default", LogLevel.Info, "General");

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
