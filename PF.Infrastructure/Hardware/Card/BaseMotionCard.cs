using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;

namespace PF.Infrastructure.Hardware.Card
{
    /// <summary>
    /// 运动控制卡抽象基类
    ///
    /// 继承链：ConcreteCard → BaseMotionCard → BaseDevice → IHardwareDevice
    ///                                                     → IMotionCard
    ///
    /// BaseDevice 已提供：
    ///   · 连接重试（最多3次，间隔2s）
    ///   · 模拟模式拦截（IsSimulated=true 时跳过真实硬件）
    ///   · 统一报警（RaiseAlarm → AlarmTriggered 事件）
    ///   · IDisposable 清理
    ///
    /// 本类额外提供：
    ///   · LoadConfigAsync 公开入口（含文件存在检查、异常拦截、日志记录）
    ///   · InternalLoadConfigAsync 钩子留给具体厂商板卡类实现配置解析逻辑
    ///   · 所有运动控制 / IO 操作方法以 abstract 形式声明，强制子类用厂商 SDK 实现
    ///
    /// 具体厂商板卡类需实现：
    ///   · CardIndex / AxisCount / InputCount / OutputCount 属性
    ///   · InternalConnectAsync / InternalDisconnectAsync / InternalResetAsync
    ///   · InternalLoadConfigAsync（可选，若不需要配置文件可直接返回 true）
    ///   · 运动控制方法：Enable/Disable/Stop/Home/MoveAbsolute/MoveRelative/Jog（带 axisIndex）
    ///   · 轴状态读取方法：GetAxisCurrentPosition / IsAxisMoving / IsAxisPositiveLimit 等
    ///   · IO 控制方法：ReadInputPort / WriteOutputPort / ReadOutputPort（带 portIndex）
    /// </summary>
    public abstract class BaseMotionCard : BaseDevice, IMotionCard
    {
        #region IMotionCard 属性（由子类实现）

        /// <inheritdoc/>
        public abstract int CardIndex { get; }

        /// <inheritdoc/>
        public abstract int AxisCount { get; }

        /// <inheritdoc/>
        public abstract int InputCount { get; }

        /// <inheritdoc/>
        public abstract int OutputCount { get; }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        protected BaseMotionCard(string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.MotionCard;
        }

        #region IMotionCard.LoadConfigAsync 实现（模板方法）

        /// <summary>
        /// 加载板卡硬件配置文件（公开入口）
        ///
        /// 封装了：文件存在校验、异常拦截、成功/失败日志。
        /// 具体的文件解析逻辑委托给 InternalLoadConfigAsync。
        /// </summary>
        public async Task<bool> LoadConfigAsync(string configFilePath)
        {
            _logger?.Info($"[{DeviceName}] 加载板卡配置文件: {configFilePath}");
            try
            {
                if (!File.Exists(configFilePath))
                {
                    _logger?.Warn($"[{DeviceName}] 板卡配置文件不存在，跳过加载: {configFilePath}");
                    return false;
                }

                var result = await InternalLoadConfigAsync(configFilePath).ConfigureAwait(false);

                if (result)
                    _logger?.Success($"[{DeviceName}] 板卡配置文件加载成功");
                else
                    _logger?.Warn($"[{DeviceName}] 板卡配置文件加载失败（InternalLoadConfigAsync 返回 false）");

                return result;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 加载板卡配置文件时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 子类实现：解析具体厂商板卡的配置文件格式（如 INI / XML / 二进制）。
        /// 调用前已保证文件存在，异常由 LoadConfigAsync 统一捕获。
        /// </summary>
        protected abstract Task<bool> InternalLoadConfigAsync(string configFilePath);

        #endregion

        #region 运动控制方法（abstract — 子类用厂商 SDK 实现，第一参数为板卡内物理轴号）

        /// <inheritdoc/>
        public abstract Task<bool> EnableAxisAsync(int axisIndex);

        /// <inheritdoc/>
        public abstract Task<bool> DisableAxisAsync(int axisIndex);

        /// <inheritdoc/>
        public abstract Task<bool> StopAxisAsync(int axisIndex, bool IsEmgStop = false);

        /// <inheritdoc/>
        public abstract Task<bool> HomeAxisAsync(int axisIndex, int HomeModel, int HomeVel, int HomeAcc, int HomeDec, int HomeOffest, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> MoveAbsoluteAsync(int axisIndex, double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> MoveRelativeAsync(int axisIndex, double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> JogAsync(int axisIndex, double velocity, double Acc, double Dec, bool isPositive);

        #endregion

        #region 轴状态读取方法（abstract — 子类用厂商 SDK 实现）

        /// <inheritdoc/>
        public abstract double? GetAxisCurrentPosition(int axisIndex);



        public abstract MotionIOStatus GetMotionIOStatus(int axisIndex);

        #endregion

        #region IO 控制方法（abstract — 子类用厂商 SDK 实现，第一参数为板卡内物理端口号）

        /// <inheritdoc/>
        public abstract bool? ReadInputPort(int portIndex);

        /// <inheritdoc/>
        public abstract bool WriteOutputPort(int portIndex, bool value);

        /// <inheritdoc/>
        public abstract bool? ReadOutputPort(int portIndex);

        #endregion


        #region 高级功能


        #region 位置锁存

        public abstract Task<bool> SetLatchMode(int LatchNo, int AxisNo, int InPutPort, int LtcMode = 0, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, CancellationToken token = default);



        public abstract Task<int> GetLatchNumber(int LatchNo, int AxisNo, CancellationToken token = default);


        public abstract Task<double?> GetLatchPos(int LatchNo, int AxisNo, CancellationToken token = default);

        #endregion 位置锁存


        #endregion 高级功能
    }
}
