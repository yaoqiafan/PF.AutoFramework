using Microsoft.EntityFrameworkCore;
using PF.Application.Base.Configuration;
using PF.Application.Base.Services;
using PF.Application.Base.ViewModels;
using PF.Application.Base.Views;
using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Production;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Core.Interfaces.Timer;
using PF.Core.Interfaces.TowerLight;
using PF.Core.Models;
using PF.Data;
using PF.Data.Context;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;
using PF.Infrastructure.Logging;
using PF.Infrastructure.SecsGem;
using PF.Infrastructure.SecsGem.Command;
using PF.Infrastructure.SecsGem.Incentive;
using PF.Infrastructure.SecsGem.Param;
using PF.Infrastructure.SecsGem.Tools;
using PF.Infrastructure.Station;
using PF.SecsGem.DataBase;
using PF.Services.Alarm;
using PF.Services.Hardware;
using PF.Services.Identity;
using PF.Services.Logging;
using PF.Services.Params;
using PF.Services.Production;
using PF.Services.Sync;
using PF.Services.Timer;
using PF.UI.Infrastructure.Dialog;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.Dialog.ViewModels;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Resources;
using PF.UI.Shared.Data;
using PF.UI.Shared.Tools;
using PF.UI.Shared.Tools.Helper;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using MsgType = PF.UI.Shared.Data.MsgType;

namespace PF.Application.Base
{
    /// <summary>
    /// 所有项目 App.xaml.cs 的抽象基类。
    /// 封装全部框架样板（互斥锁、Splash、EA 桥接、公共 DI 注册）。
    /// 子类只需实现几个钩子方法来描述项目特有的硬件、机构和每日任务。
    /// </summary>
    public abstract class PFApplicationBase : PrismApplication
    {
        #region 静态构造：程序集解析回退

