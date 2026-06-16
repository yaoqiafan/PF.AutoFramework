using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Vision;
using PF.Vision.Halcon.Services;
using Prism.Ioc;

namespace PF.Vision.Halcon.Extensions;

/// <summary>
/// Halcon 视觉引擎服务 DI 注册扩展方法。
/// 在 App.xaml.cs 的 RegisterTypes 中调用 <see cref="AddVisionServices"/>。
/// </summary>
public static class VisionServiceExtensions
{
    /// <summary>
    /// 注册 Halcon 视觉引擎服务（单例）。
    /// </summary>
    /// <param name="containerRegistry">Prism 容器注册器</param>
    /// <param name="procedureDirectory">.hdev 过程文件所在目录的完整路径</param>
    public static IContainerRegistry AddVisionServices(
        this IContainerRegistry containerRegistry,
        string procedureDirectory)
    {
        containerRegistry.RegisterSingleton<IVisionService>(cp =>
            new HalconVisionService(procedureDirectory, cp.Resolve<ILogService>()));

        return containerRegistry;
    }
}
