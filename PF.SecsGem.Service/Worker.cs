using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Infrastructure.Communication.TCP;
using PF.SecsGem.DataBase.Entities.System;
using PF.Infrastructure.SecsGem.Tools;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using DataReceivedEventArgs = PF.Core.Events.DataReceivedEventArgs;

namespace PF.SecsGem.Service
{
    public class Worker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<Worker> _logger;

        #region Parames
        private SecsGemSystemParam? _secsGemSystemParam;
        /// <summary>
        /// SECSGem状态
        /// </summary>
        private bool _SecsStatus = false;

        /// <summary>
        /// 机台编号
        /// </summary>
        byte[] _deviceId = new byte[] { 0xFF, 0xFF };

        /// <summary>
        /// SecsGem连接客户端ID
        /// </summary>
        private string SecsGemClientId = string.Empty;

        /// <summary>
        /// 本地交互客户端ID
        /// </summary>
        private string LocationClientId = string.Empty;

        /// <summary>
        /// SECSGEM服务器
        /// </summary>
        private TcpServer SecsGemServer;

        /// <summary>
        /// 本地交互服务器
        /// </summary>
        private TcpServer LocationServer;

        /// <summary>
        /// 存放SECSGEM完整消息的队列
        /// </summary>
        private ConcurrentQueue<byte[]> SecsGemMessageQueue = new ConcurrentQueue<byte[]>();

        /// <summary>
        /// 为每个SecsGem客户端维护的消息缓冲区
        /// </summary>
        private ConcurrentDictionary<string, MessageBuffer> _secsGemClientBuffers =
            new ConcurrentDictionary<string, MessageBuffer>();

        /// <summary>
        /// 为每个Location客户端维护的消息缓冲区
        /// </summary>
        private ConcurrentDictionary<string, LocationMessageBuffer> _locationClientBuffers =
            new ConcurrentDictionary<string, LocationMessageBuffer>();

        #endregion Params

        #region 内部类 - 消息缓冲区
        /// <summary>
        /// 外部SecsGem消息缓冲区
        /// </summary>
        private class MessageBuffer
        {
            private List<byte> _buffer = new List<byte>();
            private readonly object _lock = new object();

            /// <summary>
            /// 向缓冲区添加数据
            /// </summary>
            public void AppendData(byte[] data)
            {
                lock (_lock)
                {
                    _buffer.AddRange(data);
                }
            }

            /// <summary>
            /// 尝试从缓冲区提取完整的SecsGem消息
            /// </summary>
            public List<byte[]> ExtractCompleteMessages()
            {
                List<byte[]> completeMessages = new List<byte[]>();

                lock (_lock)
                {
                    while (_buffer.Count >= 4)
                    {
                        byte[] lengthBytes = _buffer.Take(4).ToArray();
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(lengthBytes);

                        int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                        int totalLength = 4 + messageLength;

                        if (_buffer.Count >= totalLength)
                        {
                            byte[] completeMessage = _buffer.Take(totalLength).ToArray();
                            completeMessages.Add(completeMessage);
                            _buffer.RemoveRange(0, totalLength);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                return completeMessages;
            }

            public void Clear()
            {
                lock (_lock)
                {
                    _buffer.Clear();
                }
            }

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

        /// <summary>
        /// Location协议消息缓冲区
        /// 包格式：[0x00 命令字节(1)] + [长度(4, 大端)] + [Body(N)]
        /// 总长度 = 5 + N
        /// </summary>
        private class LocationMessageBuffer
        {
            private List<byte> _buffer = new List<byte>();
            private readonly object _lock = new object();

            public void AppendData(byte[] data)
            {
                lock (_lock)
                {
                    _buffer.AddRange(data);
                }
            }

            public List<byte[]> ExtractCompleteMessages()
            {
                var completeMessages = new List<byte[]>();
                lock (_lock)
                {
                    while (_buffer.Count >= 5) // 1字节命令 + 4字节长度
                    {
                        // 命令字节必须是 0x00，否则丢弃无效数据
                        if (_buffer[0] != 0x00)
                        {
                            _buffer.RemoveAt(0);
                            continue;
                        }

                        // 读取4字节长度（大端序），偏移量1~4
                        byte[] lengthBytes = _buffer.Skip(1).Take(4).ToArray();
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(lengthBytes);

                        int bodyLength = BitConverter.ToInt32(lengthBytes, 0);

                        // 防止异常大的包导致内存问题
                        if (bodyLength < 0 || bodyLength > 10 * 1024 * 1024)
                        {
                            _buffer.RemoveAt(0);
                            continue;
                        }

                        int totalLength = 1 + 4 + bodyLength;

                        if (_buffer.Count >= totalLength)
                        {
                            byte[] completeMessage = _buffer.Take(totalLength).ToArray();
                            completeMessages.Add(completeMessage);
                            _buffer.RemoveRange(0, totalLength);
                        }
                        else
                        {
                            break; // 数据不足，等待下次接收
                        }
                    }
                }
                return completeMessages;
            }

            public void Clear()
            {
                lock (_lock) { _buffer.Clear(); }
            }

            public int Size
            {
                get { lock (_lock) { return _buffer.Count; } }
            }
        }
        #endregion

        #region EventHandlers

        private async void SecsGemServer_ClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            _SecsStatus = false;
            _secsGemClientBuffers.TryRemove(e.ClientId, out _);

            if (!string.IsNullOrEmpty(this.LocationClientId))
            {
                await this.LocationServer.SendAsync(this.LocationClientId,
                    new byte[] { 0x02, (byte)SecsStatus.Disconnected });
            }
        }

        private void LocationServer_ClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            this.LocationClientId = e.ClientId;
            // 为新客户端创建消息缓冲区
            _locationClientBuffers.TryAdd(e.ClientId, new LocationMessageBuffer());

            _ = this.LocationServer.SendAsync(this.LocationClientId,
                new byte[] { 0x02, (byte)(_SecsStatus ? SecsStatus.Connected : SecsStatus.Disconnected) });
        }

        private void LocationServer_ClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            _locationClientBuffers.TryRemove(e.ClientId, out _);
            if (this.LocationClientId == e.ClientId)
            {
                this.LocationClientId = string.Empty;
            }
        }

