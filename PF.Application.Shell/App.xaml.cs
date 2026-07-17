using PF.Application.Base;
using PF.Application.Base.ViewModels;
using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.ViewModels;
using PF.Core.Constants;
using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums;
using PF.Core.Enums.FileTransfer;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.TCP;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Timer;
using PF.Core.Interfaces.TowerLight;
using PF.Core.Models;
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
using System.IO;
using System.Windows;

namespace PF.Application.Shell
{
    /// <summary>AutoOCR 演示工站应用程序入口，继承 PFApplicationBase 并实现全部项目钩子。</summary>
    public partial class App : PFApplicationBase
    {
        /// <summary>AutoOCR 应用全局互斥体唯一标识。</summary>
        protected override string AppMutexId => "OCRAppID-12345678-ABCD-EFGH-IJKL-1234567890AB";

        /// <summary>返回 AutoOCR 项目默认参数集。</summary>
        protected override IDefaultParam CreateDefaultParameters() => new DefaultParameters();

        /// <summary>注册项目主窗口 ViewModel 实现类 MainWindowViewModel。</summary>
        protected override void RegisterMainWindowViewModel(IContainerRegistry containerRegistry)
            => containerRegistry.RegisterSingleton<MainWindowViewModelBase, MainWindowViewModel>();

        #region 通讯工厂

        /// <summary>
        /// 注册通讯实例工厂（TCP Server/Client、FileTransfer 通道）。
        /// 硬件工厂如需引用某个通讯实例，在 RegisterHardwareFactories 的闭包里通过
        /// hwManager 捕获的 ICommunicationManagerService.GetCommunication&lt;T&gt;(InstanceId) 查找即可，
        /// 此方法固定在硬件工厂注册之前执行，届时实例还未 StartAsync，但已可被引用。
        /// </summary>
        protected override void RegisterCommunicationFactories(ICommunicationManagerService commManager)
        {
            // 通讯层此前只有 CommunicationManagerService 自身记日志（注册/启动/reload 等编排事件），
            // 底层收发实现（TcpServer/TCPClient/SerialPortCommunication/FileTransferChannel/Modbus 两个
            // Master）完全没有落盘日志，只靠事件——没人订阅时运行期的断线/超时/CRC 失败在日志文件里
            // 找不到任何痕迹。统一在此解析一次 ILogService 传给全部工厂。
            var logger = Container.Resolve<ILogService>();

            commManager.RegisterFactory("TcpServer", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int port = cfg.ConnectionParameters.TryGetValue("Port", out var p) ? int.Parse(p) : 0;
                int backlog = cfg.ConnectionParameters.TryGetValue("Backlog", out var bl) ? int.Parse(bl) : 10;
                return new PF.Infrastructure.Communication.TCP.TcpServer(cfg.DisplayName, cfg.InstanceId, logger)
                {
                    BindIp = ip ?? "0.0.0.0",
                    BindPort = port,
                    Backlog = backlog
                };
            });

            commManager.RegisterFactory("TcpClient", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("ServerIp", out var serverIp);
                int serverPort = cfg.ConnectionParameters.TryGetValue("ServerPort", out var sp) ? int.Parse(sp) : 0;
                return new PF.Infrastructure.Communication.TCP.TCPClient(cfg.DisplayName, cfg.InstanceId, logger)
                {
                    TargetServerIp = serverIp ?? string.Empty,
                    TargetServerPort = serverPort
                };
            });

            commManager.RegisterFactory("FileTransferChannel", cfg =>
            {
                var roleStr = cfg.ConnectionParameters.GetValueOrDefault("Role", nameof(FileTransferRole.Server));
                var role = Enum.Parse<FileTransferRole>(roleStr);
                var linksJson = cfg.ConnectionParameters.GetValueOrDefault("LinksJson", "[]");
                var links = System.Text.Json.JsonSerializer.Deserialize<List<FileTransferLinkEndpoint>>(linksJson) ?? new();

                // ReceiveDirectory / InMemoryReceiveThresholdMb 为可选配置键：
                // 缺省或非法时沿用 FileTransferOptions 的默认值（借一个临时实例取默认值，避免在此处复写默认字面量）
                var optionDefaults = new FileTransferOptions { Role = role, Links = links };
                var receiveDirectory = cfg.ConnectionParameters.GetValueOrDefault("ReceiveDirectory");
                var thresholdBytes = optionDefaults.InMemoryReceiveThresholdBytes;
                if (cfg.ConnectionParameters.TryGetValue("InMemoryReceiveThresholdMb", out var thresholdStr)
                    && int.TryParse(thresholdStr, out var thresholdMb) && thresholdMb > 0)
                {
                    thresholdBytes = thresholdMb * 1024L * 1024;
                }

                var options = new FileTransferOptions
                {
                    Role = role,
                    Links = links,
                    ReceiveDirectory = string.IsNullOrWhiteSpace(receiveDirectory) ? optionDefaults.ReceiveDirectory : receiveDirectory,
                    InMemoryReceiveThresholdBytes = thresholdBytes
                };
                return new PF.Infrastructure.Communication.FileTransfer.FileTransferChannel(options, cfg.InstanceId, logger);
            });

