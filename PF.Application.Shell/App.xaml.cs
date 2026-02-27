using log4net;
using Microsoft.Extensions.Hosting;
using PF.Application.Shell.CustomConfiguration.Logging;
using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.Views;
using PF.Core.Entities.Configuration;
using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Data.Context;
using PF.Data.Entity.Category;
using PF.Data.Entity.Category.Basic;
using PF.Data.Repositories;
using PF.Modules.Debug;
using PF.Modules.Identity;
using PF.Modules.Logging;
using PF.Modules.Parameter;
using PF.Modules.Parameter.Dialog.Base;
using PF.Modules.Parameter.Dialog.Mappers;
using PF.Modules.Parameter.ViewModels.Models;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
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
using PF.Workstation.Demo.Hardware;
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

        private ILogService _logService;
        private HostApplicationBuilder? builder;

        #region 程序自检

        public static Process RunningInstance()
        {
            Process current = System.Diagnostics.Process.GetCurrentProcess();
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcesses();
            foreach (System.Diagnostics.Process process in processes) //查找相同名称的进程
            {
                if (process.Id != current.Id) //忽略当前进程
                {
                    if (process.ProcessName == current.ProcessName)//判断进程名称是否和当前运行进程名称一样
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

        #endregion 程序自检

        #region 界面资源操作

        /// <summary>
        /// 初始化界面
        /// </summary>
        private void ApplyConfiguration()
        {
            //var Params = Container.Resolve<IParams>().GetParam<GlobalSettingsDTO>(ParamType.Base);

            //if (Params.Skin != CommonConfig.Core.Dtos.SkinType.Default)
            //{
            UpdateSkin(SkinType.Dark.ToString());
            //}

            ConfigHelper.Instance.SetWindowDefaultStyle();
            ConfigHelper.Instance.SetNavigationWindowDefaultStyle();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        /// <summary>
        /// 更新皮肤资源
        /// </summary>
        /// <param name="str"></param>
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



        #endregion 界面资源操作

        #region Prism相关
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
            string VersionNumber = $"V{assembly.GetName().Version}";
            splash.VersionNumber = VersionNumber;
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

            // 2. 使用默认的操作员账号进行静默登录
            // 使用 .GetAwaiter().GetResult() 来同步等待异步方法完成
            bool loginSuccess = authService.LoginAsync("SuperUser", "PF88888").GetAwaiter().GetResult();
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

            // =======================================================
            // 【新增】注册硬件与模组
            // =======================================================
            RegisterHardwareAndMechanisms(containerRegistry);
        }

        private void RegisterHardwareAndMechanisms(IContainerRegistry containerRegistry)
        {
            var xAxis = new SimXAxis(0, _logService,"");
            var vacuumIO = new SimVacuumIO(_logService);


            // a. 供具体模组注入使用
            containerRegistry.RegisterInstance(xAxis);
            containerRegistry.RegisterInstance(vacuumIO);

            // b. 供设备调试界面的 IEnumerable<IHardwareDevice> 抓取使用
            containerRegistry.RegisterInstance<IHardwareDevice>(xAxis, "SimXAxis");
            containerRegistry.RegisterInstance<IHardwareDevice>(vacuumIO, "SimVacuumIO");


           
            // 假设你在这里或者硬件管理服务里创建了 gantryMechanism
            var gantryMechanism = new GantryMechanism(xAxis, vacuumIO, _logService);

            // 必须把它注册为单例，这样 ViewModel 的构造函数才能要到它！
            containerRegistry.RegisterInstance<GantryMechanism>(gantryMechanism);

            // 如果是通过接口管理的，最好也把接口注册上
            containerRegistry.RegisterInstance<IMechanism>(gantryMechanism);

            // ── 工站同步服务 + 工站实例 ──────────────────────────────────────
            // StationSyncService：流水线信号量管理，供 PickPlaceStation 使用
            var syncService = new StationSyncService(_logService);
            containerRegistry.RegisterInstance<IStationSyncService>(syncService);

            // PickPlaceStation：取放工站实例，注册为 StationBase 供 StationDebugViewModel
            // 通过 IEnumerable<StationBase> 自动发现（DryIoc 集合解析）
            var pickPlaceStation = new PickPlaceStation(gantryMechanism, syncService, _logService);
            containerRegistry.RegisterInstance<PickPlaceStation>(pickPlaceStation);
            containerRegistry.RegisterInstance<StationBase>(pickPlaceStation, "PickPlaceStation");
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

        #region 初始入口
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
                // 记录详细的错误信息
                Debug.WriteLine($"XamlParseException: {ex.Message}");
                throw;
            }



        }
        #endregion



        #region 参数数据库加载和初始化
        /// <summary>
        /// 初始化数据库
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            try
            {
                // 1. 获取应用程序目录
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appFolder = Path.Combine(appDataPath, "PFAutoFrameWork");

                // 如果目录不存在则创建
                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                // 2. 构建数据库文件路径
                var filePath = Path.Combine(appFolder, "SystemParamsCollection.db");

                // 3. 初始化数据库上下文工厂
                DbContextFactory<AppParamDbContext>.Initialize($"Data Source={filePath}");

                // 4. 创建数据库上下文并确保数据库创建
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
        /// 注册类型到容器
        /// </summary>
        protected void RegisterSystemParamsTypes(IContainerRegistry containerRegistry)
        {
            try
            {
                var container = containerRegistry.GetContainer();


                // 获取数据库连接字符串（与初始化时保持一致）
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appFolder = Path.Combine(appDataPath, "PFAutoFrameWork");
                var filePath = Path.Combine(appFolder, "SystemParamsCollection.db");

                //  初始化数据库上下文工厂
                DbContextFactory<AppParamDbContext>.Initialize($"Data Source={filePath}");

                // 创建数据库上下文选项
                var dbContextOptions = DbContextFactory<AppParamDbContext>.CreateDbContextOptions();

                container.RegisterInstance(dbContextOptions);

                // 注册数据库上下文为作用域（推荐）或单例
                container.Register<Microsoft.EntityFrameworkCore.DbContext, AppParamDbContext>(made: Made.Of(() =>
                  new AppParamDbContext(Arg.Of<Microsoft.EntityFrameworkCore.DbContextOptions<AppParamDbContext>>())),
                    reuse: Reuse.Scoped);

                // 注册泛型仓储
                container.Register(typeof(IParamRepository<>), typeof(ParamRepository<>),
                setup: Setup.With(condition: r => r.ServiceType.IsGenericType),
                reuse: Reuse.ScopedOrSingleton);

                // 注册其他参数模型（用于扩展）
                RegisterParamModels(containerRegistry);

                // 注册参数服务（单例，因为它有事件订阅）
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
        /// 注册参数模型（用于扩展）
        /// </summary>
        private void RegisterParamModels(IContainerRegistry containerRegistry)
        {



        }

        private void RegisterUserIdentityTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IUserService, UserService>();
        }
        #endregion



        #region 加载方法
        private async Task<bool> PerformInitializationAsync()
        {
            Splash splash = Container.Resolve<Splash>();
            ILogService? logService = Container.Resolve<ILogService>();

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
                case MsgType.Success:
                    logService?.Success(status, category);
                    break;
                case MsgType.Info:
                    logService?.Info(status, category);
                    break;
                case MsgType.Fatal:
                    logService?.Fatal(status, category);
                    break;
                case MsgType.Warning:
                    logService?.Warn(status, category);
                    break;
                case MsgType.Error:
                    logService?.Error(status, category);
                    break;
                default:
                    logService?.Info(status, category);
                    break;
            }
            splash?.UpdateMessage(status, msgType);
        }


        #endregion


        #region 硬件服务注册

        private void RegisterHardwareTypes(IContainerRegistry containerRegistry)
        {
            // dataDirectory 仍用于 SimXAxis 点表文件存储（与硬件配置存储无关）
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataDirectory = Path.Combine(appDataPath, "PFAutoFrameWork");

            // 从容器解析 IParamService（已在 RegisterSystemParamsTypes 中注册为单例）
            var container = containerRegistry.GetContainer();
            var paramService = container.Resolve<IParamService>();

            // 注册 HardwareParam → HardwareConfig 参数类型映射
            // 使得 IParamService 泛型方法能正确定位 HardwareParams 表
            paramService.RegisterParamType<HardwareParam, HardwareConfig>();

            var hwManager = new HardwareManagerService(_logService, paramService);

            // 注册 SimMotionCard 工厂（顶级板卡，必须先于子设备注册以确保工厂存在）
            hwManager.RegisterFactory("SimMotionCard", cfg =>
            {
                int cardIndex = cfg.ConnectionParameters.TryGetValue("CardIndex", out var ci)
                    ? int.Parse(ci) : 0;
                return new PF.Workstation.Demo.Hardware.SimMotionCard(cardIndex, _logService);
            });

            // 注册 SimXAxis 工厂（工厂在 Composition Root 持有具体类型引用，不违反依赖方向）
            hwManager.RegisterFactory("SimXAxis", cfg =>
            {
                int axisIndex = cfg.ConnectionParameters.TryGetValue("AxisIndex", out var idx)
                    ? int.Parse(idx) : 0;
                return new PF.Workstation.Demo.Hardware.SimXAxis(axisIndex, _logService, dataDirectory);
            });

            // 注册 SimVacuumIO 工厂
            hwManager.RegisterFactory("SimVacuumIO", cfg =>
                new PF.Workstation.Demo.Hardware.SimVacuumIO(_logService));

            containerRegistry.RegisterInstance<IHardwareManagerService>(hwManager);
        }

        #endregion

        #region 日志配置加载

        public void RegisterLogServiceTypes(IContainerRegistry containerRegistry)
        {
            try
            {
                // 1. 创建和注册配置
                var logConfig = CreateLogConfiguration();
                containerRegistry.RegisterInstance(logConfig);
                // 2. 创建日志服务实例
                _logService = new LogService(logConfig);

                // 3. 注册ILogService接口
                containerRegistry.RegisterInstance<ILogService>(_logService);

            }
            catch (Exception ex)
            {
                // 注册失败时的备用日志
                LogFallbackError("日志模块类型注册失败", ex);
                throw;
            }
        }


        private LogConfiguration CreateLogConfiguration()
        {
            try
            {
                // 获取应用程序基础路径
                var appBasePath = AppDomain.CurrentDomain.BaseDirectory;

                // 构建日志目录路径
                var logBasePath = Path.Combine(appBasePath, "Logs");

                // 创建日志配置
                var config = new LogConfiguration
                {
                    BasePath = logBasePath,
                    HistoricalLogPath = logBasePath,
                    EnableConsoleLogging = true,
                    EnableFileLogging = true,
                    EnableUiLogging = true,
                    MinimumLevel = LogLevel.Debug,
                    AutoDeleteLogs = true,
                    AutoDeleteIntervalDays = 30,
                    MaxUiEntries = 1000,
                    SplitByHour = false
                };

                // 配置默认分类
                config.ConfigureDefaultCategories();

                // 添加更多自定义分类
                config.AddCategory(LogCategories.Custom, LogLevel.Warn, "Custom");


                // 确保日志目录存在
                EnsureLogDirectoryExists(logBasePath);

                // 为每个分类创建子目录
                foreach (var category in config.GetFileLogCategories())
                {
                    var categoryPath = Path.Combine(logBasePath, category);
                    EnsureLogDirectoryExists(categoryPath);
                }

                return config;
            }
            catch (Exception ex)
            {
                // 配置失败时返回默认配置
                LogFallbackError("创建日志配置失败，使用默认配置", ex);
                return CreateFallbackConfiguration();
            }
        }

        private void EnsureLogDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                LogFallbackError($"创建目录失败: {path}", ex);
            }
        }

        private LogConfiguration CreateFallbackConfiguration()
        {
            return new LogConfiguration
            {
                BasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"),
                EnableConsoleLogging = true,
                EnableFileLogging = true,
                EnableUiLogging = true,
                MinimumLevel = LogLevel.Info,
                AutoDeleteLogs = false, // 故障时关闭自动清理
                AutoDeleteIntervalDays = 30,
                MaxUiEntries = 500
            }.ConfigureDefaultCategories();
        }

        private void LogFallbackError(string message, Exception ex)
        {
            // 当日志服务不可用时的备用日志记录
            try
            {
                // 1. 尝试使用log4net直接记录
                var logger = LogManager.GetLogger(typeof(LoggingModule));
                logger.Error($"{message}: {ex.Message}", ex);

                // 2. 输出到调试窗口
                System.Diagnostics.Debug.WriteLine($"[LOG_FALLBACK] {message}: {ex.Message}");
            }
            catch
            {
                // 所有备用方案都失败时，静默处理
            }
        }
        #endregion
    }
}
