using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Motor.Basic;

namespace PF.Workstation.Demo.Hardware
{
    /// <summary>
    /// 【硬件层示例】模拟X轴伺服电机
    ///
    /// 继承链：SimXAxis → BaseAxisDevice → BaseDevice → IHardwareDevice
    ///                                               → IAxis（含点表管理）
    ///
    /// BaseAxisDevice 已提供：
    ///   · 点表 CRUD（AddOrUpdatePoint / DeletePoint / SavePointTable）
    ///   · MoveToPointAsync 便捷方法（按名称移动到预设点位）
    ///   · 点表 JSON 持久化（{dataDirectory}/AxisPoints/{DeviceId}.json）
    ///
    /// BaseDevice 已提供：
    ///   · 连接重试（最多3次，间隔2s）
    ///   · 模拟模式拦截（IsSimulated=true 时跳过真实硬件）
    ///   · 统一报警（RaiseAlarm → AlarmTriggered 事件）
    ///   · IDisposable 清理
    ///
    /// 本类只需实现三个 BaseDevice 钩子 + IAxis 运动控制方法。
    /// 实际项目中将 Task.Delay 替换为厂商运动控制 SDK 调用即可。
    /// </summary>
    public class SimXAxis : BaseAxisDevice
    {
        private double _currentPosition;
        private bool _isMoving;
        private bool _isEnabled;

        public override int AxisIndex { get; }
        public override double CurrentPosition => _currentPosition;
        public override bool IsMoving => _isMoving;
        public override bool IsPositiveLimit => _currentPosition >= 500.0;
        public override bool IsNegativeLimit => _currentPosition <= 0.0;
        public override bool IsEnabled => _isEnabled;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="axisIndex">轴索引号</param>
        /// <param name="logger">日志服务</param>
        /// <param name="dataDirectory">点表 JSON 存储根目录（通常为 %AppData%\PFAutoFrameWork）</param>
        public SimXAxis(int axisIndex, ILogService logger, string dataDirectory)
            : base(
                deviceId:      $"SIM_X_AXIS_{axisIndex}",
                deviceName:    $"模拟X轴[{axisIndex}]",
                isSimulated:   true,
                logger:        logger,
                dataDirectory: dataDirectory)
        {
            AxisIndex = axisIndex;
            Category = Core.Enums.HardwareCategory.Axis;
        }

        // ── BaseDevice 三个钩子（模拟设备直接返回成功）────────────────────────

        protected override Task<bool> InternalConnectAsync(CancellationToken token)
            => Task.FromResult(true);

        protected override Task InternalDisconnectAsync()
            => Task.CompletedTask;

        protected override Task InternalResetAsync(CancellationToken token)
        {
            _isMoving = false;
            return Task.CompletedTask;
        }

        // ── IAxis 运动控制实现 ────────────────────────────────────────────────

        public override Task<bool> EnableAsync()
        {
            _isEnabled = true;
            _logger.Info($"[{DeviceName}] 伺服使能 ON");
            return Task.FromResult(true);
        }

        public override Task<bool> DisableAsync()
        {
            _isEnabled = false;
            _logger.Info($"[{DeviceName}] 伺服使能 OFF");
            return Task.FromResult(true);
        }

        public override Task<bool> StopAsync()
        {
            _isMoving = false;
            _logger.Warn($"[{DeviceName}] 轴急停！当前位置: {_currentPosition:F2} mm");
            return Task.FromResult(true);
        }

        public override async Task<bool> HomeAsync(CancellationToken token = default)
        {
            _logger.Info($"[{DeviceName}] 开始回原点...");
            _isMoving = true;
            await Task.Delay(1500, token);
            _currentPosition = 0.0;
            _isMoving = false;
            _logger.Success($"[{DeviceName}] 回原点完成");
            return true;
        }

        /// <summary>
        /// 绝对定位：模拟运动耗时 = 距离 / 速度，支持 CancellationToken 急停打断
        /// </summary>
        public override async Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity,
            CancellationToken token = default)
        {
            _logger.Info($"[{DeviceName}] 绝对定位 → {targetPosition:F1} mm @ {velocity} mm/s");
            _isMoving = true;

            double dist = Math.Abs(targetPosition - _currentPosition);
            int ms = Math.Clamp((int)(dist / velocity * 1000), 50, 5000);
            await Task.Delay(ms, token);

            _currentPosition = targetPosition;
            _isMoving = false;
            _logger.Success($"[{DeviceName}] 到位: {_currentPosition:F1} mm");
            return true;
        }

        public override async Task<bool> MoveRelativeAsync(double distance, double velocity,
            CancellationToken token = default)
            => await MoveAbsoluteAsync(_currentPosition + distance, velocity, token);

        public override async Task<bool> JogAsync(double velocity, bool isPositive)
        {
            _isMoving = true;
            await Task.Delay(100);
            _currentPosition += isPositive ? velocity * 0.1 : -(velocity * 0.1);
            _isMoving = false;
            return true;
        }
    }
}
