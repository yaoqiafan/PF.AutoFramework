using DryIoc.ImTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NPOI.POIFS.Storage;
using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.ViewModels;
using PF.Application.Shell.Views;
using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Production;
using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Data;
using PF.Data.Context;
using PF.Data.Entity.Category;
using PF.Data.Repositories;
using PF.Infrastructure.SecsGem;
using PF.Infrastructure.SecsGem.Command;
using PF.Infrastructure.SecsGem.Incentive;
using PF.Infrastructure.SecsGem.Param;
using PF.Infrastructure.SecsGem.Tools;
using PF.Infrastructure.Station;
using PF.Infrastructure.Station.Basic;
using PF.Modules.Debug;
using PF.Modules.Identity;
using PF.Modules.Logging;
using PF.Modules.Parameter;
using PF.Modules.Parameter.Dialog.Base;
using PF.Modules.Parameter.Dialog.Mappers;
using PF.Modules.Parameter.Dialog.Mappers.Hardware;
using PF.Modules.Parameter.ViewModels.Models;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.Modules.Production;
using PF.Modules.SecsGem;
using PF.SecsGem.DataBase;
using PF.Services.Alarm;
using PF.Services.Hardware;
using PF.Services.Identity;
using PF.Services.Logging;
using PF.Services.Params;
using PF.Services.Production;
using PF.Services.Sync;
using PF.Application.Shell.Services;
using PF.Core.Interfaces.Alarm;
using PF.UI.Infrastructure.Dialog;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.Dialog.ViewModels;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Resources;
using PF.Core.Enums;
using PF.Core.Models;
using PF.UI.Shared.Tools;
using PF.UI.Shared.Tools.Helper;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.Recipe;
using PF.WorkStation.AutoOcr.Stations;
using Prism.Ioc;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using PF.UI.Shared.Data;

