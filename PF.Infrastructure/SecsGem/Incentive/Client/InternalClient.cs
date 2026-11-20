using PF.Core.Constants;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication.TCP;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.Communication.TCP;
using PF.Infrastructure.Logging;
using PF.Infrastructure.SecsGem.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PF.Infrastructure.SecsGem.Incentive
{
    /// <summary>
    /// SecsGem内部客户端
    /// </summary>
    public class InternalClient : IinternalClient
    {
        private readonly IClient _client;
        private readonly IParams _paramConfig;
        private readonly ICommandManager _commandManager;
        private readonly SecsGemMessageProcessor _secsGemMessageProcessor;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private byte[] _deviceId;
        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// 接收到SecsdGem主机连接时触发
        /// </summary>
        public event EventHandler SecsGemHostConnected;

        /// <summary>
        /// 接收到SecsdGem主机断开连接时触发
        /// </summary>
        public event EventHandler SecsGemHostDisconnected;

        /// <summary>握手状态变化时触发</summary>
        public event EventHandler ServiceHandshakeChanged;

        /// <summary>收到服务下发的链路配置后触发</summary>
        public event EventHandler LinkConfigReceived;

        // 状态管理
        private bool _status = false;

        // 本机通道握手（见 SecsGemLocalChannel）。_connectionGeneration 每次连接 / 断开加一，
        // 让迟到的握手超时判定不会作用到新的一次连接上
        private volatile SecsGemServiceHandshake _handshake = SecsGemServiceHandshake.Pending;
        private volatile SecsGemLocalHello? _serviceHello;
        private int _connectionGeneration;

        /// <summary>与本机 SECS 服务的协议握手状态</summary>
        public SecsGemServiceHandshake ServiceHandshake => _handshake;

        /// <summary>服务最近一次发来的握手内容</summary>
        public SecsGemLocalHello? ServiceHello => _serviceHello;

        /// <summary>链路配置是否由服务管理</summary>
        public bool LinkConfigManagedByService =>
            _handshake == SecsGemServiceHandshake.Compatible && _serviceHello?.ManagesLinkConfig == true;

        // 数据缓存队列 (使用现代的 Channel 替换 ConcurrentBag，彻底解决 CPU 空转)
        private readonly Channel<byte[]> _receiveProactiveInfo = Channel.CreateUnbounded<byte[]>();
        private readonly Channel<byte[]> _receiveReplyInfo = Channel.CreateUnbounded<byte[]>();

        // 服务端下行数据帧缓冲区：TCP 为字节流，先拼接再按帧提取（仿照 Worker 的 LocationMessageBuffer）
        private readonly ServerFrameBuffer _frameBuffer = new ServerFrameBuffer();

        // 回复消息缓存
        /// <summary>
        /// 回复消息缓存（仅存放收到的应答消息，由 WaitForReplyAsync 消费移除）
        /// </summary>
        public ConcurrentDictionary<string, SecsGemMessage> ReplyMessageInfo { get; } = new ConcurrentDictionary<string, SecsGemMessage>();

        // 应答留存上限：无人等待的应答（如 S6F11 事务的 S6F12）超时后清除，防止字典无限增长
        private static readonly TimeSpan ReplyRetention = TimeSpan.FromMinutes(2);
        private readonly ConcurrentQueue<(string Key, SecsGemMessage Message, DateTime AddedAt)> _replyHistory = new();

        private readonly CategoryLogger _secsGemLogger;

        /// <summary>
        /// SecsGem连接状态
        /// </summary>
        public bool SecsGemStatus => _status;

        /// <summary>
        /// 构造内部客户端
        /// </summary>
        public InternalClient(IParams @params, ICommandManager commandManager, SecsGemMessageProcessor secsGemMessageProcessor, ILogService logService)
        {
            // 本机通道自动重连：SECS 服务重启（或被安装器重装）后，此前主程序这一侧永远停在未连接，
            // 发送全部被拒，只能重启软件。重连成功后服务会推送 0x02 状态帧，SecsGemStatus 随之恢复；
            // 处理循环在 Close 前一直运行，无需重启。主动 Close/Disconnect 会终止重连。
            _client = new TCPClient("SecsGem本机通道", logger: logService)
            {
                AutoReconnect = true,
                ReconnectInterval = 3000,
            };
            _paramConfig = @params;
            _commandManager = commandManager;
            _secsGemMessageProcessor = secsGemMessageProcessor;
            _secsGemLogger = CategoryLoggerFactory.SecsGem(logService);
            SecsGemTrace.Attach(logService);
        }

        /// <summary>
        /// 初始化客户端
        /// </summary>
        public async Task<bool> InitializationClient()
        {
            try
            {
                // 本地系统参数为空时照样连接：链路配置由服务管理时，服务会在接入后立即下发（新机器上本地表本来就是空的）。
                // 此前直接返回 false、根本不连，配置永远到不了主程序
                var systemparams = _paramConfig.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
                if (systemparams == null)
                {
                    _secsGemLogger.Warn("本地 SECS/GEM 系统参数为空，仍连接本机服务：新版服务接入后会下发链路配置；旧版服务需先在 SECS 调试页导入系统参数");
                    _deviceId = new byte[] { 0x00, 0x00 };
                }
                else
                {
                    _deviceId = BuildDeviceId(systemparams.DeviceID);
                }

                // 必须在建立连接之前订阅事件：服务端在连接瞬间就会推送 0x02 状态帧，
                // 连接成功后再订阅会丢失该帧，导致 SecsGemStatus 一直为 false（表现为已连接却拒绝发送）
                _client.Connected -= Client_Connected;
                _client.Disconnected -= Client_Disconnected;
                _client.DataReceived -= Client_DataReceived;
                _client.ErrorOccurred -= Client_ErrorOccurred;

                _client.Connected += Client_Connected;
                _client.Disconnected += Client_Disconnected;
                _client.DataReceived += Client_DataReceived;
                _client.ErrorOccurred += Client_ErrorOccurred;

                return await StartClient();
            }
            catch (Exception ex)
            {
                // 此前以 Debug 级别记录，现场默认日志级别下完全看不到
                _secsGemLogger.Error($"客户端初始化失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 启动客户端并开始处理消息
        /// </summary>
        public async Task<bool> StartClient()
        {
            try
            {
                await _client.DisconnectAsync();

                // 新连接的字节流从零开始，清掉上次连接可能残留的半截帧；
                // 必须在 ConnectAsync 之前清，连接成功后到达的数据不能被误清
                _frameBuffer.Clear();

                // 取消上一轮处理循环再启动新循环，避免重连后同一 Channel 上的消费循环越积越多。
                // 处理循环与连接是否成功无关：首连失败转入后台重连，连上后到达的数据要有人消费
                try { _cts.Cancel(); } catch (ObjectDisposedException) { /* Close() 后 CTS 已释放，属正常路径 */ }
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ProcessActiveAsync(_cts.Token));
                _ = Task.Run(() => ProcessPassiveAsync(_cts.Token));

                // 连接服务器
                var connected = await _client.ConnectAsync("127.0.0.1", 6800);
                if (!connected)
                {
                    // 此前首连失败直接返回、不再尝试：开机时主程序先于 SECS 服务启动就永远连不上。
                    // 仍返回 false 让调用方（机构初始化）如实报警，后台持续重连，服务起来后自动恢复
                    _secsGemLogger.Warn("连接本机 SECS/GEM 服务（127.0.0.1:6800）失败，转入后台自动重连");
                    _ = _client.ReconnectAsync().ContinueWith(
                        t => _secsGemLogger.Warn($"本机通道后台重连异常: {t.Exception?.GetBaseException().Message}"),
                        TaskContinuationOptions.OnlyOnFaulted);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _secsGemLogger.Error($"启动客户端失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 关闭客户端连接
        /// </summary>
        public async Task Close()
        {
            try
            {
                _cts.Cancel();
                _cts.Dispose();

                // 取消事件订阅
                _client.Connected -= Client_Connected;
                _client.Disconnected -= Client_Disconnected;
                _client.DataReceived -= Client_DataReceived;
                _client.ErrorOccurred -= Client_ErrorOccurred;

                await _client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _secsGemLogger.Debug($"关闭客户端时出错: {ex.Message}");
            }
            finally
            {
                _status = false;
            }
        }

        #region 内部类 - 服务端帧缓冲区
        /// <summary>
        /// 服务端下行数据帧缓冲区。
        /// TCP 是字节流：单条 SECS 消息超过接收缓冲区时会分多次到达（拆包），
        /// 多条小消息也可能合并到一次读取（粘包），必须先拼接再按帧提取完整消息。
        /// 帧格式（与 Worker 的 LocationMessageBuffer 对应同一协议）：
        ///   0x00 帧 = [命令1字节] + [长度4字节,大端] + [SECS消息体N字节]，长度=N（含10字节头部）
        ///   0x02 帧 = [命令1字节] + [连接状态1字节]
        ///   0x03 / 0x04 文本帧 = [命令1字节] + [JSON] + [0x0A]（握手 / 链路配置，见 SecsGemLocalChannel）
        ///   0x01 帧 = 占位（服务端目前不发送），与未知字节一样丢弃重新同步
        /// </summary>
        private class ServerFrameBuffer
        {
            private readonly List<byte> _buffer = new List<byte>();
            private readonly object _lock = new object();

            /// <summary>
            /// 向缓冲区追加数据
            /// </summary>
            public void AppendData(byte[] data)
            {
                lock (_lock)
                {
                    _buffer.AddRange(data);
                }
            }

            /// <summary>
            /// 尝试从缓冲区提取完整的帧；数据不足时保留在缓冲区等待下次接收
            /// </summary>
            public List<byte[]> ExtractCompleteFrames()
            {
                var completeFrames = new List<byte[]>();

                lock (_lock)
                {
                    while (_buffer.Count > 0)
                    {
                        byte cmd = _buffer[0];

                        if (cmd == 0x00)
                        {
                            if (_buffer.Count < 5)
                            {
                                break; // 长度字段未到齐，等待下次接收
                            }

                            // 读取4字节长度（大端序），偏移量1~4
                            byte[] lengthBytes = _buffer.Skip(1).Take(4).ToArray();
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(lengthBytes);

                            int bodyLength = BitConverter.ToInt32(lengthBytes, 0);

                            // SECS消息至少含10字节头部；上限防止异常大的包导致内存问题
                            if (bodyLength < 10 || bodyLength > 10 * 1024 * 1024)
                            {
                                _buffer.RemoveAt(0);
                                continue;
                            }

                            int totalLength = 1 + 4 + bodyLength;

                            if (_buffer.Count < totalLength)
                            {
                                break; // 数据不足，等待下次接收
                            }

                            completeFrames.Add(_buffer.Take(totalLength).ToArray());
                            _buffer.RemoveRange(0, totalLength);
                        }
                        else if (cmd == 0x02)
                        {
                            if (_buffer.Count < 2)
                            {
                                break;
                            }

                            completeFrames.Add(new[] { _buffer[0], _buffer[1] });
                            _buffer.RemoveRange(0, 2);
                        }
                        else if (SecsGemLocalChannel.IsTextFrame(cmd))
                        {
                            int end = _buffer.IndexOf(SecsGemLocalChannel.TextFrameTerminator, 1);
                            if (end < 0)
                            {
                                if (_buffer.Count > SecsGemLocalChannel.MaxTextFrameLength + 1)
                                {
                                    _buffer.RemoveAt(0); // 迟迟等不到结束符：按脏数据丢命令字节重新同步
                                    continue;
                                }
                                break; // 等待后续分段
                            }
                            // 整帧（含命令字节与结束符），由 ProcessFrame 解析正文
                            completeFrames.Add(_buffer.Take(end + 1).ToArray());
                            _buffer.RemoveRange(0, end + 1);
                        }
                        else
                        {
                            // 0x01 及未知命令字节：丢弃一字节重新同步
                            _buffer.RemoveAt(0);
                        }
                    }
                }
                return completeFrames;
            }

            /// <summary>
            /// 清空缓冲区（断线/重连时调用，防止残留的半截帧污染后续字节流）
            /// </summary>
            public void Clear()
            {
                lock (_lock)
                {
                    _buffer.Clear();
                }
            }

            /// <summary>
            /// 当前缓冲区剩余字节数
            /// </summary>
            public int Size
            {
                get
                {
                    lock (_lock)
                    {
                        return _buffer.Count;
                    }
                }
            }
        }
        #endregion

        #region 事件处理
        private void Client_Connected(object? sender, ClientConnectedEventArgs e)
        {
            _secsGemLogger.Debug($"客户端 {e.ClientId} 已连接到服务器 {e.ServerAddress}");

            int generation = Interlocked.Increment(ref _connectionGeneration);
            SetHandshake(SecsGemServiceHandshake.Pending, null);

            // 告诉服务本端的协议版本与程序版本（托盘管理界面可见）。旧版服务逐字节丢弃文本帧，发了也无害
            _ = Task.Run(async () =>
            {
                try
                {
                    var hello = SecsGemLocalHello.Create(SecsGemLocalHello.RoleMainProgram, MainProgramVersion(),
                        ConstGlobalParam.IsInitialized ? ConstGlobalParam.ProjectName : null);
                    await _client.SendAsync(SecsGemLocalChannel.BuildTextFrame(SecsGemLocalChannel.HelloFrame, hello));
                }
                catch (Exception ex)
                {
                    _secsGemLogger.Warn($"向本机 SECS 服务发送握手帧失败：{ex.Message}");
                }

                // 服务应在接入后立即发回握手帧；超时没收到即旧版服务（托盘项目之前的版本），只提示、不阻断
                await Task.Delay(SecsGemLocalChannel.HelloTimeout);
                if (Volatile.Read(ref _connectionGeneration) == generation && _handshake == SecsGemServiceHandshake.Pending)
                {
                    SetHandshake(SecsGemServiceHandshake.Legacy, null);
                    _secsGemLogger.Warn("本机 SECS 服务未发送协议握手帧，判定为旧版服务：链路配置沿用本地 SystemConfigs。" +
                                        "建议用 PF SecsGem 托盘升级服务（管理界面 → 操作）");
                }
            });
        }

        private void Client_Disconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            _secsGemLogger.Warn($"与本机 SECS/GEM 服务的连接断开: {e.Reason}");
            Interlocked.Increment(ref _connectionGeneration);
            SetHandshake(SecsGemServiceHandshake.Pending, null);
            bool wasConnected = _status;
            _status = false;
            // 本机通道断开 = 与 Host 的链路对主程序也断了，按 Host 断开通知订阅方（此前状态静默变 false）
            if (wasConnected) SecsGemHostDisconnected?.Invoke(this, EventArgs.Empty);
            // 断线时丢弃未拼接完的半截帧
            _frameBuffer.Clear();
        }

        private void Client_ErrorOccurred(object? sender, ErrorOccurredEventArgs e)
        {
            _secsGemLogger.Warn($"本机通道发生错误: {e.ErrorMessage}");
        }

        private void Client_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            try
            {
                // TCP 字节流先进缓冲区拼接，再按帧提取，解决大消息拆包与多消息粘包
                _frameBuffer.AppendData(e.Data);

                var completeFrames = _frameBuffer.ExtractCompleteFrames();
                foreach (var frame in completeFrames)
                {
                    ProcessFrame(frame);
                }

                if (_frameBuffer.Size > 0)
                {
                    _secsGemLogger.Debug($"帧缓冲区剩余数据: {_frameBuffer.Size} 字节，等待后续分段");
                }
            }
            catch (Exception ex)
            {
                _secsGemLogger.Debug($"处理接收数据时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理一个完整的帧（完整性已由 ServerFrameBuffer 保证）
        /// </summary>
        private void ProcessFrame(byte[] data1)
        {
            //首位为0x00表示为secsgem消息0x01表示为系统返回信息
            //解析到为SECSGem消息
            if (data1[0] == 0x00)
            {
                if (data1.Length < 15) // 最小长度检查（10字节头部 + 4字节长度）
                {
                    _secsGemLogger.Debug($"收到数据长度不足: {data1.Length}字节");
                    return;
                }

                var data = data1.Skip(1).ToArray();
                // 解析消息长度（大端序）
                var lengthBytes = new byte[4];
                Array.Copy(data, 0, lengthBytes, 0, 4);
                Array.Reverse(lengthBytes);
                int totalLength = BitConverter.ToInt32(lengthBytes, 0);

                if (totalLength != data.Length - 4)
                {
                    _secsGemLogger.Debug($"数据长度不匹配: 头部={totalLength}, 实际={data.Length}");
                    return;
                }

                // 解析头部
                var headerBytes = new byte[10];
                Array.Copy(data, 4, headerBytes, 0, 10);

                // 判断消息类型
                byte stream = headerBytes[2];
                byte function = headerBytes[3];

                _secsGemLogger.Debug($"收到消息: S{stream}F{function}, 长度: {totalLength}字节");

                // 回复消息（Function为偶数）
                if (function % 2 == 0)
                {
                    _secsGemLogger.Debug($"收到回复消息: {SecsGemMessageTools.ByteArrayToHexStringWithSeparator(data)}");
                    _receiveReplyInfo.Writer.TryWrite(data);
                }
                // 主动消息（Function为奇数）
                else
                {
                    _secsGemLogger.Debug($"收到主动消息: {SecsGemMessageTools.ByteArrayToHexStringWithSeparator(data)}");
                    _receiveProactiveInfo.Writer.TryWrite(data);
                }
            }
            else if (data1[0] == 0x01)
            {
                /*******数据传输异常*********/
            }
            else if (data1[0] == 0x02)
            {
                if (data1.Length < 2)
                {
                    _secsGemLogger.Debug($"收到数据长度不足: {data1.Length}字节");
                    return;
                }
                _status = data1[1] == (byte)SecsStatus.Connected;
                if ( _status )
                {
                    SecsGemHostConnected?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    SecsGemHostDisconnected?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (data1[0] == SecsGemLocalChannel.HelloFrame)
            {
                OnServiceHello(SecsGemLocalChannel.ParseTextBody<SecsGemLocalHello>(data1.AsSpan(1, data1.Length - 2)));
            }
            else if (data1[0] == SecsGemLocalChannel.LinkConfigFrame)
            {
                OnLinkConfig(SecsGemLocalChannel.ParseTextBody<SecsGemSystemParam>(data1.AsSpan(1, data1.Length - 2)));
            }
        }

        private void OnServiceHello(SecsGemLocalHello? hello)
        {
            if (hello == null)
            {
                _secsGemLogger.Warn("本机 SECS 服务的握手帧格式错误，已忽略");
                return;
            }

            if (SecsGemLocalChannel.IsCompatible(hello))
            {
                SetHandshake(SecsGemServiceHandshake.Compatible, hello);
                _secsGemLogger.Info($"本机 SECS 服务握手成功：服务 {hello.Version}，本机通道协议 v{hello.ProtocolVersion}（主程序 v{SecsGemLocalChannel.ProtocolVersion}）" +
                                    (hello.ManagesLinkConfig ? "，链路配置由服务管理" : string.Empty));
                return;
            }

            SetHandshake(SecsGemServiceHandshake.Incompatible, hello);
            // 指明该升级哪一边：对端版本低于本端可接受的下限 → 升级服务；本端低于对端下限 → 升级主程序（框架包）
            var upgrade = hello.ProtocolVersion < SecsGemLocalChannel.MinCompatibleVersion
                ? "请用 PF SecsGem 托盘把服务升级到新版本"
                : "请把主程序的 PF.AutoFramework 框架包升级到新版本";
            _secsGemLogger.Error($"本机 SECS 服务协议不兼容：服务 {hello.Version}（协议 v{hello.ProtocolVersion}，要求主程序 ≥ v{hello.MinCompatibleVersion}），" +
                                 $"主程序协议 v{SecsGemLocalChannel.ProtocolVersion}（要求服务 ≥ v{SecsGemLocalChannel.MinCompatibleVersion}）。{upgrade}");
        }

        /// <summary>
        /// 服务下发的链路配置：写入本地系统参数（内存 + SystemConfigs），报文头 DeviceID 立即按新值生效。
        /// 与本地一致时不写库。
        /// </summary>
        private void OnLinkConfig(SecsGemSystemParam? param)
        {
            if (param == null)
            {
                _secsGemLogger.Warn("本机 SECS 服务下发的链路配置格式错误，已忽略，沿用本地系统参数");
                return;
            }

            _deviceId = BuildDeviceId(param.DeviceID);

            var current = _paramConfig.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
            var changes = DescribeChanges(current, param);
            if (changes.Count == 0)
            {
                _secsGemLogger.Debug("收到服务下发的链路配置，与本地一致");
                LinkConfigReceived?.Invoke(this, EventArgs.Empty);
                return;
            }

            _secsGemLogger.Info($"收到服务下发的链路配置，已更新本地系统参数：{string.Join("；", changes)}");
            _ = _paramConfig.ReplaceSystemParamAsync(param).ContinueWith(t =>
            {
                if (t.IsFaulted || !t.Result)
                    _secsGemLogger.Warn("服务下发的链路配置写入本地 SystemConfigs 失败（已在内存生效，下次接入会再次下发）");
                LinkConfigReceived?.Invoke(this, EventArgs.Empty);
            }, TaskScheduler.Default);
        }

        /// <summary>逐字段比较两份系统参数，返回"字段: 旧 → 新"列表；旧值为 null 时列出全部字段。</summary>
        private static List<string> DescribeChanges(SecsGemSystemParam? oldValue, SecsGemSystemParam newValue)
        {
            var list = new List<string>();
            foreach (var p in typeof(SecsGemSystemParam).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
                var n = Convert.ToString(p.GetValue(newValue), System.Globalization.CultureInfo.InvariantCulture);
                var o = oldValue == null ? null : Convert.ToString(p.GetValue(oldValue), System.Globalization.CultureInfo.InvariantCulture);
                if (oldValue == null || !string.Equals(o, n, StringComparison.Ordinal))
                    list.Add(oldValue == null ? $"{p.Name}={n}" : $"{p.Name}: {o} → {n}");
            }
            return list;
        }

        private void SetHandshake(SecsGemServiceHandshake state, SecsGemLocalHello? hello)
        {
            bool changed = _handshake != state || !ReferenceEquals(_serviceHello, hello);
            _serviceHello = hello;
            _handshake = state;
            if (changed) ServiceHandshakeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// SECS/HSMS 报文头的 Device ID（Session ID）是大端序 2 字节。此前用 BitConverter.GetBytes，
        /// 在 x86 上得到小端序：DeviceID=1 写成 01 00，Host 看到的是 256；而服务端原样转发不重写头部，
        /// 严格校验设备号的 Host 会以 S9F1（Unrecognized Device ID）拒收。
        /// 非法值（非数字、超出 0~32767）按 0 处理并记 Warn，不让初始化抛异常。
        /// </summary>
        private byte[] BuildDeviceId(string? text)
        {
            if (!ushort.TryParse(text?.Trim(), out var deviceId) || deviceId > 0x7FFF)
            {
                _secsGemLogger.Warn($"DeviceID「{text}」无效（应为 0~32767 的整数），报文头按 0 处理");
                deviceId = 0;
            }
            return new[] { (byte)(deviceId >> 8), (byte)(deviceId & 0xFF) };
        }

        private static string MainProgramVersion() =>
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "未知";
        #endregion

        #region 消息处理循环




        private async Task ProcessActiveAsync(CancellationToken token)
        {
            try
            {
                // await foreach 会在没有数据时自动休眠，有数据时瞬间唤醒，CPU 占用为 0
                await foreach (var result in _receiveProactiveInfo.Reader.ReadAllAsync(token))
                {
                    // 逐条消息隔离异常：订阅方（业务 handler）抛出的异常不能杀死整个消费循环
                    try
                    {
                        var message = _secsGemMessageProcessor.ParseSecsBytes(result);
                        if (message == null)
                        {
                            continue;
                        }

                        var handler = MessageReceived;
                        if (handler == null)
                        {
                            // 此前静默丢弃：Host 只看到 T3 超时，主程序日志里什么也没有
                            _secsGemLogger.Warn($"收到 Host 主动消息 S{message.Stream}F{message.Function}，但当前没有订阅方（SECS 模组未完成初始化？），已丢弃，Host 侧将出现 T3 超时");
                            continue;
                        }

                        handler(this, new SecsMessageReceivedEventArgs() { Message = message, Timestamp = DateTime.Now });
                    }
                    catch (Exception ex)
                    {
                        _secsGemLogger.Debug($"处理主动消息时出错: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                _secsGemLogger.Debug($"主动消息处理循环终止: {ex.Message}");
            }
        }

        private async Task ProcessPassiveAsync(CancellationToken token)
        {
            try
            {
                await foreach (var result in _receiveReplyInfo.Reader.ReadAllAsync(token))
                {
                    // 逐条消息隔离异常：订阅方抛出的异常不能杀死整个消费循环
                    try
                    {
                        var message = _secsGemMessageProcessor.ParseSecsBytes(result);
                        if (message == null)
                        {
                            continue;
                        }

                        // 写入回复消息缓存（同键旧值直接覆盖）
                        var systemBytesStr = SecsGemMessageTools.ByteArrayToHexStringWithSeparator(message.SystemBytes.ToArray());
                        ReplyMessageInfo.TryRemove(systemBytesStr, out _);
                        ReplyMessageInfo.TryAdd(systemBytesStr, message);

                        // 记录留存并清理过期条目：无人等待的应答不能在字典里永久堆积
                        _replyHistory.Enqueue((systemBytesStr, message, DateTime.Now));
                        PruneExpiredReplies();

                        MessageReceived?.Invoke(this, new SecsMessageReceivedEventArgs() { Message = message, Timestamp = DateTime.Now });
                    }
                    catch (Exception ex)
                    {
                        _secsGemLogger.Debug($"处理回复消息时出错: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                _secsGemLogger.Debug($"回复消息处理循环终止: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理超过留存时间仍未被消费的应答。
        /// 用 TryRemove(KeyValuePair) 精确匹配实例，避免误删同键的更新回复。
        /// </summary>
        private void PruneExpiredReplies()
        {
            while (_replyHistory.TryPeek(out var oldest) && DateTime.Now - oldest.AddedAt > ReplyRetention)
            {
                if (_replyHistory.TryDequeue(out var entry))
                {
                    ReplyMessageInfo.TryRemove(
                        new System.Collections.Generic.KeyValuePair<string, SecsGemMessage>(entry.Key, entry.Message));
                }
            }
        }

        #endregion


        #region 消息发送
        /// <summary>
        /// 发送SECS/GEM消息
        /// </summary>
        public async Task SendMessage(SecsGemMessage msg)
        {
            /**********发送服务byte消息首位0x00表示secsgem消息  ，0x01系统异常消息**************/
            try
            {
                if (_client.Status != ClientStatus.Connected)
                {
                    throw new Exception("客户端未连接");
                }

                // 生成SystemBytes（如果需要）
                if (msg.SystemBytes == null || msg.SystemBytes.Count == 0)
                {
                    msg.SystemBytes = SecsGemMessageTools.GenerateSystemBytes();
                }

                // 生成SECS字节
                byte[] bytes = _secsGemMessageProcessor.GenerateSecsBytes(
                    msg,
                    _deviceId,
                    msg.SystemBytes?.ToArray() ?? new byte[] { 0x00, 0x00, 0x00, 0x00 });
                var sendbytes = new byte[bytes.Length + 1];
                sendbytes[0] = 0x00; // 前缀
                Buffer.BlockCopy(bytes, 0, sendbytes, 1, bytes.Length);
                // 发送消息
                bool success = await _client.SendAsync(sendbytes);
                if (success)
                {
                    _secsGemLogger.Debug($"发送SECS/GEM消息: S{msg.Stream}F{msg.Function}, 长度: {bytes.Length}字节   {SecsGemMessageTools.ByteArrayToHexStringWithSeparator(sendbytes)}");

                    // 注意：不要把发出的请求放进 ReplyMessageInfo ——
                    // WaitForReplyAsync 按同一 SystemBytes 键轮询，会立刻取到请求本体当成"回复"返回。
                    // 缓存只由 ProcessPassiveAsync 在真正收到应答时写入。
                }
                else
                {
                    _secsGemLogger.Warn($"发送SECS/GEM消息失败: S{msg.Stream}F{msg.Function}");
                }
            }
            catch (Exception ex)
            {
                _secsGemLogger.Error($"发送 S{msg?.Stream}F{msg?.Function} 时出错: {ex.Message}", ex);
                throw;
            }
        }
        #endregion

        #region 工具方法

        /// <summary>
        /// 等待回复消息（默认超时与 IinternalClient 接口声明保持一致：5000ms，对应 T3）
        /// </summary>
        public async Task<SecsGemMessage> WaitForReplyAsync(string systemBytesHex, int timeoutMs = 5000)
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (ReplyMessageInfo.TryGetValue(systemBytesHex, out var message))
                {
                    ReplyMessageInfo.TryRemove(systemBytesHex, out _);
                    return message;
                }
                await Task.Delay(10);
            }

            throw new TimeoutException($"等待回复超时 ({timeoutMs}ms), SystemBytes: {systemBytesHex}");
        }
        #endregion

        #region IDisposable Support
        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cts.Cancel();
                    _client.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