        private async void SecsGemServer_ClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            this.SecsGemClientId = e.ClientId;
            _SecsStatus = true;

            _secsGemClientBuffers.TryAdd(e.ClientId, new MessageBuffer());

            if (!string.IsNullOrEmpty(this.LocationClientId))
            {
                await this.LocationServer.SendAsync(this.LocationClientId,
                    new byte[] { 0x02, (byte)SecsStatus.Connected });
            }
        }

        bool MessageIsProcessingSucess = true;
        DateTime MessageIsProcessingFailedDate = DateTime.Now;

        private void SecsGemServer_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!_secsGemClientBuffers.TryGetValue(e.ClientId, out var buffer))
                {
                    buffer = new MessageBuffer();
                    _secsGemClientBuffers.TryAdd(e.ClientId, buffer);
                }

                buffer.AppendData(e.Data);

                var completeMessages = buffer.ExtractCompleteMessages();
                if (completeMessages.Count == 0)
                {
                    if (MessageIsProcessingSucess == false)
                    {
                        if ((DateTime.Now - MessageIsProcessingFailedDate).TotalSeconds > 20)
                        {
                            buffer.Clear();
                            MessageIsProcessingSucess = true;
                        }
                    }
                    else
                    {
                        MessageIsProcessingSucess = false;
                        MessageIsProcessingFailedDate = DateTime.Now;
                    }
                }
                else
                {
                    MessageIsProcessingSucess = true;
                }

                foreach (var message in completeMessages)
                {
                    this.SecsGemMessageQueue.Enqueue(message);
                    // 第一处日志记录，保留。反映最底层的网关收包动作
                    this.SecsGemWriteLog.Writer.TryWrite(("接收主机", message));
                }

                if (buffer.Size > 0)
                {
                    _logger.LogDebug($"客户端 {e.ClientId} 缓冲区剩余数据: {buffer.Size} 字节");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理SecsGem数据时发生错误: {ex.Message}");
            }
        }

        private async void LocationServer_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!_locationClientBuffers.TryGetValue(e.ClientId, out var buffer))
                {
                    buffer = new LocationMessageBuffer();
                    _locationClientBuffers.TryAdd(e.ClientId, buffer);
                }

                buffer.AppendData(e.Data);

                var completeMessages = buffer.ExtractCompleteMessages();
                foreach (var rec in completeMessages)
                {
                    // rec[0] == 0x00 已由缓冲区保证，剥离命令字节
                    byte[] data = rec.Skip(1).ToArray();

                    this.SecsGemWriteLog.Writer.TryWrite(("发送主机", data));
                    this.SecsGemServer?.SendAsync(this.SecsGemClientId, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理本地数据时发生错误: {ex.Message}");
            }
        }

        #endregion EventHandlers

        #region Methods
        private async Task ProcessSecsGemServiceInfo(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10, token);

                    if (this.SecsGemMessageQueue.IsEmpty)
                    {
                        continue;
                    }

                    if (this.SecsGemMessageQueue.TryDequeue(out var data))
                    {
                        await ProcessSecsGemMessage(data, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ProcessSecsGemServiceInfo 任务已取消");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"ProcessSecsGemServiceInfo 任务异常: {ex.Message}");
                }
            }
        }

        private async Task ProcessSecsGemMessage(byte[] data, CancellationToken token = default)
        {
            try
            {
                // 已移除重复的日志记录

                byte[] header_resp = data.Skip(4).Take(10).ToArray();

                if (header_resp[2] == 0 && header_resp[3] == 0)
                {
                    await ProcessS0F0(header_resp, token);
                }
                else
                {
                    byte[] send = new byte[] { 0x00 }.Concat(data).ToArray();
                    await this.LocationServer.SendAsync(this.LocationClientId, send);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理SecsGem消息时发生错误: {ex.Message}");
            }
        }

        private async Task ProcessS0F0(byte[] header, CancellationToken token = default)
        {
            if (header.Length != 10)
            {
                return;
            }

            SecsGemMessage message = new SecsGemMessage()
            {
                Stream = 0,
                Function = 0,
                SystemBytes = header.Skip(6).Take(4).ToList(),
                WBit = false,
                RootNode = null
            };

            byte linkTest = header[5];
            if (linkTest == 1) message.LinkNumber = 2;
            else if (linkTest == 5) message.LinkNumber = 6;
            else if (linkTest == 9) message.LinkNumber = 10;
            else
            {
                _logger.LogWarning($"未知的LinkTest值: {linkTest}");
                return;
            }

            byte[] sendData = SecsGemMessageTools.GenerateSecsBytes(message, _deviceId);
            this.SecsGemWriteLog.Writer.TryWrite(("发送主机", sendData));

            if (this.SecsGemServer != null && !string.IsNullOrEmpty(SecsGemClientId))
            {
                await this.SecsGemServer.SendAsync(SecsGemClientId, sendData);
            }
        }

        #endregion Methods

        public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var secsGemDataBase = scope.ServiceProvider.GetRequiredService<ISecsGemDataBase>();
                var manger0 = secsGemDataBase.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                _secsGemSystemParam = (await manger0.GetAllAsync()).Select(t => t.GetSecsGemSystemFormSecsGemSystemEntity()).ToList().FirstOrDefault();
            }

            _logger.LogInformation("SecsGem 后台工作线程已启动");

            try
            {
                LocationServer = new TcpServer("服务本地服务器");
                await LocationServer.StartAsync("127.0.0.1", 6800);
                LocationServer.DataReceived += LocationServer_DataReceived;
                LocationServer.ClientConnected += LocationServer_ClientConnected;
                LocationServer.ClientDisconnected += LocationServer_ClientDisconnected;

                SecsGemServer = new TcpServer("SecsGem服务器");
                await SecsGemServer.StartAsync(_secsGemSystemParam.IPAddress, _secsGemSystemParam.Port);
                SecsGemServer.DataReceived += SecsGemServer_DataReceived;
                SecsGemServer.ClientConnected += SecsGemServer_ClientConnected;
                SecsGemServer.ClientDisconnected += SecsGemServer_ClientDisconnected;

                _ = ProcessSecsGemServiceInfo(stoppingToken);
                _ = WriteLog(stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SecsGem 后台工作线程收到停止信号");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SecsGem 后台工作线程执行时发生错误");
            }
            finally
            {
                _logger.LogInformation("SecsGem 后台工作线程已停止");
            }
        }

        #region 日志记录模块

        /// <summary>
        /// GECSGEM日志交互记录器
        /// </summary>
        private Channel<(string, byte[])> SecsGemWriteLog = Channel.CreateUnbounded<(string, byte[])>(new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = false, // 已修复：允许多个 TCP 接收线程并发安全写入
        });

        private async Task WriteLog(CancellationToken token = default)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(10, token);
                    var info = await SecsGemWriteLog.Reader.ReadAsync(token);
                    WriteCustomLog(info);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WriteLog 任务已取消");
            }
            catch (Exception ex)
            {
                _logger.LogError("WriteLog 任务错误" + ex.Message + ex.StackTrace);
            }
        }

        private static readonly object locker = new object();

        private string logpath = $"D:\\SWLog\\SecsGemService";
        private void WriteCustomLog((string, byte[]) info)
        {
            Task.Factory.StartNew(() =>
            {
                lock (locker)
                {
                    try
                    {
                        StringBuilder strFile = new StringBuilder();
                        strFile.AppendFormat("{0}\\{1}\\{2}\\", logpath, DateTime.Now.Year.ToString(), DateTime.Now.Month.ToString());
                        if (!Directory.Exists(strFile.ToString()))
                        {
                            Directory.CreateDirectory(strFile.ToString());
                        }
                        strFile.Append(DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                        string SecsGem = ByteArrayToHexStringWithSeparator(info.Item2);

                        using (StreamWriter swAppend = File.AppendText(strFile.ToString()))
                        {
                            StringBuilder str = new StringBuilder();
                            str.AppendFormat("[{0}] [{1}]   [{2}]", DateTime.Now, info.Item1, SecsGem);
                            swAppend.WriteLine(str.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("WriteCustomLog 任务错误" + ex.Message + ex.StackTrace);
                    }
                }
            });
        }

        /// <summary>
        /// 字节数组转换为带分隔符的十六进制字符串
        /// </summary>
        private string ByteArrayToHexStringWithSeparator(byte[] bytes, string separator = " ", bool upperCase = true)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            string format = upperCase ? "X2" : "x2";

            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString(format));
                if (i < bytes.Length - 1)
                {
                    sb.Append(separator);
                }
            }
            return sb.ToString();
        }

        #endregion 日志记录模块
    }
}