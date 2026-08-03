using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PF.Core.Constants;
using PF.Core.Entities.Configuration;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.SecsGem.DataBase;
using PF.Services.Logging;

namespace PF.SecsGem.Service;

/// <summary>
/// Program 主入口类
/// </summary>
public class Program
{
    /// <summary>
    /// 服务注册名。全机唯一，不按项目区分——本服务的本机通道端口 6800 在两端都是硬编码的
    /// （Worker.LocationServer 与 PF.Infrastructure 的 InternalClient），HSMS 被动端口也只有一个，
    /// 因此一台机器上同时只可能有一个实例真正工作。若按项目注册多个 start= auto 的服务，
    /// 开机时它们会争抢 6800，谁抢到不确定，反而使"当前生效的是哪个项目的配置"变得不可预测。
    /// 一机多项目时由安装包覆盖注册（sc delete + sc create），始终指向最后安装的那个项目；
    /// 主程序启动时会校验该服务是否属于自己（VerifySecsServiceProjectBinding）。
    /// </summary>
    private const string ServiceName = "SecsGemService";

    /// <summary>服务启动失败日志目录（与 <see cref="CreateServiceLogService"/> 的 BasePath 一致）。</summary>
    private const string ServiceLogBasePath = "D://PF_Logs/SecsGem/Service";

    // 必须是属性而非静态只读字段：ConfigPath 依赖 Main 中设置的项目名，
    // 静态字段初始化器会在 ConstGlobalParam.Initialize 之前求值并抛 TypeInitializationException。
    private static string filePath => Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");

    /// <summary>
    /// 程序主入口
    /// </summary>
    public static void Main(string[] args)
    {
        // 本服务是独立进程，与主程序没有父子关系（start= auto，开机自启时主程序可能尚未运行），
        // 因此项目名只能来自安装期写入的配置。缺失时必须拒绝启动，详见 FailFast 注释。
        var projectName = ReadProjectName(args);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            FailFast(
                $"未能读取到项目名（appsettings.json 的 ProjectName 键）。" +
                $"服务无法确定 SecsGemConfig.db 所在的项目配置目录，拒绝启动。" +
                $"配置文件应位于：{Path.Combine(AppContext.BaseDirectory, "appsettings.json")}");
            return;
        }

        try
        {
            ConstGlobalParam.Initialize(projectName);
        }
        catch (Exception ex)
        {
            FailFast($"项目名 [{projectName}] 初始化失败：{ex.Message}");
            return;
        }

        var host = CreateHostBuilder(args).Build();
        // 确保数据库已创建
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecsGemDbContext>();
            dbContext?.Database.EnsureCreated();
        }
        host.Run();
    }

    /// <summary>
    /// 在 Host 构建之前读取项目名。
    /// 独立于 Host 的配置管道，因为 <see cref="ConstGlobalParam.Initialize"/> 必须先于
    /// 任何触碰 ConfigPath 的代码执行。
    ///
    /// 配置源优先级（后者覆盖前者）：appsettings.json → 环境变量 → 命令行。
    /// 因此现场排查时可用 <c>--ProjectName=Foo</c> 或服务级环境变量临时覆盖，无需改文件。
    /// </summary>
    private static string? ReadProjectName(string[] args)
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            return config["ProjectName"];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 记录启动致命错误并以非零退出码结束进程。
    ///
    /// 为什么是拒绝启动而不是回退到共享根目录：各设备的 SecsGemConfig.db 内容不同
    /// （设备号、Host IP、CEID/VID 定义），回退意味着服务打开一个空库、EnsureCreated
    /// 建出默认结构、再用空配置与 Host 建链——从外部看服务"正常运行"，实际配置全错。
    /// 这种静默错误在 SEMI 联机场景比服务起不来危险得多。服务停止在服务管理器和
    /// 主程序 SECS 面板上都是显性可见的。
    ///
    /// 双写留证：EventLog 尽力而为（Source 可能尚未注册），文件日志兜底（无需注册）。
    /// </summary>
    private static void FailFast(string message)
    {
        var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] {message}";

        try
        {
            Directory.CreateDirectory(ServiceLogBasePath);
            // 显式带 BOM 的 UTF-8：现场多为中文 Windows，无 BOM 时记事本和
            // Windows PowerShell 会按 GBK 解码，日志里的中文全是乱码——
            // 而这个文件正是服务起不来时唯一的排查依据。
            File.AppendAllText(
                Path.Combine(ServiceLogBasePath, "startup-error.log"),
                text + Environment.NewLine,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch { /* 落盘失败不得掩盖原始错误 */ }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                System.Diagnostics.EventLog.WriteEntry(
                    ServiceName, text, System.Diagnostics.EventLogEntryType.Error);
            }
            catch { /* Source 未注册或无权限，已有文件日志兜底 */ }
        }

        Console.Error.WriteLine(text);
        Environment.Exit(1);
    }

    /// <summary>
    /// 创建主机构建器
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = ServiceName;
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<ILogService>(_ => CreateServiceLogService());
                ConfigureDatabase(services);
                // 注册Worker作为后台服务
                services.AddHostedService<Worker>();
            })
            .ConfigureLogging((context, logging) =>
            {
                // 配置日志：清空默认提供器后按配置重建
                logging.ClearProviders();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));

                if (OperatingSystem.IsWindows())
                {
                    logging.AddEventLog(settings =>
                    {
                        settings.SourceName = ServiceName;
                        settings.LogName = "Application";
                    });
                }

                logging.AddConsole();
            });

    private static ILogService CreateServiceLogService()
    {
        var config = new LogConfiguration
        {
            BasePath               = ServiceLogBasePath,
            HistoricalLogPath      = ServiceLogBasePath,
            EnableConsoleLogging   = true,
            EnableFileLogging      = true,
            EnableUiLogging        = false,
            MinimumLevel           = PF.Core.Enums.LogLevel.Debug,
            AutoDeleteLogs         = true,
            AutoDeleteIntervalDays = 30,
            SplitByHour            = false
        };
        config.AddCategory(LogCategories.Communication, PF.Core.Enums.LogLevel.Debug, LogCategories.Communication);
        return new LogService(config);
    }

    private static void ConfigureDatabase(IServiceCollection services)
    {
        services.AddScoped<ISecsGemDataBase, SecsGemDataBaseManger>();
        // 工厂委托：到 Main 中解析 SecsGemDbContext 时才求值，此时 Initialize 已完成。
        services.AddDbContext<SecsGemDbContext>(options =>
            options.UseSqlite($"Data Source = {filePath}"));
    }
}
