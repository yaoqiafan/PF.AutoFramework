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



        public static CategoryLogger Hardware(ILogService logService) =>
           new CategoryLogger(logService, LogCategories.HaraWare );

        public static CategoryLogger Recipe (ILogService logService) =>
          new CategoryLogger(logService, LogCategories.Recipe );


        public static CategoryLogger SecsGem(ILogService logService) =>
            new CategoryLogger(logService, LogCategories.SecsGem);
    }
}
