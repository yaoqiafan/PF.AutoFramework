using PF.Core.Attributes;
using PF.Core.Enums;

namespace PF.WorkStation.AutoOcr.CostParam
{
    /// <summary>
    /// 全局报警代码常量库。
    /// 所有报警代码必须在此处以常量形式定义，并打上 <see cref="AlarmInfoAttribute"/> 标签。
    /// 严禁在业务代码中硬编码字符串，调用时必须引用此类中的常量。
    /// </summary>
    public class AlarmCodesExtensions
    {
        // ─────────────────────────────────────────────────────────────────────
        // 工艺层 (PRC_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 工艺层报警代码
        /// </summary>
        public static class Process
        {
            /// <summary>OCR识别连续失败</summary>
            [AlarmInfo("工艺异常", "OCR 识别连续失败，超过阈值", AlarmSeverity.Warning,
                "1. 检查相机焦距是否正确（参数页面调整）;\n" +
                "2. 调整光源亮度配方;\n" +
                "3. 清洁镜头表面;\n" +
                "4. 检查产品定位是否准确;\n" +
                "5. 联系工艺工程师调整识别参数;")]
            public const string OcrFailure = "PRC_OCR_001";

            /// <summary>批次产品数量与预期不符</summary>
            [AlarmInfo("工艺异常", "批次产品数量与预期不符", AlarmSeverity.Error,
                "1. 手动核对当前产品数量;\n" +
                "2. 检查上料是否符合批次要求;\n" +
                "3. 检查计数传感器状态;\n" +
                "4. 确认无误后手动纠正批次记录并复位;")]
            public const string BatchCountError = "PRC_BCH_001";

            /// <summary>工站运动超时</summary>
            [AlarmInfo("工艺异常", "工站运动超时，轴未到达目标位", AlarmSeverity.Fatal,
                "1. 检查运动轴是否被卡死或碰到异物;\n" +
                "2. 检查限位传感器指示状态;\n" +
                "3. 在调试模式下手动点动轴到安全位置;\n" +
                "4. 确认无机械障碍后执行回原点;\n" +
                "5. 点击【复位】继续生产;")]
            public const string StationMotionTimeout = "PRC_MOT_001";

            /// <summary>上料工站送料超时</summary>
            [AlarmInfo("工艺异常", "上料工站送料超时", AlarmSeverity.Error,
                "1. 检查料盘是否有料;\n" +
                "2. 检查送料机构是否被卡住;\n" +
                "3. 手动清除卡料后复位;\n" +
                "4. 检查送料气缸传感器状态;")]
            public const string FeedingTimeout = "PRC_FEED_001";

            /// <summary>拉料超时</summary>
            [AlarmInfo("工艺异常", "拉料工站取料超时", AlarmSeverity.Error,
                "1. 检查拉料机构是否被卡住;\n" +
                "2. 检查真空吸力是否正常;\n" +
                "3. 手动清除异常后复位;\n" +
                "4. 检查真空传感器及气路;")]
            public const string PullingTimeout = "PRC_PULL_001";
        }

        // ═════════════════════════════════════════════════════════════════════
        //  细化流程报警代码 — 按工站拆分，每个触发点唯一编码
        //  命名规则: PROC_{STATION}_{CATEGORY}_{NNN}
        //    STATION: WS1F(工位1上下料) WS1P(工位1拉料) WS2F(工位2上下料)
        //             WS2P(工位2拉料) DET(检测)
        //    CATEGORY: MOT(运动) ACT(执行器) SEN(传感器) MAT(物料) DATA(数据)
        //              CAM(相机) SIG(信号) ALG(算法) SYS(系统)
        // ═════════════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────────────
        // 工位1 上下料工站 (PROC_WS1F_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 工位1上下料工站报警代码
        /// </summary>
        public static class WS1Feeding
        {
            /// <summary>批次产品个数为0，无法启动生产</summary>
            [AlarmInfo("流程异常/数据", "工位1上下料-批次产品个数为0", AlarmSeverity.Error,
                "1. 检查 MES 批次数据是否已正确下发;\n" +
                "2. 确认批次产品数量字段不为空;\n" +
                "3. 重新下发批次数据后复位重启;")]
            public const string BatchCountZero = "PROC_WS1F_DATA_001";

