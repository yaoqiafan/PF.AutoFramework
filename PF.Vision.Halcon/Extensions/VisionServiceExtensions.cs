using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Vision;
using PF.Vision.Halcon.Services;
using Prism.Ioc;
using System.IO;

namespace PF.Vision.Halcon.Extensions;

/// <summary>
/// Halcon 视觉引擎服务 DI 注册扩展方法。
/// 在 App.xaml.cs 的 RegisterTypes 中调用 <see cref="AddVisionServices"/>。
/// </summary>
public static class VisionServiceExtensions
{
    /// <summary>
    /// 注册三模式视觉引擎管理器和管线加载器（均为单例）。
    /// </summary>
    /// <param name="containerRegistry">Prism 容器注册器</param>
    /// <param name="procedureDirectory">.hdev 过程文件所在目录的完整路径</param>
    /// <param name="pipelineDirectory">
    /// 管线 JSON 配置目录；为 null 时默认使用 procedureDirectory 同级的 "Workflows" 目录
    /// </param>
    public static IContainerRegistry AddVisionServices(
        this IContainerRegistry containerRegistry,
        string  procedureDirectory,
        string? pipelineDirectory = null)
    {
        containerRegistry.RegisterSingleton<IVisionContextManager>(cp =>
            new VisionContextManager(procedureDirectory, cp.Resolve<ILogService>()));

        containerRegistry.RegisterSingleton<IHalconDebugService>(cp =>
            new HalconDebugService(
                cp.Resolve<IVisionContextManager>(),
                cp.Resolve<ILogService>()));

        var pipelineDir = pipelineDirectory
                       ?? Path.Combine(procedureDirectory, "..", "Workflows");

        containerRegistry.RegisterSingleton<VisionPipelineLoader>(cp =>
            new VisionPipelineLoader(pipelineDir, cp.Resolve<ILogService>()));

        return containerRegistry;
    }
}
