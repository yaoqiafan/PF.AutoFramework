using Microsoft.EntityFrameworkCore;
using PF.Core.Constants;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.SecsGem.DataBase;

namespace PF.SecsGem.Service;

public class Program
{
    private static readonly string filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        // 创建数据库
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecsGemDbContext>();
            dbContext?.Database.EnsureCreated();
        }
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "SecsGemService";
            })
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureDatabase(services);
                // 注册Worker作为后台服务
                services.AddHostedService<Worker>();
            })
            .ConfigureLogging((context, logging) =>
            {
                // 配置日志，包括事件日志
                logging.ClearProviders();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));

                if (OperatingSystem.IsWindows())
                {
                    logging.AddEventLog(settings =>
                    {
                        settings.SourceName = "SecsGemService";
                        settings.LogName = "Application";
                    });
                }

                logging.AddConsole();
            });


    private static void ConfigureDatabase(IServiceCollection services)
    {
        services.AddScoped<ISecsGemDataBase, SecsGemDataBaseManger>();
        services.AddDbContext<SecsGemDbContext>(options =>
            options.UseSqlite($"Data Source = {filePath}"));
    }


}

