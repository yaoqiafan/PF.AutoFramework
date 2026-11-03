using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Infrastructure.Hardware.Camera.Network.Hikvision;

namespace HikCameraMinDemo;

/// <summary>
/// 海康网络摄像机（HikNetworkCamera）最小功能 Demo：
/// 只测「连接 → 设备信息 → 抓图到内存 → 抓图落盘 → 断开」五步。
///
/// 用法：
///   HikCameraMinDemo --sim                 仿真模式（无硬件跑通全链路，合成 JPEG）
///   HikCameraMinDemo --ip 192.168.1.64 --port 8000 --user admin --pwd xxx [--ch 0] [--buf 8]
///                                          真机模式；不传 --ip 时连 127.0.0.1:8000，
///                                          在没有设备的机器上预期"登录失败"——
///                                          这本身就是 SDK 加载链路是否完整的验证：
///                                          原生库缺失时看到的是 DllNotFoundException /
///                                          Init 失败 / 进程崩溃，而不是干净的登录失败。
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        bool sim = Array.IndexOf(args, "--sim") >= 0;
        string ip = ArgValue(args, "--ip") ?? "127.0.0.1";
        int port = int.TryParse(ArgValue(args, "--port"), out var p) ? p : 8000;
        string user = ArgValue(args, "--user") ?? "admin";
        string pwd = ArgValue(args, "--pwd") ?? "admin12345";
        int channel = int.TryParse(ArgValue(args, "--ch"), out var c) ? c : 0;
        int bufferMb = int.TryParse(ArgValue(args, "--buf"), out var b) ? b : 8;

        Console.WriteLine($"进程 x64 = {Environment.Is64BitProcess}，模式 = {(sim ? "仿真" : $"真机 {ip}:{port}")}");

        // BaseNetworkCamera 构造时会用 ConfigPath 推导默认存图目录，
        // 必须先初始化，否则抛 InvalidOperationException（框架刻意的防呆）。
        ConstGlobalParam.Initialize("HikCameraMinDemo");

        var logger = new ConsoleLogService();
        using var camera = new HikNetworkCamera(ip, port, user, pwd, channel, bufferMb,
            "NetworkCamera_0", "最小Demo相机", sim, logger);

        try
        {
            // ── ① 连接（BaseDevice 内部 3 次重试、间隔 2s；失败会经 ILogService 报错） ──
            bool ok = await camera.ConnectAsync();
            Console.WriteLine($"① 连接结果 = {ok}（IsConnected = {camera.IsConnected}）");
            if (!ok)
                return 2;

            // ── ② 登录后的设备信息 ──
            Console.WriteLine($"② 序列号 = {camera.SerialNumber}");
            Console.WriteLine($"   抓图通道 = {camera.ActiveChannel}，设备通道 {camera.Channels.Count} 个");
            foreach (var ch in camera.Channels)
                Console.WriteLine($"     通道 {ch.ChannelNo,-3} {ch.Name}{(ch.IsIpChannel ? " [IP]" : "")}");

            // ── ③ 抓图到内存（真机走 NET_DVR_CaptureJPEGPicture_NEW；仿真合成 JPEG） ──
            NetworkCameraFrame frame = await camera.CaptureJpegAsync();
            string local = Path.Combine(AppContext.BaseDirectory, $"demo_{DateTime.Now:HHmmss_fff}.jpg");
            await File.WriteAllBytesAsync(local, frame.Data);
            Console.WriteLine($"③ 抓图 {frame.Data.Length} 字节（通道 {frame.Channel}）→ {local}");

            // ── ④ 抓图并由框架落盘（目录默认 {ConfigPath}\\NetworkCamera\\{DeviceId}） ──
            string saved = await camera.CaptureAndSaveAsync();
            Console.WriteLine($"④ 框架落盘 → {saved}");

            // ── ⑤ 断开（登出 + SDK 引用计数归零 → NET_DVR_Cleanup） ──
            await camera.DisconnectAsync();
            Console.WriteLine($"⑤ 断开完成（IsConnected = {camera.IsConnected}）");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static string ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
