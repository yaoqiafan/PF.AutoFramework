using PF.Core.Events;
using PF.Infrastructure.Communication.TCP;
using PF.Infrastructure.Logging;

namespace PF.SecsGem.Service
{
    /// <summary>
    /// 面向 Host 的 HSMS 监听生命周期。
    ///
    /// <para>与 <c>LocationServer</c>（本机回环通道，随服务进程常驻、从不关闭）不同，
    /// 本监听按需开关：主程序接入回环通道时开启，断开时关闭。</para>
    ///
    /// <para><b>为什么把开关权交给回环连接</b>：主程序不在时（正常退出、崩溃、被任务管理器结束），
    /// 设备应当对 Host 不可见——否则 Host 连得上却无人应答报文，只能干等 T3 超时。
    /// 而 TCP 断开由操作系统保证投递，比心跳或"退出前发条通知"都可靠：进程被强杀一样会触发。
    /// 于是正常退出与异常死亡走同一条代码路径，不存在"优雅路径写对了、异常路径漏了"的缺口。
    /// 相比"退出时停止 Windows 服务"的做法，这条路还额外省掉了管理员权限。</para>
    /// </summary>
    internal sealed class HsmsListener : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CategoryLogger _log;
        private TcpServer? _server;

        /// <summary>构造 HSMS 监听管理器。</summary>
        public HsmsListener(CategoryLogger log) => _log = log;

        /// <summary>当前监听实例；未监听时为 <c>null</c>。</summary>
        public TcpServer? Server => _server;

        /// <summary>是否正在监听。</summary>
        public bool IsListening => _server != null;

        // 事件对外转发而非直接暴露 TcpServer：订阅方只在装配时订阅一次，
        // 内部 TcpServer 实例的反复创建与销毁对外完全透明。
        // 这也是 StopAsync 能安全摘除内部订阅（见下）而不影响外部订阅方的前提。

        /// <summary>主机接入。</summary>
        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

        /// <summary>主机断开。</summary>
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        /// <summary>收到主机数据。</summary>
        public event EventHandler<DataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// 开启监听。已在监听时为幂等空操作。
        /// </summary>
        /// <param name="ip">绑定地址</param>
        /// <param name="port">绑定端口</param>
        /// <returns>true=已在监听/开启成功</returns>
        public async Task<bool> StartAsync(string ip, int port)
        {
            await _gate.WaitAsync();
            try
            {
                if (_server != null)
                {
                    _log.Info($"HSMS 监听已开启，忽略重复开启请求（{ip}:{port}）");
                    return true;
                }

                var server = new TcpServer("SecsGem服务器");
                server.ClientConnected += OnClientConnected;
                server.ClientDisconnected += OnClientDisconnected;
                server.DataReceived += OnDataReceived;

                if (!await server.StartAsync(ip, port))
                {
                    // 常见原因：端口被上一次未正常释放的实例占用，或配置的 IP 不属于本机任一网卡
                    _log.Error($"HSMS 监听启动失败：{ip}:{port}");
                    server.ClientConnected -= OnClientConnected;
                    server.ClientDisconnected -= OnClientDisconnected;
                    server.DataReceived -= OnDataReceived;
                    server.Dispose();
                    return false;
                }

                _server = server;
                _log.Info($"HSMS 监听已开启：{ip}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"开启 HSMS 监听时发生错误：{ip}:{port}", ex);
                return false;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// 关闭监听并断开在连主机。未监听时为幂等空操作。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志以便现场区分"我方主动关闭"与"主机异常断开"。</param>
        public async Task StopAsync(string reason)
        {
            await _gate.WaitAsync();
            try
            {
                var server = _server;
                if (server == null) return;
                _server = null;

                // 必须先摘除内部订阅再 StopAsync：TcpServer.StopAsync 会逐个断开在连客户端，
                // 每断一个都触发 ClientDisconnected。此时订阅若仍在，本次"我方主动关闭"会被
                // 转发出去、记成"主机异常断线"，日志与告警都会误导现场排查。
                server.ClientConnected -= OnClientConnected;
                server.ClientDisconnected -= OnClientDisconnected;
                server.DataReceived -= OnDataReceived;

                await server.StopAsync();
                server.Dispose();

                _log.Info($"HSMS 监听已关闭（{reason}）");
            }
            catch (Exception ex)
            {
                _log.Error($"关闭 HSMS 监听时发生错误（{reason}）", ex);
            }
            finally
            {
                _gate.Release();
            }
        }

        private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
            => ClientConnected?.Invoke(this, e);

        private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
            => ClientDisconnected?.Invoke(this, e);

        private void OnDataReceived(object? sender, DataReceivedEventArgs e)
            => DataReceived?.Invoke(this, e);

        /// <summary>释放资源，顺带关闭仍在运行的监听。</summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync("服务停止");
            _gate.Dispose();
        }
    }
}
