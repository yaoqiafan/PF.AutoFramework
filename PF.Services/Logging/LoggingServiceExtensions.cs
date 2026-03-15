using PF.Core.Constants;
using PF.Core.Entities.Configuration;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using Prism.Ioc;
using System.IO;

namespace PF.Services.Logging
{
    /// <summary>
    /// 日志服务 DI 注册扩展方法
    /// </summary>
    public static class LoggingServiceExtensions
    {
        /// <summary>
        /// 注册日志服务到 DI 容器。如不传入配置，自动使用默认配置（Logs 目录，Debug 级别）。
        /// </summary>
        public static IContainerRegistry AddLogging(
            this IContainerRegistry containerRegistry,
            LogConfiguration? config = null)
        {
            var logConfig = config ?? CreateDefaultLogConfiguration();
            EnsureLogDirectories(logConfig);

            var logService = new LogService(logConfig);
            containerRegistry.RegisterInstance(logConfig);
            containerRegistry.RegisterInstance<ILogService>(logService);
            return containerRegistry;
        }

        private static LogConfiguration CreateDefaultLogConfiguration()
        {
            var appBasePath = AppDomain.CurrentDomain.BaseDirectory;
            var logBasePath = Path.Combine(appBasePath, "Logs");

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

        private static void EnsureLogDirectories(LogConfiguration config)
        {
            try
            {
                if (!Directory.Exists(config.BasePath))
                    Directory.CreateDirectory(config.BasePath);

                foreach (var category in config.GetFileLogCategories())
                {
                    var dir = Path.Combine(config.BasePath, category);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
            }
            catch
            {
                // 启动阶段静默处理目录创建失败
            }
        }
    }
}
