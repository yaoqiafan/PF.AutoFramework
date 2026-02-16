namespace PF.Core.Interfaces.Hardware.Motor.Basic
{
    /// <summary>
    /// 单轴运动控制器接口，继承自基础硬件设备接口
    /// </summary>
    public interface IAxis : IHardwareDevice
    {
        #region 轴状态属性

        /// <summary>轴在系统中的索引号 (如 0, 1, 2...)</summary>
        int AxisIndex { get; }

        /// <summary>当前实时物理位置 (工程单位，如 mm)</summary>
        double CurrentPosition { get; }

        /// <summary>是否正在运动中</summary>
        bool IsMoving { get; }

        /// <summary>是否触碰正向硬件限位传感器</summary>
        bool IsPositiveLimit { get; }

        /// <summary>是否触碰负向硬件限位传感器</summary>
        bool IsNegativeLimit { get; }

        /// <summary>伺服是否已使能 (Servo On)</summary>
        bool IsEnabled { get; }

        #endregion

        #region 轴控制指令

        /// <summary>
        /// 伺服使能
        /// </summary>
        Task<bool> EnableAsync();

        /// <summary>
        /// 伺服断使能
        /// </summary>
        Task<bool> DisableAsync();

        /// <summary>
        /// 停止运动 (减速停止或急停，由具体实现决定)
        /// </summary>
        Task<bool> StopAsync();

        /// <summary>
        /// 回原点动作 (Home)
        /// </summary>
        Task<bool> HomeAsync(CancellationToken token = default);

        /// <summary>
        /// 绝对位置定位
        /// </summary>
        /// <param name="targetPosition">目标绝对位置</param>
        /// <param name="velocity">运动速度</param>
        /// <param name="token">取消令牌，用于急停打断</param>
        Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, CancellationToken token = default);

        /// <summary>
        /// 相对位置定位
        /// </summary>
        /// <param name="distance">相对移动距离</param>
        /// <param name="velocity">运动速度</param>
        /// <param name="token">取消令牌，用于急停打断</param>
        Task<bool> MoveRelativeAsync(double distance, double velocity, CancellationToken token = default);

        /// <summary>
        /// 持续点动 (Jog)
        /// </summary>
        /// <param name="velocity">点动速度</param>
        /// <param name="isPositive">是否为正向</param>
        Task<bool> JogAsync(double velocity, bool isPositive);

        #endregion
    }
}
