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
    public class SecsGemManger : Core.Interfaces.SecsGem.ISecsGemManger
    {
        private readonly IParams _paramManger;
        private readonly ICommandManager _commandManager;
        private readonly IinternalClient _secsGemClient;
        private readonly ISecsGemMessageUpdater _messageUpdater;
        private bool _disposed = false;

        public SecsGemManger(IParams paramManger, ICommandManager commandManager, IinternalClient secsGemClient, ISecsGemMessageUpdater messageUpdater)
        {
            _paramManger = paramManger ?? throw new ArgumentNullException(nameof(paramManger));
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
            _secsGemClient = secsGemClient ?? throw new ArgumentNullException(nameof(secsGemClient));
            _messageUpdater = messageUpdater ?? throw new ArgumentNullException(nameof(messageUpdater));
        }

        public bool IsConnected => _secsGemClient.SecsGemStatus;

        public IParams ParamsManager => _paramManger;

        public ICommandManager CommandManager => _commandManager;

        public IinternalClient SecsGemClient => _secsGemClient;
        public ISecsGemMessageUpdater MessageUpdater => _messageUpdater;

        public event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;

        public async Task<bool> InitializeAsync()
        {
            try
            {
                //初始化参数
                bool res1 = await ParamsManager.InitializationParams();

                //验证参数正确性
                bool res2 = await ParamsManager.ValidateCommand();

                bool res3 = await ConnectAsync();

                return res1 & res2;
            }
            catch (Exception ex)
            {
                // 记录日志
                Console.WriteLine($"InitializeAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                bool res3 = await SecsGemClient.InitializationClient();

                if (res3)
                {
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // 析构函数
        ~SecsGemManger()
        {
            Dispose(false);
        }

        #endregion
    }
}
