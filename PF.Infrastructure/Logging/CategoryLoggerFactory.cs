using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;

namespace PF.Infrastructure.Logging
{
    /// <summary>
    /// 分类日志工厂
    /// </summary>
    public static class CategoryLoggerFactory
    {
        /// <summary>
        /// 创建系统分类日志记录器
        /// </summary>
        public static CategoryLogger System(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.System);

        /// <summary>
        /// 创建数据库分类日志记录器
        /// </summary>
        public static CategoryLogger Database(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.Database);

        /// <summary>
        /// 创建UI分类日志记录器
        /// </summary>
        public static CategoryLogger UI(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.UI);

        /// <summary>
        /// 创建通讯分类日志记录器
        /// </summary>
        public static CategoryLogger Communication(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.Communication);

        /// <summary>
        /// 创建自定义分类日志记录器
        /// </summary>
        public static CategoryLogger Custom(ILogService logService) =>
           new CategoryLogger(logService, LogCategories.Custom);



        /// <summary>
        /// 创建硬件分类日志记录器
        /// </summary>
        public static CategoryLogger Hardware(ILogService logService) =>
           new CategoryLogger(logService, LogCategories.HaraWare );

        /// <summary>
        /// 创建配方分类日志记录器
        /// </summary>
        public static CategoryLogger Recipe (ILogService logService) =>
          new CategoryLogger(logService, LogCategories.Recipe );


        /// <summary>
        /// 创建SecsGem分类日志记录器
        /// </summary>
        public static CategoryLogger SecsGem(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.SecsGem);
    }
}