            /// <summary>料盒尺寸与配方不匹配</summary>
            [AlarmInfo("流程异常/数据", "工位1上下料-料盒尺寸与配方不匹配", AlarmSeverity.Error,
                "1. 核实料盒内实际晶圆尺寸;\n" +
                "2. 核对配方中要求的晶圆尺寸;\n" +
                "3. 更换正确料盒或修改配方后复位重启;")]
            public const string WaferSizeMismatch = "PROC_WS1F_DATA_002";

            /// <summary>配方参数为空</summary>
            [AlarmInfo("流程异常/数据", "工位1上下料-配方参数为空", AlarmSeverity.Error,
                "1. 确认配方已正确下发至工位1;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位重启;")]
            public const string RecipeNull = "PROC_WS1F_DATA_003";

            /// <summary>寻层算法判定为0层</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法判定为0层", AlarmSeverity.Error,
                "1. 确认料盒已正确放置到位;\n" +
                "2. 检查寻层传感器信号是否正常;\n" +
                "3. 确认料盒内确实有物料;\n" +
                "4. 复位后重新执行寻层;")]
            public const string AlgorithmZeroLayers = "PROC_WS1F_ALG_001";

            /// <summary>寻层算法出现严重异常</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法出现严重异常", AlarmSeverity.Error,
                "1. 查看日志中算法异常详情;\n" +
                "2. 检查寻层传感器信号与原始数据;\n" +
                "3. 确认物料摆放无严重倾斜;\n" +
                "4. 复位后重新执行寻层;")]
            public const string AlgorithmException = "PROC_WS1F_ALG_002";

            /// <summary>料盒尺寸识别失败（传感器信号异常）</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-料盒尺寸识别失败（传感器信号异常）", AlarmSeverity.Error,
                "1. 检查尺寸识别传感器安装位置;\n" +
                "2. 确认料盒是否放正;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后将重新识别尺寸;")]
            public const string SizeDetectionSensorFailed = "PROC_WS1F_SEN_001";

            /// <summary>Z轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴运动条件不满足", AlarmSeverity.Error,
                "1. 检查Z轴伺服是否报警;\n" +
                "2. 确认互锁信号是否就绪;\n" +
                "3. 处理轴故障后复位，将重新评估Z轴状态;")]
            public const string ZAxisPreconditionFailed = "PROC_WS1F_MOT_001";

            /// <summary>X轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-X轴运动条件不满足", AlarmSeverity.Error,
                "1. 检查夹爪是否处于张开状态;\n" +
                "2. 确认X轴伺服是否报警;\n" +
                "3. 处理后复位，将重新评估X轴状态;")]
            public const string XAxisPreconditionFailed = "PROC_WS1F_MOT_002";

            /// <summary>Z轴寻层扫描异常</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-Z轴寻层扫描异常（结果为空或过程出错）", AlarmSeverity.Error,
                "1. 检查寻层传感器信号线连接;\n" +
                "2. 确认料盒位置摆放正确;\n" +
                "3. 查看日志中扫描原始数据;\n" +
                "4. 复位后将重新执行扫描;")]
            public const string LayerScanFailed = "PROC_WS1F_SEN_002";

            /// <summary>物料错层翘起，禁止拉料</summary>
            [AlarmInfo("流程异常/物料", "工位1上下料-物料错层翘起，禁止拉料", AlarmSeverity.Error,
                "1. 人工检查当前取料位置物料状态;\n" +
                "2. 小心处理错层/翘起物料;\n" +
                "3. 确认物料归位后复位，将重新检查该层;")]
            public const string MaterialTiltedMisaligned = "PROC_WS1F_MAT_001";

            /// <summary>Z轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴运动超时", AlarmSeverity.Error,
                "1. 检查Z轴是否卡在中途（机械干涉）;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 检查运动参数（速度/加速度）;\n" +
                "4. 复位后重新运行;")]
            public const string ZAxisMoveTimeout = "PROC_WS1F_MOT_003";

            /// <summary>X轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-X轴运动超时", AlarmSeverity.Error,
                "1. 检查X轴是否卡在中途（机械干涉）;\n" +
                "2. 手动点动X轴确认运动正常;\n" +
                "3. 检查运动参数（速度/加速度）;\n" +
                "4. 复位后重新运行;")]
            public const string XAxisMoveTimeout = "PROC_WS1F_MOT_004";

            /// <summary>状态机指针漂移，进入未定义步序</summary>
            [AlarmInfo("流程异常/系统", "工位1上下料-状态机指针漂移，进入未定义步序", AlarmSeverity.Fatal,
                "1. 记录当前操作步骤并联系开发人员;\n" +
                "2. 查看日志中异常步序编号;\n" +
                "3. 重启软件后重新运行;\n" +
                "4. 提供日志文件给技术支持;")]
            public const string UndefinedStep = "PROC_WS1F_SYS_001";
        }

        // ─────────────────────────────────────────────────────────────────────
        // 工位2 上下料工站 (PROC_WS2F_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 工位2上下料工站报警代码
        /// </summary>
        public static class WS2Feeding
        {
            /// <summary>批次产品个数为0，无法启动生产</summary>
            [AlarmInfo("流程异常/数据", "工位2上下料-批次产品个数为0", AlarmSeverity.Error,
                "1. 检查 MES 批次数据是否已正确下发;\n" +
                "2. 确认批次产品数量字段不为空;\n" +
                "3. 重新下发批次数据后复位重启;")]
            public const string BatchCountZero = "PROC_WS2F_DATA_001";

            /// <summary>料盒尺寸与配方不匹配</summary>
            [AlarmInfo("流程异常/数据", "工位2上下料-料盒尺寸与配方不匹配", AlarmSeverity.Error,
                "1. 核实料盒内实际晶圆尺寸;\n" +
                "2. 核对配方中要求的晶圆尺寸;\n" +
                "3. 更换正确料盒或修改配方后复位重启;")]
            public const string WaferSizeMismatch = "PROC_WS2F_DATA_002";

            /// <summary>配方参数为空</summary>
            [AlarmInfo("流程异常/数据", "工位2上下料-配方参数为空", AlarmSeverity.Error,
                "1. 确认配方已正确下发至工位2;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位重启;")]
            public const string RecipeNull = "PROC_WS2F_DATA_003";

            /// <summary>寻层算法判定为0层</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法判定为0层", AlarmSeverity.Error,
                "1. 确认料盒已正确放置到位;\n" +
                "2. 检查寻层传感器信号是否正常;\n" +
                "3. 确认料盒内确实有物料;\n" +
                "4. 复位后重新执行寻层;")]
            public const string AlgorithmZeroLayers = "PROC_WS2F_ALG_001";

            /// <summary>寻层算法出现严重异常</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法出现严重异常", AlarmSeverity.Error,
                "1. 查看日志中算法异常详情;\n" +
                "2. 检查寻层传感器信号与原始数据;\n" +
                "3. 确认物料摆放无严重倾斜;\n" +
                "4. 复位后重新执行寻层;")]
            public const string AlgorithmException = "PROC_WS2F_ALG_002";

            /// <summary>料盒尺寸识别失败（传感器信号异常）</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-料盒尺寸识别失败（传感器信号异常）", AlarmSeverity.Error,
                "1. 检查尺寸识别传感器安装位置;\n" +
                "2. 确认料盒是否放正;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后将重新识别尺寸;")]
            public const string SizeDetectionSensorFailed = "PROC_WS2F_SEN_001";

            /// <summary>Z轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴运动条件不满足", AlarmSeverity.Error,
                "1. 检查Z轴伺服是否报警;\n" +
                "2. 确认互锁信号是否就绪;\n" +
                "3. 处理轴故障后复位，将重新评估Z轴状态;")]
            public const string ZAxisPreconditionFailed = "PROC_WS2F_MOT_001";

            /// <summary>X轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-X轴运动条件不满足", AlarmSeverity.Error,
                "1. 检查夹爪是否处于张开状态;\n" +
                "2. 确认X轴伺服是否报警;\n" +
                "3. 处理后复位，将重新评估X轴状态;")]
            public const string XAxisPreconditionFailed = "PROC_WS2F_MOT_002";

            /// <summary>Z轴寻层扫描异常</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-Z轴寻层扫描异常（结果为空或过程出错）", AlarmSeverity.Error,
                "1. 检查寻层传感器信号线连接;\n" +
                "2. 确认料盒位置摆放正确;\n" +
                "3. 查看日志中扫描原始数据;\n" +
                "4. 复位后将重新执行扫描;")]
            public const string LayerScanFailed = "PROC_WS2F_SEN_002";

            /// <summary>物料错层翘起，禁止拉料</summary>
            [AlarmInfo("流程异常/物料", "工位2上下料-物料错层翘起，禁止拉料", AlarmSeverity.Error,
                "1. 人工检查当前取料位置物料状态;\n" +
                "2. 小心处理错层/翘起物料;\n" +
                "3. 确认物料归位后复位，将重新检查该层;")]
            public const string MaterialTiltedMisaligned = "PROC_WS2F_MAT_001";

            /// <summary>Z轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴运动超时", AlarmSeverity.Error,
                "1. 检查Z轴是否卡在中途（机械干涉）;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 检查运动参数（速度/加速度）;\n" +
                "4. 复位后重新运行;")]
            public const string ZAxisMoveTimeout = "PROC_WS2F_MOT_003";

            /// <summary>X轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-X轴运动超时", AlarmSeverity.Error,
                "1. 检查X轴是否卡在中途（机械干涉）;\n" +
                "2. 手动点动X轴确认运动正常;\n" +
                "3. 检查运动参数（速度/加速度）;\n" +
                "4. 复位后重新运行;")]
            public const string XAxisMoveTimeout = "PROC_WS2F_MOT_004";

            /// <summary>状态机指针漂移，进入未定义步序</summary>
            [AlarmInfo("流程异常/系统", "工位2上下料-状态机指针漂移，进入未定义步序", AlarmSeverity.Fatal,
                "1. 记录当前操作步骤并联系开发人员;\n" +
                "2. 查看日志中异常步序编号;\n" +
                "3. 重启软件后重新运行;\n" +
                "4. 提供日志文件给技术支持;")]
            public const string UndefinedStep = "PROC_WS2F_SYS_001";
        }

        // ─────────────────────────────────────────────────────────────────────
        // 工位1 拉料工站 (PROC_WS1P_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 工位1拉料工站报警代码
        /// </summary>
        public static class WS1Pulling
        {
            /// <summary>配方参数为空</summary>
            [AlarmInfo("流程异常/数据", "工位1拉料-配方参数为空", AlarmSeverity.Error,
                "1. 确认配方已正确下发至工位1;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位;")]
            public const string RecipeNull = "PROC_WS1P_DATA_001";

            /// <summary>调整流道尺寸失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-调整流道尺寸失败", AlarmSeverity.Error,
                "1. 检查流道宽度调整电机是否报警;\n" +
                "2. 确认气源压力是否在正常范围;\n" +
                "3. 手动操作确认流道机构;\n" +
                "4. 复位后将重试调整;")]
            public const string TrackSizeMotorFailed = "PROC_WS1P_MOT_001";

            /// <summary>Y轴移动到取料位失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-Y轴移动到取料位失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警或超时;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后将重试移动;")]
            public const string YAxisToPickupFailed = "PROC_WS1P_MOT_002";

            /// <summary>关闭夹爪失败（未感应到闭合信号）</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-关闭夹爪失败（未感应到闭合信号）", AlarmSeverity.Error,
                "1. 检查气源压力是否在正常范围;\n" +
                "2. 检查夹爪闭合传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;")]
            public const string GripperCloseFailed = "PROC_WS1P_ACT_001";

            /// <summary>检测到叠料异常</summary>
            [AlarmInfo("流程异常/物料", "工位1拉料-检测到叠料异常", AlarmSeverity.Error,
                "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离叠料;\n" +
                "3. 确认物料正常后复位;")]
            public const string StackedPiecesDetected = "PROC_WS1P_MAT_001";

            /// <summary>拉出至检测位失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出至检测位失败（运动被中断）", AlarmSeverity.Error,
                "1. 检查是否触发卡料或掉料防呆;\n" +
                "2. 手动确认Y轴运动是否顺畅;\n" +
                "3. 复位后将重试拉出;")]
            public const string PullOutToInspectionFailed = "PROC_WS1P_MOT_003";

            /// <summary>推回至料盒失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-推回至料盒失败（运动被中断）", AlarmSeverity.Error,
                "1. 检查是否触发防呆拦截;\n" +
                "2. 确认Y轴无卡阻;\n" +
                "3. 复位后将重试推回;")]
            public const string PushBackToCassetteFailed = "PROC_WS1P_MOT_004";

            /// <summary>打开夹爪失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-打开夹爪失败", AlarmSeverity.Error,
                "1. 检查气源压力;\n" +
                "2. 检查夹爪张开传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;")]
            public const string GripperOpenFailed = "PROC_WS1P_ACT_002";

            /// <summary>Y轴退回待机位失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-Y轴退回待机位失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后将重试退回;")]
            public const string YAxisRetractFailed = "PROC_WS1P_MOT_005";

            /// <summary>退回安全位后夹爪仍检测到带料</summary>
            [AlarmInfo("流程异常/物料", "工位1拉料-退回安全位后夹爪仍检测到带料", AlarmSeverity.Error,
                "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 检查夹爪内传感器;\n" +
                "4. 确认无料后复位;")]
            public const string WaferStuckInGripper = "PROC_WS1P_MAT_002";
        }

        // ─────────────────────────────────────────────────────────────────────
        // 工位2 拉料工站 (PROC_WS2P_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 工位2拉料工站报警代码
        /// </summary>
        public static class WS2Pulling
        {
            /// <summary>配方参数为空</summary>
            [AlarmInfo("流程异常/数据", "工位2拉料-配方参数为空", AlarmSeverity.Error,
                "1. 确认配方已正确下发至工位2;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位;")]
            public const string RecipeNull = "PROC_WS2P_DATA_001";

            /// <summary>调整流道尺寸失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-调整流道尺寸失败", AlarmSeverity.Error,
                "1. 检查流道宽度调整电机是否报警;\n" +
                "2. 确认气源压力是否在正常范围;\n" +
                "3. 手动操作确认流道机构;\n" +
                "4. 复位后将重试调整;")]
            public const string TrackSizeMotorFailed = "PROC_WS2P_MOT_001";

            /// <summary>Y轴移动到取料位失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-Y轴移动到取料位失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警或超时;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后将重试移动;")]
            public const string YAxisToPickupFailed = "PROC_WS2P_MOT_002";

            /// <summary>关闭夹爪失败（未感应到闭合信号）</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-关闭夹爪失败（未感应到闭合信号）", AlarmSeverity.Error,
                "1. 检查气源压力是否在正常范围;\n" +
                "2. 检查夹爪闭合传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;")]
            public const string GripperCloseFailed = "PROC_WS2P_ACT_001";

            /// <summary>检测到叠料异常</summary>
            [AlarmInfo("流程异常/物料", "工位2拉料-检测到叠料异常", AlarmSeverity.Error,
                "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离叠料;\n" +
                "3. 确认物料正常后复位;")]
            public const string StackedPiecesDetected = "PROC_WS2P_MAT_001";

            /// <summary>拉出至检测位失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出至检测位失败（运动被中断）", AlarmSeverity.Error,
                "1. 检查是否触发卡料或掉料防呆;\n" +
                "2. 手动确认Y轴运动是否顺畅;\n" +
                "3. 复位后将重试拉出;")]
            public const string PullOutToInspectionFailed = "PROC_WS2P_MOT_003";

            /// <summary>推回至料盒失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-推回至料盒失败（运动被中断）", AlarmSeverity.Error,
                "1. 检查是否触发防呆拦截;\n" +
                "2. 确认Y轴无卡阻;\n" +
                "3. 复位后将重试推回;")]
            public const string PushBackToCassetteFailed = "PROC_WS2P_MOT_004";

            /// <summary>打开夹爪失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-打开夹爪失败", AlarmSeverity.Error,
                "1. 检查气源压力;\n" +
                "2. 检查夹爪张开传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;")]
            public const string GripperOpenFailed = "PROC_WS2P_ACT_002";

            /// <summary>Y轴退回待机位失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-Y轴退回待机位失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后将重试退回;")]
            public const string YAxisRetractFailed = "PROC_WS2P_MOT_005";

            /// <summary>退回安全位后夹爪仍检测到带料</summary>
            [AlarmInfo("流程异常/物料", "工位2拉料-退回安全位后夹爪仍检测到带料", AlarmSeverity.Error,
                "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 检查夹爪内传感器;\n" +
                "4. 确认无料后复位;")]
            public const string WaferStuckInGripper = "PROC_WS2P_MAT_002";
        }

        // ─────────────────────────────────────────────────────────────────────
        // OCR 检测工站 (PROC_DET_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// OCR检测工站报警代码
        /// </summary>
        public static class Detection
        {
            /// <summary>等待工位检测信号任务池异常中断</summary>
            [AlarmInfo("流程异常/信号", "OCR检测-等待工位检测信号任务池异常中断", AlarmSeverity.Error,
                "1. 检查上游拉料工站信号是否正常发出;\n" +
                "2. 确认工站间同步配置是否正确;\n" +
                "3. 查看关联工站报警信息;\n" +
                "4. 复位后重新运行;")]
            public const string SignalWaitFault = "PROC_DET_SIG_001";

            /// <summary>龙门模组移动到检测位置失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-龙门模组移动到检测位置失败", AlarmSeverity.Error,
                "1. 检查龙门X/Y/Z轴是否报警;\n" +
                "2. 确认配方中目标坐标是否正确;\n" +
                "3. 手动点动确认龙门运动;\n" +
                "4. 复位后重试定位;")]
            public const string GantryMoveFailed = "PROC_DET_MOT_001";

            /// <summary>相机握手失败（光源或相机掉线）</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-相机握手失败（光源或相机掉线）", AlarmSeverity.Error,
                "1. 检查相机连接状态;\n" +
                "2. 确认相机触发参数配置正确;\n" +
                "3. 手动触发相机确认功能;\n" +
                "4. 复位后重新运行;")]
            public const string CameraTriggerFailed = "PROC_DET_CAM_001";

            /// <summary>相机Z轴无法抬起避位（紧急锁死防撞）</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-相机Z轴无法抬起避位（紧急锁死防撞）", AlarmSeverity.Error,
                "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认是否能抬起;\n" +
                "3. 确认无机械干涉;\n" +
                "4. 复位后重试;")]
            public const string ZAxisRetractAfterScan = "PROC_DET_MOT_002";

            /// <summary>检测数据写入失败</summary>
            [AlarmInfo("流程异常/数据", "OCR检测-检测数据写入失败", AlarmSeverity.Error,
                "1. 检查数据库连接与磁盘空间;\n" +
                "2. 查看日志中具体写入错误信息;\n" +
                "3. 尝试重启数据服务后复位;\n" +
                "4. 联系维护人员检查数据库;")]
            public const string DataWriteFailed = "PROC_DET_DATA_001";

            /// <summary>状态机越界，进入未定义步序</summary>
            [AlarmInfo("流程异常/系统", "OCR检测-状态机越界，进入未定义步序", AlarmSeverity.Fatal,
                "1. 记录当前操作步骤并联系开发人员;\n" +
                "2. 查看日志中异常步序编号;\n" +
                "3. 重启软件后重新运行;\n" +
                "4. 提供日志文件给技术支持;")]
            public const string UndefinedStep = "PROC_DET_SYS_001";
        }
    }
}
