using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;

namespace PF.Core.Interfaces.Device.Hardware.Motor.Basic
{
    /// <summary>
    /// 单轴运动控制器接口，继承自基础硬件设备接口
    /// </summary>
    public interface IAxis : IHardwareDevice
    {
        #region 点表管理 (Point Table)

        /// <summary>当前轴的所有预设点位（只读快照，修改请通过 AddOrUpdatePoint）</summary>
        IReadOnlyList<AxisPoint> PointTable { get; }

        /// <summary>
        /// 按名称移动到预设点位（坐标和速度从点表中自动取得）。
        /// 若点位不存在，抛出 <see cref="KeyNotFoundException"/>。
        /// </summary>
        Task<bool> MoveToPointAsync(string pointName, CancellationToken token = default);

        /// <summary>添加新点位或按 Name 覆盖已有点位</summary>
        void AddOrUpdatePoint(AxisPoint point);

        /// <summary>按名称删除点位，返回是否删除成功</summary>
        bool DeletePoint(string pointName);

        /// <summary>将当前内存中的点表持久化保存到存储介质</summary>
        void SavePointTable();

        #endregion

        #region 轴状态属性

        /// <summary>轴在系统中的索引号 (如 0, 1, 2...)</summary>
        int AxisIndex { get; }

        /// <summary>当前实时物理位置 (工程单位，如 mm)</summary>
        double? CurrentPosition { get; }

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

        /// <summary>伺服使能</summary>
        Task<bool> EnableAsync();

        /// <summary>伺服断使能</summary>
        Task<bool> DisableAsync();

        /// <summary>停止运动 (减速停止或急停，由具体实现决定)</summary>
        Task<bool> StopAsync();

        /// <summary>回原点动作 (Home)</summary>
        Task<bool> HomeAsync(CancellationToken token = default);

        /// <summary>绝对位置定位</summary>
        Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, CancellationToken token = default);

        /// <summary>相对位置定位</summary>
        Task<bool> MoveRelativeAsync(double distance, double velocity, CancellationToken token = default);

        /// <summary>持续点动 (Jog)</summary>
        Task<bool> JogAsync(double velocity, bool isPositive);

        #endregion
    }
}
