using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using System.Numerics;

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



        /// <summary>
        /// 轴参数列表
        /// </summary>
        AxisParam Param { get; set; }


        /// <summary>
        /// 轴IO映射状态
        /// </summary>
        MotionIOStatus? AxisIOStatus { get; }
        #endregion

        #region 轴控制指令

        /// <summary>伺服使能</summary>
        Task<bool> EnableAsync(CancellationToken token = default);

        /// <summary>伺服断使能</summary>
        Task<bool> DisableAsync(CancellationToken token = default);

        /// <summary>停止运动 (减速停止或急停，由具体实现决定)</summary>
        Task<bool> StopAsync(CancellationToken token = default);

        /// <summary>回原点动作 (Home)</summary>
        Task<bool> HomeAsync(CancellationToken token = default);

        /// <summary>绝对位置定位</summary>
        Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default);

        /// <summary>相对位置定位</summary>
        Task<bool> MoveRelativeAsync(double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default);

        /// <summary>持续点动 (Jog)</summary>
        Task<bool> JogAsync(double velocity, bool isPositive, double Acc, double Dec);








        #endregion




        #region 高级功能

        #region 位置锁存



        /// <summary>
        /// 设置位置锁存参数
        /// </summary>
        /// <param name="LatchNo">锁存器ID</param>
        /// <param name="InPutPort">输入端口号</param>
        /// <param name="LtcMode">锁存模式</param>
        /// <param name="LtcLogic">锁存逻辑</param>
        /// <param name="Filter">滤波器</param>
        /// <param name="LatchSource">锁存源</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> SetLatchMode(int LatchNo, int InPutPort, int LtcMode = 0, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, CancellationToken token = default);



        /// <summary>
        /// 读取位置锁存个数
        /// </summary>
        /// <param name="LatchNo">锁存器ID</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<int> GetLatchNumber(int LatchNo, CancellationToken token = default);



        /// <summary>
        /// 读取锁存位置
        /// </summary>
        /// <param name="LatchNo">锁存器ID</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<double?> GetLatchPos(int LatchNo, CancellationToken token = default);


        #endregion 位置锁存


        #endregion 高级功能


    }


    /// <summary>
    /// 轴参数信息
    /// </summary>
    public class AxisParam
    {

        /// <summary>
        /// 运行速度
        /// </summary>
        public double Vel { get; set; }


        /// <summary>
        /// 运行加速度
        /// </summary>
        public double Acc { get; set; }


        /// <summary>
        /// 运行减速度
        /// </summary>
        public double Dec { get; set; }

        /// <summary>
        /// 正极限硬限位启用标志
        /// </summary>
        public bool PelVisEnabled { get; set; }

        /// <summary>
        /// 负极限硬限位启用标志
        /// </summary>

        public bool MelVisEnabled { get; set; }


        /// <summary>
        /// 原点限位启用标志
        /// </summary>

        public bool ORGVisEnabled { get; set; }


        /// <summary>
        /// 回零模式
        /// </summary>
        public int HomeModel { get; set; }



        /// <summary>
        /// 回零速度
        /// </summary>
        public double HomeVel { get; set; }

        /// <summary>
        /// 回零加速度
        /// </summary>
        public double HomeAcc { get; set; }

        /// <summary>
        /// 回零减速度
        /// </summary>
        public double HomeDec { get; set; }


        /// <summary>
        /// 回零偏移
        /// </summary>
        public double HomeOffest { get; set; }



        /// <summary>
        /// 回零模式固定标志
        /// </summary>
        public bool HomeModelFixed { get; set; }


        /// <summary>
        /// 正极限回零模式
        /// </summary>
        public int PelHomeModel { get; set; }


        /// <summary>
        /// 负极限回零模式
        /// </summary>
        public int MelHomeModel { get; set; }


        /// <summary>
        /// 负限位启用标志
        /// </summary>
        public bool LimitVisEnable { get; set; }


        /// <summary>
        /// 正极限软限位
        /// </summary>

        public double LimitPel { get; set; }


        /// <summary>
        /// 负极限软限位
        /// </summary>

        public double LimitMel { get; set; }



        /// <summary>
        /// 轴定位精度
        /// </summary>
        public double PositioningAccuracy { get ; set; }
    }
}
