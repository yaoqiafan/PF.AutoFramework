namespace PF.Core.Interfaces.Device.Hardware.Card
{
    /// <summary>
    /// 运动控制卡接口
    ///
    /// 继承链：ConcreteCard → IMotionCard → IHardwareDevice
    ///
    /// 代表一块物理运动控制板卡，轴设备和IO设备均挂载于其上。
    /// 板卡负责初始化底层驱动和加载硬件参数配置文件，
    /// 子设备（IAxis / IIOController）通过 IAttachedDevice 接口引用其父板卡。
    ///
    /// 代理/委托模式说明：
    ///   板卡作为唯一持有厂商 SDK 句柄的对象，对外暴露所有底层硬件操作。
    ///   BaseAxisDevice / BaseIODevice 不再是抽象类，而是通用代理包装器，
    ///   将方法调用委托给此接口的对应方法。
    ///   新增硬件品牌时，只需实现一个 XXXMotionCard 类，无需再改动轴/IO 代码。
    ///
    ///   所有运动/IO 方法均以目标索引（axisIndex / portIndex）作为第一参数，
    ///   以便板卡用同一 SDK 句柄管理多个子设备。
    /// </summary>
    public interface IMotionCard : IHardwareDevice
    {
        #region 板卡基本属性

        /// <summary>板卡在系统中的物理槽位/索引号（如 0, 1, 2…）</summary>
        int CardIndex { get; }

        /// <summary>该板卡支持的运动控制轴总数</summary>
        int AxisCount { get; }

        /// <summary>该板卡数字量输入端口总数</summary>
        int InputCount { get; }

        /// <summary>该板卡数字量输出端口总数</summary>
        int OutputCount { get; }

        /// <summary>
        /// 加载板卡专属硬件参数配置文件（如运动参数 INI / XML 文件）
        /// </summary>
        /// <param name="configFilePath">配置文件绝对路径</param>
        /// <returns>加载成功返回 true，否则返回 false</returns>
        Task<bool> LoadConfigAsync(string configFilePath);

        #endregion

        #region 运动控制方法（以板卡为视角，axisIndex 为板卡内物理轴号）

        /// <summary>使能指定轴（Servo On）</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        Task<bool> EnableAxisAsync(int axisIndex);

        /// <summary>断使能指定轴（Servo Off）</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        Task<bool> DisableAxisAsync(int axisIndex);

        /// <summary>停止指定轴运动（减速停止或急停，由具体实现决定）</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        /// /// <param name="IsEmgStop">是否紧急停止（默认为减速停止）</param>
        Task<bool> StopAxisAsync(int axisIndex, bool IsEmgStop = false);

        /// <summary>
        /// 执行指定轴回原点动作（Home）
        /// </summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        /// <param name="HomeModel">回零模式</param>
        /// <param name="HomeVel">回零速度</param>
        /// <param name="HomeAcc">回零加速度</param>
        /// <param name="HomeDec">回零减速度</param>
        /// <param name="HomeOffest">回零偏移量</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> HomeAxisAsync(int axisIndex, int HomeModel, int HomeVel, int HomeAcc, int HomeDec, int HomeOffest, CancellationToken token = default);

        /// <summary>指定轴绝对位置定位</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        /// <param name="targetPosition">目标位置（工程单位，如 mm）</param>
        /// <param name="velocity">运动速度（工程单位，如 mm/s）</param>
        /// <param name="Acc">运动加速度（工程单位）</param>
        /// <param name="Dec">运动减速度（工程单位）</param>
        ///  <param name="Dec">S段速度（工程单位）</param>
        /// <param name="token">取消令牌</param>
        Task<bool> MoveAbsoluteAsync(int axisIndex, double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default);

        /// <summary>指定轴相对位置定位</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        /// <param name="distance">相对移动距离（工程单位，正负表示方向）</param>
        /// <param name="velocity">运动速度（工程单位，如 mm/s）</param>
        /// <param name="Acc">运动加速度（工程单位）</param>
        /// <param name="Dec">运动减速度（工程单位）</param>
        ///  <param name="Dec">S段速度（工程单位）</param>
        /// <param name="token">取消令牌</param>
        Task<bool> MoveRelativeAsync(int axisIndex, double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default);

        /// <summary>指定轴持续点动（Jog）</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        /// <param name="velocity">点动速度（工程单位，如 mm/s）</param>
        /// <param name="Acc">点动加速度（工程单位）</param>
        /// <param name="Dec">点动减速度（工程单位）</param>
        /// <param name="isPositive">true = 正方向，false = 负方向</param>
        Task<bool> JogAsync(int axisIndex, double velocity, double Acc, double Dec, bool isPositive);

        #endregion

        #region 轴状态读取方法

        /// <summary>读取指定轴的当前实时位置（工程单位，如 mm）</summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        double? GetAxisCurrentPosition(int axisIndex);


        /// <summary>
        /// 获取轴状态
        /// </summary>
        /// <param name="axisIndex">板卡内物理轴索引</param>
        /// <returns></returns>
        MotionIOStatus GetMotionIOStatus(int axisIndex);


        /// <summary>
        /// 清除轴报警状态
        /// </summary>
        /// <param name="axisIndex"></param>
        /// <returns></returns>
        Task <bool > ClearAxisError(int axisIndex);

        #endregion

        #region IO 控制方法（portIndex 为板卡内物理端口号）

        /// <summary>读取指定输入端口的当前信号（true = 高电平）</summary>
        /// <param name="portIndex">板卡内物理输入端口号</param>
        bool? ReadInputPort(int portIndex);

        /// <summary>设置指定输出端口的信号（true = 开启输出）</summary>
        /// <param name="portIndex">板卡内物理输出端口号</param>
        /// <param name="value">输出值</param>
        bool WriteOutputPort(int portIndex, bool value);

        /// <summary>读取指定输出端口的当前锁存状态（用于 UI 回显）</summary>
        /// <param name="portIndex">板卡内物理输出端口号</param>
        bool? ReadOutputPort(int portIndex);

        #endregion




        #region 高级功能

        #region 位置锁存



        /// <summary>
        /// 设置位置锁存参数
        /// </summary>
        /// <param name="LatchNo">锁存器ID</param>
        /// <param name="AxisNo">轴号</param>
        /// <param name="InPutPort">输入端口号</param>
        /// <param name="LtcMode">锁存模式</param>
        /// <param name="LtcLogic">锁存逻辑</param>
        /// <param name="Filter">滤波器</param>
        /// <param name="LatchSource">锁存源</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<bool> SetLatchMode(int LatchNo, int AxisNo, int InPutPort,int LtcMode = 0, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, CancellationToken token = default);



        /// <summary>
        /// 读取位置锁存个数
        /// </summary>
        /// <param name="LatchNo">锁存器ID</param>
        /// <param name="AxisNo">轴号</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<int> GetLatchNumber(int LatchNo, int AxisNo, CancellationToken token = default);



        /// <summary>
        /// 读取锁存位置
        /// </summary>
        /// <param name="LatchNo">锁存器ID</param>
        /// <param name="AxisNo">轴号</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        Task<double?> GetLatchPos(int LatchNo, int AxisNo, CancellationToken token = default);


        #endregion 位置锁存


        #endregion 高级功能
    }






    /// <summary>
    /// 轴IO信号
    /// </summary>
    public class MotionIOStatus
    {
        /// <summary>
        /// 原点
        /// </summary>
        public bool ORG;

        /// <summary>
        /// 正极限
        /// </summary>
        public bool PEL;

        /// <summary>
        /// 负极限
        /// </summary>
        public bool MEL;

        /// <summary>
        /// 报警
        /// </summary>
        public bool ALM;

        /// <summary>
        /// 使能
        /// </summary>
        public bool SVO;

        /// <summary>
        /// 急停
        /// </summary>
        public bool Emg;

        /// <summary>
        /// 定位完成
        /// </summary>
        public bool MoveDone;

        /// <summary>
        /// 回零完成
        /// </summary>
        public bool HomeDone;

        /// <summary>
        /// 回零中判断
        /// </summary>
        public bool Homing;

        /// <summary>
        /// 运动中判断
        /// </summary>
        public bool Moving;
    }
}
