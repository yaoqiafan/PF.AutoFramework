using PF.Core.Attributes;
using PF.Core.Enums;

namespace PF.Core.Constants
{
    /// <summary>
    /// 全局报警代码常量库。
    /// 所有报警代码必须在此处以常量形式定义，并打上 <see cref="AlarmInfoAttribute"/> 标签。
    /// 严禁在业务代码中硬编码字符串，调用时必须引用此类中的常量。
    /// </summary>
    public static class AlarmCodes
    {
        // ─────────────────────────────────────────────────────────────────────
        // 硬件层 (HW_*)
        // ─────────────────────────────────────────────────────────────────────
        public static class Hardware
        {
            [AlarmInfo("硬件异常", "伺服驱动器离线或报错", AlarmSeverity.Fatal,
                "1. 检查伺服驱动器电源指示灯是否正常;\n" +
                "2. 检查伺服驱动器与运动控制卡之间的通讯线是否松动;\n" +
                "3. 查看驱动器面板报警代码，对照手册处理;\n" +
                "4. 重启驱动器后点击【复位】按钮;")]
            public const string ServoError = "HW_SRV_001";

            [AlarmInfo("硬件异常", "IO 模块连接失败", AlarmSeverity.Error,
                "1. 检查 EtherCAT 通讯线是否正确连接;\n" +
                "2. 检查 IO 模块供电是否正常;\n" +
                "3. 在调试页面尝试重新初始化硬件;\n" +
                "4. 重新上电后点击【复位】按钮;")]
            public const string IoModuleError = "HW_IO_001";

            [AlarmInfo("硬件异常", "运动控制卡初始化失败", AlarmSeverity.Fatal,
                "1. 检查运动控制卡是否安装到位;\n" +
                "2. 检查控制卡驱动是否安装;\n" +
                "3. 检查设备管理器中是否存在控制卡设备;\n" +
                "4. 尝试重启电脑后重新启动软件;")]
            public const string MotionCardInitFailed = "HW_CARD_001";

            [AlarmInfo("硬件异常", "相机连接超时", AlarmSeverity.Error,
                "1. 检查相机网线是否正确连接;\n" +
                "2. 检查网络适配器 IP 配置是否与相机在同一网段;\n" +
                "3. 使用 Ping 命令测试相机 IP 是否可达;\n" +
                "4. 检查相机供电;\n" +
                "5. 重启相机后重新初始化;")]
            public const string CameraTimeout = "HW_CAM_001";

            [AlarmInfo("硬件异常", "条码扫描枪连接失败", AlarmSeverity.Warning,
                "1. 检查扫描枪 USB 或串口连接是否正常;\n" +
                "2. 尝试重新插拔扫描枪;\n" +
                "3. 检查设备管理器中是否正确识别;\n" +
                "4. 确认端口号与参数配置一致;")]
            public const string BarcodeReaderError = "HW_BCR_001";

            [AlarmInfo("硬件异常", "光源控制器通讯异常", AlarmSeverity.Warning,
                "1. 检查光源控制器串口线是否连接;\n" +
                "2. 确认波特率等串口参数配置正确;\n" +
                "3. 重启光源控制器;\n" +
                "4. 在参数页面核对 COM 端口号;")]
            public const string LightControllerError = "HW_LGT_001";

            [AlarmInfo("硬件异常", "运动控制卡总线通讯错误（运行期检测）", AlarmSeverity.Fatal,
                "1. 检查 EtherCAT 总线连接线是否松动或断开;\n" +
                "2. 检查各伺服驱动器及 IO 模块供电是否正常;\n" +
                "3. 在调试页面重新初始化运动控制卡;\n" +
                "4. 尝试重启设备后重新启动软件;")]
            public const string MotionCardBusError = "HW_CARD_002";

