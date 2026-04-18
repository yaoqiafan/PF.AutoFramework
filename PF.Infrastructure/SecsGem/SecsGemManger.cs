using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.SecsGem.Tools;
using System;
using System.Threading.Tasks;
using PF.Core.Entities.SecsGem.Message;

namespace PF.Infrastructure.SecsGem
{
    /// <summary>
    /// SecsGem管理器
    /// </summary>
    public class SecsGemManger : Core.Interfaces.SecsGem.ISecsGemManager
    {
        private readonly IParams _paramManger;
        private readonly ICommandManager _commandManager;
        private readonly IinternalClient _secsGemClient;
        private readonly ISecsGemMessageUpdater _messageUpdater;
        private bool _disposed = false;

        /// <summary>
        /// 构造SecsGem管理器
        /// </summary>
        public SecsGemManger(IParams paramManger, ICommandManager commandManager, IinternalClient secsGemClient, ISecsGemMessageUpdater messageUpdater)
        {
            _paramManger = paramManger ?? throw new ArgumentNullException(nameof(paramManger));
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            _secsGemClient = secsGemClient ?? throw new ArgumentNullException(nameof(secsGemClient));
            _messageUpdater = messageUpdater ?? throw new ArgumentNullException(nameof(messageUpdater));
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _secsGemClient.SecsGemStatus;

        /// <summary>
        /// 参数管理器
        /// </summary>
        public IParams ParamsManager => _paramManger;

        /// <summary>
        /// 命令管理器
        /// </summary>
        public ICommandManager CommandManager => _commandManager;

        /// <summary>
        /// SecsGem客户端
        /// </summary>
        public IinternalClient SecsGemClient => _secsGemClient;
        /// <summary>
        /// 消息更新器
        /// </summary>
        public ISecsGemMessageUpdater MessageUpdater => _messageUpdater;

        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// 异步初始化
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                //初始化参数
                bool res1 = await ParamsManager.InitializationParams();

                //验证参数正确性
                bool res2 = await ParamsManager.ValidateCommand();

                bool res3 = await ConnectAsync();

                // Bug1 Fix: res3 (连接结果) 之前被忽略，现在纳入返回值
                return res1 & res2 & res3;
            }
            catch (Exception ex)
            {
                // 记录日志
                Console.WriteLine($"InitializeAsync failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                bool res3 = await SecsGemClient.InitializationClient();

                if (res3)
                {
                    _secsGemClient.MessageReceived -= OnSecsMessageReceived;
                    _secsGemClient.MessageReceived += OnSecsMessageReceived;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConnectAsync failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _secsGemClient.MessageReceived -= OnSecsMessageReceived;

                // 这里实现断开连接逻辑
                await _secsGemClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DisconnectAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步发送消息
        /// </summary>
        public async Task SendMessageAsync(SecsGemMessage message)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot send message when not connected");
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                // 这里实现发送消息的逻辑
                await _secsGemClient.SendMessage(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendMessageAsync failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送消息并等待回复
        /// </summary>
        public async Task<bool> WaitSendMessageAsync(SecsGemMessage message, string systemBytesHex)
        {
            if (!IsConnected)
            {
                return false;
            }

            if (message == null)
            {
                return false;
            }

            try
            {
                await _secsGemClient.SendMessage(message);

                var rec = await _secsGemClient.WaitForReplyAsync(systemBytesHex);
                return rec != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WaitSendMessageAsync failed: {ex.Message}");
                return false;
            }
        }

        private void OnSecsMessageReceived(object sender, SecsMessageReceivedEventArgs e)
        {
            // 这里可以添加消息处理逻辑
            // 例如：解析消息、执行命令等

            // 触发消息接收事件
            MessageReceived?.Invoke(this, new SecsMessageReceivedEventArgs
            {
                Message = e.Message,
                Timestamp = DateTime.UtcNow
            });
        }

        #region IDisposable Implementation

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    try
                    {
                        // 断开连接
                        if (IsConnected)
                        {
                            DisconnectAsync().GetAwaiter().GetResult();
                        }

                        // 检查并释放客户端资源
                        if (_secsGemClient is IDisposable disposableClient)
                        {
                            disposableClient.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during disposal: {ex.Message}");
                    }

                    MessageReceived = null;
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

        /// <summary>析构函数</summary>
        ~SecsGemManger()
        {
            Dispose(false);
        }

        #endregion
    }
}
