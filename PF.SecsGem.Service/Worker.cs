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

        #endregion Params

        #region 内部类 - 消息缓冲区
        /// <summary>
        /// 消息缓冲区，用于处理粘包和半包问题
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
            /// <returns>完整的消息列表</returns>
            public List<byte[]> ExtractCompleteMessages()
            {
                List<byte[]> completeMessages = new List<byte[]>();

                lock (_lock)
                {
                    while (_buffer.Count >= 4)
                    {
                        // 读取消息长度（大端序）
                        byte[] lengthBytes = _buffer.Take(4).ToArray();
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(lengthBytes);

                        int messageLength = BitConverter.ToInt32(lengthBytes, 0);

                        // 检查是否已经收到完整的消息
                        // 总长度 = 4字节长度字段 + messageLength
                        int totalLength = 4 + messageLength;

                        if (_buffer.Count >= totalLength)
                        {
                            // 提取完整消息
                            byte[] completeMessage = _buffer.Take(totalLength).ToArray();
                            completeMessages.Add(completeMessage);

                            // 从缓冲区移除已处理的数据
                            _buffer.RemoveRange(0, totalLength);
                        }
                        else
                        {
                            // 还没有收到完整消息，等待更多数据
                            break;
                        }
                    }
                }

                return completeMessages;
            }

            /// <summary>
            /// 清空缓冲区
            /// </summary>
            public void Clear()
            {
                lock (_lock)
                {
                    _buffer.Clear();
                }
            }

            /// <summary>
            /// 获取当前缓冲区大小
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

        #region EventHandlers

        private async void SecsGemServer_ClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            _SecsStatus = false;
            // 清理客户端的缓冲区
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
            _ = this.LocationServer.SendAsync(this.LocationClientId,
                new byte[] { 0x02, (byte)(_SecsStatus ? SecsStatus.Connected : SecsStatus.Disconnected) });
        }

        private async void SecsGemServer_ClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            this.SecsGemClientId = e.ClientId;
            _SecsStatus = true;

            // 为新客户端创建消息缓冲区
            _secsGemClientBuffers.TryAdd(e.ClientId, new MessageBuffer());

            if (!string.IsNullOrEmpty(this.LocationClientId))
            {
                await this.LocationServer.SendAsync(this.LocationClientId,
                    new byte[] { 0x02, (byte)SecsStatus.Connected });
            }
        }

        /// <summary>
        /// 消息解析成功标志
        /// </summary>
        bool MessageIsProcessingSucess = true;


        DateTime MessageIsProcessingFailedDate = DateTime.Now;



        private void SecsGemServer_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            try
            {
                // 获取或创建客户端的消息缓冲区
                if (!_secsGemClientBuffers.TryGetValue(e.ClientId, out var buffer))
                {
                    buffer = new MessageBuffer();
                    _secsGemClientBuffers.TryAdd(e.ClientId, buffer);
                }

                // 将接收到的数据添加到缓冲区
                buffer.AppendData(e.Data);

                // 尝试提取完整的消息
                var completeMessages = buffer.ExtractCompleteMessages();
                if (completeMessages.Count == 0)
                {
                    if (MessageIsProcessingSucess == false)
                    {
                        if ((DateTime.Now - MessageIsProcessingFailedDate).TotalSeconds > 20)
                        {
                            // 超过5秒没有收到完整消息，清空缓冲区
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
                    // 将完整消息放入队列
                    this.SecsGemMessageQueue.Enqueue(message);
                    this.SecsGemWriteLog.Writer.TryWrite(("接收主机", message));
                }

                // 可选：记录缓冲区大小（用于调试）
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
                byte[] rec = e.Data;
                if (rec.Length < 1)
                {
                    return;
                }

                if (rec[0] == 0x00)
                {
                    byte[] data = rec.Skip(1).ToArray();

                    // 验证消息长度
                    if (data.Length < 4)
                    {
                        byte[] send = new byte[] { 0x01, (byte)SecsErrorCode.数据长度错误 };
                        await this.LocationServer.SendAsync(this.LocationClientId, send);
                        return;
                    }

                    byte[] len_resp = data.Take(4).ToArray();
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(len_resp);

                    int len = BitConverter.ToInt32(len_resp, 0);
                    if (len != data.Length - 4)
                    {
                        byte[] send = new byte[] { 0x01, (byte)SecsErrorCode.数据长度错误 };
                        await this.LocationServer.SendAsync(this.LocationClientId, send);
                    }
                    else
                    {
                        this.SecsGemWriteLog.Writer.TryWrite(("发送主机", data));
                        this.SecsGemServer?.SendAsync(this.SecsGemClientId, data);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理本地数据时发生错误: {ex.Message}");
            }
        }

        #endregion EventHandlers

        #region Methods
        /// <summary>
        /// 处理SecsGem服务信息
        /// </summary>
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

        /// <summary>
        /// 处理单个SecsGem消息
        /// </summary>
        private async Task ProcessSecsGemMessage(byte[] data, CancellationToken token = default)
        {
            try
            {
                this.SecsGemWriteLog.Writer.TryWrite(("接收主机", data));
                // 消息长度验证已经在缓冲区处理时完成
                // 直接解析消息头
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

            // 根据header[5]设置LinkNumber
            byte linkTest = header[5];
            if (linkTest == 1)
            {
                message.LinkNumber = 2;
            }
            else if (linkTest == 5)
            {
                message.LinkNumber = 6;
            }
            else if (linkTest == 9)
            {
                message.LinkNumber = 10;
            }
            else
            {
                _logger.LogWarning($"未知的LinkTest值: {linkTest}");
                return;
            }

            byte[] sendData = SecsGemMessageTools.GenerateSecsBytes(message, _deviceId);
            this.SecsGemWriteLog.Writer.TryWrite(("发送主机", sendData));
            //this.SecsGemWriteLog.Writer.TryWrite(("发送主机", sendData));

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
                // 从 Scope 中获取 Scoped 服务
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

                SecsGemServer = new TcpServer("SecsGem服务器");
                await SecsGemServer.StartAsync(_secsGemSystemParam.IPAddress, _secsGemSystemParam.Port);
                SecsGemServer.DataReceived += SecsGemServer_DataReceived;
                SecsGemServer.ClientConnected += SecsGemServer_ClientConnected;
                SecsGemServer.ClientDisconnected += SecsGemServer_ClientDisconnected;

                // 启动处理任务
                _ = ProcessSecsGemServiceInfo(stoppingToken);
                _ = WriteLog(stoppingToken);

                // 等待停止信号
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
                // 清理资源
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
            SingleWriter = true,
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
            catch (OperationCanceledException ex)
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
        /// <param name="bytes"></param>
        /// <param name="separator"></param>
        /// <param name="upperCase"></param>
        /// <returns></returns>
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