            [AlarmInfo("硬件异常", "伺服轴触发限位保护（PEL/MEL）", AlarmSeverity.Error,
                "1. 检查轴当前位置是否超出行程范围;\n" +
                "2. 手动将轴移离限位开关后点击【复位】;\n" +
                "3. 确认限位开关接线和信号极性是否正确;\n" +
                "4. 检查运动参数中行程保护设置是否合理;")]
            public const string AxisLimitError = "HW_AXIS_002";

            [AlarmInfo("硬件异常", "相机通讯心跳超时（TCP 连接丢失）", AlarmSeverity.Error,
                "1. 检查相机网线是否松动或断开;\n" +
                "2. 使用 Ping 命令验证相机 IP 是否可达;\n" +
                "3. 确认网络适配器 IP 与相机在同一网段;\n" +
                "4. 重启相机后点击【复位】重新连接;")]
            public const string CameraHeartbeatTimeout = "HW_CAM_002";

            [AlarmInfo("硬件异常", "扫码枪通讯心跳超时（TCP 连接丢失）", AlarmSeverity.Warning,
                "1. 检查扫码枪网线或 USB 连接是否正常;\n" +
                "2. 使用 Ping 命令验证扫码枪 IP 是否可达;\n" +
                "3. 重启扫码枪后点击【复位】重新连接;\n" +
                "4. 确认端口号与配置文件一致;")]
            public const string BarcodeScannerHeartbeatTimeout = "HW_BCR_002";

            [AlarmInfo("运动超时", "伺服轴运动完成等待超时", AlarmSeverity.Error,
                "1. 检查轴当前是否卡在中途（机械干涉、摩擦过大）;\n" +
                "2. 手动点动该轴，确认运动是否正常;\n" +
                "3. 检查运动参数（速度/加速度）是否合理;\n" +
                "4. 复位后重新运行;")]
            public const string AxisMoveTimeout = "HW_AXIS_003";

            [AlarmInfo("运动超时", "伺服轴回原点完成等待超时", AlarmSeverity.Error,
                "1. 检查原点传感器信号是否正常;\n" +
                "2. 确认回零方向与速度参数配置是否正确;\n" +
                "3. 手动移动轴后重新执行初始化;\n" +
                "4. 检查限位开关是否触发;")]
            public const string HomingTimeout = "HW_AXIS_004";
        }

        // ─────────────────────────────────────────────────────────────────────
        // 工艺流程层 (PROC_*)
        // ─────────────────────────────────────────────────────────────────────
        public static class Process
        {
            [AlarmInfo("流程异常", "工站运动失败（轴未到位/运动超时）", AlarmSeverity.Error,
                "1. 检查轴当前状态是否存在卡阻;\n" +
                "2. 手动点动确认运动是否正常;\n" +
                "3. 检查运动参数（速度/加速度/超时）;\n" +
                "4. 复位后重新运行;")]
            public const string StationMotionFailed = "PROC_MOT_001";

            [AlarmInfo("流程异常", "执行机构动作失败（气缸/夹爪未到位）", AlarmSeverity.Error,
                "1. 检查气源压力是否在正常范围;\n" +
                "2. 检查对应传感器信号是否正常;\n" +
                "3. 手动操作气缸/夹爪确认动作;\n" +
                "4. 复位后重新运行;")]
            public const string StationActuatorFailed = "PROC_ACT_001";

            [AlarmInfo("流程异常", "传感器信号异常（尺寸识别/寻层扫描失败）", AlarmSeverity.Error,
                "1. 检查传感器安装位置与信号线连接;\n" +
                "2. 确认料盒/物料位置摆放正确;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新运行;")]
            public const string StationSensorError = "PROC_SEN_001";

            [AlarmInfo("流程异常", "物料异常（叠料/错层/带片检测）", AlarmSeverity.Error,
                "1. 人工检查当前取料位置物料状态;\n" +
                "2. 手动处理叠料或移除带片;\n" +
                "3. 确认料盒归位正常;\n" +
                "4. 复位后重新运行;")]
            public const string StationMaterialError = "PROC_MAT_001";

