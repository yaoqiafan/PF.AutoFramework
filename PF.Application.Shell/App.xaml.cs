using PF.Application.Base;
using PF.Application.Base.ViewModels;
using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.ViewModels;
using PF.Core.Constants;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Timer;
using PF.Core.Interfaces.TowerLight;
using PF.Data.Entity.Category.Basic;
using PF.Infrastructure.Station.Basic;
using PF.Modules.Alarm;
using PF.Modules.Debug;
using PF.Modules.Halcon;
using PF.Modules.Identity;
using PF.Modules.Logging;
using PF.Modules.Parameter;
using PF.Modules.Production;
using PF.Modules.SecsGem;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.Vision.Halcon.Extensions;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.Recipe;
using PF.WorkStation.AutoOcr.Stations;
using PF.WorkStation.AutoOcr.UI;
using Prism.Modularity;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PF.Application.Shell
{
    public partial class App : PFApplicationBase
    {
        protected override string AppMutexId => "OCRAppID-12345678-ABCD-EFGH-IJKL-1234567890AB";

        protected override IDefaultParam CreateDefaultParameters() => new DefaultParameters();

        protected override void RegisterMainWindowViewModel(IContainerRegistry containerRegistry)
            => containerRegistry.RegisterSingleton<MainWindowViewModelBase, MainWindowViewModel>();

        #region 硬件工厂

        protected override void RegisterHardwareFactories(IHardwareManagerService hwManager)
        {
            var dataDirectory = ConstGlobalParam.ConfigPath;

            hwManager.RegisterFactory("LTDMCMotionCard", cfg =>
            {
                int cardIndex = cfg.ConnectionParameters.TryGetValue("CardIndex", out var ci)
                    ? int.Parse(ci) : 0;
                return new PF.Infrastructure.Hardware.Card.LTDMC.LTMDCMotionCard(
                    cardIndex, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });

            hwManager.RegisterFactory("EtherCatAxis", cfg =>
            {
                int axisIndex = cfg.ConnectionParameters.TryGetValue("AxisIndex", out var idx)
                    ? int.Parse(idx) : 0;
                string axisParamStr = cfg.ConnectionParameters.TryGetValue("AxisParam", out var ap)
                    ? ap : System.Text.Json.JsonSerializer.Serialize(new AxisParam());
                var axisParam = System.Text.Json.JsonSerializer.Deserialize<AxisParam>(axisParamStr) ?? new AxisParam();
                return new Infrastructure.Hardware.Motor.EtherCatAxis(
                    cfg.DeviceId, axisIndex, axisParam, cfg.DeviceName, cfg.IsSimulated, LogService, dataDirectory);
            });

            hwManager.RegisterFactory("EtherCatIO", cfg =>
            {
                int inCount = cfg.ConnectionParameters.TryGetValue("InPutCount", out var ic) ? int.Parse(ic) : 0;
                int outCount = cfg.ConnectionParameters.TryGetValue("OutPutCount", out var oc) ? int.Parse(oc) : 0;
                return new Infrastructure.Hardware.IO.EtherCatIO(
                    inCount, outCount, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });

            hwManager.RegisterFactory("HKBarcodeScan", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int trigPort = cfg.ConnectionParameters.TryGetValue("TiggerPort", out var tp) ? int.Parse(tp) : 0;
                int userPort = cfg.ConnectionParameters.TryGetValue("UserPort", out var up) ? int.Parse(up) : 0;
                int timeout  = cfg.ConnectionParameters.TryGetValue("TimeOutMs", out var to) ? int.Parse(to) : 0;
                return new Infrastructure.Hardware.BarcodeScan.HKRobot.HKBarcodeScan(
                    ip, trigPort, userPort, timeout, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });

            hwManager.RegisterFactory("KeyenceIntelligentCamera", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int trigPort = cfg.ConnectionParameters.TryGetValue("TiggerPort", out var tp) ? int.Parse(tp) : 0;
                int timeout  = cfg.ConnectionParameters.TryGetValue("TimeOutms", out var to) ? int.Parse(to) : 0;
                return new Infrastructure.Hardware.Carame.IntelligentCamera.Keyence.KeyenceIntelligentCamera(
                    ip, trigPort, timeout, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });

            hwManager.RegisterFactory("CTS_LightControoller", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("COM", out var com);
                return new Infrastructure.Hardware.LightController.CTS.CTSLightController(
                    com, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });
        }

        #endregion

        #region IO 映射

        protected override void RegisterIOMappings(IIOMappingService ioMappingService)
        {
            ioMappingService.RegisterInputEnum<E_InPutName>("IO_Collectorll");
            ioMappingService.RegisterOutputEnum<E_OutPutName>("IO_Collectorll");
        }

        #endregion

        #region 机构、工站、主控注册

        protected override void RegisterMechanismsAndStations(IContainerRegistry containerRegistry)
        {
            var container = containerRegistry.GetContainer();

            containerRegistry.RegisterSingleton<IPanelIoConfig, PF.WorkStation.AutoOcr.CostParam.PanelIoConfig>();
            containerRegistry.RegisterSingleton<ITowerLightDoWriterConfig, TowerLightDoWriterConfig>();

            container.RegisterMany(
                [typeof(WS1FeedingModel), typeof(IMechanism)],
                typeof(WS1FeedingModel), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WS1FeedingModel));

            container.RegisterMany(
                [typeof(WSDetectionModule), typeof(IMechanism)],
                typeof(WSDetectionModule), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WSDetectionModule));

            container.RegisterMany(
                [typeof(WS1MaterialPullingModule), typeof(IMechanism)],
                typeof(WS1MaterialPullingModule), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WS1MaterialPullingModule));

            container.RegisterMany(
                [typeof(WSDataModule), typeof(IMechanism)],
                typeof(WSDataModule), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WSDataModule));

            container.RegisterMany(
                [typeof(WSSecsGemModule), typeof(IMechanism)],
                typeof(WSSecsGemModule), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WSSecsGemModule));

            container.RegisterMany(
                [typeof(WS2FeedingModel), typeof(IMechanism)],
                typeof(WS2FeedingModel), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WS2FeedingModel));

            container.RegisterMany(
                [typeof(WS2MaterialPullingModule), typeof(IMechanism)],
                typeof(WS2MaterialPullingModule), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WS2MaterialPullingModule));

            container.RegisterMany(
                [typeof(WS1FeedingStation), typeof(IStation)],
                typeof(WS1FeedingStation), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WS1FeedingStation));

            container.RegisterMany(
                [typeof(WSDetectionStation<StationMemoryBaseParam>), typeof(IStation)],
                typeof(WSDetectionStation<StationMemoryBaseParam>), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WSDetectionStation<StationMemoryBaseParam>));

            container.RegisterMany(
                [typeof(WS1MaterialPullingStation), typeof(IStation)],
                typeof(WS1MaterialPullingStation), reuse: DryIoc.Reuse.Singleton);

            container.RegisterMany(
                [typeof(WS2FeedingStation), typeof(IStation)],
                typeof(WS2FeedingStation), reuse: DryIoc.Reuse.Singleton,
                serviceKey: nameof(WS2FeedingStation));

            container.RegisterMany(
                [typeof(WS2MaterialPullingStation), typeof(IStation)],
                typeof(WS2MaterialPullingStation), reuse: DryIoc.Reuse.Singleton);

            containerRegistry.RegisterSingleton<IMasterController, AutoOCRMachineController>();
        }

        #endregion

        #region 配方注册

        protected override void RegisterRecipes(IContainerRegistry containerRegistry)
        {
            containerRegistry.GetContainer().RegisterMany(
                [typeof(IRecipeService<OCRRecipeParam>), typeof(OCRRecipe<OCRRecipeParam>)],
                typeof(OCRRecipe<OCRRecipeParam>),
                reuse: DryIoc.Reuse.Singleton);
        }

        #endregion

        #region 机构初始化序列

        protected override async Task<bool> InitializeMechanismsAsync()
        {
            var c = this.Container;

            if (!await c.Resolve<IMechanism>(nameof(WS1FeedingModel)).InitializeAsync()) return false;
            if (!await c.Resolve<IMechanism>(nameof(WS1MaterialPullingModule)).InitializeAsync()) return false;
            if (!await c.Resolve<IMechanism>(nameof(WS2FeedingModel)).InitializeAsync()) return false;
            if (!await c.Resolve<IMechanism>(nameof(WS2MaterialPullingModule)).InitializeAsync()) return false;
            if (!await c.Resolve<IMechanism>(nameof(WSDetectionModule)).InitializeAsync()) return false;
            if (!await c.Resolve<IMechanism>(nameof(WSDataModule)).InitializeAsync()) return false;
            if (!await c.Resolve<IMechanism>(nameof(WSSecsGemModule)).InitializeAsync()) return false;

            return true;
        }

        #endregion

        #region 模块目录

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<IdentityModule>();
            moduleCatalog.AddModule<AlarmModule>();
            moduleCatalog.AddModule<LoggingModule>();
            moduleCatalog.AddModule<ParameterModule>();
            moduleCatalog.AddModule<DebugModule>();
            moduleCatalog.AddModule<ProductionRecordModule>();
            moduleCatalog.AddModule<SecsGemModule>();
            moduleCatalog.AddModule<AutoOcrUIModule>();
            moduleCatalog.AddModule<HalconModule>();
        }

        #endregion

        #region 视觉服务

        protected override void RegisterVisionServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.AddVisionServices(
                procedureDirectory: ConstGlobalParam.VisionProceduresPath,
                pipelineDirectory:  ConstGlobalParam.VisionWorkflowsPath);
        }

        #endregion

        #region 每日定时任务

        protected override void RegisterProjectDailyTasks()
        {
            var timer = Container.Resolve<IAppTimerService>();

            timer.RegisterDailyAt(
                key: "DiskWarning_OCRImagePath",
                timeOfDay: new TimeSpan(8, 0, 0),
                callback: () => _ = CheckDiskUsageAsync(),
                catchUpOnStart: true);

            UILogger.Info("[磁盘预警] 定时任务已注册（每日 08:00）");

            timer.RegisterDailyAt(
                key: "ImageCleanup_OCRImagePath",
                timeOfDay: new TimeSpan(8, 0, 0),
                callback: () => _ = CleanupOldImagesAsync(),
                catchUpOnStart: true);
            UILogger.Info("[图片清理] 定时任务已注册（每日 08:00）");
        }

        private async Task CheckDiskUsageAsync()
        {
            try
            {
                var paramService = Container.Resolve<IParamService>();

                var imagePath = await paramService.GetParamAsync<string>(
                    E_Params.OCRCameraImageSavePath.ToString());
                var threshold = await paramService.GetParamAsync<double>(
                    E_Params.DiskWarningThreshold.ToString(), 80.0);

                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    UILogger.Warn("[磁盘预警] 图片保存路径参数为空，跳过检查");
                    return;
                }

                // 兼容 "E//path"、"E:\\path"、"E:/path" 等多种路径格式
                var normalized = imagePath.Replace("//", "\\").Replace("/", "\\");
                var root = Path.GetPathRoot(normalized);
                if (string.IsNullOrEmpty(root))
                {
                    UILogger.Warn($"[磁盘预警] 无法从路径 [{imagePath}] 解析驱动器，跳过检查");
                    return;
                }

                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                {
                    UILogger.Warn($"[磁盘预警] 驱动器 {root} 未就绪，跳过检查");
                    return;
                }

                double usedPercent = (1.0 - (double)drive.AvailableFreeSpace / drive.TotalSize) * 100.0;
                double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                double totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);

                UILogger.Info($"[磁盘预警] 驱动器 {root}  使用率 {usedPercent:F1}%，" +
                               $"剩余 {freeGb:F2} GB / 总计 {totalGb:F2} GB，阈值 {threshold}%");

                if (usedPercent < threshold) return;

                UILogger.Warn($"[磁盘预警] 触发预警：使用率 {usedPercent:F1}% ≥ 阈值 {threshold}%");

                await this.Dispatcher.InvokeAsync(() =>
                {
                    Container.Resolve<IMessageService>().ShowMessage(
                        $"图片保存路径所在驱动器：{root}\n" +
                        $"当前使用率：{usedPercent:F1}%（预警阈值：{threshold}%）\n" +
                        $"剩余空间：{freeGb:F2} GB / 总容量：{totalGb:F2} GB\n\n" +
                        $"请及时清理磁盘，避免影响生产图片的正常存储！",
                        "磁盘存储预警",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
            catch (Exception ex)
            {
                UILogger.Error("[磁盘预警] 检查过程发生异常", ex);
            }
        }

        private async Task CleanupOldImagesAsync()
        {
            try
            {
                var paramService = Container.Resolve<IParamService>();

                var rawPath = await paramService.GetParamAsync<string>(
                    E_Params.OCRCameraImageSavePath.ToString());
                var retentionMonths = await paramService.GetParamAsync<int>(
                    E_Params.OCRCameraImageRetentionMonths.ToString(), 3);

                if (retentionMonths <= 0)
                {
                    UILogger.Info("[图片清理] 存储时间参数为 0，自动清理已禁用，跳过");
                    return;
                }

                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    UILogger.Warn("[图片清理] 图片保存路径参数为空，跳过");
                    return;
                }

                // 兼容 "E//path"、"E:\\path"、"E:/path" 格式
                var basePath = rawPath.Replace("//", "\\").Replace("/", "\\");
                if (!Directory.Exists(basePath))
                {
                    UILogger.Info($"[图片清理] 路径不存在，跳过: {basePath}");
                    return;
                }

                var cutoff = DateTime.Now.AddMonths(-retentionMonths);
                UILogger.Info($"[图片清理] 开始扫描 {basePath}，保留近 {retentionMonths} 个月，截止日期: {cutoff:yyyy-MM-dd}");

                int deleted = 0, skipped = 0;

                // 文件 I/O 在线程池执行，避免阻塞定时器线程
                await Task.Run(() =>
                {
                    foreach (var dir in Directory.EnumerateDirectories(basePath))
                    {
                        try
                        {
                            var info = new DirectoryInfo(dir);
                            if (info.CreationTime >= cutoff)
                                continue;

                            UILogger.Info($"[图片清理] 准备删除: {dir}（创建于 {info.CreationTime:yyyy-MM-dd}）");

                            if (TryDeleteDirectorySafe(dir))
                                deleted++;
                            else
                                skipped++;
                        }
                        catch (Exception ex)
                        {
                            UILogger.Warn($"[图片清理] 处理目录异常: {dir} — {ex.Message}");
                            skipped++;
                        }
                    }
                });

                UILogger.Info($"[图片清理] 完成，已删除: {deleted} 个目录，跳过: {skipped} 个目录");
            }
            catch (Exception ex)
            {
                UILogger.Error("[图片清理] 清理过程发生异常", ex);
            }
        }

        /// <summary>
        /// 尝试安全删除指定目录（含子目录和文件）。
        /// 遇到文件锁或权限不足时记录警告并返回 false，不抛出异常。
        /// </summary>
        private bool TryDeleteDirectorySafe(string dirPath)
        {
            try
            {
                // 逐文件去除只读属性，防止只读标记阻止删除
                foreach (var file in Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); }
                    catch { /* 属性清除失败时忽略，后续 Delete 若失败会被外层捕获 */ }
                }

                Directory.Delete(dirPath, recursive: true);
                UILogger.Info($"[图片清理] 已删除: {dirPath}");
                return true;
            }
            catch (IOException ex)
            {
                // 文件被其他进程占用（文件锁）
                UILogger.Warn($"[图片清理] 文件被占用，跳过本次: {dirPath} — {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                // 权限不足
                UILogger.Warn($"[图片清理] 无访问权限，跳过: {dirPath} — {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