            commManager.RegisterFactory("ModbusRtuMaster", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("PortName", out var portName);
                int baudRate = cfg.ConnectionParameters.TryGetValue("BaudRate", out var br) && int.TryParse(br, out var brv) ? brv : 9600;
                // 可选串口帧参数（缺省 N81）：Parity=None/Odd/Even/Mark/Space，DataBits=5~8，StopBits=One/Two/OnePointFive
                var parity = cfg.ConnectionParameters.TryGetValue("Parity", out var pa)
                    && Enum.TryParse<System.IO.Ports.Parity>(pa, true, out var pav) ? pav : System.IO.Ports.Parity.None;
                int dataBits = cfg.ConnectionParameters.TryGetValue("DataBits", out var db) && int.TryParse(db, out var dbv) ? dbv : 8;
                var stopBits = cfg.ConnectionParameters.TryGetValue("StopBits", out var sb)
                    && Enum.TryParse<System.IO.Ports.StopBits>(sb, true, out var sbv) ? sbv : System.IO.Ports.StopBits.One;
                var master = new PF.Infrastructure.Communication.Modbus.ModbusRtuMaster(
                    portName ?? string.Empty, baudRate, cfg.InstanceId, parity, dataBits, stopBits, logger);
                var (timeoutMs, autoReconnect, reconnectIntervalMs) = ParseModbusCommonOptions(cfg.ConnectionParameters);
                if (timeoutMs is int t1) master.TimeoutMs = t1;
                if (autoReconnect is bool a1) master.AutoReconnect = a1;
                if (reconnectIntervalMs is int r1) master.ReconnectIntervalMs = r1;
                return master;
            });

            commManager.RegisterFactory("ModbusTcpMaster", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int port = cfg.ConnectionParameters.TryGetValue("Port", out var p) && int.TryParse(p, out var pv) ? pv : 502;
                var master = new PF.Infrastructure.Communication.Modbus.ModbusTcpMaster(
                    ip ?? string.Empty, port, cfg.InstanceId, logger);
                var (timeoutMs, autoReconnect, reconnectIntervalMs) = ParseModbusCommonOptions(cfg.ConnectionParameters);
                if (timeoutMs is int t2) master.TimeoutMs = t2;
                if (autoReconnect is bool a2) master.AutoReconnect = a2;
                if (reconnectIntervalMs is int r2) master.ReconnectIntervalMs = r2;
                return master;
            });
        }

        /// <summary>
        /// 解析 Modbus 主站的可选公共配置键（TimeoutMs / AutoReconnect / ReconnectIntervalMs）。
        /// 缺省或解析失败返回 null，保留实现内的默认值（1000ms / true / 5000ms）。
        /// </summary>
        private static (int? TimeoutMs, bool? AutoReconnect, int? ReconnectIntervalMs) ParseModbusCommonOptions(
            Dictionary<string, string> parameters)
        {
            int? timeout = parameters.TryGetValue("TimeoutMs", out var to) && int.TryParse(to, out var tov) && tov > 0 ? tov : null;
            bool? autoReconnect = parameters.TryGetValue("AutoReconnect", out var ar) && bool.TryParse(ar, out var arv) ? arv : null;
            int? interval = parameters.TryGetValue("ReconnectIntervalMs", out var ri) && int.TryParse(ri, out var riv) && riv > 0 ? riv : null;
            return (timeout, autoReconnect, interval);
        }

        #endregion

        #region 硬件工厂

        /// <summary>注册 6 种硬件工厂（运动控制卡、轴、IO、条码枪、OCR 相机、三色灯）。</summary>
        protected override void RegisterHardwareFactories(IHardwareManagerService hwManager, ICommunicationManagerService commManager)
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
                int timeout = cfg.ConnectionParameters.TryGetValue("TimeOutMs", out var to) ? int.Parse(to) : 0;

                // 底层两条 TCP 通道由通讯管理服务按配置创建（AutoStart=false，实际连接时机仍由本设备的
                // InternalConnectAsync 驱动，避免和通讯管理器抢先连接冲突）；IP/端口从注入的 IClient 自身读取，
                // 不再由 HardwareConfig 重复提供
                var triggerClient = commManager.GetCommunication<IClient>(cfg.ConnectionParameters["TriggerCommInstanceId"]);
                var userPowerClient = commManager.GetCommunication<IClient>(cfg.ConnectionParameters["UserPowerCommInstanceId"]);

                // HKBarcodeScan（TCP 透传协议版）已弃用，新开发请用 MvCodeReaderBarcodeScan；
                // 此处保留工厂注册仅为兼容现网已部署的 "HKBarcodeScan" 配置，故显式抑制 Obsolete 警告。
