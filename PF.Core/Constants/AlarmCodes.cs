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
        /// <summary>
        /// 硬件相关报警代码
        /// </summary>
        public static class Hardware
        {
            /// <summary>伺服驱动器离线或报错</summary>
            [AlarmInfo("硬件异常", "伺服驱动器离线或报错", AlarmSeverity.Fatal,
                "1. 检查伺服驱动器电源指示灯是否正常;\n" +
                "2. 检查伺服驱动器与运动控制卡之间的通讯线是否松动;\n" +
                "3. 查看驱动器面板报警代码，对照手册处理;\n" +
                "4. 重启驱动器后点击【复位】按钮;")]
            public const string ServoError = "HW_SRV_001";

            /// <summary>IO模块连接失败</summary>
            [AlarmInfo("硬件异常", "IO 模块连接失败", AlarmSeverity.Error,
                "1. 检查 EtherCAT 通讯线是否正确连接;\n" +
                "2. 检查 IO 模块供电是否正常;\n" +
                "3. 在调试页面尝试重新初始化硬件;\n" +
                "4. 重新上电后点击【复位】按钮;")]
            public const string IoModuleError = "HW_IO_001";

            /// <summary>运动控制卡初始化失败</summary>
            [AlarmInfo("硬件异常", "运动控制卡初始化失败", AlarmSeverity.Fatal,
                "1. 检查运动控制卡是否安装到位;\n" +
                "2. 检查控制卡驱动是否安装;\n" +
                "3. 检查设备管理器中是否存在控制卡设备;\n" +
                "4. 尝试重启电脑后重新启动软件;")]
            public const string MotionCardInitFailed = "HW_CARD_001";

            /// <summary>相机连接超时</summary>
            [AlarmInfo("硬件异常", "相机连接超时", AlarmSeverity.Error,
                "1. 检查相机网线是否正确连接;\n" +
                "2. 检查网络适配器 IP 配置是否与相机在同一网段;\n" +
                "3. 使用 Ping 命令测试相机 IP 是否可达;\n" +
                "4. 检查相机供电;\n" +
                "5. 重启相机后重新初始化;")]
            public const string CameraTimeout = "HW_CAM_001";

            /// <summary>条码扫描枪连接失败</summary>
            [AlarmInfo("硬件异常", "条码扫描枪连接失败", AlarmSeverity.Warning,
                "1. 检查扫描枪 USB 或串口连接是否正常;\n" +
                "2. 尝试重新插拔扫描枪;\n" +
                "3. 检查设备管理器中是否正确识别;\n" +
                "4. 确认端口号与参数配置一致;")]
            public const string BarcodeReaderError = "HW_BCR_001";

            /// <summary>光源控制器通讯异常</summary>
            [AlarmInfo("硬件异常", "光源控制器通讯异常", AlarmSeverity.Warning,
                "1. 检查光源控制器串口线是否连接;\n" +
                "2. 确认波特率等串口参数配置正确;\n" +
                "3. 重启光源控制器;\n" +
                "4. 在参数页面核对 COM 端口号;")]
            public const string LightControllerError = "HW_LGT_001";

            /// <summary>运动控制卡总线通讯错误</summary>
            [AlarmInfo("硬件异常", "运动控制卡总线通讯错误（运行期检测）", AlarmSeverity.Fatal,
                "1. 检查 EtherCAT 总线连接线是否松动或断开;\n" +
                "2. 检查各伺服驱动器及 IO 模块供电是否正常;\n" +
                "3. 在调试页面重新初始化运动控制卡;\n" +
                "4. 尝试重启设备后重新启动软件;")]
            public const string MotionCardBusError = "HW_CARD_002";

            /// <summary>伺服轴触发限位保护</summary>
            [AlarmInfo("硬件异常", "伺服轴触发限位保护（PEL/MEL）", AlarmSeverity.Error,
                "1. 检查轴当前位置是否超出行程范围;\n" +
                "2. 手动将轴移离限位开关后点击【复位】;\n" +
                "3. 确认限位开关接线和信号极性是否正确;\n" +
                "4. 检查运动参数中行程保护设置是否合理;")]
            public const string AxisLimitError = "HW_AXIS_002";

            /// <summary>相机通讯心跳超时</summary>
            [AlarmInfo("硬件异常", "相机通讯心跳超时（TCP 连接丢失）", AlarmSeverity.Error,
                "1. 检查相机网线是否松动或断开;\n" +
                "2. 使用 Ping 命令验证相机 IP 是否可达;\n" +
                "3. 确认网络适配器 IP 与相机在同一网段;\n" +
                "4. 重启相机后点击【复位】重新连接;")]
            public const string CameraHeartbeatTimeout = "HW_CAM_002";

            /// <summary>扫码枪通讯心跳超时</summary>
            [AlarmInfo("硬件异常", "扫码枪通讯心跳超时（TCP 连接丢失）", AlarmSeverity.Warning,
                "1. 检查扫码枪网线或 USB 连接是否正常;\n" +
                "2. 使用 Ping 命令验证扫码枪 IP 是否可达;\n" +
                "3. 重启扫码枪后点击【复位】重新连接;\n" +
                "4. 确认端口号与配置文件一致;")]
            public const string BarcodeScannerHeartbeatTimeout = "HW_BCR_002";

            /// <summary>伺服轴运动完成等待超时</summary>
            [AlarmInfo("运动超时", "伺服轴运动完成等待超时", AlarmSeverity.Error,
                "1. 检查轴当前是否卡在中途（机械干涉、摩擦过大）;\n" +
                "2. 手动点动该轴，确认运动是否正常;\n" +
                "3. 检查运动参数（速度/加速度）是否合理;\n" +
                "4. 复位后重新运行;")]
            public const string AxisMoveTimeout = "HW_AXIS_003";

            /// <summary>伺服轴回原点完成等待超时</summary>
            [AlarmInfo("运动超时", "伺服轴回原点完成等待超时", AlarmSeverity.Error,
                "1. 检查原点传感器信号是否正常;\n" +
                "2. 确认回零方向与速度参数配置是否正确;\n" +
                "3. 手动移动轴后重新执行初始化;\n" +
                "4. 检查限位开关是否触发;")]
            public const string HomingTimeout = "HW_AXIS_004";
        }

       

        // ─────────────────────────────────────────────────────────────────────
        // 系统层 (SYS_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 系统相关报警代码
        /// </summary>
        public static class System
        {
            /// <summary>系统初始化超时</summary>
            [AlarmInfo("系统异常", "系统初始化超时，硬件未全部就绪", AlarmSeverity.Fatal,
                "1. 检查所有硬件设备连接状态;\n" +
                "2. 查看调试页面中各硬件连接指示灯;\n" +
                "3. 逐一排除连接失败的设备;\n" +
                "4. 全部就绪后点击【复位】按钮;")]
            public const string InitializationTimeout = "SYS_INIT_001";

            /// <summary>数据库写入失败</summary>
            [AlarmInfo("系统异常", "数据库写入失败", AlarmSeverity.Error,
                "1. 检查程序运行目录磁盘空间是否充足;\n" +
                "2. 检查数据库文件是否被其他程序占用;\n" +
                "3. 以管理员权限重启软件;\n" +
                "4. 联系维护人员检查数据库文件完整性;")]
            public const string DatabaseWriteError = "SYS_DB_001";

            /// <summary>工站同步服务异常</summary>
            [AlarmInfo("系统异常", "工站同步服务异常", AlarmSeverity.Error,
                "1. 检查各工站状态机是否处于正常态;\n" +
                "2. 查看日志中工站异常原因;\n" +
                "3. 逐一复位各工站;\n" +
                "4. 重启同步服务（重启软件）;")]
            public const string StationSyncError = "SYS_SYNC_001";

        }

       
    }
}