        static PFApplicationBase()
        {
            AppDomain.CurrentDomain.AssemblyResolve += static (_, args) =>
            {
                var shortName = new AssemblyName(args.Name).Name;
                var already = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == shortName);
                if (already != null) return already;
                var path = Path.Combine(AppContext.BaseDirectory, shortName + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
        }

        #endregion

        #region 私有字段

        private static readonly string _mutexPrefix = "Global\\PFAutoFramework-";
        private static Mutex _appMutex;
        private static bool _isNewInstance;
        /// <summary>
        /// 界面日志
        /// </summary>
        public CategoryLogger UILogger { get; private set; }
        private CategoryLogger _dbLogger;

        /// <summary>日志服务（子类可在钩子方法中使用）</summary>
        protected ILogService LogService { get; private set; }

        #endregion

        #region 子类必须实现的钩子

        /// <summary>项目唯一互斥体 ID（格式示例："MyProject-XXXX-YYYY"）</summary>
        protected abstract string AppMutexId { get; }

        /// <summary>返回项目的默认参数集（硬件配置、系统参数、用户默认）</summary>
        protected abstract IDefaultParam CreateDefaultParameters();

        /// <summary>向 HardwareManagerService 注册项目特有的硬件工厂</summary>
        protected abstract void RegisterHardwareFactories(IHardwareManagerService hwManager);

        /// <summary>注册项目特有的机构、工站和主控（包括 IPanelIoConfig、ITowerLightDoWriterConfig）</summary>
        protected abstract void RegisterMechanismsAndStations(IContainerRegistry containerRegistry);

        /// <summary>注册项目特有的配方服务</summary>
        protected abstract void RegisterRecipes(IContainerRegistry containerRegistry);

        /// <summary>按顺序初始化项目机构，返回 true 表示全部成功</summary>
        protected abstract Task<bool> InitializeMechanismsAsync();


        #endregion

        #region 子类可选重写的钩子

        /// <summary>按顺序初始化项目机构，返回 true 表示全部成功</summary>
        protected virtual async Task<bool> LoadConfigurationAsync()
        {
            await Task.Delay(1000);
            return true;
        }

        /// <summary>注册 IO 映射枚举（默认空实现，有 IO 映射的项目覆写）</summary>
        protected virtual void RegisterIOMappings(IIOMappingService ioMappingService) { }

        /// <summary>注册视觉引擎服务（默认空实现）</summary>
        protected virtual void RegisterVisionServices(IContainerRegistry containerRegistry) { }

        /// <summary>注册 SECS/GEM 服务（默认提供完整实现，不需要的项目覆写为空）</summary>
        protected virtual void RegisterSecsGemServices(IContainerRegistry containerRegistry)
        {
            try
            {
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");
                var opts = new DbContextOptionsBuilder<SecsGemDbContext>()
                    .UseSqlite($"Data Source={filePath}").Options;
                containerRegistry.RegisterInstance<DbContextOptions<SecsGemDbContext>>(opts);
                containerRegistry.RegisterSingleton<SecsGemDbContext>();
                containerRegistry.RegisterSingleton<ISecsGemDataBase, SecsGemDataBaseManger>();
                containerRegistry.RegisterSingleton<ICommandManager, SecsGemCommandManger>();
                containerRegistry.RegisterSingleton<SecsGemMessageProcessor>();
                containerRegistry.RegisterSingleton<IParams, ParamsManger>();
                containerRegistry.RegisterSingleton<IinternalClient, InternalClient>();
                containerRegistry.RegisterSingleton<ISecsGemMessageUpdater, SecsGemMessageUpdater>();
                containerRegistry.RegisterSingleton<ISecsGemManager, SecsGemManger>();
            }
            catch (Exception ex)
            {
                _dbLogger?.Error("SecsGem 数据库注册失败", ex);
                throw;
            }
        }

        /// <summary>注册主窗口 ViewModel（默认注册基类型本身，有自定义子类的项目覆写）</summary>
        protected virtual void RegisterMainWindowViewModel(IContainerRegistry containerRegistry)
            => containerRegistry.RegisterSingleton<MainWindowViewModelBase>();

        /// <summary>注册项目特有每日定时任务（默认空实现）</summary>
        protected virtual void RegisterProjectDailyTasks() { }

        #endregion

        #region 单实例保护

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                if (RunningInstance())
                {
                    base.OnStartup(e);
                    this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                }
                else
                {
                    MessageBox.Show("当前应用程序已经在运行！", "警告", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    this.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup error: {ex.Message}");
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            if (_isNewInstance)
            {
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
            }
        }

        private bool RunningInstance()
        {
            string mutexName = _mutexPrefix + AppMutexId;
            try
            {
                _appMutex = new Mutex(true, mutexName, out _isNewInstance);
            }
            catch (UnauthorizedAccessException)
            {
                _appMutex = new Mutex(true, mutexName.Replace("Global\\", ""), out _isNewInstance);
            }
            return _isNewInstance;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var error = e.Exception;
            var str = error != null
                ? $"未处理异常: {error.Message}\r\n堆栈: {error.StackTrace}"
                : $"未处理错误: {e}";
            Container.Resolve<IMessageService>().ShowMessage(
                "发生错误，请查看程序日志！\n" + str, "系统错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        #endregion

        #region Prism 核心方法

        protected override Window CreateShell()
        {
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var commonParam = Container.Resolve<CommonSettings>();
            ApplyConfiguration(commonParam.Skin);

            var splash = Container.Resolve<Splash>();
            splash.WelcomeText = $"欢迎使用{commonParam.SoftWareName}";
            splash.WelcomeText_small = $"Welcome to the {commonParam.SoftWareName_EN}";
            splash.VersionNumber = $"V{Assembly.GetEntryAssembly()?.GetName().Version}";
            splash.LoadingAction = PerformInitializationAsync;

            if (splash.ShowDialog() == true)
            {
                splash.Close();
                Container.Resolve<IHardwareInputMonitor>().StartStandardMonitoring();
            }
            else
            {
                var msgSvc = Container.Resolve<IMessageService>();
                var res = msgSvc.ShowMessageAsync("软件加载失败，是否退出系统?", "系统错误",
                    MessageBoxButton.YesNo, MessageBoxImage.Error).GetAwaiter().GetResult();
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
            var navMenuService = Container.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetEntryAssembly());

            PermissionHelper.Initialize(navMenuService);

            var authService = Container.Resolve<IUserService>();
            authService.ResetToOperator();
            authService.LoginAsync("SuperUser", DateTime.Now.ToString("yyyyMMddHH00")).GetAwaiter().GetResult();

            var controller = Container.Resolve<IMasterController>();
            var ea = Container.Resolve<IEventAggregator>();

            ea.GetEvent<HardwareResetRequestedEvent>()
              .Subscribe(req => (controller as BaseMasterController)?.OnHardwareResetRequested(req),
                  ThreadOption.BackgroundThread, keepSubscriberReferenceAlive: true);

            ea.GetEvent<SystemResetRequestedEvent>()
              .Subscribe(() => _ = controller.RequestSystemResetAsync(),
                  ThreadOption.BackgroundThread, keepSubscriberReferenceAlive: true);

            controller.MasterStateChanged += (_, state) => ea.GetEvent<MachineStateChangedEvent>().Publish(state);
            Container.Resolve<TowerLightManager>();

            controller.ReinitializationRequired += (_, _) => ea.GetEvent<ReinitializeRequiredEvent>().Publish();

            Container.Resolve<IAppTimerService>().Start();

            RegisterProjectDailyTasks();

            base.OnInitialized();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var commonSettings = CommonSettings.Load();
            if (!File.Exists(CommonSettings.ConfigFilePath))
                commonSettings.Save();
            containerRegistry.RegisterInstance<CommonSettings>(commonSettings);

            containerRegistry.AddLogging();
            LogService = containerRegistry.GetContainer().Resolve<ILogService>();
            UILogger = CategoryLoggerFactory.UI(LogService);
            _dbLogger = CategoryLoggerFactory.Database(LogService);

            RegisterParamDbContext(containerRegistry);
            containerRegistry.AddParameterServices(CreateDefaultParameters());

            RegisterProductionDataService(containerRegistry);
            RegisterSecsGemServices(containerRegistry);
            RegisterHardwareTypes(containerRegistry);

            containerRegistry.RegisterSingleton<Splash>();
            containerRegistry.RegisterDialogWindow<PFDialogBaseWindow>();
            containerRegistry.RegisterSingleton<INavigationMenuService, NavigationMenuService>();
            containerRegistry.RegisterForNavigation<CommonParamView, BaseParamsViewModel>(
                NavigationConstants.Views.CommonParamView);

            containerRegistry.RegisterSingleton<IUserService, UserService>();

            containerRegistry.RegisterDialog<MessageDialogView, MessageDialogViewModel>("MessageDialog");
            containerRegistry.RegisterDialog<InputDialogView, InputDialogViewModel>("InputDialog");
            containerRegistry.RegisterDialog<WaitDialogView, WaitDialogViewModel>("WaitDialog");
            containerRegistry.RegisterSingleton<IMessageService, MessageService>();

            RegisterMainWindowViewModel(containerRegistry);
            RegisterHardwareAndMechanisms(containerRegistry);
            RegisterRecipes(containerRegistry);
            RegisterAlarmServices(containerRegistry);
            RegisterTimerService(containerRegistry);
            RegisterVisionServices(containerRegistry);
        }

        protected override IModuleCatalog CreateModuleCatalog()
        {
            var modulesPath = Path.Combine(AppContext.BaseDirectory, "Modules");
            return new DirectoryModuleCatalog { ModulePath = modulesPath };
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog) { }

        #endregion

        #region 公共方法（供 MainWindow.xaml.cs 调用）

        public void UpdateSkin(string str = "Default")
        {
            if (!Enum.TryParse<SkinType>(str, out SkinType skin)) return;

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

        #endregion

        #region 私有注册方法

        private async void RegisterParamDbContext(IContainerRegistry containerRegistry)
        {
            try
            {
                var container = containerRegistry.GetContainer();
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SystemParamsCollection.db");
                DbContextFactory<AppParamDbContext>.Initialize($"Data Source={filePath}");

                using var ctx = DbContextFactory<AppParamDbContext>.CreateDbContext();
                await ctx.Database.EnsureCreatedAsync();
                await ctx.EnsureDefaultParametersCreatedAsync(CreateDefaultParameters());

                var opts = DbContextFactory<AppParamDbContext>.CreateDbContextOptions();
                container.RegisterInstance(opts);

                Func<Microsoft.EntityFrameworkCore.DbContext> factory =
                    () => new AppParamDbContext(opts);
                container.RegisterInstance<Func<Microsoft.EntityFrameworkCore.DbContext>>(factory);

                _dbLogger.Info("参数数据库注册完成");
            }
            catch (Exception ex)
            {
                _dbLogger?.Error("参数数据库注册失败", ex);
                throw;
            }
        }

        private void RegisterProductionDataService(IContainerRegistry containerRegistry)
        {
            try
            {
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "ProductionHistory.db");
                var opts = new DbContextOptionsBuilder<ProductionDbContext>()
                    .UseSqlite($"Data Source={filePath}").Options;
                containerRegistry.RegisterInstance<DbContextOptions<ProductionDbContext>>(opts);
                containerRegistry.RegisterSingleton<IProductionDataService, ProductionDataService>();
                _dbLogger.Info("生产数据服务注册完成");
            }
            catch (Exception ex)
            {
                _dbLogger?.Error("生产数据服务注册失败", ex);
                throw;
            }
        }

        private void RegisterHardwareTypes(IContainerRegistry containerRegistry)
        {
            var container = containerRegistry.GetContainer();
            var paramService = container.Resolve<IParamService>();
            paramService.RegisterParamType<HardwareParam, HardwareConfig>();

            var hwManager = new HardwareManagerService(LogService, paramService);
            RegisterHardwareFactories(hwManager);

            containerRegistry.RegisterSingleton<IIOMappingService, IOMappingService>();
            var ioMappingService = container.Resolve<IIOMappingService>();
            RegisterIOMappings(ioMappingService);

            containerRegistry.RegisterInstance<IHardwareManagerService>(hwManager);
        }

        private void RegisterHardwareAndMechanisms(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<HardwareInputEventBus>();

            // 子类的 RegisterMechanismsAndStations 必须注册 IPanelIoConfig 和 ITowerLightDoWriterConfig
            RegisterMechanismsAndStations(containerRegistry);

            containerRegistry.RegisterSingleton<IHardwareInputMonitor, HardwareInputMonitor>();
            containerRegistry.RegisterSingleton<IStationSyncService, StationSyncService>();
            containerRegistry.RegisterSingleton<ITowerLightDoWriter, TowerLightDoWriter>();
            containerRegistry.RegisterSingleton<ITowerLightService, TowerLightService>();
            containerRegistry.RegisterSingleton<TowerLightManager>();
        }

        private void RegisterAlarmServices(IContainerRegistry containerRegistry)
        {
            try
            {
                containerRegistry.RegisterSingleton<IAlarmEventPublisher, PrismAlarmEventPublisher>();
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "AlarmHistory.db");
                containerRegistry.AddAlarmServices(filePath);
                _dbLogger.Info("报警服务注册完成");
            }
            catch (Exception ex)
            {
                _dbLogger?.Error("报警服务注册失败", ex);
                throw;
            }
        }

        private void RegisterTimerService(IContainerRegistry containerRegistry)
        {
            try
            {
                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "timer_schedule.json");
                containerRegistry.AddTimerService(filePath);
                _dbLogger.Info("定时服务注册完成");
            }
            catch (Exception ex)
            {
                _dbLogger?.Error("定时服务注册失败", ex);
                throw;
            }
        }

        #endregion

        #region Splash 初始化序列

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
                    SplashUpdateMessage(splash, logService, payload.Status, payload.Category, payload.MsgType.ToString()));
                await hwManager.LoadAndInitializeAsync(hwProgress);
                SplashUpdateMessage(splash, logService, "硬件设备初始化完成", msgType: MsgType.Success);
                await Task.Delay(300);

                SplashUpdateMessage(splash, logService, "模组初始化中。。。", msgType: MsgType.Info);
                await Task.Delay(300);
                if (await InitializeMechanismsAsync())
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

      
        private static void SplashUpdateMessage(Splash splash, ILogService? logService, string status, string category = "Splash", string msgType = "Info")
        {
            switch (msgType)
            {
                case "Success": logService?.Success(status, category); break;
                case "Info": logService?.Info(status, category); break;
                case "Fatal": logService?.Fatal(status, category); break;
                case "Warning": logService?.Warn(status, category); break;
                case "Error": logService?.Error(status, category); break;
                default: logService?.Info(status, category); break;
            }
            splash?.UpdateMessage(status, Enum.Parse<MsgType>(msgType));
        }

        private static void SplashUpdateMessage(Splash splash, ILogService? logService, string status, string category = "Splash", MsgType msgType = MsgType.Info)
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

        #region 界面皮肤

        private void ApplyConfiguration(SkinType skinType)
        {
            UpdateSkin(skinType.ToString());
            ConfigHelper.Instance.SetWindowDefaultStyle();
            ConfigHelper.Instance.SetNavigationWindowDefaultStyle();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        #endregion
    }
}
