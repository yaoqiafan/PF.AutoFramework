using PF.Core.Interfaces.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware;

namespace PF.Workstation.Demo.Hardware
{
    /// <summary>
    /// 【硬件层示例】模拟X轴伺服电机
    ///
    /// 继承链：SimXAxis → BaseDevice → IHardwareDevice
    ///                              → IAxis
    ///
    /// BaseDevice 已提供：
    ///   · 连接重试（最多3次，间隔2s）
    ///   · 模拟模式拦截（IsSimulated=true 时跳过真实硬件）
    ///   · 统一报警抛出（RaiseAlarm → AlarmTriggered 事件）
    ///   · IDisposable 清理
    ///
    /// 本类只需实现三个钩子（InternalConnectAsync / InternalDisconnectAsync / InternalResetAsync）
    /// 以及 IAxis 的运动控制方法。
    /// 实际项目中将 Task.Delay 替换为厂商运动控制 SDK 调用即可。
    /// </summary>
    public class SimXAxis : BaseDevice, IAxis
    {
        private double _currentPosition;
        private bool _isMoving;
        private bool _isEnabled;

        public int AxisIndex { get; }
        public double CurrentPosition => _currentPosition;
        public bool IsMoving => _isMoving;
        public bool IsPositiveLimit => _currentPosition >= 500.0;
        public bool IsNegativeLimit => _currentPosition <= 0.0;
        public bool IsEnabled => _isEnabled;

        public SimXAxis( int axisIndex, ILogService logger)
            : base($"SIM_X_AXIS_{axisIndex}", $"模拟X轴[{axisIndex}]", isSimulated: true, logger)
        {
            AxisIndex = axisIndex;
            Category = Core.Enums.HardwareCategory.Axis;
        }

        // ── BaseDevice 三个必须实现的钩子（模拟设备直接返回成功）────────────
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
            => Task.FromResult(true);

        protected override Task InternalDisconnectAsync()
            => Task.CompletedTask;

        protected override Task InternalResetAsync(CancellationToken token)
        {
            _isMoving = false;
            return Task.CompletedTask;
        }

        // ── IAxis 运动控制实现 ──────────────────────────────────────────────

        public Task<bool> EnableAsync()
        {
            _isEnabled = true;
            _logger.Info($"[{DeviceName}] 伺服使能 ON");
            return Task.FromResult(true);
        }

        public Task<bool> DisableAsync()
        {
            _isEnabled = false;
            _logger.Info($"[{DeviceName}] 伺服使能 OFF");
            return Task.FromResult(true);
        }

        public Task<bool> StopAsync()
        {
            _isMoving = false;
            _logger.Warn($"[{DeviceName}] 轴急停！当前位置: {_currentPosition:F2} mm");
            return Task.FromResult(true);
        }

        public async Task<bool> HomeAsync(CancellationToken token = default)
        {
            _logger.Info($"[{DeviceName}] 开始回原点...");
            _isMoving = true;
            await Task.Delay(1500, token); // 模拟回原点耗时
            _currentPosition = 0.0;
            _isMoving = false;
            _logger.Success($"[{DeviceName}] 回原点完成");
            return true;
        }

        /// <summary>
        /// 绝对定位：模拟运动耗时 = 距离 / 速度，支持 CancellationToken 急停打断
        /// </summary>
        public async Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity,
            CancellationToken token = default)
        {
            _logger.Info($"[{DeviceName}] 绝对定位 → {targetPosition:F1} mm @ {velocity} mm/s");
            _isMoving = true;

            double dist = Math.Abs(targetPosition - _currentPosition);
            int ms = Math.Clamp((int)(dist / velocity * 1000), 50, 5000);
            await Task.Delay(ms, token); // token 取消时立即抛 OperationCanceledException

            _currentPosition = targetPosition;
            _isMoving = false;
            _logger.Success($"[{DeviceName}] 到位: {_currentPosition:F1} mm");
            return true;
        }

        public async Task<bool> MoveRelativeAsync(double distance, double velocity,
            CancellationToken token = default)
            => await MoveAbsoluteAsync(_currentPosition + distance, velocity, token);

        public async Task<bool> JogAsync(double velocity, bool isPositive)
        {
            _isMoving = true;
            await Task.Delay(100);
            _currentPosition += isPositive ? velocity * 0.1 : -(velocity * 0.1);
            _isMoving = false;
            return true;
        }

       
    }
}
