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
        /// SECSGem״̬
        /// </summary>
        private bool _SecsStatus = false;

        /// <summary>
        /// ��̨���
        /// </summary>
        byte[] _deviceId = new byte[] { 0xFF, 0xFF };

        /// <summary>
        /// SecsGem���ӿͻ���ID
        /// </summary>
        private string SecsGemClientId = string.Empty;

        /// <summary>
        /// ���ؽ����ͻ���ID
        /// </summary>
        private string LocationClientId = string.Empty;

        /// <summary>
        /// SECSGEM������
        /// </summary>
        private TcpServer SecsGemServer;

        /// <summary>
        /// ���ؽ���������
        /// </summary>
        private TcpServer LocationServer;

        /// <summary>
        /// ���SECSGEM������Ϣ�Ķ���
        /// </summary>
        private ConcurrentQueue<byte[]> SecsGemMessageQueue = new ConcurrentQueue<byte[]>();

        /// <summary>
        /// Ϊÿ��SecsGem�ͻ���ά������Ϣ������
        /// </summary>
        private ConcurrentDictionary<string, MessageBuffer> _secsGemClientBuffers =
            new ConcurrentDictionary<string, MessageBuffer>();

        #endregion Params

        #region �ڲ��� - ��Ϣ������
        /// <summary>
        /// ��Ϣ�����������ڴ���ճ���Ͱ������
        /// </summary>
        private class MessageBuffer
        {
            private List<byte> _buffer = new List<byte>();
            private readonly object _lock = new object();

            /// <summary>
            /// �򻺳�����������
            /// </summary>
            public void AppendData(byte[] data)
            {
                lock (_lock)
                {
                    _buffer.AddRange(data);
                }
            }

            /// <summary>
            /// ���Դӻ�������ȡ������SecsGem��Ϣ
            /// </summary>
            /// <returns>��������Ϣ�б�</returns>
            public List<byte[]> ExtractCompleteMessages()
            {
                List<byte[]> completeMessages = new List<byte[]>();

                lock (_lock)
                {
                    while (_buffer.Count >= 4)
                    {
                        // ��ȡ��Ϣ���ȣ������
                        byte[] lengthBytes = _buffer.Take(4).ToArray();
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(lengthBytes);

                        int messageLength = BitConverter.ToInt32(lengthBytes, 0);

                        // ����Ƿ��Ѿ��յ���������Ϣ
                        // �ܳ��� = 4�ֽڳ����ֶ� + messageLength
                        int totalLength = 4 + messageLength;

                        if (_buffer.Count >= totalLength)
                        {
                            // ��ȡ������Ϣ
                            byte[] completeMessage = _buffer.Take(totalLength).ToArray();
                            completeMessages.Add(completeMessage);

                            // �ӻ������Ƴ��Ѵ���������
                            _buffer.RemoveRange(0, totalLength);
                        }
                        else
                        {
                            // ��û���յ�������Ϣ���ȴ���������
                            break;
                        }
                    }
                }

                return completeMessages;
            }

            /// <summary>
            /// ��ջ�����
            /// </summary>
            public void Clear()
            {
                lock (_lock)
                {
                    _buffer.Clear();
                }
            }

            /// <summary>
            /// ��ȡ��ǰ��������С
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
            // �����ͻ��˵Ļ�����
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

            // Ϊ�¿ͻ��˴�����Ϣ������
            _secsGemClientBuffers.TryAdd(e.ClientId, new MessageBuffer());

            if (!string.IsNullOrEmpty(this.LocationClientId))
            {
                await this.LocationServer.SendAsync(this.LocationClientId,
                    new byte[] { 0x02, (byte)SecsStatus.Connected });
            }
        }

        /// <summary>
        /// ��Ϣ�����ɹ���־
        /// </summary>
        bool MessageIsProcessingSucess = true;


        DateTime MessageIsProcessingFailedDate = DateTime.Now;



        private void SecsGemServer_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            try
            {
                // ��ȡ�򴴽��ͻ��˵���Ϣ������
                if (!_secsGemClientBuffers.TryGetValue(e.ClientId, out var buffer))
                {
                    buffer = new MessageBuffer();
                    _secsGemClientBuffers.TryAdd(e.ClientId, buffer);
                }

                // �����յ����������ӵ�������
                buffer.AppendData(e.Data);

                // ������ȡ��������Ϣ
                var completeMessages = buffer.ExtractCompleteMessages();
                if (completeMessages.Count == 0)
                {
                    if (MessageIsProcessingSucess == false)
                    {
                        if ((DateTime.Now - MessageIsProcessingFailedDate).TotalSeconds > 20)
                        {
                            // ����5��û���յ�������Ϣ����ջ�����
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
                    // ��������Ϣ�������
                    this.SecsGemMessageQueue.Enqueue(message);
                    this.SecsGemWriteLog.Writer.TryWrite(("��������", message));
                }

                // ��ѡ����¼��������С�����ڵ��ԣ�
                if (buffer.Size > 0)
                {
                    _logger.LogDebug($"�ͻ��� {e.ClientId} ������ʣ������: {buffer.Size} �ֽ�");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"����SecsGem����ʱ��������: {ex.Message}");
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

                    // ��֤��Ϣ����
                    if (data.Length < 4)
                    {
                        byte[] send = new byte[] { 0x01, (byte)SecsErrorCode.���ݳ��ȴ��� };
                        await this.LocationServer.SendAsync(this.LocationClientId, send);
                        return;
                    }

                    byte[] len_resp = data.Take(4).ToArray();
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(len_resp);

                    int len = BitConverter.ToInt32(len_resp, 0);
                    if (len != data.Length - 4)
                    {
                        byte[] send = new byte[] { 0x01, (byte)SecsErrorCode.���ݳ��ȴ��� };
                        await this.LocationServer.SendAsync(this.LocationClientId, send);
                    }
                    else
                    {
                        this.SecsGemWriteLog.Writer.TryWrite(("��������", data));
                        this.SecsGemServer?.SendAsync(this.SecsGemClientId, data);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"������������ʱ��������: {ex.Message}");
            }
        }

        #endregion EventHandlers

        #region Methods
        /// <summary>
        /// ����SecsGem������Ϣ
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
                    _logger.LogInformation("ProcessSecsGemServiceInfo ������ȡ��");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"ProcessSecsGemServiceInfo �����쳣: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ��������SecsGem��Ϣ
        /// </summary>
        private async Task ProcessSecsGemMessage(byte[] data, CancellationToken token = default)
        {
            try
            {
                this.SecsGemWriteLog.Writer.TryWrite(("��������", data));
                // ��Ϣ������֤�Ѿ��ڻ���������ʱ���
                // ֱ�ӽ�����Ϣͷ
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
                _logger.LogError($"����SecsGem��Ϣʱ��������: {ex.Message}");
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

            // ����header[5]����LinkNumber
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
                _logger.LogWarning($"δ֪��LinkTestֵ: {linkTest}");
                return;
            }

            byte[] sendData = SecsGemMessageTools.GenerateSecsBytes(message, _deviceId);
            this.SecsGemWriteLog.Writer.TryWrite(("��������", sendData));
            //this.SecsGemWriteLog.Writer.TryWrite(("��������", sendData));

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
                // �� Scope �л�ȡ Scoped ����
                var secsGemDataBase = scope.ServiceProvider.GetRequiredService<ISecsGemDataBase>();
                var manger0 = secsGemDataBase.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                _secsGemSystemParam = (await manger0.GetAllAsync()).Select(t => t.GetSecsGemSystemFormSecsGemSystemEntity()).ToList().FirstOrDefault();
            }


            _logger.LogInformation("SecsGem ��̨�����߳�������");

            try
            {
                LocationServer = new TcpServer("���񱾵ط�����");
                await LocationServer.StartAsync("127.0.0.1", 6800);
                LocationServer.DataReceived += LocationServer_DataReceived;
                LocationServer.ClientConnected += LocationServer_ClientConnected;

                SecsGemServer = new TcpServer("SecsGem������");
                await SecsGemServer.StartAsync(_secsGemSystemParam.IPAddress, _secsGemSystemParam.Port);
                SecsGemServer.DataReceived += SecsGemServer_DataReceived;
                SecsGemServer.ClientConnected += SecsGemServer_ClientConnected;
                SecsGemServer.ClientDisconnected += SecsGemServer_ClientDisconnected;

                // ������������
                _ = ProcessSecsGemServiceInfo(stoppingToken);
                _ = WriteLog(stoppingToken);

                // �ȴ�ֹͣ�ź�
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SecsGem ��̨�����߳��յ�ֹͣ�ź�");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SecsGem ��̨�����߳�ִ��ʱ��������");
            }
            finally
            {
                // ������Դ
                _logger.LogInformation("SecsGem ��̨�����߳���ֹͣ");
            }
        }



        #region ��־��¼ģ��


        /// <summary>
        /// GECSGEM��־������¼��
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
                _logger.LogInformation("WriteLog ������ȡ��");
            }
            catch (Exception ex)
            {
                _logger.LogError("WriteLog �������" + ex.Message + ex.StackTrace);
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
                        _logger.LogError("WriteCustomLog �������" + ex.Message + ex.StackTrace);
                    }
                }

            });
        }


        /// <summary>
        /// �ֽ�����ת��Ϊ���ָ�����ʮ�������ַ���
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

        #endregion ��־��¼ģ��



    }
}