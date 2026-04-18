using Microsoft.EntityFrameworkCore;
using PF.Core.Constants;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.SecsGem.DataBase;

namespace PF.SecsGem.Service;

/// <summary>
/// Program 主入口类
/// </summary>
public class Program
{
    private static readonly string filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");
    /// <summary>
    /// 程序主入口
    /// </summary>
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        // �������ݿ�
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecsGemDbContext>();
            dbContext?.Database.EnsureCreated();
        }
        host.Run();
    }

    /// <summary>
    /// 创建主机构建器
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "SecsGemService";
            })
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureDatabase(services);
                // ע��Worker��Ϊ��̨����
                services.AddHostedService<Worker>();
            })
            .ConfigureLogging((context, logging) =>
            {
                // ������־�������¼���־
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

