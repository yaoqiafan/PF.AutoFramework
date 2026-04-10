using Microsoft.EntityFrameworkCore;
using PF.Core.Constants;
using PF.Core.Interfaces.Alarm;
using PF.Data.Context;
using Prism.Ioc;

namespace PF.Services.Alarm
{
    /// <summary>
    /// 报警模块 DI 注册扩展方法。
    /// 在 App.xaml.cs 的 RegisterTypes 中调用 <see cref="AddAlarmServices"/>。
    /// </summary>
    public static class AlarmServiceExtensions
    {
        /// <summary>
        /// 注册报警字典服务和报警业务服务（均为单例）。
        /// </summary>
        /// <param name="containerRegistry">Prism 容器注册器</param>
        /// <param name="dbFilePath">AlarmHistory.db 的完整路径</param>
        public static IContainerRegistry AddAlarmServices(
            this IContainerRegistry containerRegistry,
            string dbFilePath)
        {
            // 构建 AlarmDbContext 的 DbContextOptions（连接字符串固定，表名在 OnModelCreating 动态生成）
            var options = new DbContextOptionsBuilder<AlarmDbContext>()
                .UseSqlite($"Data Source={dbFilePath}")
                .Options;

            containerRegistry.RegisterInstance<DbContextOptions<AlarmDbContext>>(options);

            // 字典服务：单例，程序生命周期内只初始化一次
            containerRegistry.RegisterSingleton<IAlarmDictionaryService, AlarmDictionaryService>();

            // 业务服务：单例，管理全局活跃报警状态
            containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();

            return containerRegistry;
        }
    }
}
