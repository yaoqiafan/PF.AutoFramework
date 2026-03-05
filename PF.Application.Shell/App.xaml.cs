using log4net;
using Microsoft.Extensions.Hosting;
using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.Views;
using PF.Core.Constants;
using PF.Core.Entities.Configuration;
using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Data.Context;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;
using PF.Data.Repositories;
using PF.Infrastructure.Station.Basic;
using PF.Modules.Debug;
using PF.Modules.Identity;
using PF.Modules.Logging;
using PF.Modules.Parameter;
using PF.Modules.Parameter.Dialog.Base;
using PF.Modules.Parameter.Dialog.Mappers;
using PF.Modules.Parameter.ViewModels.Models;
using PF.Services.Hardware;
using PF.Services.Identity;
using PF.Services.Logging;
using PF.Services.Params;
using PF.Services.Sync;
using PF.UI.Infrastructure.Dialog;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.Dialog.ViewModels;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Resources;
using PF.UI.Shared.Data;
using PF.UI.Shared.Tools;
using PF.UI.Shared.Tools.Helper;
using PF.Workstation.Demo;
using PF.Workstation.Demo.Mechanisms;
using PF.Workstation.Demo.UI;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace PF.Application.Shell
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        #region 私有字段

        private ILogService _logService;
        private HostApplicationBuilder? builder;

        #endregion

        #region 程序启动与自检

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await InitializeDatabaseAsync();

                base.OnStartup(e);

                if (RunningInstance() == null)
                {
                    try
                    {
                        this.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);
                        ApplyConfiguration();
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
                    IMessageService messageService = Container.Resolve<IMessageService>();
                    messageService.ShowMessage("当前应用程序已经在运行！", "警告", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    this.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"XamlParseException: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检测是否已有同名进程在运行，防止重复启动。
        /// </summary>
        public static Process RunningInstance()
        {
            Process current = System.Diagnostics.Process.GetCurrentProcess();
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcesses();
            foreach (System.Diagnostics.Process process in processes)
            {
                if (process.Id != current.Id)
                {
                    if (process.ProcessName == current.ProcessName)
                    {
                        if (Assembly.GetExecutingAssembly().Location.Replace(@"/", @"\") == current.MainModule.FileName)
                        {
                            return process;
                        }
                    }
                }
            }
            return null;
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
            UpdateSkin("Dark");

            Splash splash = Container.Resolve<Splash>();
            IParamService paramService = Container.Resolve<IParamService>();

            var name = paramService.GetParamAsync<string>("SoftWareName").GetAwaiter().GetResult();
            splash.WelcomeText = $"欢迎使用{name}";

            var nameEN = paramService.GetParamAsync<string>("SoftWareName_EN").GetAwaiter().GetResult();
            splash.WelcomeText_small = $"Welcome to the {nameEN}";

            Assembly assembly = Assembly.GetEntryAssembly();
            splash.VersionNumber = $"V{assembly.GetName().Version}";
            splash.LoadingAction = PerformInitializationAsync;

            if (splash.ShowDialog() == true)
            {
                splash.Close();
            }

            return Container.Resolve<MainWindow>();
        }

        protected override void OnInitialized()
        {
            var authService = Container.Resolve<IUserService>();
            // 用所有已注册菜单的 Title 初始化 PermissionHelper 的动态中文名称映射
            PermissionHelper.Initialize(Container.Resolve<INavigationMenuService>());
            // 使用默认的超级管理员账号进行静默登录
            authService.LoginAsync("SuperUser", "PF88888").GetAwaiter().GetResult();
            base.OnInitialized();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            RegisterLogServiceTypes(containerRegistry);
            RegisterSystemParamsTypes(containerRegistry);
            RegisterHardwareTypes(containerRegistry);

            containerRegistry.RegisterSingleton<Splash>();
            containerRegistry.RegisterDialogWindow<PFDialogBaseWindow>();
            containerRegistry.RegisterSingleton<INavigationMenuService, NavigationMenuService>();

            RegisterUserIdentityTypes(containerRegistry);

            ViewFactory.PreloadAssemblies();
            ViewFactory.RegisterCustomType<UserInfo, UserParamView, UserParamViewMapper>();

            containerRegistry.RegisterDialog<MessageDialogView, MessageDialogViewModel>("MessageDialog");
            containerRegistry.RegisterDialog<InputDialogView, InputDialogViewModel>("InputDialog");
            containerRegistry.RegisterDialog<WaitDialogView, WaitDialogViewModel>("WaitDialog");
            containerRegistry.RegisterSingleton<IMessageService, MessageService>();

            RegisterHardwareAndMechanisms(containerRegistry);
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);
            moduleCatalog.AddModule<LoggingModule>();
            moduleCatalog.AddModule<ParameterModule>();
            moduleCatalog.AddModule<IdentityModule>();
            moduleCatalog.AddModule<DebugModule>();
            moduleCatalog.AddModule<UIModule>();
        }

        #endregion

        #region 日志服务注册

        public void RegisterLogServiceTypes(IContainerRegistry containerRegistry)
        {
            try
            {
                var logConfig = CreateLogConfiguration();
                containerRegistry.RegisterInstance(logConfig);
                _logService = new LogService(logConfig);
                containerRegistry.RegisterInstance<ILogService>(_logService);
            }
            catch (Exception ex)
            {
                LogFallbackError("日志模块类型注册失败", ex);
                throw;
            }
        }

        private LogConfiguration CreateLogConfiguration()
        {
            try
            {
                var appBasePath = AppDomain.CurrentDomain.BaseDirectory;
                var logBasePath = Path.Combine(appBasePath, "Logs");

                var config = new LogConfiguration
                {
                    BasePath             = logBasePath,
                    HistoricalLogPath    = logBasePath,
                    EnableConsoleLogging = true,
                    EnableFileLogging    = true,
                    EnableUiLogging      = true,
                    MinimumLevel         = LogLevel.Debug,
                    AutoDeleteLogs       = true,
                    AutoDeleteIntervalDays = 30,
                    MaxUiEntries         = 1000,
                    SplitByHour          = false
                };

                config.ConfigureDefaultCategories();
                config.AddCategory(LogCategories.Custom, LogLevel.Warn, LogCategories.Custom);

                EnsureLogDirectoryExists(logBasePath);
                foreach (var category in config.GetFileLogCategories())
                {
                    EnsureLogDirectoryExists(Path.Combine(logBasePath, category));
                }

                return config;
            }
            catch (Exception ex)
            {
                LogFallbackError("创建日志配置失败，使用默认配置", ex);
                return CreateFallbackConfiguration();
            }
        }

        private LogConfiguration CreateFallbackConfiguration()
        {
            return new LogConfiguration
            {
                BasePath               = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"),
                EnableConsoleLogging   = true,
                EnableFileLogging      = true,
                EnableUiLogging        = true,
                MinimumLevel           = LogLevel.Info,
                AutoDeleteLogs         = false,
                AutoDeleteIntervalDays = 30,
                MaxUiEntries           = 500
            }.ConfigureDefaultCategories();
        }

        private void EnsureLogDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                LogFallbackError($"创建目录失败: {path}", ex);
            }
        }

        private void LogFallbackError(string message, Exception ex)
        {
            try
            {
                var logger = LogManager.GetLogger(typeof(LoggingModule));
                logger.Error($"{message}: {ex.Message}", ex);
                System.Diagnostics.Debug.WriteLine($"[LOG_FALLBACK] {message}: {ex.Message}");
            }
            catch
            {
                // 所有备用方案都失败时，静默处理
            }
        }

        #endregion

        #region 参数数据库服务注册

        /// <summary>
        /// 初始化数据库：确保文件存在、表结构已创建、默认参数已写入。
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            try
            {
                if (!Directory.Exists(ConstGlobalParam.ConfigPath))
                    Directory.CreateDirectory(ConstGlobalParam.ConfigPath);

                var filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SystemParamsCollection.db");
                DbContextFactory<AppParamDbContext>.Initialize($"Data Source={filePath}");

                using var dbContext = DbContextFactory<AppParamDbContext>.CreateDbContext();
                await dbContext.Database.EnsureCreatedAsync();
                await dbContext.EnsureDefaultParametersCreatedAsync(new DefaultParameters());
            }
            catch (Exception ex)
            {
                _logService.Error("数据库初始化失败", exception: ex);
                throw;
            }
        }

        /// <summary>
        /// 向容器注册参数仓储、数据库上下文及参数服务。
        /// </summary>
        protected void RegisterSystemParamsTypes(IContainerRegistry containerRegistry)
        {
            try
            {
                var container = containerRegistry.GetContainer();
                var filePath  = Path.Combine(ConstGlobalParam.ConfigPath, "SystemParamsCollection.db");

                DbContextFactory<AppParamDbContext>.Initialize($"Data Source={filePath}");
                var dbContextOptions = DbContextFactory<AppParamDbContext>.CreateDbContextOptions();
                container.RegisterInstance(dbContextOptions);

                container.Register<Microsoft.EntityFrameworkCore.DbContext, AppParamDbContext>(
                    made: Made.Of(() => new AppParamDbContext(
                        Arg.Of<Microsoft.EntityFrameworkCore.DbContextOptions<AppParamDbContext>>())),
                    reuse: Reuse.Scoped);

                container.Register(typeof(IParamRepository<>), typeof(ParamRepository<>),
                    setup: Setup.With(condition: r => r.ServiceType.IsGenericType),
                    reuse: Reuse.ScopedOrSingleton);

                RegisterParamModels(containerRegistry);

                container.Register<IParamService, ParamService>(reuse: Reuse.Singleton);
                container.Register<IDefaultParam, DefaultParameters>();

                _logService.Info("系统参数服务注册完成", "DependencyInjection");
            }
            catch (Exception ex)
            {
                _logService.Error("系统参数服务注册失败", exception: ex);
                throw;
            }
        }

        /// <summary>
        /// 注册自定义参数模型（扩展点）。
        /// </summary>
        private void RegisterParamModels(IContainerRegistry containerRegistry)
        {
        }

        #endregion

        #region 用户身份服务注册

        private void RegisterUserIdentityTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IUserService, UserService>();
        }

        #endregion

        #region 硬件服务注册

        private void RegisterHardwareTypes(IContainerRegistry containerRegistry)
        {
            // dataDirectory 仍用于 SimXAxis 点表文件存储（与硬件配置存储无关）
            var dataDirectory = ConstGlobalParam.ConfigPath;

            var container    = containerRegistry.GetContainer();
            var paramService = container.Resolve<IParamService>();
            paramService.RegisterParamType<HardwareParam, HardwareConfig>();

            var hwManager = new HardwareManagerService(_logService, paramService);

            hwManager.RegisterFactory("LTDMCMotionCard", cfg =>
            {
                int cardIndex = cfg.ConnectionParameters.TryGetValue("CardIndex", out var ci)
                    ? int.Parse(ci) : 0;
                return new PF.Infrastructure.Hardware.Card.LTDMC.LTMDCMotionCard(cardIndex, _logService);
            });

            hwManager.RegisterFactory("EtherCatAxis", cfg =>
            {
                int axisIndex = cfg.ConnectionParameters.TryGetValue("AxisIndex", out var idx)
                    ? int.Parse(idx) : 0;
                return new PF.Workstation.AutoOcr.Hardware.EtherCatAxis(cfg.DeviceId, axisIndex, cfg.DeviceName,cfg.IsSimulated, _logService, dataDirectory);
            });

            hwManager.RegisterFactory("EtherCatIO", cfg =>
                new PF.Workstation.AutoOcr.Hardware.EtherCatIO(cfg.DeviceId,  cfg.DeviceName, cfg.IsSimulated, _logService));

            containerRegistry.RegisterInstance<IHardwareManagerService>(hwManager);
        }

        private void RegisterHardwareAndMechanisms(IContainerRegistry containerRegistry)
        {
            // 事件总线
            containerRegistry.RegisterSingleton<PhysicalButtonEventBus>();

            // 工站同步服务
            containerRegistry.RegisterSingleton<IStationSyncService, StationSyncService>();

            // 机构层：GantryMechanism 同时映射到自身类型和 IMechanism 接口
            var container = containerRegistry.GetContainer();
            container.RegisterMany(
                new[] { typeof(GantryMechanism), typeof(IMechanism) },
                typeof(GantryMechanism),
                reuse: DryIoc.Reuse.Singleton);

            // 工站层：PickPlaceStation 同时映射到自身类型和 StationBase
            container.RegisterMany(
                new[] { typeof(PickPlaceStation), typeof(StationBase) },
                typeof(PickPlaceStation),
                reuse: DryIoc.Reuse.Singleton);

            // 主控调度器
            containerRegistry.RegisterSingleton<IMasterController, DemoMachineController>();
        }

        #endregion

        #region 启动初始化流程（Splash）

        private async Task<bool> PerformInitializationAsync()
        {
            Splash splash        = Container.Resolve<Splash>();
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
                    return false;
                }
                SplashUpdateMessage(splash, logService, "配置文件加载成功。。。", msgType: MsgType.Success);
                await Task.Delay(500);

                SplashUpdateMessage(splash, logService, "硬件设备初始化中。。。", msgType: MsgType.Info);
                var hwManager = Container.Resolve<IHardwareManagerService>();
                await hwManager.LoadAndInitializeAsync();
                SplashUpdateMessage(splash, logService, "硬件设备初始化完成", msgType: MsgType.Success);
                await Task.Delay(300);

                SplashUpdateMessage(splash, logService, "初始化完成", msgType: MsgType.Success);
                await Task.Delay(500);
                return true;
            }
            catch (Exception ex)
            {
                SplashUpdateMessage(splash, logService, $"初始化过程中发生错误: {ex.Message}", msgType: MsgType.Error);
                return false;
            }
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
                case MsgType.Info:    logService?.Info(status, category);    break;
                case MsgType.Fatal:   logService?.Fatal(status, category);   break;
                case MsgType.Warning: logService?.Warn(status, category);    break;
                case MsgType.Error:   logService?.Error(status, category);   break;
                default:              logService?.Info(status, category);    break;
            }
            splash?.UpdateMessage(status, msgType);
        }

        #endregion

        #region 界面资源

        /// <summary>
        /// 应用初始皮肤与窗口样式配置。
        /// </summary>
        private void ApplyConfiguration()
        {
            UpdateSkin(SkinType.Dark.ToString());
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