#pragma warning disable CS0618
                return new Infrastructure.Hardware.BarcodeScan.HKRobot.HKBarcodeScan(
                    triggerClient, userPowerClient, timeout, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
#pragma warning restore CS0618
            });

            hwManager.RegisterFactory("MvCodeReaderBarcodeScan", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int timeouts = cfg.ConnectionParameters.TryGetValue("TimeOutMs", out var timeout) ? int.Parse(timeout) : 0;
                return new Infrastructure.Hardware.BarcodeScan.Hikvision.MvCodeReaderBarcodeScan(ip, timeouts, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });

            hwManager.RegisterFactory("KeyenceBarcodeScan", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int timeouts = cfg.ConnectionParameters.TryGetValue("TimeOutMs", out var timeout) ? int.Parse(timeout) : 0;
                return new Infrastructure.Hardware.BarcodeScan.Keyence.KeyenceBarcodeScan(ip, timeouts, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
            });

            hwManager.RegisterFactory("KeyenceIntelligentCamera", cfg =>
            {
                int timeout = cfg.ConnectionParameters.TryGetValue("TimeOutms", out var to) ? int.Parse(to) : 0;

                var triggerClient = commManager.GetCommunication<IClient>(cfg.ConnectionParameters["CommInstanceId"]);

                return new Infrastructure.Hardware.Camera.IntelligentCamera.Keyence.KeyenceIntelligentCamera(
                    triggerClient, timeout, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, LogService);
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

        /// <summary>注册 IO 映射枚举（输入 E_InPutName、输出 E_OutPutName）。</summary>
        protected override void RegisterIOMappings(IIOMappingService ioMappingService)
        {
            ioMappingService.RegisterInputEnum<E_InPutName>("IO_Collectorll");
            ioMappingService.RegisterOutputEnum<E_OutPutName>("IO_Collectorll");
        }

        #endregion

        #region 机构、工站、主控注册

        /// <summary>注册 7 个机构、5 个工站及 AutoOCR 主控（DryIoc 多键 Singleton）。</summary>
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

        /// <summary>注册 OCR 配方服务（IRecipeService&lt;OCRRecipeParam&gt;）。</summary>
        protected override void RegisterRecipes(IContainerRegistry containerRegistry)
        {
            containerRegistry.GetContainer().RegisterMany(
                [typeof(IRecipeService<OCRRecipeParam>), typeof(OCRRecipe<OCRRecipeParam>)],
                typeof(OCRRecipe<OCRRecipeParam>),
                reuse: DryIoc.Reuse.Singleton);
        }

        #endregion

        #region 机构初始化序列

        /// <summary>按顺序初始化 7 个机构（任一失败立即返回 false）。</summary>
        protected override async Task<bool> InitializeMechanismsAsync(IProgress<SplashProgressPayload>? progress = null)
        {
            var c = this.Container;

            // 机构名称与显示名对应，便于进度反馈
            var mechanismNames = new[] { nameof(WS1FeedingModel), nameof(WS1MaterialPullingModule), nameof(WS2FeedingModel), nameof(WS2MaterialPullingModule), nameof(WSDetectionModule), nameof(WSDataModule), nameof(WSSecsGemModule) };

            for (int i = 0; i < mechanismNames.Length; i++)
            {
                var name = mechanismNames[i];
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"正在初始化机构 ({i + 1}/{mechanismNames.Length}): {name}",
                    MsgType = MsgType.Info
                });
             

                try
                {
                    var mechanism = c.Resolve<IMechanism>(name);
                    bool initialized = await mechanism.InitializeAsync();
                    if (!initialized)
                    {
                        progress?.Report(new SplashProgressPayload
                        {
                            Status = $"机构初始化失败: {name} 返回 false",
                            MsgType = MsgType.Error
                        });
                      
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report(new SplashProgressPayload
                    {
                        Status = $"机构初始化异常: {name} - {ex.Message}",
                        MsgType = MsgType.Error
                    });
                    
                    return false;
                }

                progress?.Report(new SplashProgressPayload
                {
                    Status = $"机构初始化成功: {name}",
                    MsgType = MsgType.Success
                });
             
            }

            progress?.Report(new SplashProgressPayload
            {
                Status = "所有机构初始化完成",
                MsgType = MsgType.Success
            });
            await Task.Delay(300);
            return true;
        }

        #endregion

        #region 模块目录

        /// <summary>注册 9 个 Prism 模块（Identity、Alarm、Logging、Parameter、Debug、Production、SecsGem、AutoOcrUI、Halcon）。</summary>
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

        /// <summary>注册 Halcon 视觉引擎服务（AddVisionServices 扩展，含三模式引擎管理器）。</summary>
        protected override void RegisterVisionServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.AddVisionServices(
                procedureDirectory: ConstGlobalParam.VisionProceduresPath,
                pipelineDirectory: ConstGlobalParam.VisionWorkflowsPath);
        }

        #endregion

        #region 每日定时任务

        /// <summary>注册每日 08:00 执行的磁盘预警和 OCR 图片清理定时任务。</summary>
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
