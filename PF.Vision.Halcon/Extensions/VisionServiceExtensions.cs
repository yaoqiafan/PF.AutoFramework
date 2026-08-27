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
        {
            var logService = cp.Resolve<ILogService>();
            // 静态工具类无法参与构造注入，在此挂接日志（幂等赋值，重复注册无副作用）
            Internal.RoiRegionBuilder.Logger = logService;
            return new VisionContextManager(procedureDirectory, logService);
        });

        // 显式传入过程目录：签名解析 / 过程枚举 / HDevelop 启动参数都只需要这个字符串，
        // 让调试服务自己持有，避免为取目录而拉起一个永不使用的 Debug 引擎
        containerRegistry.RegisterSingleton<IHalconDebugService>(cp =>
            new HalconDebugService(
                cp.Resolve<IVisionContextManager>(),
                cp.Resolve<ILogService>(),
                procedureDirectory));

        var pipelineDir = pipelineDirectory
                       ?? Path.Combine(procedureDirectory, "..", "Workflows");

        containerRegistry.RegisterSingleton<VisionPipelineLoader>(cp =>
            new VisionPipelineLoader(pipelineDir, cp.Resolve<ILogService>()));

        return containerRegistry;
    }

    /// <summary>
    /// 配置 <see cref="ShapeTemplateService"/> 的模板存放目录（消费项目启动时调用一次）。
    /// 之后无论是框架侧的 ROI 模板编辑弹窗，还是消费方自己的代码，存/取模板都只需要传一个名字，
    /// 不用互相传递完整路径——跟 <see cref="AddVisionServices"/> 里 <c>procedureDirectory</c>
    /// 的思路一致。
    /// </summary>
    /// <param name="containerRegistry">Prism 容器注册器（<see cref="ShapeTemplateService"/> 本身是
    /// 静态类，不需要真的注册进容器，这里只是保持跟 <see cref="AddVisionServices"/> 一致的调用手感）。</param>
    /// <param name="templateDirectory">模板包（<c>.roipk</c>）存放目录，不存在会自动创建。</param>
    public static IContainerRegistry AddShapeTemplateServices(
        this IContainerRegistry containerRegistry,
        string templateDirectory)
    {
        Directory.CreateDirectory(templateDirectory);
        ShapeTemplateService.TemplateDirectory = Path.GetFullPath(templateDirectory);
        return containerRegistry;
    }
}