            [AlarmInfo("流程异常", "工站数据校验失败（配方/MES数据为空或不匹配）", AlarmSeverity.Error,
                "1. 确认 MES 批次数据已正确下发;\n" +
                "2. 检查配方参数是否已加载;\n" +
                "3. 确认物料规格与配方匹配;\n" +
                "4. 重新下发数据后复位重启;")]
            public const string StationDataInvalid = "PROC_DATA_001";

            [AlarmInfo("流程异常", "检测数据写入失败（数据库/数据模块异常）", AlarmSeverity.Error,
                "1. 检查数据库连接与磁盘空间;\n" +
                "2. 查看日志中具体写入错误信息;\n" +
                "3. 尝试重启数据服务后复位;\n" +
                "4. 联系维护人员检查数据库;")]
            public const string StationDataWriteFailed = "PROC_DATA_002";

            [AlarmInfo("流程异常", "相机触发检测失败", AlarmSeverity.Error,
                "1. 检查相机连接状态;\n" +
                "2. 确认相机触发参数配置正确;\n" +
                "3. 手动触发相机确认功能;\n" +
                "4. 复位后重新运行;")]
            public const string CameraTriggerFailed = "PROC_CAM_001";

            [AlarmInfo("流程异常", "工站信号等待超时或异常取消", AlarmSeverity.Error,
                "1. 检查上游工站信号是否正常发出;\n" +
                "2. 确认工站间同步配置是否正确;\n" +
                "3. 查看关联工站报警信息;\n" +
                "4. 复位后重新运行;")]
            public const string StationSignalTimeout = "PROC_SIG_001";

            [AlarmInfo("流程异常", "寻层算法运算异常或返回空值", AlarmSeverity.Error,
                "1. 确认物料已正确放置到料盒;\n" +
                "2. 检查寻层传感器信号;\n" +
                "3. 查看日志中算法输入数据;\n" +
                "4. 复位后重新运行;")]
            public const string StationAlgorithmError = "PROC_ALG_001";

            [AlarmInfo("流程异常", "工站进入未定义步序（程序逻辑错误）", AlarmSeverity.Fatal,
                "1. 记录当前操作步骤并联系开发人员;\n" +
                "2. 查看日志中异常步序编号;\n" +
                "3. 重启软件后重新运行;\n" +
                "4. 提供日志文件给技术支持;")]
            public const string StationUnexpectedStep = "PROC_SYS_001";
        }

        // ─────────────────────────────────────────────────────────────────────
        // 系统层 (SYS_*)
        // ─────────────────────────────────────────────────────────────────────
        public static class System
        {
            [AlarmInfo("系统异常", "系统初始化超时，硬件未全部就绪", AlarmSeverity.Fatal,
                "1. 检查所有硬件设备连接状态;\n" +
                "2. 查看调试页面中各硬件连接指示灯;\n" +
                "3. 逐一排除连接失败的设备;\n" +
                "4. 全部就绪后点击【复位】按钮;")]
            public const string InitializationTimeout = "SYS_INIT_001";

            [AlarmInfo("系统异常", "数据库写入失败", AlarmSeverity.Error,
                "1. 检查程序运行目录磁盘空间是否充足;\n" +
                "2. 检查数据库文件是否被其他程序占用;\n" +
                "3. 以管理员权限重启软件;\n" +
                "4. 联系维护人员检查数据库文件完整性;")]
            public const string DatabaseWriteError = "SYS_DB_001";

            [AlarmInfo("系统异常", "工站同步服务异常", AlarmSeverity.Error,
                "1. 检查各工站状态机是否处于正常态;\n" +
                "2. 查看日志中工站异常原因;\n" +
                "3. 逐一复位各工站;\n" +
                "4. 重启同步服务（重启软件）;")]
            public const string StationSyncError = "SYS_SYNC_001";
        }

       
    }
}
