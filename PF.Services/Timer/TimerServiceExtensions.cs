using PF.Core.Interfaces.Timer;
using PF.Services.Timer.Internal;
using Prism.Ioc;
using System.IO;

namespace PF.Services.Timer
{
    /// <summary>
    /// 定时服务 DI 注册扩展方法。
    /// 在 App.xaml.cs 的 RegisterTypes 中调用 <see cref="AddTimerService"/>。
    /// </summary>
    public static class TimerServiceExtensions
    {
        /// <summary>
        /// 注册集中定时服务（单例）及其持久化层。
        /// </summary>
        /// <param name="containerRegistry">Prism 容器注册器</param>
        /// <param name="persistenceFilePath">调度状态持久化文件完整路径（如 D://PFConfig/.../timer_schedule.json）</param>
        public static IContainerRegistry AddTimerService(
            this IContainerRegistry containerRegistry,
            string persistenceFilePath)
        {
            var persistence = new TimerPersistence(persistenceFilePath);
            containerRegistry.RegisterInstance(persistence);
            containerRegistry.RegisterSingleton<IAppTimerService, AppTimerService>();
            return containerRegistry;
        }
    }
}
