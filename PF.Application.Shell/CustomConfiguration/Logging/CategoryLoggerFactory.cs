using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;

namespace PF.Application.Shell.CustomConfiguration.Logging
{
    /// <summary>
    /// 分类日志工厂
    /// </summary>
    public static class CategoryLoggerFactory
    {
        public static CategoryLogger System(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.System);

        public static CategoryLogger Database(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.Database);

        public static CategoryLogger UI(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.UI);

        public static CategoryLogger Communication(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.Communication);
        public static CategoryLogger Custom(ILogService logService) =>
           new CategoryLogger(logService, LogCategories.Custom);
    }
}
