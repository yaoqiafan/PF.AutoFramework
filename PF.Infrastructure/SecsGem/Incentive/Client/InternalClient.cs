using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication.TCP;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.SecsGem.Tools;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Interfaces.SecsGem.Communication;

namespace PF.Infrastructure.SecsGem.Incentive
{
    public class InternalClient : IinternalClient
    {
        private readonly IClient _client;
        private readonly IParams _paramConfig;
        private readonly ICommandManager _commandManager;
        private readonly SecsGemMessageProcessor _secsGemMessageProcessor;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private byte[] _deviceId;
        public event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;

        // 状态管理
        private bool _status = false;

        // 数据缓存队列
        private readonly ConcurrentBag<byte[]> _receiveProactiveInfo = new ConcurrentBag<byte[]>();
        private readonly ConcurrentBag<byte[]> _receiveReplyInfo = new ConcurrentBag<byte[]>();

        // 回复消息缓存
        public ConcurrentDictionary<string, SecsGemMessage> ReplyMessageInfo { get; } = new ConcurrentDictionary<string, SecsGemMessage>();

        // 日志路径
        private readonly string _logPathName = @"D:\SWLog\PC\MESLog";
        private static readonly object _logLock = new object();

        public bool SecsGemStatus => _status;

        public InternalClient(IClient client, IParams @params, ICommandManager commandManager, SecsGemMessageProcessor secsGemMessageProcessor)
        {
            _client = client;
            _paramConfig = @params;
            _commandManager = commandManager;
            _secsGemMessageProcessor= secsGemMessageProcessor;
        }

