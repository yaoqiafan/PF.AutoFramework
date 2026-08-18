using Microsoft.EntityFrameworkCore;
using PF.Application.Base.Configuration;
using PF.Application.Base.Services;
using PF.Application.Base.ViewModels;
using PF.Application.Base.Views;
using PF.CommonTools.ServeTool;
using PF.Core.Constants;
using PF.Core.Entities.Communication;
using PF.Core.Entities.Hardware;
using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Communication;
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
using PF.Services.Communication;
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
using System.Windows.Media.Animation;
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

        #region 实例构造：项目配置路径隔离

        /// <summary>
        /// 项目名，作为 <c>D:\PFConfig\PFAutoFrameWork\</c> 下的隔离子目录名。
        /// 默认取入口程序集名（如 <c>PF.Application.Shell</c>）。
        ///
        /// 重写此属性的项目，<b>必须</b>在 <c>Installer\installer.conf</c> 中同步设置
        /// <c>PROJECT_NAME</c> 为相同的值，否则 PF.SecsGem.Service 会指向另一个目录的
        /// SecsGemConfig.db（启动时 <see cref="VerifySecsServiceProjectBinding"/> 会弹窗告警）。
        /// </summary>
        protected virtual string ProjectName =>
            Assembly.GetEntryAssembly()?.GetName().Name ?? "Default";

        /// <summary>
        /// 本项目是否使用 SECS/GEM。默认 <c>true</c>，保持既有项目的行为不变；
        /// 不需要 SECS/GEM 的项目在自己的 App 中重写为 <c>false</c> 即可整体关闭。
        ///
        /// <para>关闭后跳过的内容：</para>
        /// <list type="bullet">
        /// <item><see cref="RegisterSecsGemServices"/> 整段 DI 注册（ISecsGemManager /
        /// ICommandManager / IinternalClient 等），同时不再在项目配置目录下创建
        /// 空的 SecsGemConfig.db。</item>
        /// <item><see cref="VerifySecsServiceProjectBinding"/> 服务项目绑定校验。</item>
        /// <item>模块目录里命名空间以 <c>PF.Modules.SecsGem</c> 开头的模块——
        /// 即便子类的 ConfigureModuleCatalog 里仍写着 AddModule，也会被拦下不加载。</item>
        /// </list>
        ///
        /// <para><b>项目自己的 SECS 相关注册也必须跟着关</b>：本开关只管框架自己那部分，
        /// 管不到项目在 RegisterMechanismsAndStations / RegisterTypes 里写的注册。
        /// 若项目注册了依赖 ISecsGemManager 的机构（如 Demo 的 WSSecsGemModule），
        /// 关掉开关后启动会崩在解析 MainWindow 上——MainWindow 构造注入的是
        /// IEnumerable&lt;IMechanism&gt;，DryIoc 会把<b>每一个</b>已注册机构都构造出来，
        /// 其中一个依赖解析不到，整个 MainWindow 就解析失败。
        /// 同理，构造注入了 ISecsGemManager 的 ViewModel 会在导航到该页时才崩；
        /// 这类 ViewModel 应改为先 IsRegistered&lt;ISecsGemManager&gt;() 判断再 Resolve。
        /// Demo Shell 的 App.xaml.cs 里有完整示例。</para>
        ///
        /// <para><b>为什么校验必须跟着一起关</b>：该校验解析的是 SCM 中<b>实际注册</b>的
        /// 服务 ImagePath，而服务全机唯一、不按项目区分。一机多项目时，只要有<b>任何一个</b>
        /// 项目装过该服务，<c>File.Exists</c> 就成立，于是不用 SECS/GEM 的项目也会被判为
        /// "服务属于别的项目"并弹窗，还会建议它"重新运行本项目的安装包以接管服务"——
        /// 而真按提示做，就把别的项目正在用的服务抢走了。</para>
        /// </summary>
        protected virtual bool UsesSecsGemService => true;

        /// <summary>
        /// 在任何配置读写之前完成项目名初始化。
        ///
        /// 必须放在实例构造函数而非 OnStartup/CreateShell/RegisterTypes：
        /// C# 中派生类字段初始化器先于基类构造函数体执行，因此子类以
        /// <c>=&gt; "XXX"</c> 形式重写 <see cref="ProjectName"/> 是安全的；
        /// 而其余入口都晚于 App 实例构造，期间已可能有类型触碰 ConfigPath。
        /// </summary>
        protected PFApplicationBase()
        {
            ConstGlobalParam.Initialize(ProjectName);
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

        /// <summary>
        /// 向 HardwareManagerService 注册项目特有的硬件工厂。
        /// 同时传入 ICommunicationManagerService，供需要引用通讯实例的硬件工厂闭包
        /// 通过 GetCommunication&lt;T&gt;(InstanceId) 查找（此时通讯实例已实例化完成，AutoStart=true 的也已连接/监听）。
        /// </summary>
        protected abstract void RegisterHardwareFactories(IHardwareManagerService hwManager, ICommunicationManagerService commManager);

        /// <summary>
        /// 向 CommunicationManagerService 注册项目特有的通讯实例工厂。
        /// 硬件工厂如需引用某个通讯实例，可在 RegisterHardwareFactories 的闭包里
        /// 通过 ICommunicationManagerService.GetCommunication&lt;T&gt; 按 InstanceId 查找——
        /// 因为 RegisterCommunicationTypes 固定先于 RegisterHardwareTypes 执行，届时通讯管理服务已可解析。
        /// </summary>
        protected abstract void RegisterCommunicationFactories(ICommunicationManagerService commManager);

        /// <summary>注册项目特有的机构、工站和主控（包括 IPanelIoConfig、ITowerLightDoWriterConfig）</summary>
        protected abstract void RegisterMechanismsAndStations(IContainerRegistry containerRegistry);

        /// <summary>注册项目特有的配方服务</summary>
        protected abstract void RegisterRecipes(IContainerRegistry containerRegistry);

        /// <summary>按顺序初始化项目机构，返回 true 表示全部成功</summary>
        protected abstract Task<bool> InitializeMechanismsAsync(IProgress<SplashProgressPayload>? progress = null);


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

        /// <summary>
        /// 校验 PF.SecsGem.Service 的项目绑定与主程序是否一致。未部署该服务的项目自动跳过。
        ///
        /// 为什么校验放在主程序而不是服务里：
        /// <list type="number">
        /// <item>服务运行在 Session 0（Vista 之后的会话隔离），弹不出对话框；且 start= auto
        /// 开机自启时可能根本没有用户登录。</item>
        /// <item>服务只能发现"没配 ProjectName"，发现不了"配错了值"——它没有参照物。
        /// 主程序两种故障都能查出，后者更隐蔽：服务照常启动、照常运行，只是读了
        /// 另一个项目的 SecsGemConfig.db。</item>
        /// </list>
        ///
        /// 一机多项目场景：服务全机唯一（本机通道端口 6800 两端硬编码），归属最后安装的项目。
        /// 因此这里解析的是 SCM 中<b>实际注册</b>的 ImagePath 而非本项目安装目录，
        /// 才能查出"当前生效的服务是别的项目装的"。
        ///
        /// 只告警不阻断：产线上因配置不一致导致软件起不来，代价比让人看见告警更大，
        /// 日志里留证即可。
        /// </summary>
        protected virtual void VerifySecsServiceProjectBinding(Action<string, MsgType> updateStage)
        {
            try
            {
                var exePath = ServicePathResolver.ResolveSecsServiceExePath();
                if (!File.Exists(exePath)) return;            // 项目未部署该服务，静默跳过

                var settingsPath = ServicePathResolver.GetSecsServiceSettingsPath(exePath);
                var svcProject   = ServicePathResolver.TryReadServiceProjectName(settingsPath);

                if (string.Equals(svcProject, ConstGlobalParam.ProjectName, StringComparison.Ordinal))
                    return;

                var detail = svcProject is null
                    ? "当前部署的 SECS/GEM 服务未配置项目名，服务将拒绝启动。"
                    : $"当前注册的 SECS/GEM 服务属于项目「{svcProject}」，而本程序是「{ConstGlobalParam.ProjectName}」。" +
                      "服务会读取另一个项目的 SecsGemConfig.db（设备号、Host 地址、CEID/VID 均不同）。";

                var msg = $"{detail}\n\n服务位置：{settingsPath}\n" +
                          "处理方式：重新运行本项目的安装包以接管服务，或联系工程人员修正后重启服务。";

                LogService?.Error(msg, "Startup");
                updateStage("SECS/GEM 服务项目绑定校验未通过", MsgType.Warning);
                Container.Resolve<IMessageService>().ShowMessage(
                    msg, "配置校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                // 校验本身失败不得阻断启动
                LogService?.Warn($"SECS/GEM 服务项目绑定校验异常：{ex.Message}", "Startup");
            }
        }

        #endregion

        #region 单实例保护

        /// <summary>单实例保护：检测到重复实例时提示并退出，否则注册 DispatcherUnhandledException 处理器。</summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                if (RunningInstance())
                {
                    base.OnStartup(e);
                    this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                    // 兜底非 UI 线程异常：后台 Task、Timer 回调、AppDomain 级异常。
                    // DispatcherUnhandledException 仅捕获 UI 线程，以下两者覆盖其余线程，
                    // 避免进程静默崩溃而无任何日志/提示。
                    AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
                    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
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

        /// <summary>释放全局互斥锁并执行基类退出逻辑。</summary>
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

        /// <summary>
        /// 捕获所有非 UI 线程的未处理异常（后台 Task 抛出且未被观察、native 回调等）。
        /// IsTerminating 恒为 true（CLR 规范），异常后进程必然终止，此处尽力留下日志。
        /// </summary>
        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                PF.Services.Logging.LogService.Instance?.Fatal(
                    $"[AppDomain 未处理异常] IsTerminating={e.IsTerminating}。" +
                    $"{(ex != null ? $"{ex.Message}\r\n堆栈: {ex.StackTrace}" : $"ExceptionObject: {e.ExceptionObject}")}",
                    "AppDomain");
            }
            catch { /* 日志组件自身异常不得在此再抛，否则掩盖原始错误 */ }
        }

        /// <summary>
        /// 捕获未被观察的 Task 异常（Task 抛异常且无 await/ContinueWith 观察）。
        /// 调用 SetObserved 阻止该异常在 GC 终结时升级为进程崩溃。
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                PF.Services.Logging.LogService.Instance?.Fatal(
                    $"[未观察的 Task 异常] {e.Exception?.Message}\r\n堆栈: {e.Exception?.StackTrace}",
                    "TaskScheduler");
            }
            catch { /* 同上 */ }
            e.SetObserved();
        }

        #endregion

        #region Prism 核心方法

        /// <summary>创建并显示 Splash 加载窗口，完成后返回 MainWindow 作为主 Shell。</summary>
        protected override Window CreateShell()
        {
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var commonParam = Container.Resolve<CommonSettings>();
            ApplyConfiguration(commonParam.Skin);

            var splash = Container.Resolve<Splash>();
            splash.WelcomeText = $"欢迎使用{commonParam.SoftWareName}";
            splash.WelcomeText_small = $"Welcome to the {commonParam.SoftWareName_EN}";
            splash.VersionNumber = $"V{Assembly.GetEntryAssembly()?.GetName().Version}";
            var str = Assembly.GetEntryAssembly()?.GetName();
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

        /// <summary>注册导航菜单、桥接 Prism EA 事件、完成权限初始化并启动定时服务。</summary>
        protected override async void OnInitialized()
        {
            // async void：Prism 的 OnInitialized 契约为 void，无法改 Task 返回。
            // async void 的异常无法被 Prism 捕获，故全程 try/catch 兜底防崩溃。
            try
            {
            var navMenuService = Container.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetEntryAssembly());
            navMenuService.RegisterAssembly(typeof(PFApplicationBase).Assembly);


            PermissionHelper.Initialize(navMenuService);

            var authService = Container.Resolve<IUserService>();
            authService.ResetToOperator();
            // 原 .GetAwaiter().GetResult() 同步阻塞 UI 线程（WPF SynchronizationContext 死锁风险），改 await。
            await authService.LoginAsync("SuperUser", DateTime.Now.ToString("yyyyMMddHH00"));

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
            catch (Exception ex)
            {
                // async void 异常兜底：记录日志防静默崩溃（Prism 无法捕获 async void 的异常）。
                Debug.WriteLine($"[OnInitialized] 初始化阶段异常: {ex.Message}");
                PF.Services.Logging.LogService.Instance?.Error(
                    $"初始化阶段异常: {ex.Message}", "Startup", ex);
            }
        }

        /// <summary>注册全部框架公共服务（日志、参数、生产数据、硬件、报警、定时器、视觉等）及子类项目服务。</summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var commonSettings = CommonSettings.Load();
            if (!File.Exists(CommonSettings.ConfigFilePath))
                commonSettings.Save();
            containerRegistry.RegisterInstance<CommonSettings>(commonSettings);
            containerRegistry.RegisterForNavigation<CommonParamView, BaseParamsViewModel>(
               NavigationConstants.Views.CommonParamView);
            containerRegistry.RegisterForNavigation<MainView, MainViewModel>(
               NavigationConstants.Views.MainView);

            containerRegistry.AddLogging();
            LogService = containerRegistry.GetContainer().Resolve<ILogService>();
            UILogger = CategoryLoggerFactory.UI(LogService);
            _dbLogger = CategoryLoggerFactory.Database(LogService);

            RegisterParamDbContext(containerRegistry);
            containerRegistry.AddParameterServices(CreateDefaultParameters());

            RegisterProductionDataService(containerRegistry);
            // 门开在调用点而不是方法体内：子类整体重写 RegisterSecsGemServices 时，
            // UsesSecsGemService=false 依然能保证一行 SECS/GEM 注册都不执行
            if (UsesSecsGemService)
            {
                RegisterSecsGemServices(containerRegistry);
            }
            // 必须先于 RegisterHardwareTypes：硬件工厂闭包可能要捕获 ICommunicationManagerService 引用
            RegisterCommunicationTypes(containerRegistry);
            RegisterHardwareTypes(containerRegistry);

            containerRegistry.RegisterSingleton<Splash>();
            containerRegistry.RegisterDialogWindow<PFDialogBaseWindow>();
            containerRegistry.RegisterSingleton<INavigationMenuService, NavigationMenuService>();


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

        /// <summary>
        /// 创建 ModuleCatalog，由 ConfigureModuleCatalog 通过 AddModule&lt;T&gt;() 显式注册模块。
        /// <see cref="UsesSecsGemService"/> 为 false 时返回会拦截 SECS/GEM 模块的目录，
        /// 这样消费项目不必为了关闭 SECS/GEM 而去改自己的 ConfigureModuleCatalog。
        /// </summary>
        protected override IModuleCatalog CreateModuleCatalog()
            => new SecsGemAwareModuleCatalog(this);

        /// <summary>
        /// 按 <see cref="UsesSecsGemService"/> 过滤 SECS/GEM 模块的模块目录。
        ///
        /// 之所以在目录层拦而不是让各项目自己少写一行 AddModule：SECS/GEM 模块启动时要解析
        /// ISecsGemManager 等服务，而关闭开关后这些服务根本没注册，模块一旦被加载就是
        /// 解析失败崩在启动阶段。与其依赖每个项目都记得同时改两处，不如在这里兜住。
        ///
        /// 用命名空间前缀而非类型引用来识别：PF.Application.Base 不引用 PF.Modules.SecsGem，
        /// 硬引一个 UI 模块会把分层反过来。
        /// </summary>
        private sealed class SecsGemAwareModuleCatalog : ModuleCatalog
        {
            /// <summary>
            /// SECS/GEM 模块的命名空间前缀。带末尾的点，避免误伤将来可能出现的
            /// PF.Modules.SecsGemXxx 这类同前缀但不同命名空间的模块。
            /// </summary>
            private const string SecsGemModuleNamespacePrefix = "PF.Modules.SecsGem.";

            private readonly PFApplicationBase _owner;

            public SecsGemAwareModuleCatalog(PFApplicationBase owner) => _owner = owner;

            /// <summary>加入模块。开关关闭时直接拦下 SECS/GEM 模块，不进目录。</summary>
            public override IModuleCatalog AddModule(IModuleInfo moduleInfo)
            {
                if (!_owner.UsesSecsGemService && IsSecsGemModule(moduleInfo))
                {
                    LogSkipped(moduleInfo);
                    return this;
                }
                return base.AddModule(moduleInfo);
            }

            /// <summary>
            /// 目录初始化。ModuleManager 加载模块前必定先调这里，因此在这一步再清扫一次，
            /// 作为 <see cref="AddModule"/> 那道拦截的兜底——Prism 的 AddModule&lt;T&gt;() 等重载
            /// 将来若不再经由本方法收口，这里仍能保证 SECS/GEM 模块不会被加载。
            /// </summary>
            public override void Initialize()
            {
                if (!_owner.UsesSecsGemService)
                {
                    var leaked = Items.OfType<IModuleInfo>().Where(IsSecsGemModule).ToList();
                    foreach (var moduleInfo in leaked)
                    {
                        LogSkipped(moduleInfo);
                        Items.Remove(moduleInfo);
                    }
                }
                base.Initialize();
            }

            private void LogSkipped(IModuleInfo moduleInfo) =>
                _owner.LogService?.Info(
                    $"[启动] UsesSecsGemService=false，已跳过 SECS/GEM 模块 '{moduleInfo?.ModuleName}'",
                    "Startup");

            private static bool IsSecsGemModule(IModuleInfo moduleInfo)
            {
                // ModuleType 是程序集限定名字符串，形如
                // "PF.Modules.SecsGem.SecsGemModule, PF.Modules.SecsGem, Version=..."
                var moduleType = moduleInfo?.ModuleType;
                return !string.IsNullOrEmpty(moduleType)
                       && moduleType.StartsWith(SecsGemModuleNamespacePrefix, StringComparison.Ordinal);
            }
        }

        /// <summary>基类默认为空实现，子类 override 后调用 AddModule&lt;T&gt;() 注册项目所需 Prism 模块。</summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog) { }

        #endregion

        #region 公共方法（供 MainWindow.xaml.cs 调用）

        /// <summary>切换应用程序皮肤主题，同时刷新主题资源字典。</summary>
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

            var commManager = container.Resolve<ICommunicationManagerService>();
            var hwManager = new HardwareManagerService(LogService, paramService);
            RegisterHardwareFactories(hwManager, commManager);

            containerRegistry.RegisterSingleton<IIOMappingService, IOMappingService>();
            var ioMappingService = container.Resolve<IIOMappingService>();
            RegisterIOMappings(ioMappingService);

            containerRegistry.RegisterInstance<IHardwareManagerService>(hwManager);
        }

        /// <summary>
        /// 注册通讯实例管理服务：结构对齐 RegisterHardwareTypes，但通讯实例之间没有硬件那种父子拓扑，
        /// CommunicationManagerService 内部加载流程更简单。
        /// </summary>
        private void RegisterCommunicationTypes(IContainerRegistry containerRegistry)
        {
            var container = containerRegistry.GetContainer();
            var paramService = container.Resolve<IParamService>();
            paramService.RegisterParamType<CommunicationParam, CommunicationConfig>();

            var commManager = new CommunicationManagerService(LogService, paramService);
            RegisterCommunicationFactories(commManager);

            containerRegistry.RegisterInstance<ICommunicationManagerService>(commManager);
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
            var commonParam = Container.Resolve<CommonSettings>();

            Splash splash = Container.Resolve<Splash>();
            splash.Progress = 0;
            ILogService logService = Container.Resolve<ILogService>();

            SplashMessageThrottler? throttler = null;
            IProgress<SplashProgressPayload>? splashProgress = null;
            if (commonParam.EnableDetailedLog)
            {
                // 详细日志模式下把突发的 Report() 消息节流显示，避免加载过快时界面来不及渲染；
                // 未启用时 splashProgress 为 null，底层服务不会调用 Report，加载速度不受影响。
                throttler = new SplashMessageThrottler(payload =>
                    SplashUpdateMessage(splash, logService, payload.Status, payload.Category, payload.MsgType.ToString()));
                splashProgress = new Progress<SplashProgressPayload>(throttler.Enqueue);
            }

            // 阶段横幅消息统一走这里：详细日志模式下和底层 Report() 的逐条消息共用同一条
            // FIFO 队列，保证显示顺序严格等于事件发生顺序，不会被队列里滞后的旧消息覆盖；
            // 未启用详细日志时退化为直接同步显示，不引入任何额外延迟。
            void UpdateStage(string status, MsgType msgType = MsgType.Info, string category = "Splash")
            {
                if (throttler != null)
                {
                    // SplashProgressPayload.MsgType 是 PF.Core.Enums.MsgType，与本文件 using 别名的
                    // PF.UI.Shared.Data.MsgType 是两个不同的枚举类型（仅成员名相同），按名称转换。
                    var payloadMsgType = Enum.Parse<PF.Core.Enums.MsgType>(msgType.ToString());
                    throttler.Enqueue(new SplashProgressPayload { Status = status, Category = category, MsgType = payloadMsgType });
                }
                else
                {
                    SplashUpdateMessage(splash, logService, status, category, msgType);
                }
            }

            // 进度条动画完全在外部（消费侧）实现，不依赖控件库改动：ProgressProperty 是
            // Splash 上已公开的依赖属性，直接对它 BeginAnimation 即可从当前值平滑插值到
            // 目标值，避免整百分比跳变显得生硬。
            // 时长必须明显小于两次调用之间的实际间隔（各阶段 Task.Delay 通常 300~500ms），
            // 否则上一段动画还没跑完就被下一次调用打断重启，视觉上会变成"卡住不动再跳变"，
            // 而不是真正的丝滑过渡。
            void AnimateProgress(double target)
            {
                splash.BeginAnimation(Splash.ProgressProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(4000))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
            }

            // 段落阻塞：等当前阶段队列里的消息全部按节奏显示完，再放行下一阶段的实际初始化工作
            // 和它自己的消息上报。避免不同阶段的消息挤在同一个队列里越堆越多，
            // 也避免只在最后 drain 一次时，恰好积压较多导致那一下等待时间明显变长、像是卡死。
            async Task DrainStage()
            {
                if (throttler != null)
                {
                    await throttler.DrainAsync();
                }
            }

            AnimateProgress(0);
            UpdateStage("程序加载中。。。");
            try
            {
                if (UsesSecsGemService)
                {
                    VerifySecsServiceProjectBinding((status, msgType) => UpdateStage(status, msgType));
                }
                else
                {
                    await Task.Delay(500);
                    UpdateStage("本项目不使用SecsGem服务，跳过服务检查。。。");
                }

                await Task.Delay(500);
                UpdateStage("配置文件加载中。。。");
                var configLoaded = await LoadConfigurationAsync();
                if (!configLoaded)
                {
                    UpdateStage("配置文件加载失败", MsgType.Error);
                    loadErr = true;
                    return false;
                }
                UpdateStage("配置文件加载成功。。。", MsgType.Success);
               
                await DrainStage();
                AnimateProgress(15);

                UpdateStage("通讯实例初始化中。。。");
              
                var commManager = Container.Resolve<ICommunicationManagerService>();
                await commManager.LoadAndInitializeAsync(splashProgress);
                UpdateStage("通讯实例初始化完成", MsgType.Success);
               
                await DrainStage();
              
                AnimateProgress(35);

                UpdateStage("硬件设备初始化中。。。");
               
                var hwManager = Container.Resolve<IHardwareManagerService>();
                await hwManager.LoadAndInitializeAsync(splashProgress);
                UpdateStage("硬件设备初始化完成", MsgType.Success);
               
                await DrainStage();
               
                AnimateProgress(60);

                UpdateStage("模组初始化中。。。");
              
                if (await InitializeMechanismsAsync(splashProgress))
                {
                    UpdateStage("模组初始化完成！", MsgType.Success);
                }
                else
                {
                    UpdateStage("模组初始化失败！", MsgType.Error);
                    loadErr = true;
                }
                await DrainStage();
                AnimateProgress(85);

              
                if (!loadErr)
                {
                    UpdateStage("软件初始化成功！", MsgType.Success);
                    
                }
                else
                {
                    UpdateStage("软件初始化失败！", MsgType.Error);
                }
                await DrainStage();
                AnimateProgress(100);
                
                return !loadErr;
            }
            catch (Exception ex)
            {
                UpdateStage($"初始化过程中发生错误: {ex.Message}", MsgType.Error);
                return false;
            }
            finally
            {
                await Task.Delay(4000);
                // 等待节流队列剩余消息按节奏显示完，避免实际加载速度快于消息显示节奏时
                // Splash 提前关闭、剩余消息只能在 Splash 关闭后才继续写入日志面板。
                if (throttler != null)
                {
                    await throttler.DrainAsync();
                }
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

        /// <summary>
        /// 仅供详细日志模式使用：把短时间内突发的 Progress 消息排队，按固定节奏逐条显示，
        /// 避免 Report() 密集调用时界面来不及渲染、只能看到最后几条。
        /// Enqueue 本身不阻塞调用方（底层加载线程/UI线程），显示节奏与实际加载耗时解耦，
        /// 因此不会拖慢 EnableDetailedLog=false 时的整体加载速度。
        /// </summary>
        private sealed class SplashMessageThrottler
        {
            private const int DisplayIntervalMs = 500;

            private readonly Action<SplashProgressPayload> _display;
            private readonly Queue<SplashProgressPayload> _pending = new();
            private Task _pumpTask = Task.CompletedTask;

            public SplashMessageThrottler(Action<SplashProgressPayload> display) => _display = display;

            public void Enqueue(SplashProgressPayload payload)
            {
                _pending.Enqueue(payload);
                if (_pumpTask.IsCompleted)
                {
                    _pumpTask = PumpAsync();
                }
            }

            /// <summary>等待队列中剩余消息按节奏显示完毕；队列已空时立即返回。</summary>
            public Task DrainAsync() => _pumpTask;

            private async Task PumpAsync()
            {
                while (_pending.Count > 0)
                {
                    _display(_pending.Dequeue());
                    await Task.Delay(DisplayIntervalMs);
                }
            }
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
