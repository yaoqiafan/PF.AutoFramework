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

        // 状态管理
        private bool _status = false;

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
            _client = new TCPClient();
            _paramConfig = @params;
            _commandManager = commandManager;
            _secsGemMessageProcessor = secsGemMessageProcessor;
            _secsGemLogger = CategoryLoggerFactory.SecsGem(logService);
        }

        /// <summary>
        /// 初始化客户端
        /// </summary>
        public async Task<bool> InitializationClient()
        {
            try
            {
                var systemparams = _paramConfig.GetParam<SecsGemSystemParam>(ParamType.System);
                if (systemparams == null) { return false; }
                _deviceId = BitConverter.GetBytes(Convert.ToInt16(systemparams.DeviceID));

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
                _secsGemLogger.Debug($"客户端初始化失败: {ex.Message}");
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

                // 连接服务器
                var connected = await _client.ConnectAsync("127.0.0.1", 6800);
                if (!connected)
                {
                    return false;
                }

                // 取消上一轮处理循环再启动新循环，避免重连后同一 Channel 上的消费循环越积越多
                try { _cts.Cancel(); } catch (ObjectDisposedException) { /* Close() 后 CTS 已释放，属正常路径 */ }
                _cts = new CancellationTokenSource();

                // 启动处理任务
                _ = Task.Run(() => ProcessActiveAsync(_cts.Token));
                _ = Task.Run(() => ProcessPassiveAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                _secsGemLogger.Debug($"启动客户端失败: {ex.Message}");
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
        }

        private void Client_Disconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            _secsGemLogger.Debug($"客户端 {e.ClientId} 断开连接: {e.Reason}");
            _status = false;
            // 断线时丢弃未拼接完的半截帧
            _frameBuffer.Clear();
        }

        private void Client_ErrorOccurred(object? sender, ErrorOccurredEventArgs e)
        {
            _secsGemLogger.Debug($"客户端发生错误: {e.ErrorMessage}");
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
        }
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

                        MessageReceived?.Invoke(this, new SecsMessageReceivedEventArgs() { Message = message, Timestamp = DateTime.Now });
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
                    _secsGemLogger.Debug($"发送SECS/GEM消息失败: S{msg.Stream}F{msg.Function}");
                }
            }
            catch (Exception ex)
            {
                _secsGemLogger.Debug($"发送消息时出错: {ex.Message}");
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