        public async Task<bool> InitializationClient()
        {
            try
            {
                // 订阅事件
                _client.Connected += Client_Connected;
                _client.Disconnected += Client_Disconnected;
                _client.DataReceived += Client_DataReceived;
                _client.ErrorOccurred += Client_ErrorOccurred;
                var systemparams = _paramConfig.GetParam<SecsGemSystemParam>(ParamType.System);
                _deviceId = BitConverter.GetBytes(Convert.ToInt16(systemparams.DeviceID));
                // 连接到服务器
                return await StartClient();
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"客户端初始化失败: {ex.Message}");
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
                // 连接服务器
                var connected = await _client.ConnectAsync("127.0.0.1", 6800);
                if (!connected)
                {
                    return false;
                }

                // 启动处理任务
                _ = Task.Run(() => ProcessActiveAsync(_cts.Token));
                _ = Task.Run(() => ProcessPassiveAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"启动客户端失败: {ex.Message}");
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

                // 取消事件订阅
                _client.Connected -= Client_Connected;
                _client.Disconnected -= Client_Disconnected;
                _client.DataReceived -= Client_DataReceived;
                _client.ErrorOccurred -= Client_ErrorOccurred;

                await _client.DisconnectAsync();
                _status = false;
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"关闭客户端时出错: {ex.Message}");
            }
        }

        #region 事件处理
        private void Client_Connected(object? sender, ClientConnectedEventArgs e)
        {
            WriteSecsGemLog($"客户端 {e.ClientId} 已连接到服务器 {e.ServerAddress}");
        }

        private void Client_Disconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            WriteSecsGemLog($"客户端 {e.ClientId} 断开连接: {e.Reason}");
            _status = false;
        }

        private void Client_ErrorOccurred(object? sender, ErrorOccurredEventArgs e)
        {
            WriteSecsGemLog($"客户端发生错误: {e.ErrorMessage}");
        }

        private void Client_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            try
            {
                var data1 = e.Data;

                /**首位为0x00表示为secsgem消息0x01表示为系统返回信息***/
                //解析到为SECSGem消息
                if (data1[0] == 0x00)
                {
                    if (data1.Length < 15) // 最小长度检查（10字节头部 + 4字节长度）
                    {
                        WriteSecsGemLog($"收到数据长度不足: {data1.Length}字节");
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
                        WriteSecsGemLog($"数据长度不匹配: 头部={totalLength}, 实际={data.Length}");
                        return;
                    }

                    // 解析头部
                    var headerBytes = new byte[10];
                    Array.Copy(data, 4, headerBytes, 0, 10);

                    // 判断消息类型
                    byte stream = headerBytes[2];
                    byte function = headerBytes[3];

                    WriteSecsGemLog($"收到消息: S{stream}F{function}, 长度: {totalLength}字节");

                    // 回复消息（Function为偶数）
                    if (function % 2 == 0)
                    {
                        WriteSecsGemLog($"收到回复消息: {SecsGemMessageTools.ByteArrayToHexStringWithSeparator(data)}");
                        _receiveReplyInfo.Add(data);
                    }
                    // 主动消息（Function为奇数）
                    else
                    {
                        WriteSecsGemLog($"收到主动消息: {SecsGemMessageTools.ByteArrayToHexStringWithSeparator(data)}");
                        _receiveProactiveInfo.Add(data);
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
                        WriteSecsGemLog($"收到数据长度不足: {data1.Length}字节");
                        return;
                    }
                    _status = data1[1] == (byte)SecsStatus.Connected;
                }
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"处理接收数据时出错: {ex.Message}");
            }
        }
        #endregion

        #region 消息处理循环

        private async Task ProcessActiveAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1, token);

                    if (_receiveProactiveInfo.TryTake(out var result))
                    {
                        var message = _secsGemMessageProcessor.ParseSecsBytes(result);
                        if (message == null)
                        {
                            continue;
                        }

                        MessageReceived?.Invoke(this, new SecsMessageReceivedEventArgs() { Message = message, Timestamp = DateTime.Now });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"处理主动消息时出错: {ex.Message}");
            }
        }

        private async Task ProcessPassiveAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1, token);

                    if (_receiveReplyInfo.TryTake(out var result))
                    {
                        var message = _secsGemMessageProcessor.ParseSecsBytes(result);
                        if (message == null)
                        {
                            continue;
                        }

                        // 从缓存中移除对应的消息
                        var systemBytesStr = SecsGemMessageTools.ByteArrayToHexStringWithSeparator(message.SystemBytes.ToArray());
                        ReplyMessageInfo.TryRemove(systemBytesStr, out _);

                        // 添加到回复消息缓存
                        ReplyMessageInfo.TryAdd(systemBytesStr, message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"处理回复消息时出错: {ex.Message}");
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
                    WriteSecsGemLog($"发送SECS/GEM消息: S{msg.Stream}F{msg.Function}, 长度: {bytes.Length}字节   {SecsGemMessageTools.ByteArrayToHexStringWithSeparator(sendbytes)}");

                    // 如果是主动消息（Function为奇数），添加到回复消息缓存等待回复
                    if (msg.Function % 2 == 1)
                    {
                        var systemBytesStr = SecsGemMessageTools.ByteArrayToHexStringWithSeparator(msg.SystemBytes.ToArray());
                        //ReplyMessageInfo.TryAdd(systemBytesStr, msg);
                    }
                }
                else
                {
                    WriteSecsGemLog($"发送SECS/GEM消息失败: S{msg.Stream}F{msg.Function}");
                }
            }
            catch (Exception ex)
            {
                WriteSecsGemLog($"发送消息时出错: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region 工具方法

        /// <summary>
        /// 等待回复消息
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

        #region 日志记录
        /// <summary>
        /// 记录SECS/GEM交互日志
        /// </summary>
        public void WriteSecsGemLog(string strData)
        {
            Task.Run(() =>
            {
                lock (_logLock)
                {
                    try
                    {
                        StringBuilder strFile = new StringBuilder();
                        strFile.AppendFormat("{0}\\{1}\\{2}\\{3}\\",
                            _logPathName,
                            "SecsGem",
                            DateTime.Now.Year.ToString(),
                            DateTime.Now.Month.ToString());

                        if (!Directory.Exists(strFile.ToString()))
                        {
                            Directory.CreateDirectory(strFile.ToString());
                        }

                        strFile.Append(DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                        using (StreamWriter swAppend = File.AppendText(strFile.ToString()))
                        {
                            StringBuilder str = new StringBuilder();
                            str.AppendFormat("[{0}][{1}]    [{2}]",
                                DateTime.Now,
                                DateTime.Now.Millisecond.ToString("d4"),
                                strData);
                            swAppend.WriteLine(str.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"写入日志失败: {ex.Message}");
                    }
                }
            });
        }
        #endregion

        #region IDisposable Support
        private bool _disposed = false;

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
