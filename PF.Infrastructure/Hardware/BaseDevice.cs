using PF.Core.Interfaces.Hardware;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Hardware
{
    /// <summary>
    /// 工业硬件设备抽象基类
    /// 封装了底层的状态机、重连逻辑、脱机模拟机制以及统一的异常拦截
    /// </summary>
    public abstract class BaseDevice : IHardwareDevice
    {
        protected readonly ILogService _logger; // 依赖注入日志服务
        private bool _isConnected;
        private bool _hasAlarm;
        private bool _isDisposed;

        #region IHardwareDevice 属性实现

        public string DeviceId { get; }
        public string DeviceName { get; }
        public bool IsSimulated { get; }

        public bool IsConnected
        {
            get => _isConnected;
            protected set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    // 状态改变时自动触发事件，供 UI 层（红绿灯指示）订阅
                    ConnectionChanged?.Invoke(this, _isConnected);

                    if (_isConnected)
                        _logger?.Success($"[{DeviceName}] 设备已连接");
                    else
                        _logger?.Warn($"[{DeviceName}] 设备已断开");
                }
            }
        }

        public bool HasAlarm
        {
            get => _hasAlarm;
            protected set => _hasAlarm = value;
        }

        public event EventHandler<bool> ConnectionChanged;
        public event EventHandler<DeviceAlarmEventArgs> AlarmTriggered;

        #endregion

        /// <summary>
        /// 构造函数 (强制要求子类提供基本信息和日志服务)
        /// </summary>
        protected BaseDevice(string deviceId, string deviceName, bool isSimulated, ILogService logger)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            IsSimulated = isSimulated;
            _logger = logger;
        }

        #region 核心生命周期与重连封装 (Template Methods)

        /// <summary>
        /// 异步连接设备（包含脱机拦截与重连机制）
        /// </summary>
        public async Task<bool> ConnectAsync(CancellationToken token = default)
        {
            if (IsConnected) return true;

            // 1. 脱机模拟模式拦截
            if (IsSimulated)
            {
                _logger?.Info($"[{DeviceName}] 当前为模拟脱机模式，跳过真实连接过程。");
                await Task.Delay(500, token); // 模拟连接耗时
                IsConnected = true;
                HasAlarm = false;
                return true;
            }

            // 2. 真实硬件连接与重试逻辑 (默认重试 3 次)
            int maxRetries = 3;
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    _logger?.Info($"[{DeviceName}] 正在尝试连接... (第 {i} 次)");

                    // 调用子类必须实现的真实物理连接方法
                    bool success = await InternalConnectAsync(token);

                    if (success)
                    {
                        IsConnected = true;
                        HasAlarm = false;
                        return true;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.Warn($"[{DeviceName}] 连接操作被外部取消。");
                    return false;
                }
                catch (Exception ex)
                {
                    _logger?.Error($"[{DeviceName}] 第 {i} 次连接发生异常: {ex.Message}");

                    if (i == maxRetries)
                    {
                        // 达到最大重试次数，触发全局底层报警
                        RaiseAlarm("ERR_CONN_FAILED", $"设备连接彻底失败，已重试 {maxRetries} 次", ex);
                    }
                    else
                    {
                        // 间隔 2 秒后重试
                        await Task.Delay(2000, token);
                    }
                }
            }

            return false;
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected) return;

            try
            {
                if (!IsSimulated)
                {
                    await InternalDisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 断开连接时发生异常: {ex.Message}");
            }
            finally
            {
                // 无论物理断开是否成功，逻辑状态必须置为断开
                IsConnected = false;
            }
        }

        public async Task<bool> ResetAsync(CancellationToken token = default)
        {
            _logger?.Info($"[{DeviceName}] 正在复位并清除报警...");
            try
            {
                if (!IsSimulated)
                {
                    await InternalResetAsync(token);
                }
                HasAlarm = false;
                return true;
            }
            catch (Exception ex)
            {
                RaiseAlarm("ERR_RESET_FAILED", "设备复位失败", ex);
                return false;
            }
        }

        #endregion

        #region 供子类继承与实现的方法 (Hook Methods)

        /// <summary>子类实现：具体的物理连接建立代码（如 TCP Socket Connect, 或调用板卡 InitDll）</summary>
        protected abstract Task<bool> InternalConnectAsync(CancellationToken token);

        /// <summary>子类实现：具体的物理连接断开代码</summary>
        protected abstract Task InternalDisconnectAsync();

        /// <summary>子类实现：具体的报警复位代码</summary>
        protected abstract Task InternalResetAsync(CancellationToken token);

        #endregion

        #region 内部工具方法

        /// <summary>
        /// 供子类在运行时抛出硬件报警（例如运行中网线断开、伺服过载等）
        /// </summary>
        protected void RaiseAlarm(string errorCode, string message, Exception internalException = null)
        {
            HasAlarm = true;
            _logger?.Fatal($"[{DeviceName}] 硬件报警 [{errorCode}]: {message}",exception: internalException);

            // 通过事件推给 DeviceManager 或状态机，进而通过 Prism EventAggregator 广播全网
            AlarmTriggered?.Invoke(this, new DeviceAlarmEventArgs
            {
                ErrorCode = errorCode,
                ErrorMessage = message,
                InternalException = internalException
            });
        }

        #endregion

        #region IDisposable 实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (IsConnected)
                    {
                        DisconnectAsync().Wait(); // 同步等待断开
                    }
                }
                _isDisposed = true;
            }
        }

        #endregion
    }
}
