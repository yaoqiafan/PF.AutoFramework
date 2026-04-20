using PF.Core.Enums;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PF.Infrastructure.Hardware
{
    /// <summary>
    /// 工业硬件设备抽象基类。
    /// 封装了底层的状态机、重连逻辑、脱机模拟机制、统一异常拦截，
    /// 以及 <see cref="INotifyPropertyChanged"/> 与后台健康监控循环。
    /// </summary>
    public abstract class BaseDevice : IHardwareDevice, INotifyPropertyChanged
    {
        /// <summary>
        /// 日志服务实例
        /// </summary>
        protected readonly ILogService _logger;
        private bool _isConnected;
        private bool _hasAlarm;
        private bool _isDisposed;

        // 健康监控后台任务
        private CancellationTokenSource? _healthMonitorCts;
        private Task? _healthMonitorTask;

        #region INotifyPropertyChanged

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion

        #region IHardwareDevice 属性实现

        /// <summary>
        /// 设备唯一标识
        /// </summary>
        public string DeviceId { get; }
        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get; }
        /// <summary>
        /// 硬件分类
        /// </summary>
        public HardwareCategory Category { get; protected set; } = HardwareCategory.General;
        /// <summary>
        /// 是否为模拟模式
        /// </summary>
        public bool IsSimulated { get; set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            protected set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    ConnectionChanged?.Invoke(this, _isConnected);
                    RaisePropertyChanged();

                    if (_isConnected)
                        _logger?.Success($"[{DeviceName}] 设备已连接");
                    else
                        _logger?.Warn($"[{DeviceName}] 设备已断开");
                }
            }
        }

        /// <summary>
        /// 是否存在报警
        /// </summary>
        public bool HasAlarm
        {
            get => _hasAlarm;
            protected set
            {
                if (_hasAlarm != value)
                {
                    bool wasAlarm = _hasAlarm;
                    _hasAlarm = value;
                    RaisePropertyChanged();

                    // true → false：设备自恢复，向上通知 BaseMechanism 检查是否整体清警
                    if (wasAlarm && !_hasAlarm)
                        HardwareAlarmAutoCleared?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        public event EventHandler<bool> ConnectionChanged;
        /// <summary>
        /// 报警触发事件
        /// </summary>
        public event EventHandler<DeviceAlarmEventArgs> AlarmTriggered;
        /// <summary>
        /// 硬件报警自动清除事件
        /// </summary>
        public event EventHandler HardwareAlarmAutoCleared;

        /// <summary>
        /// 硬件专用日志记录器
        /// </summary>
        public readonly CategoryLogger HardwareLogger;

        #endregion

        /// <summary>
        /// 健康检查轮询间隔（毫秒）。
        /// 模拟模式下自动扩大为 5 倍（避免几十个模拟设备空转浪费 CPU）。
        /// </summary>
        protected virtual int HealthCheckIntervalMs => 1000;

        /// <summary>
        /// 构造函数（强制要求子类提供基本信息和日志服务）
        /// </summary>
        protected BaseDevice(string deviceId, string deviceName, bool isSimulated, ILogService logger)
        {
            DeviceId    = deviceId;
            DeviceName  = deviceName;
            IsSimulated = isSimulated;
            _logger     = logger;
            HardwareLogger = CategoryLoggerFactory.Hardware(logger);
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
                _logger?.Info($"[{DeviceName}] 当前为模拟脱机模式，进入模拟连接流程。");
                bool simResult = await InternalConnectSimulatedAsync(token);
                if (simResult)
                {
                    IsConnected = true;
                    HasAlarm    = false;
                    StartHealthMonitor();
                }
                return simResult;
            }

            // 2. 真实硬件连接与重试逻辑（默认重试 3 次）
            int maxRetries = 3;
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    _logger?.Info($"[{DeviceName}] 正在尝试连接... (第 {i} 次)");

                    bool success = await InternalConnectAsync(token);
                    if (success)
                    {
                        IsConnected = true;
                        HasAlarm    = false;
                        StartHealthMonitor();
                        return true;
                    }

                    // InternalConnectAsync 返回 false 但未抛异常（如握手未通过），
                    // 补充等待间隔，避免高频空转瞬间耗尽全部重试次数。
                    _logger?.Warn($"[{DeviceName}] 第 {i} 次连接返回失败，等待后重试...");
                    if (i < maxRetries)
                        await Task.Delay(2000, token);
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
                        RaiseAlarm("ERR_CONN_FAILED", $"设备连接彻底失败，已重试 {maxRetries} 次", ex);
                    else
                        await Task.Delay(2000, token);
                }
            }

            return false;
        }

        /// <summary>
        /// 异步断开设备连接
        /// </summary>
        public async Task DisconnectAsync(CancellationToken token = default)
        {
            if (!IsConnected) return;

            // 停止健康监控，再进行物理断开
            await StopHealthMonitorAsync();

            try
            {
                if (!IsSimulated)
                    await InternalDisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 断开连接时发生异常: {ex.Message}");
            }
            finally
            {
                IsConnected = false;
            }
        }

        /// <summary>
        /// 异步复位设备并清除报警
        /// </summary>
        public async Task<bool> ResetAsync(CancellationToken token = default)
        {
            _logger?.Info($"[{DeviceName}] 正在复位并清除报警...");
            try
            {
                if (!IsSimulated)
                    await InternalResetAsync(token);

                HasAlarm = false;
                return true;
            }
            catch (Exception ex)
            {
                RaiseAlarm("ERR_RESET_FAILED", "设备复位失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 仅清除硬件层报警标志，不执行回原点（对应驱动器"清警"指令）。
        /// 模拟模式下直接将 HasAlarm 置为 false 并返回 true。
        /// </summary>
        public virtual async Task<bool> ResetHardwareAlarmAsync(CancellationToken token = default)
        {
            _logger?.Info($"[{DeviceName}] 执行硬件清警复位...");

            if (IsSimulated)
            {
                HasAlarm = false;
                return true;
            }

            try
            {
                await InternalResetHardwareAlarmAsync(token).ConfigureAwait(false);
                HasAlarm = false;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 硬件清警失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 子类重写：调用底层 SDK 的清警 API（如伺服清警命令）。
        /// 默认实现为空（无额外清警指令的设备无需重写）。
        /// </summary>
        protected virtual Task InternalResetHardwareAlarmAsync(CancellationToken token)
            => Task.CompletedTask;

        #endregion

        #region 健康监控循环

        /// <summary>
        /// 连接成功后启动后台健康监控任务。
        /// </summary>
        private void StartHealthMonitor()
        {
            // 防止重复启动
            _healthMonitorCts?.Cancel();
            _healthMonitorCts?.Dispose();

            _healthMonitorCts  = new CancellationTokenSource();
            _healthMonitorTask = Task.Run(
                () => HealthMonitorLoopAsync(_healthMonitorCts.Token),
                _healthMonitorCts.Token);
        }

        /// <summary>
        /// 停止健康监控任务，等待其退出（最多 3 秒）。
        /// </summary>
        private async Task StopHealthMonitorAsync()
        {
            if (_healthMonitorCts == null) return;

            _healthMonitorCts.Cancel();

            if (_healthMonitorTask != null)
            {
                try
                {
                    // 等待任务退出，超时后继续（不阻塞关闭流程）
                    await Task.WhenAny(_healthMonitorTask, Task.Delay(3000)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger?.Warn($"[{DeviceName}] 健康监控停止时发生异常: {ex.Message}");
                }
            }

            _healthMonitorCts.Dispose();
            _healthMonitorCts  = null;
            _healthMonitorTask = null;
        }

        /// <summary>
        /// 后台健康轮询主循环。
        /// 模拟模式下延长间隔（HealthCheckIntervalMs × 5）避免空转浪费 CPU。
        /// </summary>
        private async Task HealthMonitorLoopAsync(CancellationToken token)
        {
            int intervalMs = IsSimulated
                ? HealthCheckIntervalMs * 5
                : HealthCheckIntervalMs;

            while (!token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    await Task.Delay(intervalMs, token).ConfigureAwait(false);

                    if (!token.IsCancellationRequested && IsConnected)
                        await InternalCheckHealthAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!HasAlarm)
                        RaiseAlarm("ERR_HEALTH_CHECK", $"健康检查异常: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 子类重写：主动读取硬件状态并更新缓存属性；获取到硬件错误码时调用 <see cref="RaiseAlarm"/>。
        /// 默认实现为空（模拟模式或无需轮询的设备无需重写）。
        /// </summary>
        protected virtual Task InternalCheckHealthAsync(CancellationToken token)
            => Task.CompletedTask;

        #endregion

        #region 供子类继承与实现的方法 (Hook Methods)

        /// <summary>
        /// 模拟模式下的连接逻辑。默认实现：延迟 500ms 后返回 true。
        /// 子类可重写以在模拟模式下初始化虚拟状态。
        /// </summary>
        protected virtual async Task<bool> InternalConnectSimulatedAsync(CancellationToken token)
        {
            await Task.Delay(500, token);
            return true;
        }

        /// <summary>子类实现：具体的物理连接建立代码</summary>
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
            _logger?.Fatal($"[{DeviceName}] 硬件报警 [{errorCode}]: {message}", exception: internalException);

            AlarmTriggered?.Invoke(this, new DeviceAlarmEventArgs
            {
                ErrorCode         = errorCode,
                ErrorMessage      = message,
                InternalException = internalException
            });
        }

        /// <summary>
        /// 外部触发模拟硬件报警（供调试页面使用）。
        /// 无论是否处于模拟模式，均可调用，用于验证完整的报警级联链路：
        /// Device → Mechanism → Station → MasterController。
        /// </summary>
        /// <param name="errorCode">报警码</param>
        /// <param name="message">报警描述</param>
        public void SimulateAlarm(string errorCode, string message)
        {
            RaiseAlarm(errorCode, message);
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的核心方法
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // 先取消健康监控（不等待，Dispose 不应阻塞）
                    _healthMonitorCts?.Cancel();
                    _healthMonitorCts?.Dispose();
                    _healthMonitorCts = null;

                    if (IsConnected)
                    {
                        // 在线程池线程上运行，脱离当前 SynchronizationContext 防止死锁
                        Task.Run(() => DisconnectAsync()).GetAwaiter().GetResult();
                    }
                }
                _isDisposed = true;
            }
        }

        #endregion
    }
}
