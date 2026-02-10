// LoggingModule.cs
using log4net;
using PF.Common.Core.Logging;
using PF.Common.Data.Constant;
using PF.Common.Data.Logging;
using PF.Common.Data.Param;
using PF.Common.Interface;
using PF.Common.Interface.Loging;
using PF.Common.Logging.ViewModels;
using PF.Common.Logging.Views;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.IO;

namespace PF.Common.Logging
{
    /// <summary>
    /// 日志模块 - Prism模块实现
    /// </summary>
    public class LoggingModule : IModule
    {
        private readonly IRegionManager _regionManager;
        private readonly ILogService _logService;

        public LoggingModule(IRegionManager regionManager, ILogService logService)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _logService = logService?? throw new ArgumentNullException(nameof(logService));
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            try
            {
                // 注册视图到区域
                _regionManager.RegisterViewWithRegion(NavigationConstants.Regions.LoggingListRegion, NavigationConstants.Views.LoggingListView);

                // 记录模块初始化日志
                _logService.Info("日志模块初始化完成", "LoggingModule");
            }
            catch (Exception ex)
            {
                // 使用备用方式记录错误
                LogFallbackError("日志模块初始化失败", ex);
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            try
            {
                containerRegistry.RegisterForNavigation<LogListView, LogListViewModel>(NavigationConstants.Views.LoggingListView);
            }
            catch (Exception ex)
            {
                // 注册失败时的备用日志
                LogFallbackError("日志模块类型注册失败", ex);
                throw;
            }
        }

        #region 辅助方法
        private void LogFallbackError(string message, Exception ex)
        {
            // 当日志服务不可用时的备用日志记录
            try
            {
                // 1. 尝试使用log4net直接记录
                var logger = LogManager.GetLogger(typeof(LoggingModule));
                logger.Error($"{message}: {ex.Message}", ex);

                // 2. 输出到调试窗口
                System.Diagnostics.Debug.WriteLine($"[LOG_FALLBACK] {message}: {ex.Message}");
            }
            catch
            {
                // 所有备用方案都失败时，静默处理
            }
        }
        #endregion
    }
}