namespace PF.Application.Shell
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        #region 私有字段

        // 定义全局唯一的互斥体名称（建议使用公司/程序唯一标识，避免冲突）
        private static readonly string MutexName = "Global\\PFAutoFrameworkOCRAppID-12345678-ABCD-EFGH-IJKL-1234567890AB";
        private static Mutex _appMutex = null;
        private static bool IsNewInstance;

        private ILogService _logService;
        private HostApplicationBuilder? builder;

        #endregion

        #region 程序启动与自检

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                if (RunningInstance())
                {
                    base.OnStartup(e);
                    try
                    {
                        this.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);
                    }
                    catch (Exception ex)
                    {
                        var strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now + "\r\n";
                        var str = string.Format(strDateInfo + "异常类型：{0}\r\n异常消息：{1}\r\n异常信息：{2}\r\n",
                            ex.GetType().Name, ex.Message, ex.StackTrace);

                        IMessageService messageService = Container.Resolve<IMessageService>();
                        messageService.ShowMessage("发生错误，请查看程序日志！" + Environment.NewLine + str, "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("当前应用程序已经在运行！", "警告", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    this.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"XamlParseException: {ex.Message}");
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            if (IsNewInstance)
            {
                // 释放互斥体（必须释放，否则下次启动会误判）
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
            }
        }




        static bool RunningInstance()
        {

            try
            {
                // 尝试创建/获取互斥体的独占权
                // isNewInstance 为 true 表示是新实例（程序未运行），false 表示已存在实例
                _appMutex = new Mutex(true, MutexName, out IsNewInstance);
            }
            catch (UnauthorizedAccessException)
            {
                // 无权限访问全局互斥体（如非管理员运行），降级为本地互斥体
                _appMutex = new Mutex(true, MutexName.Replace("Global\\", ""), out IsNewInstance);
            }

            if (IsNewInstance)
                return true;
            else
                return false;

        }




        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string str;
            var error = e.Exception;
            if (error != null)
            {
                str = string.Format("出现应用程序未处理的异常----->" + "Application UnhandledException:{0};\n\r堆栈信息:{1}", error.Message, error.StackTrace);
            }
            else
            {
                str = string.Format("Application UnhandledError:{0}", e);
            }
            IMessageService messageService = Container.Resolve<IMessageService>();
            messageService.ShowMessage("发生错误，请查看程序日志！" + Environment.NewLine + str, "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        #endregion

        #region Prism 框架核心

        protected override Window CreateShell()
        {
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var commonparam = Container.Resolve<CommonSettings>();

            ApplyConfiguration(commonparam.Skin);

            Splash splash = Container.Resolve<Splash>();

            var name = commonparam.SoftWareName;
            splash.WelcomeText = $"欢迎使用{name}";

            var nameEN = commonparam.SoftWareName_EN;
            splash.WelcomeText_small = $"Welcome to the {nameEN}";

            Assembly assembly = Assembly.GetEntryAssembly();
            splash.VersionNumber = $"V{assembly.GetName().Version}";
            splash.LoadingAction = PerformInitializationAsync;

            if (splash.ShowDialog() == true)
            {
                splash.Close();
                Container.Resolve<IHardwareInputMonitor>().StartStandardMonitoring();
            }
            else
            {
                IMessageService messageService = Container.Resolve<IMessageService>();
                var res = messageService.ShowMessageAsync("软件加载失败,是否退出系统?", "系统错误", MessageBoxButton.YesNo, MessageBoxImage.Error).GetAwaiter().GetResult();
                if (res == ButtonResult.Yes)
                {
                    splash.Close();
                    Environment.Exit(0);
                }
            }

            return Container.Resolve<MainWindow>();
        }

        protected override void OnInitialized()
        {
            // 解析导航服务并扫描当前程序集自动注册菜单
            var navMenuService = Container.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            var authService = Container.Resolve<IUserService>();
            // 用所有已注册菜单的 Title 初始化 PermissionHelper 的动态中文名称映射
            PermissionHelper.Initialize(Container.Resolve<INavigationMenuService>());
            // 使用默认的超级管理员账号进行静默登录
            authService.LoginAsync("SuperUser", DateTime.Now.ToString("yyyyMMddHH00")).GetAwaiter().GetResult();

            // ── 软硬联动：将 Prism EA 硬件复位事件路由到主控 ─────────────────────────
            // BaseMasterController 不依赖 Prism，通过 RegisterHardwareResetHandler 委托桥接，
            // 保持 PF.Infrastructure 对 Prism 的零依赖。
            var controller = Container.Resolve<IMasterController>();
            var ea         = Container.Resolve<IEventAggregator>();
            ea.GetEvent<HardwareResetRequestedEvent>()
              .Subscribe(
                  req => (controller as BaseMasterController)?.OnHardwareResetRequested(req),
                  ThreadOption.BackgroundThread,
                  keepSubscriberReferenceAlive: true);

            // ── 系统复位：AlarmCenterView 中"系统复位"按钮 → SystemResetRequestedEvent → 主控 ──
            // Shell 作为 Prism 与 Infrastructure 之间的桥接层，保持 PF.Infrastructure 对 Prism 零依赖。
            ea.GetEvent<SystemResetRequestedEvent>()
              .Subscribe(
                  () => _ = controller.RequestSystemResetAsync(),
                  ThreadOption.BackgroundThread,
                  keepSubscriberReferenceAlive: true);

            base.OnInitialized();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            CommonSettings commonSettings = CommonSettings.Load();
            if (!File.Exists(CommonSettings.ConfigFilePath))
            {
                commonSettings.Save();
            }

            containerRegistry.RegisterInstance<CommonSettings>(commonSettings);
            containerRegistry.RegisterForNavigation<CommonParamView, BaseParamsViewModel>(NavigationConstants.Views.CommonParamView);


            // 日志服务（配置和注册逻辑委托到 LoggingServiceExtensions）
            containerRegistry.AddLogging();
            _logService = containerRegistry.GetContainer().Resolve<ILogService>();

            // 参数数据库（AppParamDbContext 是应用层专属，保留在此）
            RegisterParamDbContext(containerRegistry);
            // 参数仓储和服务（委托到 ParameterServiceExtensions）
            containerRegistry.AddParameterServices(new DefaultParameters());

            RegisterProductionDataService(containerRegistry);

            RegisterSecsGemSever(containerRegistry);

            RegisterHardwareTypes(containerRegistry);

            containerRegistry.RegisterSingleton<Splash>();
            containerRegistry.RegisterDialogWindow<PFDialogBaseWindow>();
            containerRegistry.RegisterSingleton<INavigationMenuService, NavigationMenuService>();


            RegisterUserIdentityTypes(containerRegistry);

            ViewFactory.PreloadAssemblies();
            ViewFactory.RegisterCustomType<UserInfo, UserParamView, UserParamViewMapper>();

            // 注册硬件配置参数视图（按 ImplementationClassName 路由）
            ViewFactory.RegisterHardwareConfigType<LTDMCMotionCardParamView,          LTDMCMotionCardParamViewMapper>         ("LTDMCMotionCard");
            ViewFactory.RegisterHardwareConfigType<EtherCatAxisParamView,             EtherCatAxisParamViewMapper>            ("EtherCatAxis");
            ViewFactory.RegisterHardwareConfigType<EtherCatIOParamView,               EtherCatIOParamViewMapper>              ("EtherCatIO");
            ViewFactory.RegisterHardwareConfigType<HKBarcodeScanParamView,            HKBarcodeScanParamViewMapper>           ("HKBarcodeScan");
            ViewFactory.RegisterHardwareConfigType<KeyenceIntelligentCameraParamView, KeyenceIntelligentCameraParamViewMapper>("KeyenceIntelligentCamera");
            ViewFactory.RegisterHardwareConfigType<CTSLightControllerParamView,       CTSLightControllerParamViewMapper>      ("CTS_LightControoller");

            containerRegistry.RegisterDialog<MessageDialogView, MessageDialogViewModel>("MessageDialog");
            containerRegistry.RegisterDialog<InputDialogView, InputDialogViewModel>("InputDialog");
            containerRegistry.RegisterDialog<WaitDialogView, WaitDialogViewModel>("WaitDialog");
            containerRegistry.RegisterSingleton<IMessageService, MessageService>();

            RegisterHardwareAndMechanisms(containerRegistry);

            RegisterRecipeRelated(containerRegistry);

            // 报警模块：独立数据库，字典 + 业务服务
            RegisterAlarmServices(containerRegistry);
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);
            moduleCatalog.AddModule<PF.Modules.Alarm.AlarmModule>();
            moduleCatalog.AddModule<LoggingModule>();
            moduleCatalog.AddModule<ParameterModule>();
            moduleCatalog.AddModule<IdentityModule>();
            moduleCatalog.AddModule<DebugModule>();
            //moduleCatalog.AddModule<UIModule>();
            moduleCatalog.AddModule<PF.WorkStation.AutoOcr.UI.AutoOcrUIModule>();
            moduleCatalog.AddModule<SecsGemModule>();
            moduleCatalog.AddModule<ProductionRecordModule>();
        }

        #endregion

        #region 日志服务注册
        // 日志服务的配置和注册逻辑已迁移到 PF.Services.Logging.LoggingServiceExtensions。
        // 调用入口：RegisterTypes 中的 containerRegistry.AddLogging()
        #endregion

        #region 参数数据库服务注册

        /// <summary>
        /// 注册应用专属的参数数据库上下文和开放泛型仓储（使用 DryIoc 专属 API）。
        /// IParamService、IDefaultParam、CommonSettings 的注册委托到 ParameterServiceExtensions.AddParameterServices。
        /// </summary>
        private async void RegisterParamDbContext(IContainerRegistry containerRegistry)
        {
            try
            {
                var container = containerRegistry.GetContainer();
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SystemParamsCollection.db");

                DbContextFactory<AppParamDbContext>.Initialize($"Data Source={filePath}");

                using var dbContext = DbContextFactory<AppParamDbContext>.CreateDbContext();
                await dbContext.Database.EnsureCreatedAsync();
                await dbContext.EnsureDefaultParametersCreatedAsync(new DefaultParameters());

                var dbContextOptions = DbContextFactory<AppParamDbContext>.CreateDbContextOptions();
                container.RegisterInstance(dbContextOptions);

                container.Register<Microsoft.EntityFrameworkCore.DbContext, AppParamDbContext>(
                    made: Made.Of(() => new AppParamDbContext(
                        Arg.Of<Microsoft.EntityFrameworkCore.DbContextOptions<AppParamDbContext>>())),
                    reuse: Reuse.Scoped);

                // 开放泛型仓储（DryIoc 专属 Setup/Reuse API，须在此处注册）
                container.Register(typeof(IParamRepository<>), typeof(ParamRepository<>),
                    setup: Setup.With(condition: r => r.ServiceType.IsGenericType),
                    reuse: Reuse.ScopedOrSingleton);

                _logService.Info("参数数据库上下文注册完成", "DependencyInjection");
            }
            catch (Exception ex)
            {
                _logService.Error("参数数据库上下文注册失败", exception: ex);
                throw;
            }
        }

        #endregion

        #region 生产数据服务注册

        /// <summary>
        /// 注册生产数据服务（独立数据库，支持多后端）。
        /// 切换数据库只需修改此处的 DbContextOptionsBuilder，其余代码零改动。
        /// </summary>
        private void RegisterProductionDataService(IContainerRegistry containerRegistry)
        {
            try
            {
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "ProductionHistory.db");
                var options = new DbContextOptionsBuilder<ProductionDbContext>()
                    .UseSqlite($"Data Source={filePath}")
                    .Options;

                containerRegistry.RegisterInstance<DbContextOptions<ProductionDbContext>>(options);
                containerRegistry.RegisterSingleton<IProductionDataService, ProductionDataService>();

                _logService.Info("生产数据服务注册完成", "DependencyInjection");
            }
            catch (Exception ex)
            {
                _logService.Error("生产数据服务注册失败", exception: ex);
                throw;
            }
        }

        #endregion

        #region 用户身份服务注册

        private void RegisterUserIdentityTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IUserService, UserService>();
        }

        #endregion

        #region 参数数据库服务注册

        /// <summary>
        /// 注册应用专属的参数数据库上下文和开放泛型仓储（使用 DryIoc 专属 API）。
        /// IParamService、IDefaultParam、CommonSettings 的注册委托到 ParameterServiceExtensions.AddParameterServices。
        /// </summary>
        private async void RegisterSecsGemSever(IContainerRegistry containerRegistry)
        {
            try
            {
                var filePath = System.IO.Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");

                var dbContextOptions = new DbContextOptionsBuilder<SecsGemDbContext>()
                    .UseSqlite($"Data Source={filePath}")
                    .Options;
                containerRegistry.RegisterInstance<DbContextOptions<SecsGemDbContext>>(dbContextOptions);
                containerRegistry.RegisterSingleton<SecsGemDbContext>();

                containerRegistry.RegisterSingleton<ISecsGemDataBase, SecsGemDataBaseManger>();
                containerRegistry.RegisterSingleton<ICommandManager, SecsGemCommandManger>();
                containerRegistry.RegisterSingleton<SecsGemMessageProcessor>();
                containerRegistry.RegisterSingleton<IParams, ParamsManger>();
                containerRegistry.RegisterSingleton<IinternalClient, InternalClient>();
                containerRegistry.RegisterSingleton<ISecsGemMessageUpdater, SecsGemMessageUpdater>();
                containerRegistry.RegisterSingleton<ISecsGemManger, SecsGemManger>();
            }
            catch (Exception ex)
            {
                _logService.Error("SecsGem据库上下文注册失败", exception: ex);
                throw;
            }
        }

        #endregion

        #region 硬件服务注册

        private void RegisterHardwareTypes(IContainerRegistry containerRegistry)
        {
            // dataDirectory 仍用于 SimXAxis 点表文件存储（与硬件配置存储无关）
            var dataDirectory = ConstGlobalParam.ConfigPath;

            var container = containerRegistry.GetContainer();
            var paramService = container.Resolve<IParamService>();
            paramService.RegisterParamType<HardwareParam, HardwareConfig>();

            var hwManager = new HardwareManagerService(_logService, paramService);

            hwManager.RegisterFactory("LTDMCMotionCard", cfg =>
            {
                int cardIndex = cfg.ConnectionParameters.TryGetValue("CardIndex", out var ci)
                    ? int.Parse(ci) : 0;
                return new PF.Infrastructure.Hardware.Card.LTDMC.LTMDCMotionCard(cardIndex, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, _logService);
            });

            hwManager.RegisterFactory("EtherCatAxis", cfg =>
            {
                int axisIndex = cfg.ConnectionParameters.TryGetValue("AxisIndex", out var idx)
                    ? int.Parse(idx) : 0;
                string axisparamstr = cfg.ConnectionParameters.TryGetValue("AxisParam", out var axispa) ? axispa : System.Text.Json.JsonSerializer.Serialize(new AxisParam());
                var  axisparam = System.Text.Json.JsonSerializer.Deserialize<AxisParam>(axisparamstr);
                if (axisparam == null)
                {
                    axisparam = new AxisParam();
                }
                return new Infrastructure.Hardware.Motor.EtherCatAxis(cfg.DeviceId, axisIndex, axisparam, cfg.DeviceName, cfg.IsSimulated, _logService, dataDirectory);
            });


            hwManager.RegisterFactory("EtherCatIO", cfg =>
            {
                int incount = cfg.ConnectionParameters.TryGetValue("InPutCount", out var tig) ? int.Parse(tig) : 0;
                int outcount = cfg.ConnectionParameters.TryGetValue("OutPutCount", out var us) ? int.Parse(us) : 0;
                return new Infrastructure.Hardware.IO.EtherCatIO(incount, outcount, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, _logService);
            });

            hwManager.RegisterFactory("HKBarcodeScan", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int tiggerport = cfg.ConnectionParameters.TryGetValue("TiggerPort", out var tig) ? int.Parse(tig) : 0;
                int userport = cfg.ConnectionParameters.TryGetValue("UserPort", out var us) ? int.Parse(us) : 0;
                int timeouts = cfg.ConnectionParameters.TryGetValue("TimeOutMs", out var timeout) ? int.Parse(timeout) : 0;
                return new Infrastructure.Hardware.BarcodeScan.HKRobot.HKBarcodeScan(ip, tiggerport, userport, timeouts, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, _logService);
            });

            hwManager.RegisterFactory("KeyenceIntelligentCamera", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("IP", out var ip);
                int tiggerport = cfg.ConnectionParameters.TryGetValue("TiggerPort", out var tig) ? int.Parse(tig) : 0;
                int timeouts = cfg.ConnectionParameters.TryGetValue("TimeOutms", out var timeout) ? int.Parse(timeout) : 0;
                return new Infrastructure.Hardware.Carame.IntelligentCamera.Keyence.KeyenceIntelligentCamera(ip, tiggerport, timeouts, cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, _logService);
            });

            hwManager.RegisterFactory("CTS_LightControoller", cfg =>
            {
                cfg.ConnectionParameters.TryGetValue("COM", out var COM);
               
                return new Infrastructure.Hardware.LightController .CTS.CTSLightController (COM ,  cfg.DeviceId, cfg.DeviceName, cfg.IsSimulated, _logService);
            });



            containerRegistry.RegisterSingleton<IIOMappingService, IOMappingService>();

            // 注册 AutoOcr 工站的 IO 映射
            var ioMappingService = container.Resolve<IIOMappingService>();
            ioMappingService.RegisterInputEnum<PF.Workstation.AutoOcr.CostParam.E_InPutName>("IO_Collectorll");
            ioMappingService.RegisterOutputEnum<PF.Workstation.AutoOcr.CostParam.E_OutPutName>("IO_Collectorll");

            containerRegistry.RegisterInstance<IHardwareManagerService>(hwManager);
        }

        private void RegisterHardwareAndMechanisms(IContainerRegistry containerRegistry)
        {
            // 硬件输入事件总线（取代 PhysicalButtonEventBus）
            containerRegistry.RegisterSingleton<HardwareInputEventBus>();

            // 硬件输入面板配置（AutoOcr 工站实现）
            containerRegistry.RegisterSingleton<IPanelIoConfig, PF.WorkStation.AutoOcr.CostParam.PanelIoConfig>();

            // 硬件输入监控服务（双线程分组扫描）
            containerRegistry.RegisterSingleton<IHardwareInputMonitor, HardwareInputMonitor>();

            // 工站同步服务
            containerRegistry.RegisterSingleton<IStationSyncService, StationSyncService>();

            // 机构层：GantryMechanism 同时映射到自身类型和 IMechanism 接口
            var container = containerRegistry.GetContainer();


            container.RegisterMany(
    new[] { typeof(WorkStation1FeedingModule), typeof(IMechanism) },
    typeof(WorkStation1FeedingModule),
    reuse: DryIoc.Reuse.Singleton,
    serviceKey: nameof(WorkStation1FeedingModule));


            container.RegisterMany(
               new[] { typeof(WorkStationDetectionModule), typeof(IMechanism) },
               typeof(WorkStationDetectionModule),
               reuse: DryIoc.Reuse.Singleton,
               serviceKey: nameof(WorkStationDetectionModule));


            container.RegisterMany(
               new[] { typeof(WorkStation1MaterialPullingModule), typeof(IMechanism) },
               typeof(WorkStation1MaterialPullingModule),
               reuse: DryIoc.Reuse.Singleton,
               serviceKey: nameof(WorkStation1MaterialPullingModule));


            container.RegisterMany(
               new[] { typeof(WorkStationDataModule), typeof(IMechanism) },
               typeof(WorkStationDataModule),
               reuse: DryIoc.Reuse.Singleton,
               serviceKey: nameof(WorkStationDataModule));


            container.RegisterMany(
               new[] { typeof(WorkStationSecsGemModule), typeof(IMechanism) },
               typeof(WorkStationSecsGemModule),
               reuse: DryIoc.Reuse.Singleton,
               serviceKey: nameof(WorkStationSecsGemModule));

            // 工站层
            container.RegisterMany(
                new[] { typeof(WorkStation1FeedingStation<StationMemoryBaseParam>), typeof(StationBase<StationMemoryBaseParam>) },
                typeof(WorkStation1FeedingStation<StationMemoryBaseParam>),
                reuse: DryIoc.Reuse.Singleton,
               serviceKey: nameof(WorkStation1FeedingStation<StationMemoryBaseParam>)
                );


            container.RegisterMany(
                new[] { typeof(WorkStationDetectionStation<StationMemoryBaseParam>), typeof(StationBase<StationMemoryBaseParam>) },
                typeof(WorkStationDetectionStation<StationMemoryBaseParam>),
                reuse: DryIoc.Reuse.Singleton,
               serviceKey: nameof(WorkStationDetectionStation<StationMemoryBaseParam>)
                );

            container.RegisterMany(
               new[] { typeof(WorkStation1MaterialPullingStation<StationMemoryBaseParam>), typeof(StationBase<StationMemoryBaseParam>) },
               typeof(WorkStation1MaterialPullingStation<StationMemoryBaseParam>),
               reuse: DryIoc.Reuse.Singleton);

            // 主控调度器
            containerRegistry.RegisterSingleton<IMasterController, AutoOCRMachineController>();
        }

        #endregion

        #region 配方服务注册

        private void RegisterRecipeRelated(IContainerRegistry containerRegistry)
        {
            var container = containerRegistry.GetContainer();

            // 将 OCRRecipe 同时映射到 IRecipeService、IRecipeManger 接口及其自身类型，确保全局共享同一个配方字典实例
            container.RegisterMany(
                new[]
                {
                    typeof(IRecipeService<OCRRecipeParam>),
                    typeof(OCRRecipe<OCRRecipeParam>)
                },
                typeof(OCRRecipe<OCRRecipeParam>),
                reuse: DryIoc.Reuse.Singleton);

            // 后续如有其他工站类型的配方（如 T 为 DispensingRecipeParam），可在此处继续沿用该模式注册
        }

        #endregion

        #region 报警服务注册

        /// <summary>
        /// 注册报警字典服务和业务服务，使用独立的 AlarmHistory.db。
        /// 表名按年份动态分表（AlarmRecord_YYYY），跨年自动建表。
        /// </summary>
        private void RegisterAlarmServices(IContainerRegistry containerRegistry)
        {
            try
            {
                // IAlarmEventPublisher 必须在 AlarmService 解析前注册（AlarmService 构造函数可选注入）
                containerRegistry.RegisterSingleton<IAlarmEventPublisher, PrismAlarmEventPublisher>();

                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "AlarmHistory.db");
                containerRegistry.AddAlarmServices(filePath);
                _logService.Info("报警服务注册完成", "DependencyInjection");
            }
            catch (Exception ex)
            {
                _logService.Error("报警服务注册失败", exception: ex);
                throw;
            }
        }

        #endregion

        #region 启动初始化流程（Splash）

        private async Task<bool> PerformInitializationAsync()
        {
            bool loadErr = false;

            Splash splash = Container.Resolve<Splash>();
            ILogService logService = Container.Resolve<ILogService>();

            SplashUpdateMessage(splash, logService, "程序加载中。。。", msgType: MsgType.Info);
            try
            {
                await Task.Delay(500);
                SplashUpdateMessage(splash, logService, "配置文件加载中。。。", msgType: MsgType.Info);
                var configLoaded = await LoadConfigurationAsync();
                if (!configLoaded)
                {
                    SplashUpdateMessage(splash, logService, "配置文件加载失败", msgType: MsgType.Error);
                    loadErr = true;
                    return false;
                }
                SplashUpdateMessage(splash, logService, "配置文件加载成功。。。", msgType: MsgType.Success);
                await Task.Delay(500);

                SplashUpdateMessage(splash, logService, "硬件设备初始化中。。。", msgType: MsgType.Info);
                await Task.Delay(500);
                var hwManager = Container.Resolve<IHardwareManagerService>();
                var hwProgress = new Progress<SplashProgressPayload>(payload =>
                    SplashUpdateMessage(splash, logService, payload.Status, payload.Category, payload.MsgType));
                await hwManager.LoadAndInitializeAsync(hwProgress);
                SplashUpdateMessage(splash, logService, "硬件设备初始化完成", msgType: MsgType.Success);
                await Task.Delay(300);

                SplashUpdateMessage(splash, logService, "模组初始化中。。。", msgType: MsgType.Info);
                await Task.Delay(300);
                if (await InitializeMechanism())
                {
                    SplashUpdateMessage(splash, logService, "模组初始化完成！", msgType: MsgType.Success);
                }
                else
                {
                    SplashUpdateMessage(splash, logService, "模组初始化失败！", msgType: MsgType.Error);
                    loadErr = true;
                }


                await Task.Delay(500);

                if (!loadErr)
                {
                    SplashUpdateMessage(splash, logService, "软件初始化成功！", msgType: MsgType.Success);
                }
                else
                {
                    SplashUpdateMessage(splash, logService, "软件初始化失败！", msgType: MsgType.Error);
                }

                await Task.Delay(500);
                return !loadErr;
            }
            catch (Exception ex)
            {
                SplashUpdateMessage(splash, logService, $"初始化过程中发生错误: {ex.Message}", msgType: MsgType.Error);
                return false;
            }
        }



        private async Task<bool> InitializeMechanism()
        {
            var workStation1FeedingModule = Container.Resolve<IMechanism>(nameof(WorkStation1FeedingModule));
            if (!await workStation1FeedingModule.InitializeAsync())
            {
                return false;
            }
            var workStation1DetectionModule = Container.Resolve<IMechanism>(nameof(WorkStationDetectionModule));
            if (!await workStation1DetectionModule.InitializeAsync())
            {
                return false;
            }
            var workStation1MaterialPullingModule = Container.Resolve<IMechanism>(nameof(WorkStation1MaterialPullingModule));

            if (!await workStation1MaterialPullingModule.InitializeAsync())
            {
                return false;
            }

            var workStationDataModule = Container.Resolve<IMechanism>(nameof(WorkStationDataModule));
            if (!await workStationDataModule.InitializeAsync())
            {
                return false;
            }


            var workStationSecsGemModule = Container.Resolve<IMechanism>(nameof(WorkStationSecsGemModule));
            if (!await workStationSecsGemModule.InitializeAsync())
            {
                return false;
            }

            return true;

        }





        private async Task<bool> LoadConfigurationAsync()
        {
            await Task.Delay(1000);
            return true;
        }

        private void SplashUpdateMessage(Splash splash, ILogService? logService, string status, string category = "Splash", MsgType msgType = MsgType.Info)
        {
            switch (msgType)
            {
                case MsgType.Success: logService?.Success(status, category); break;
                case MsgType.Info: logService?.Info(status, category); break;
                case MsgType.Fatal: logService?.Fatal(status, category); break;
                case MsgType.Warning: logService?.Warn(status, category); break;
                case MsgType.Error: logService?.Error(status, category); break;
                default: logService?.Info(status, category); break;
            }
            splash?.UpdateMessage(status, msgType);
        }

        #endregion

        #region 界面资源

        /// <summary>
        /// 应用初始皮肤与窗口样式配置。
        /// </summary>
        private void ApplyConfiguration(SkinType skinType)
        {
            var commonparam = Container.Resolve<CommonSettings>();
            if (commonparam.Skin != skinType)
            {
                UpdateSkin(skinType.ToString());
            }

            ConfigHelper.Instance.SetWindowDefaultStyle();
            ConfigHelper.Instance.SetNavigationWindowDefaultStyle();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        /// <summary>
        /// 动态切换皮肤资源字典。
        /// </summary>
        internal void UpdateSkin(string str = "Default")
        {
            if (Enum.TryParse<SkinType>(str, out SkinType skin))
            {
                var skins0 = Resources.MergedDictionaries[0];
                skins0.MergedDictionaries.Clear();
                skins0.MergedDictionaries.Add(ResourceHelper.GetSkin(skin));

                var skins1 = Resources.MergedDictionaries[1];
                skins1.MergedDictionaries.Clear();
                skins1.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/PF.UI.Resources;component/Themes/Default.xaml")
                });

                Current.MainWindow?.OnApplyTemplate();
            }
        }

        #endregion
    }
}
