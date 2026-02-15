namespace PF.Core.Interfaces.Hardware.Motor.Basic
{
    /// <summary>
    /// 电机基础接口，定义电机通用的属性和操作
    /// </summary>
    public interface IAxis : IHardwareDevice
    {
        /// <summary>当前实时物理位置</summary>
        double CurrentPosition { get; }

        /// <summary>是否正在运动中</summary>
        bool IsMoving { get; }

        /// <summary>是否触碰正向限位传感器</summary>
        bool IsPositiveLimit { get; }

        /// <summary>是否触碰负向限位传感器</summary>
        bool IsNegativeLimit { get; }
        /// <summary>
        /// 电机使能
        /// </summary>
        /// <returns>操作是否成功</returns>
        Task<bool> EnableAsync();

        /// <summary>
        /// 电机去使能
        /// </summary>
        /// <returns>操作是否成功</returns>
        Task<bool> DisableAsync();

        /// <summary>
        /// 正向点动
        /// </summary>
        /// <param name="velocity">点动速度，单位取决于电机类型</param>
        /// <returns>操作是否成功</returns>
        Task<bool> JogPositiveAsync(double velocity);

        /// <summary>
        /// 反向点动
        /// </summary>
        /// <param name="velocity">点动速度，单位取决于电机类型</param>
        /// <returns>操作是否成功</returns>
        Task<bool> JogNegativeAsync(double velocity);

        /// <summary>
        /// 停止运动
        /// </summary>
        /// <returns>操作是否成功</returns>
        Task<bool> StopAsync();

        /// <summary>
        /// 速度运行
        /// </summary>
        /// <param name="velocity">运动速度，正负表示方向</param>
        /// <returns>操作是否成功</returns>
        Task<bool> RunWithVelocityAsync(double velocity);

        Task<bool> HomeAsync(CancellationToken token = default);
        Task<bool> MoveToPositionAsync(double position, double velocity, CancellationToken token = default);
        Task<bool> MoveRelativeAsync(double distance, double velocity, CancellationToken token = default);

    }
}
