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
            [AlarmInfo("流程异常/数据", "工位1上下料-批次产品个数为0", "WS1 load/unload: batch count 0", AlarmSeverity.Error,
    "1. 检查 MES 批次数据是否已正确下发;\n" +
                "2. 确认批次产品数量字段不为空;\n" +
                "3. 重新下发批次数据后复位重启;",
    10026, "WS1 load/unload: batch count 0",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string BatchCountZero = "PROC_WS1F_DATA_001";

            /// <summary>料盒尺寸与配方不匹配</summary>
            [AlarmInfo("流程异常/数据", "工位1上下料-料盒尺寸与配方不匹配", "WS1 load/unload: box size mismatch", AlarmSeverity.Error,
    "1. 核实料盒内实际晶圆尺寸;\n" +
                "2. 核对配方中要求的晶圆尺寸;\n" +
                "3. 更换正确料盒或修改配方后复位重启;",
    10027, "WS1 load/unload: box size mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string WaferSizeMismatch = "PROC_WS1F_DATA_002";

            /// <summary>配方参数为空</summary>
            [AlarmInfo("流程异常/数据", "工位1上下料-配方参数为空", "WS1 load/unload: recipe empty", AlarmSeverity.Error,
    "1. 确认配方已正确下发至工位1;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位重启;",
    10028, "WS1 load/unload: recipe empty",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string RecipeNull = "PROC_WS1F_DATA_003";

            /// <summary>寻层算法判定为0层</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法判定为0层", "WS1 load/unload: layer find = 0", AlarmSeverity.Error,
    "1. 确认料盒内确实有物料;\n" +
                "2. 确认料盒已正确放置到位;\n" +
                "3. 检查寻层传感器信号是否正常;\n" +
                "4. 复位后重新执行寻层;",
    10029, "WS1 load/unload: layer find = 0",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmZeroLayers = "PROC_WS1F_ALG_001";

            /// <summary>寻层算法出现严重异常</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法出现严重异常", "WS1 LD/ULD: layer find fatal err", AlarmSeverity.Error,
    "1. 查看日志中算法异常详情;\n" +
                "2. 检查寻层传感器信号与原始数据;\n" +
                "3. 确认物料摆放无严重倾斜;\n" +
                "4. 复位后重新执行寻层;",
    10030, "WS1 LD/ULD: layer find fatal err",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmException = "PROC_WS1F_ALG_002";

            /// <summary>料盒尺寸识别失败（传感器信号异常）</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-料盒尺寸识别失败（传感器信号异常）", "WS1 LD/ULD: box size detect fail", AlarmSeverity.Error,
    "1. 检查尺寸识别传感器安装位置;\n" +
                "2. 确认料盒是否放正;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后将重新识别尺寸;",
    10031, "WS1 LD/ULD: box size detect fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string SizeDetectionSensorFailed = "PROC_WS1F_SEN_001";

            /// <summary>Z轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴运动条件不满足", "WS1 LD/ULD: Z-axis mot not ready", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 确认互锁信号是否就绪;\n" +
                "3. 处理轴故障后复位，将重新评估Z轴状态;",
    10032, "WS1 LD/ULD: Z-axis mot not ready",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ZAxisPreconditionFailed = "PROC_WS1F_MOT_001";

            /// <summary>X轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-X轴运动条件不满足", "WS1 LD/ULD: X-axis mot not ready", AlarmSeverity.Error,
    "1. 检查凸片传感器是否有信号（铁环凸片是否触发）;\n" +
                "2. 确认X轴伺服是否报警;\n" +
                "3. 处理后复位，将重新评估X轴状态;",
    10033, "WS1 LD/ULD: X-axis mot not ready",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string XAxisPreconditionFailed = "PROC_WS1F_MOT_002";

            /// <summary>Z轴寻层扫描异常</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-Z轴寻层扫描异常（结果为空或过程出错）", "WS1 load/unload: Z layer scan error", AlarmSeverity.Error,
    "1. 检查寻层传感器信号线连接;\n" +
                "2. 确认料盒位置摆放正确;\n" +
                "3. 查看日志中扫描原始数据;\n" +
                "4. 复位后将重新执行扫描;",
    10034, "WS1 load/unload: Z layer scan error",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerScanFailed = "PROC_WS1F_SEN_002";

            /// <summary>物料错层翘起，禁止拉料</summary>
            [AlarmInfo("流程异常/物料", "工位1上下料-物料错层翘起，禁止拉料", "WS1 LD/ULD: wafer warp, pull locked", AlarmSeverity.Error,
    "1. 人工检查当前取料位置物料状态;\n" +
                "2. 小心处理错层/翘起物料;\n" +
                "3. 确认物料归位后复位，将重新检查该层;",
    10035, "WS1 LD/ULD: wafer warp, pull locked",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string MaterialTiltedMisaligned = "PROC_WS1F_MAT_001";

            /// <summary>Z轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴运动超时", "WS1 LD/ULD: Z-axis mot timeout", AlarmSeverity.Error,
    "1. 检查Z轴伺服驱动器是否报警;\n" +
                "2. 检查Z轴是否卡在中途（机械干涉）;\n" +
                "3. 手动点动Z轴确认运动正常;\n" +
                "4. 检查运动参数（速度/加速度）;\n" +
                "5. 复位后重新运行;",
    10036, "WS1 LD/ULD: Z-axis mot timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ZAxisMoveTimeout = "PROC_WS1F_MOT_003";

            /// <summary>X轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-X轴运动超时", "WS1 LD/ULD: X-axis mot timeout", AlarmSeverity.Error,
    "1. 检查X轴伺服驱动器是否报警;\n" +
                "2. 检查X轴是否卡在中途（机械干涉）;\n" +
                "3. 手动点动X轴确认运动正常;\n" +
                "4. 检查运动参数（速度/加速度）;\n" +
                "5. 复位后重新运行;",
    10037, "WS1 LD/ULD: X-axis mot timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string XAxisMoveTimeout = "PROC_WS1F_MOT_004";

            

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化上料状态失败（Z/X轴运动到待机位失败）</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-初始化上料状态失败（Z/X轴运动到待机位失败）", "WS1 LD/ULD: init LD state fail", AlarmSeverity.Error,
    "1. 检查Z轴和X轴是否处于报警状态;\n" +
                "2. 手动点动确认各轴运动正常;\n" +
                "3. 复位后重新运行;",
    10038, "WS1 LD/ULD: init LD state fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string InitFeedingStateFailed = "PROC_WS1F_MOT_005";

            /// <summary>切换阵列配方尺寸失败（SwitchProductionStateAsync 执行失败）</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-切换阵列配方尺寸失败", "WS1 LD/ULD: recipe size switch fail", AlarmSeverity.Error,
    "1. 确认当前配方尺寸与料盒规格一致;\n" +
                "2. 检查配方中阵列点位参数是否完整;\n" +
                "3. 重新下发配方后复位重新运行;",
    10039, "WS1 LD/ULD: recipe size switch fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string SwitchArrayRecipeSizeFailed = "PROC_WS1F_MOT_007";

            /// <summary>料盒公用底座未检测到物体</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-料盒公用底座未检测到物体", "WS1 load/unload: box base no object", AlarmSeverity.Error,
    "1. 确认料盒是否正确放入;\n" +
                "2. 检查底座光电传感器是否正常;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;",
    10040, "WS1 load/unload: box base no object",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string BoxBaseNotDetected = "PROC_WS1F_SEN_003";

           

            /// <summary>料盒尺寸传感器信号冲突</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发）", "WS1: box size sensor clash", AlarmSeverity.Error,
    "1. 检查料盒是否倾斜或放歪;\n" +
                "2. 检查8寸和12寸传感器安装位置;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;",
    10041, "WS1: box size sensor clash",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string BoxSizeConflict = "PROC_WS1F_SEN_006";

            /// <summary>目标层数超出有效范围</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-目标层数超出有效范围", "WS1: target layer out of range", AlarmSeverity.Error,
    "1. 检查配方中最大层数设置;\n" +
                "2. 确认料盒规格;\n" +
                "3. 复位后重新运行;",
    10042, "WS1: target layer out of range",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerOutOfRange = "PROC_WS1F_ALG_003";

            /// <summary>未找到目标层的阵列点位</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-未找到目标层的阵列点位（可能未执行生产状态切换）", "WS1: target layer missing", AlarmSeverity.Error,
    "1. 确认已执行切换生产状态步骤;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;",
    10043, "WS1: target layer missing",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerPointNotFound = "PROC_WS1F_ALG_004";

            /// <summary>Z轴切换层运动失败</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴切换层运动失败", "WS1 LD/ULD: Z layer switch fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10044, "WS1 LD/ULD: Z layer switch fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerMoveFailed = "PROC_WS1F_MOT_006";

            /// <summary>Z轴互锁失败：料盒未到位禁止升降</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴互锁失败：料盒未到位禁止升降", "WS1: Z ILK, box not ready", AlarmSeverity.Error,
    "1. 确认料盒已完全落座;\n" +
                "2. 检查底座到位传感器;\n" +
                "3. 复位后重新检查;",
    10045, "WS1: Z ILK, box not ready",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ZAxisBoxNotInPlace = "PROC_WS1F_MOT_011";

            /// <summary>X轴互锁失败：存在铁环突片</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-X轴互锁失败：存在铁环突片", "WS1: X ILK, ring tab present", AlarmSeverity.Error,
    "1. 检查铁环突片检测传感器;\n" +
                "2. 确认铁环安装方向;\n" +
                "3. 复位后重新检查;",
    10046, "WS1: X ILK, ring tab present",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string XAxisTabDetected = "PROC_WS1F_MOT_008";

            /// <summary>拉料互锁失败：晶圆盒挡杆未打开</summary>
            [AlarmInfo("流程异常/执行器", "工位1上下料-拉料互锁失败：晶圆盒挡杆未打开", "WS1: pull ILK, latch closed", AlarmSeverity.Error,
    "1. 检查挡杆驱动气缸状态;\n" +
                "2. 确认挡杆传感器信号;\n" +
                "3. 复位后重新检查;",
    10047, "WS1: pull ILK, latch closed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string PullOutLeverNotOpen = "PROC_WS1F_ACT_001";

            /// <summary>寻层扫描移动到起点失败</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-寻层扫描移动到起点失败", "WS1 LD/ULD: scan move to start fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服驱动器是否报警;\n" +
                "2. 检查Z轴是否卡在中途;\n" +
                "3. 手动点动Z轴确认运动正常;\n" +
                "4. 复位后重新运行;",
    10048, "WS1 LD/ULD: scan move to start fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ScanMoveToStartFailed = "PROC_WS1F_MOT_009";

            /// <summary>寻层扫描硬件锁存配置失败</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-寻层扫描硬件锁存配置失败", "WS1 LD/ULD: scan latch config fail", AlarmSeverity.Error,
    "1. 检查运动控制卡连接;\n" +
                "2. 确认传感器接线;\n" +
                "3. 复位后重新运行;",
    10049, "WS1 LD/ULD: scan latch config fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ScanLatchConfigFailed = "PROC_WS1F_SEN_007";

            /// <summary>寻层扫描移动到终点失败</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-寻层扫描移动到终点失败", "WS1 LD/ULD: scan move to end fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服驱动器是否报警;\n" +
                "2. 检查Z轴是否卡在中途;\n" +
                "3. 手动点动Z轴确认运动正常;\n" +
                "4. 复位后重新运行;",
    10050, "WS1 LD/ULD: scan move to end fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ScanMoveToEndFailed = "PROC_WS1F_MOT_010";

            /// <summary>寻层算法理论层坐标未初始化</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法理论层坐标未初始化", "WS1 LD: slot-find coords not init", AlarmSeverity.Error,
    "1. 确认已执行切换生产状态;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;",
    10051, "WS1 LD: slot-find coords not init",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmNotInitialized = "PROC_WS1F_ALG_005";

            /// <summary>寻层算法传感器原始数据不足</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法传感器原始数据不足", "WS1 LD: low sensor raw data", AlarmSeverity.Error,
    "1. 检查传感器信号线连接;\n" +
                "2. 确认料盒位置正确;\n" +
                "3. 复位后重新运行;",
    10052, "WS1 LD: low sensor raw data",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmRawDataMissing = "PROC_WS1F_ALG_006";

            /// <summary>寻层算法双传感器识别数量差异过大</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法双传感器识别数量差异过大（疑似斜片或传感器失效）", "WS1 LD: dual-sensor count mismatch", AlarmSeverity.Error,
    "1. 检查左右传感器信号;\n" +
                "2. 确认物料摆放无倾斜;\n" +
                "3. 复位后重新运行;",
    10053, "WS1 LD: dual-sensor count mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmCountMismatch = "PROC_WS1F_ALG_007";

            /// <summary>寻层算法检测到严重斜片(Cross-slot)</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法检测到严重斜片(Cross-slot)", "WS1 Load: Cross-slot wafer detected", AlarmSeverity.Error,
    "1. 人工检查料盒内物料状态;\n" +
                "2. 小心处理斜片物料;\n" +
                "3. 复位后重新执行寻层;",
    10054, "WS1 Load: Cross-slot wafer detected",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmCrossSlot = "PROC_WS1F_ALG_008";

            /// <summary>寻层算法检测到重叠片(Double-wafer)</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法检测到重叠片(Double-wafer)", "WS1 Load: Double-wafer detected", AlarmSeverity.Error,
    "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离重叠物料;\n" +
                "3. 复位后重新执行寻层;",
    10055, "WS1 Load: Double-wafer detected",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmDoubleWafer = "PROC_WS1F_ALG_009";

            /// <summary>寻层算法晶圆偏离标准槽位</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法晶圆严重偏离标准槽位（可能未插到底）", "WS1 LD: wafer off-slot, not seated", AlarmSeverity.Error,
    "1. 检查物料是否正确插入槽位;\n" +
                "2. 确认料盒无损坏;\n" +
                "3. 复位后重新执行寻层;",
    10056, "WS1 LD: wafer off-slot, not seated",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmSlotMismatch = "PROC_WS1F_ALG_010";

            /// <summary>指定层与实际寻层结果不匹配</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-指定层与实际寻层结果不匹配", "WS1 Load: Target slot mismatch", AlarmSeverity.Error,
    "1. 核查料盒内实际物料层位;\n" +
                "2. 确认切换批次时选择的指定层是否正确;\n" +
                "3. 修正指定层设置后重新切换批次;",
    10057, "WS1 Load: Target slot mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string SpecifiedLayersMismatch = "PROC_WS1F_ALG_011";

            /// <summary>断点续跑：重启后实际物料层数与记忆不一致</summary>
            [AlarmInfo("断点续跑", "工位1上下料-重启后物料状态与记忆不一致", "WS1 Load: Resume status mismatch", AlarmSeverity.Fatal,
    "1. 人工核查料盒内物料数量是否与系统记忆一致;\n" +
                "2. 若物料已被取走，请清空批次后重新下发;\n" +
                "3. 若物料仍在，检查传感器或算法是否异常;\n" +
                "4. 确认状态后手动复位重启;",
    10058, "WS1 Load: Resume status mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ResumeConsistencyFailed = "PROC_WS1F_RSM_001";

            /// <summary>初始化异常（非预期故障统一归口）</summary>
            [AlarmInfo("流程异常/初始化", "工位1上下料-初始化异常", "WS1 Load: Initialization failed", AlarmSeverity.Error,
    "1. 查看日志中具体异常信息;\n" +
                "2. 排查对应硬件或配置项;\n" +
                "3. 排除故障后复位重新初始化;",
    10059, "WS1 Load: Initialization failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string InitException = "PROC_WS1F_INIT_001";

            /// <summary>断点续跑异常（非预期故障统一归口）</summary>
            [AlarmInfo("断点续跑", "工位1上下料-断点续跑异常", "WS1 Load: Resume run failed", AlarmSeverity.Error,
    "1. 查看日志中具体异常信息;\n" +
                "2. 排查对应硬件或状态异常;\n" +
                "3. 排除故障后复位重新运行;",
    10060, "WS1 Load: Resume run failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ResumeException = "PROC_WS1F_RSM_002";
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
            [AlarmInfo("流程异常/数据", "工位2上下料-批次产品个数为0", "WS2 LD: batch product count is zero", AlarmSeverity.Error,
    "1. 检查 MES 批次数据是否已正确下发;\n" +
                "2. 确认批次产品数量字段不为空;\n" +
                "3. 重新下发批次数据后复位重启;",
    10061, "WS2 LD: batch product count is zero",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string BatchCountZero = "PROC_WS2F_DATA_001";

            /// <summary>料盒尺寸与配方不匹配</summary>
            [AlarmInfo("流程异常/数据", "工位2上下料-料盒尺寸与配方不匹配", "WS2 Load: Cassette size mismatch", AlarmSeverity.Error,
    "1. 核实料盒内实际晶圆尺寸;\n" +
                "2. 核对配方中要求的晶圆尺寸;\n" +
                "3. 更换正确料盒或修改配方后复位重启;",
    10062, "WS2 Load: Cassette size mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string WaferSizeMismatch = "PROC_WS2F_DATA_002";

            /// <summary>配方参数为空</summary>
            [AlarmInfo("流程异常/数据", "工位2上下料-配方参数为空", "WS2 Load: Recipe params empty", AlarmSeverity.Error,
    "1. 确认配方已正确下发至工位2;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位重启;",
    10063, "WS2 Load: Recipe params empty",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string RecipeNull = "PROC_WS2F_DATA_003";

            /// <summary>寻层算法判定为0层</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法判定为0层", "WS2 Load: Slot-find algo=0 layers", AlarmSeverity.Error,
    "1. 确认料盒内确实有物料;\n" +
                "2. 确认料盒已正确放置到位;\n" +
                "3. 检查寻层传感器信号是否正常;\n" +
                "4. 复位后重新执行寻层;",
    10064, "WS2 Load: Slot-find algo=0 layers",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmZeroLayers = "PROC_WS2F_ALG_001";

            /// <summary>寻层算法出现严重异常</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法出现严重异常", "WS2 LD: slot-find algo critical err", AlarmSeverity.Error,
    "1. 查看日志中算法异常详情;\n" +
                "2. 检查寻层传感器信号与原始数据;\n" +
                "3. 确认物料摆放无严重倾斜;\n" +
                "4. 复位后重新执行寻层;",
    10065, "WS2 LD: slot-find algo critical err",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmException = "PROC_WS2F_ALG_002";

            /// <summary>料盒尺寸识别失败（传感器信号异常）</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-料盒尺寸识别失败（传感器信号异常）", "WS2 LD: cassette size detect fail", AlarmSeverity.Error,
    "1. 检查尺寸识别传感器安装位置;\n" +
                "2. 确认料盒是否放正;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后将重新识别尺寸;",
    10066, "WS2 LD: cassette size detect fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string SizeDetectionSensorFailed = "PROC_WS2F_SEN_001";

            /// <summary>Z轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴运动条件不满足", "WS2 Load: Z-axis motion not ready", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 确认互锁信号是否就绪;\n" +
                "3. 处理轴故障后复位，将重新评估Z轴状态;",
    10067, "WS2 Load: Z-axis motion not ready",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ZAxisPreconditionFailed = "PROC_WS2F_MOT_001";

            /// <summary>X轴运动条件不满足</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-X轴运动条件不满足", "WS2 Load: X-axis motion not ready", AlarmSeverity.Error,
    "1. 检查凸片传感器是否有信号（铁环凸片是否触发）;\n" +
                "2. 确认X轴伺服是否报警;\n" +
                "3. 处理后复位，将重新评估X轴状态;",
    10068, "WS2 Load: X-axis motion not ready",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string XAxisPreconditionFailed = "PROC_WS2F_MOT_002";

            /// <summary>Z轴寻层扫描异常</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-Z轴寻层扫描异常（结果为空或过程出错）", "WS2 Load: Z-axis slot scan abnormal", AlarmSeverity.Error,
    "1. 检查寻层传感器信号线连接;\n" +
                "2. 确认料盒位置摆放正确;\n" +
                "3. 查看日志中扫描原始数据;\n" +
                "4. 复位后将重新执行扫描;",
    10069, "WS2 Load: Z-axis slot scan abnormal",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerScanFailed = "PROC_WS2F_SEN_002";

            /// <summary>物料错层翘起，禁止拉料</summary>
            [AlarmInfo("流程异常/物料", "工位2上下料-物料错层翘起，禁止拉料", "WS2 Load: Wafer翘曲, pull blocked", AlarmSeverity.Error,
    "1. 人工检查当前取料位置物料状态;\n" +
                "2. 小心处理错层/翘起物料;\n" +
                "3. 确认物料归位后复位，将重新检查该层;",
    10070, "WS2 Load: Wafer翘曲, pull blocked",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string MaterialTiltedMisaligned = "PROC_WS2F_MAT_001";

            /// <summary>Z轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴运动超时", "WS2 Load: Z-axis motion timeout", AlarmSeverity.Error,
    "1. 检查Z轴伺服驱动器是否报警;\n" +
                "2. 检查Z轴是否卡在中途（机械干涉）;\n" +
                "3. 手动点动Z轴确认运动正常;\n" +
                "4. 检查运动参数（速度/加速度）;\n" +
                "5. 复位后重新运行;",
    10071, "WS2 Load: Z-axis motion timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ZAxisMoveTimeout = "PROC_WS2F_MOT_003";

            /// <summary>X轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-X轴运动超时", "WS2 Load: X-axis motion timeout", AlarmSeverity.Error,
    "1. 检查X轴伺服驱动器是否报警;\n" +
                "2. 检查X轴是否卡在中途（机械干涉）;\n" +
                "3. 手动点动X轴确认运动正常;\n" +
                "4. 检查运动参数（速度/加速度）;\n" +
                "5. 复位后重新运行;",
    10072, "WS2 Load: X-axis motion timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string XAxisMoveTimeout = "PROC_WS2F_MOT_004";

          

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化上料状态失败（Z/X轴运动到待机位失败）</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-初始化上料状态失败（Z/X轴运动到待机位失败）", "WS2 Load: Init load state failed", AlarmSeverity.Error,
    "1. 检查Z轴和X轴是否处于报警状态;\n" +
                "2. 手动点动确认各轴运动正常;\n" +
                "3. 复位后重新运行;",
    10073, "WS2 Load: Init load state failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string InitFeedingStateFailed = "PROC_WS2F_MOT_005";

            /// <summary>切换阵列配方尺寸失败（SwitchProductionStateAsync 执行失败）</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-切换阵列配方尺寸失败", "WS2 LD: switch array recipe fail", AlarmSeverity.Error,
    "1. 确认当前配方尺寸与料盒规格一致;\n" +
                "2. 检查配方中阵列点位参数是否完整;\n" +
                "3. 重新下发配方后复位重新运行;",
    10074, "WS2 LD: switch array recipe fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string SwitchArrayRecipeSizeFailed = "PROC_WS2F_MOT_007";

            /// <summary>料盒公用底座未检测到物体</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-料盒公用底座未检测到物体", "WS2 Load: Cassette base empty", AlarmSeverity.Error,
    "1. 确认料盒是否正确放入;\n" +
                "2. 检查底座光电传感器是否正常;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;",
    10075, "WS2 Load: Cassette base empty",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string BoxBaseNotDetected = "PROC_WS2F_SEN_003";

          

            /// <summary>料盒尺寸传感器信号冲突</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发）", "WS2 Load: Cassette sensor conflict", AlarmSeverity.Error,
    "1. 检查料盒是否倾斜或放歪;\n" +
                "2. 检查8寸和12寸传感器安装位置;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;",
    10076, "WS2 Load: Cassette sensor conflict",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string BoxSizeConflict = "PROC_WS2F_SEN_006";

            /// <summary>目标层数超出有效范围</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-目标层数超出有效范围", "WS2 Load: Target layer out of range", AlarmSeverity.Error,
    "1. 检查配方中最大层数设置;\n" +
                "2. 确认料盒规格;\n" +
                "3. 复位后重新运行;",
    10077, "WS2 Load: Target layer out of range",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerOutOfRange = "PROC_WS2F_ALG_003";

            /// <summary>未找到目标层的阵列点位</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-未找到目标层的阵列点位（可能未执行生产状态切换）", "WS2 LD: target layer missing", AlarmSeverity.Error,
    "1. 确认已执行切换生产状态步骤;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;",
    10078, "WS2 LD: target layer missing",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerPointNotFound = "PROC_WS2F_ALG_004";

            /// <summary>Z轴切换层运动失败</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴切换层运动失败", "WS2 LD: Z-axis layer switch fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10079, "WS2 LD: Z-axis layer switch fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string LayerMoveFailed = "PROC_WS2F_MOT_006";

            /// <summary>Z轴互锁失败：料盒未到位禁止升降</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴互锁失败：料盒未到位禁止升降", "WS2 LD: Z ILK, cassette not ready", AlarmSeverity.Error,
    "1. 确认料盒已完全落座;\n" +
                "2. 检查底座到位传感器;\n" +
                "3. 复位后重新检查;",
    10080, "WS2 LD: Z ILK, cassette not ready",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ZAxisBoxNotInPlace = "PROC_WS2F_MOT_011";

            /// <summary>X轴互锁失败：存在铁环突片</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-X轴互锁失败：存在铁环突片", "WS2 LD: X ILK, ring protrusion", AlarmSeverity.Error,
    "1. 检查铁环突片检测传感器;\n" +
                "2. 确认铁环安装方向;\n" +
                "3. 复位后重新检查;",
    10081, "WS2 LD: X ILK, ring protrusion",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string XAxisTabDetected = "PROC_WS2F_MOT_008";

            /// <summary>拉料互锁失败：晶圆盒挡杆未打开</summary>
            [AlarmInfo("流程异常/执行器", "工位2上下料-拉料互锁失败：晶圆盒挡杆未打开", "WS2 LD: pull ILK, latch not open", AlarmSeverity.Error,
    "1. 检查挡杆驱动气缸状态;\n" +
                "2. 确认挡杆传感器信号;\n" +
                "3. 复位后重新检查;",
    10082, "WS2 LD: pull ILK, latch not open",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string PullOutLeverNotOpen = "PROC_WS2F_ACT_001";

            /// <summary>寻层扫描移动到起点失败</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-寻层扫描移动到起点失败", "WS2 LD: slot scan start move fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服驱动器是否报警;\n" +
                "2. 检查Z轴是否卡在中途;\n" +
                "3. 手动点动Z轴确认运动正常;\n" +
                "4. 复位后重新运行;",
    10083, "WS2 LD: slot scan start move fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ScanMoveToStartFailed = "PROC_WS2F_MOT_009";

            /// <summary>寻层扫描硬件锁存配置失败</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-寻层扫描硬件锁存配置失败", "WS2 Load: Scan latch config failed", AlarmSeverity.Error,
    "1. 检查运动控制卡连接;\n" +
                "2. 确认传感器接线;\n" +
                "3. 复位后重新运行;",
    10084, "WS2 Load: Scan latch config failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ScanLatchConfigFailed = "PROC_WS2F_SEN_007";

            /// <summary>寻层扫描移动到终点失败</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-寻层扫描移动到终点失败", "WS2 LD: slot scan move to end fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服驱动器是否报警;\n" +
                "2. 检查Z轴是否卡在中途;\n" +
                "3. 手动点动Z轴确认运动正常;\n" +
                "4. 复位后重新运行;",
    10085, "WS2 LD: slot scan move to end fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ScanMoveToEndFailed = "PROC_WS2F_MOT_010";

            /// <summary>寻层算法理论层坐标未初始化</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法理论层坐标未初始化", "WS2 LD: slot-find coords not init", AlarmSeverity.Error,
    "1. 确认已执行切换生产状态;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;",
    10086, "WS2 LD: slot-find coords not init",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmNotInitialized = "PROC_WS2F_ALG_005";

            /// <summary>寻层算法传感器原始数据不足</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法传感器原始数据不足", "WS2 LD: low sensor raw data", AlarmSeverity.Error,
    "1. 检查传感器信号线连接;\n" +
                "2. 确认料盒位置正确;\n" +
                "3. 复位后重新运行;",
    10087, "WS2 LD: low sensor raw data",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmRawDataMissing = "PROC_WS2F_ALG_006";

            /// <summary>寻层算法双传感器识别数量差异过大</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法双传感器识别数量差异过大（疑似斜片或传感器失效）", "WS2 LD: dual-sensor count mismatch", AlarmSeverity.Error,
    "1. 检查左右传感器信号;\n" +
                "2. 确认物料摆放无倾斜;\n" +
                "3. 复位后重新运行;",
    10088, "WS2 LD: dual-sensor count mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmCountMismatch = "PROC_WS2F_ALG_007";

            /// <summary>寻层算法检测到严重斜片(Cross-slot)</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法检测到严重斜片(Cross-slot)", "WS2 Load: Cross-slot wafer detected", AlarmSeverity.Error,
    "1. 人工检查料盒内物料状态;\n" +
                "2. 小心处理斜片物料;\n" +
                "3. 复位后重新执行寻层;",
    10089, "WS2 Load: Cross-slot wafer detected",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmCrossSlot = "PROC_WS2F_ALG_008";

            /// <summary>寻层算法检测到重叠片(Double-wafer)</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法检测到重叠片(Double-wafer)", "WS2 Load: Double-wafer detected", AlarmSeverity.Error,
    "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离重叠物料;\n" +
                "3. 复位后重新执行寻层;",
    10090, "WS2 Load: Double-wafer detected",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmDoubleWafer = "PROC_WS2F_ALG_009";

            /// <summary>寻层算法晶圆偏离标准槽位</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法晶圆严重偏离标准槽位（可能未插到底）", "WS2 LD: wafer off-slot, not seated", AlarmSeverity.Error,
    "1. 检查物料是否正确插入槽位;\n" +
                "2. 确认料盒无损坏;\n" +
                "3. 复位后重新执行寻层;",
    10091, "WS2 LD: wafer off-slot, not seated",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string AlgorithmSlotMismatch = "PROC_WS2F_ALG_010";

            /// <summary>指定层与实际寻层结果不匹配</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-指定层与实际寻层结果不匹配", "WS2 Load: Target slot mismatch", AlarmSeverity.Error,
    "1. 核查料盒内实际物料层位;\n" +
                "2. 确认切换批次时选择的指定层是否正确;\n" +
                "3. 修正指定层设置后重新切换批次;",
    10092, "WS2 Load: Target slot mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string SpecifiedLayersMismatch = "PROC_WS2F_ALG_011";

            /// <summary>断点续跑：重启后实际物料层数与记忆不一致</summary>
            [AlarmInfo("断点续跑", "工位2上下料-重启后物料状态与记忆不一致", "WS2 Load: Resume status mismatch", AlarmSeverity.Fatal,
    "1. 人工核查料盒内物料数量是否与系统记忆一致;\n" +
                "2. 若物料已被取走，请清空批次后重新下发;\n" +
                "3. 若物料仍在，检查传感器或算法是否异常;\n" +
                "4. 确认状态后手动复位重启;",
    10093, "WS2 Load: Resume status mismatch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ResumeConsistencyFailed = "PROC_WS2F_RSM_001";

            /// <summary>初始化异常（非预期故障统一归口）</summary>
            [AlarmInfo("流程异常/初始化", "工位2上下料-初始化异常", "WS2 Load: Initialization failed", AlarmSeverity.Error,
    "1. 查看日志中具体异常信息;\n" +
                "2. 排查对应硬件或配置项;\n" +
                "3. 排除故障后复位重新初始化;",
    10094, "WS2 Load: Initialization failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string InitException = "PROC_WS2F_INIT_001";

            /// <summary>断点续跑异常（非预期故障统一归口）</summary>
            [AlarmInfo("断点续跑", "工位2上下料-断点续跑异常", "WS2 Load: Resume run failed", AlarmSeverity.Error,
    "1. 查看日志中具体异常信息;\n" +
                "2. 排查对应硬件或状态异常;\n" +
                "3. 排除故障后复位重新运行;",
    10095, "WS2 Load: Resume run failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/上晶圆-成品.png")]
            public const string ResumeException = "PROC_WS2F_RSM_002";
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
            [AlarmInfo("流程异常/数据", "工位1拉料-配方参数为空", "WS1 Pull: Recipe params empty", AlarmSeverity.Error,
    "1. 确认配方已正确下发至工位1;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位;",
    10096, "WS1 Pull: Recipe params empty",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string RecipeNull = "PROC_WS1P_DATA_001";

            /// <summary>初始化校验失败（物料状态/尺寸校验等无专属硬件动作码的初始化失败统一归口）</summary>
            [AlarmInfo("流程异常/初始化", "工位1拉料-初始化校验失败", "WS1 Pull: Init check failed", AlarmSeverity.Error,
    "1. 根据报警描述核对夹爪物料状态与配方尺寸是否一致;\n" +
                "2. 确认轨道/夹爪上无残留或异常物料;\n" +
                "3. 排除后复位重新初始化;",
    10097, "WS1 Pull: Init check failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string InitCheckFailed = "PROC_WS1P_INIT_001";

            /// <summary>调整流道尺寸失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-调整流道尺寸失败", "WS1 Pull: Adjust track size failed", AlarmSeverity.Error,
    "1. 检查流道宽度调整电机是否报警;\n" +
                "2. 确认气源压力是否在正常范围;\n" +
                "3. 手动操作确认流道机构;\n" +
                "4. 复位后将重试调整;",
    10098, "WS1 Pull: Adjust track size failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string TrackSizeMotorFailed = "PROC_WS1P_MOT_001";

            /// <summary>Y轴移动到取料位失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-Y轴移动到取料位失败", "WS1 Pull: Y-axis move to pick fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警或超时;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后将重试移动;",
    10099, "WS1 Pull: Y-axis move to pick fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string YAxisToPickupFailed = "PROC_WS1P_MOT_002";

            /// <summary>关闭夹爪失败（未感应到闭合信号）</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-关闭夹爪失败（未感应到闭合信号）", "WS1 Pull: Close grip failed", AlarmSeverity.Error,
    "1. 检查气源压力是否在正常范围;\n" +
                "2. 检查夹爪闭合传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;",
    10100, "WS1 Pull: Close grip failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseFailed = "PROC_WS1P_ACT_001";

            /// <summary>检测到叠料异常</summary>
            [AlarmInfo("流程异常/物料", "工位1拉料-检测到叠料异常", "WS1 Pull: Stacked wafer detected", AlarmSeverity.Error,
    "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离叠料;\n" +
                "3. 确认物料正常后复位;",
    10101, "WS1 Pull: Stacked wafer detected",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string StackedPiecesDetected = "PROC_WS1P_MAT_001";



            /// <summary>8寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-8寸晶圆放反", "WS1 Pull: 8\" Wafer Reversed", AlarmSeverity.Error,
    "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;",
    10102, "WS1 Pull: 8\" Wafer Reversed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string Wafer8InchReversed = "PROC_WS1P_SEN_004";

            /// <summary>12寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-12寸晶圆放反", "WS1 Pull: 12\" Wafer Reversed", AlarmSeverity.Error,
    "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;",
    10103, "WS1 Pull: 12\" Wafer Reversed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string Wafer12InchReversed = "PROC_WS1P_SEN_005";




            /// <summary>拉出至检测位失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出至检测位失败（运动被中断）", "WS1 Pull: Pull to Check Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服驱动器是否报警;\n" +
                "2. 检查是否触发卡料或掉料防呆;\n" +
                "3. 手动确认Y轴运动是否顺畅;\n" +
                "4. 复位后将重试拉出;",
    10104, "WS1 Pull: Pull to Check Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutToInspectionFailed = "PROC_WS1P_MOT_003";

            /// <summary>推回至料盒失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-推回至料盒失败（运动被中断）", "WS1 Pull: push back fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服驱动器是否报警;\n" +
                "2. 检查是否触发防呆拦截;\n" +
                "3. 确认Y轴无卡阻;\n" +
                "4. 复位后将重试推回;",
    10105, "WS1 Pull: push back fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackToCassetteFailed = "PROC_WS1P_MOT_004";

            /// <summary>打开夹爪失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-打开夹爪失败", "WS1 Pull: Open Gripper Failed", AlarmSeverity.Error,
    "1. 检查气源压力;\n" +
                "2. 检查夹爪张开传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;",
    10106, "WS1 Pull: Open Gripper Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperOpenFailed = "PROC_WS1P_ACT_002";

            /// <summary>Y轴退回待机位失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-Y轴退回待机位失败", "WS1 Pull: Y-Axis Return Home Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后将重试退回;",
    10107, "WS1 Pull: Y-Axis Return Home Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string YAxisRetractFailed = "PROC_WS1P_MOT_005";

            /// <summary>退回安全位后夹爪仍检测到带料</summary>
            [AlarmInfo("流程异常/物料", "工位1拉料-退回安全位后夹爪仍检测到带料", "WS1 Pull: Material Still in Gripper", AlarmSeverity.Error,
    "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 检查夹爪内传感器;\n" +
                "4. 确认无料后复位;",
    10108, "WS1 Pull: Material Still in Gripper",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string WaferStuckInGripper = "PROC_WS1P_MAT_002";

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化拉料流程失败（Y轴运动到待机位失败）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-初始化拉料流程失败（Y轴运动到待机位失败）", "WS1 Pull: Init Sequence Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10109, "WS1 Pull: Init Sequence Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string InitPullingFailed = "PROC_WS1P_MOT_006";

            /// <summary>轨道有物料阻止尺寸切换</summary>
            [AlarmInfo("流程异常/物料", "工位1拉料-轨道有物料，无法执行尺寸切换", "WS1 Pull: track busy, no switch", AlarmSeverity.Error,
    "1. 清除轨道上的残留物料;\n" +
                "2. 确认轨道无料后复位;",
    10110, "WS1 Pull: track busy, no switch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string ChangeSizeTrackHasMaterial = "PROC_WS1P_MAT_003";

            /// <summary>尺寸切换气缸IO操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-尺寸切换气缸IO操作失败", "WS1 Pull: Size Cylinder IO Failed", AlarmSeverity.Error,
    "1. 检查IO模块输出信号;\n" +
                "2. 确认电磁阀接线;\n" +
                "3. 复位后重新运行;",
    10111, "WS1 Pull: Size Cylinder IO Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string ChangeSizeCylinderFailed = "PROC_WS1P_ACT_003";

            /// <summary>尺寸切换气缸超时</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-尺寸切换气缸动作超时", "WS1 Pull: Size Cylinder Timeout", AlarmSeverity.Error,
    "1. 检查气源压力是否正常;\n" +
                "2. 确认磁性开关信号;\n" +
                "3. 复位后重新运行;",
    10112, "WS1 Pull: Size Cylinder Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string ChangeSizeCylinderTimeout = "PROC_WS1P_ACT_004";

            /// <summary>夹爪张开气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪张开气缸操作失败", "WS1 Pull: Gripper Open Cyl Failed", AlarmSeverity.Error,
    "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;",
    10113, "WS1 Pull: Gripper Open Cyl Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperOpenCylinderFailed = "PROC_WS1P_ACT_005";

            /// <summary>夹爪张开超时</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪张开超时，未感应到张开信号", "WS1 Pull: Gripper Open Timeout", AlarmSeverity.Error,
    "1. 检查气源压力;\n" +
                "2. 确认气缸张开传感器信号;\n" +
                "3. 复位后重新运行;",
    10114, "WS1 Pull: Gripper Open Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperOpenTimeout = "PROC_WS1P_ACT_006";

            /// <summary>夹爪闭合气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪闭合气缸操作失败", "WS1 Pull: Gripper Close Cyl Failed", AlarmSeverity.Error,
    "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;",
    10115, "WS1 Pull: Gripper Close Cyl Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseCylinderFailed = "PROC_WS1P_ACT_007";

            /// <summary>夹爪闭合超时</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪闭合超时，未感应到闭合信号", "WS1 Pull: Gripper Close Timeout", AlarmSeverity.Error,
    "1. 检查气源压力;\n" +
                "2. 确认气缸闭合传感器信号;\n" +
                "3. 复位后重新运行;",
    10116, "WS1 Pull: Gripper Close Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseTimeout = "PROC_WS1P_ACT_008";

            /// <summary>夹爪闭合后未检测到铁环</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-夹爪闭合后未检测到铁环（空夹）", "WS1 Pull: No Ring Detected (Empty)", AlarmSeverity.Error,
    "1. 确认上料工站已将物料正确推送至拉料位;\n" +
                "2. 确认晶圆铁环是否在正确位置;\n" +
                "3. 检查铁环检测传感器;\n" +
                "4. 复位后重新运行;",
    10117, "WS1 Pull: No Ring Detected (Empty)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseNoRing = "PROC_WS1P_SEN_001";

            /// <summary>移动到待机位失败（带余料防呆）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到待机位失败", "WS1 Pull: Move to Home Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10118, "WS1 Pull: Move to Home Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string MoveInitialFailed = "PROC_WS1P_MOT_007";

            /// <summary>待机位检测到残留物料</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-待机位检测到残留物料", "WS1 Pull: Residual Material at Home", AlarmSeverity.Error,
    "1. 人工确认夹爪内是否有残留物料;\n" +
                "2. 清除残留物料后复位;",
    10119, "WS1 Pull: Residual Material at Home",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string MoveInitialResidualMaterial = "PROC_WS1P_SEN_002";

            /// <summary>移动到待机位失败（无检测模式）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到待机位失败（强制复位）", "WS1 Pull: move to home fail (force)", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10120, "WS1 Pull: move to home fail (force)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string MoveInitialNoScanFailed = "PROC_WS1P_MOT_008";

            /// <summary>移动到取出安全位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到取出安全位置失败", "WS1 Pull: Move to Safe Pos Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10121, "WS1 Pull: Move to Safe Pos Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PutOverMoveFailed = "PROC_WS1P_MOT_009";

            /// <summary>卸料后物料粘连未脱落</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-卸料后夹爪物料粘连未脱落", "WS1 Pull: mat stuck, unload fail", AlarmSeverity.Error,
    "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 复位后重新运行;",
    10122, "WS1 Pull: mat stuck, unload fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PutOverMaterialStuck = "PROC_WS1P_SEN_003";

            /// <summary>移动到取料位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到取料位置失败", "WS1 Pull: Move to Pick Pos Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10123, "WS1 Pull: Move to Pick Pos Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string InitialMoveFeedingFailed = "PROC_WS1P_MOT_010";

            /// <summary>拉出运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出运动触发失败", "WS1 Pull: pull motion trigger fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;",
    10124, "WS1 Pull: pull motion trigger fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutTriggerFailed = "PROC_WS1P_MOT_011";

            /// <summary>拉出过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出过程卡料报警，已紧急停止", "WS1 Pull: Pull Jam, E-Stop Active", AlarmSeverity.Fatal,
    "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;",
    10125, "WS1 Pull: Pull Jam, E-Stop Active",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutJamAlarm = "PROC_WS1P_MOT_012";

            /// <summary>拉出过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出过程丢料报警，已紧急停止", "WS1 Pull: Pull Drop, E-Stop Active", AlarmSeverity.Fatal,
    "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;",
    10126, "WS1 Pull: Pull Drop, E-Stop Active",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutDropAlarm = "PROC_WS1P_MOT_013";

            /// <summary>拉出运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-Y轴拉出运动超时", "WS1 Pull: Y-Axis Pull Timeout", AlarmSeverity.Error,
    "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10127, "WS1 Pull: Y-Axis Pull Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutTimeout = "PROC_WS1P_MOT_014";

            /// <summary>送入运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入运动触发失败", "WS1 Pull: feed motion trigger fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;",
    10128, "WS1 Pull: feed motion trigger fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackTriggerFailed = "PROC_WS1P_MOT_015";

            /// <summary>送入过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入过程卡料报警，已紧急刹停", "WS1 Pull: Feed Jam, E-Stop Active", AlarmSeverity.Fatal,
    "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;",
    10129, "WS1 Pull: Feed Jam, E-Stop Active",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackJamAlarm = "PROC_WS1P_MOT_016";

            /// <summary>送入过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入过程丢料报警，已紧急刹停", "WS1 Pull: Feed Drop, E-Stop Active", AlarmSeverity.Fatal,
    "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;",
    10130, "WS1 Pull: Feed Drop, E-Stop Active",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackDropAlarm = "PROC_WS1P_MOT_017";

            /// <summary>送入运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入运动超时", "WS1 Pull: Feed Motion Timeout", AlarmSeverity.Error,
    "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10131, "WS1 Pull: Feed Motion Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackTimeout = "PROC_WS1P_MOT_018";

            /// <summary>扫码失败</summary>
            [AlarmInfo("流程异常/相机", "工位1拉料-扫码失败或校验不合法", "WS1 Pull: Scan/Verify Failed", AlarmSeverity.Error,
    "1. 检查扫码枪连接;\n" +
                "2. 确认光源亮度;\n" +
                "3. 清洁扫码枪镜头;\n" +
                "4. 复位后重新运行;",
    10132, "WS1 Pull: Scan/Verify Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string CodeScanFailed = "PROC_WS1P_CAM_001";
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
            [AlarmInfo("流程异常/数据", "工位2拉料-配方参数为空", "WS2 Pull: Recipe Params Empty", AlarmSeverity.Error,
    "1. 确认配方已正确下发至工位2;\n" +
                "2. 检查配方参数页面数据是否完整;\n" +
                "3. 重新下发配方后复位;",
    10133, "WS2 Pull: Recipe Params Empty",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string RecipeNull = "PROC_WS2P_DATA_001";

            /// <summary>初始化校验失败（物料状态/尺寸校验等无专属硬件动作码的初始化失败统一归口）</summary>
            [AlarmInfo("流程异常/初始化", "工位2拉料-初始化校验失败", "WS2 Pull: Init Check Failed", AlarmSeverity.Error,
    "1. 根据报警描述核对夹爪物料状态与配方尺寸是否一致;\n" +
                "2. 确认轨道/夹爪上无残留或异常物料;\n" +
                "3. 排除后复位重新初始化;",
    10134, "WS2 Pull: Init Check Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string InitCheckFailed = "PROC_WS2P_INIT_001";

            /// <summary>调整流道尺寸失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-调整流道尺寸失败", "WS2 Pull: Adjust Track Size Failed", AlarmSeverity.Error,
    "1. 检查流道宽度调整电机是否报警;\n" +
                "2. 确认气源压力是否在正常范围;\n" +
                "3. 手动操作确认流道机构;\n" +
                "4. 复位后将重试调整;",
    10135, "WS2 Pull: Adjust Track Size Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string TrackSizeMotorFailed = "PROC_WS2P_MOT_001";

            /// <summary>Y轴移动到取料位失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-Y轴移动到取料位失败", "WS2 Pull: Y-axis move to pick fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警或超时;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后将重试移动;",
    10136, "WS2 Pull: Y-axis move to pick fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string YAxisToPickupFailed = "PROC_WS2P_MOT_002";

            /// <summary>关闭夹爪失败（未感应到闭合信号）</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-关闭夹爪失败（未感应到闭合信号）", "WS2 Pull: Close Gripper Failed", AlarmSeverity.Error,
    "1. 检查气源压力是否在正常范围;\n" +
                "2. 检查夹爪闭合传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;",
    10137, "WS2 Pull: Close Gripper Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseFailed = "PROC_WS2P_ACT_001";

            /// <summary>检测到叠料异常</summary>
            [AlarmInfo("流程异常/物料", "工位2拉料-检测到叠料异常", "WS2 Pull: Double Material Detected", AlarmSeverity.Error,
    "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离叠料;\n" +
                "3. 确认物料正常后复位;",
    10138, "WS2 Pull: Double Material Detected",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string StackedPiecesDetected = "PROC_WS2P_MAT_001";

            /// <summary>拉出至检测位失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出至检测位失败（运动被中断）", "WS2 Pull: Pull to Check Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服驱动器是否报警;\n" +
                "2. 检查是否触发卡料或掉料防呆;\n" +
                "3. 手动确认Y轴运动是否顺畅;\n" +
                "4. 复位后将重试拉出;",
    10139, "WS2 Pull: Pull to Check Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutToInspectionFailed = "PROC_WS2P_MOT_003";

            /// <summary>推回至料盒失败（运动被中断）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-推回至料盒失败（运动被中断）", "WS2 Pull: push back fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服驱动器是否报警;\n" +
                "2. 检查是否触发防呆拦截;\n" +
                "3. 确认Y轴无卡阻;\n" +
                "4. 复位后将重试推回;",
    10140, "WS2 Pull: push back fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackToCassetteFailed = "PROC_WS2P_MOT_004";

            /// <summary>打开夹爪失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-打开夹爪失败", "WS2 Pull: Open Gripper Failed", AlarmSeverity.Error,
    "1. 检查气源压力;\n" +
                "2. 检查夹爪张开传感器信号;\n" +
                "3. 手动操作夹爪确认动作;\n" +
                "4. 复位后将重试;",
    10141, "WS2 Pull: Open Gripper Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperOpenFailed = "PROC_WS2P_ACT_002";

            /// <summary>Y轴退回待机位失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-Y轴退回待机位失败", "WS2 Pull: Y-Axis Return Home Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后将重试退回;",
    10142, "WS2 Pull: Y-Axis Return Home Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string YAxisRetractFailed = "PROC_WS2P_MOT_005";

            /// <summary>退回安全位后夹爪仍检测到带料</summary>
            [AlarmInfo("流程异常/物料", "工位2拉料-退回安全位后夹爪仍检测到带料", "WS2 Pull: Material Still in Gripper", AlarmSeverity.Error,
    "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 检查夹爪内传感器;\n" +
                "4. 确认无料后复位;",
    10143, "WS2 Pull: Material Still in Gripper",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string WaferStuckInGripper = "PROC_WS2P_MAT_002";

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化拉料流程失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-初始化拉料流程失败（Y轴运动到待机位失败）", "WS2 Pull: Init Sequence Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10144, "WS2 Pull: Init Sequence Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string InitPullingFailed = "PROC_WS2P_MOT_006";

            /// <summary>轨道有物料阻止尺寸切换</summary>
            [AlarmInfo("流程异常/物料", "工位2拉料-轨道有物料，无法执行尺寸切换", "WS2 Pull: track busy, no switch", AlarmSeverity.Error,
    "1. 清除轨道上的残留物料;\n" +
                "2. 确认轨道无料后复位;",
    10145, "WS2 Pull: track busy, no switch",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string ChangeSizeTrackHasMaterial = "PROC_WS2P_MAT_003";

            /// <summary>尺寸切换气缸IO操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-尺寸切换气缸IO操作失败", "WS2 Pull: Size Cylinder IO Failed", AlarmSeverity.Error,
    "1. 检查IO模块输出信号;\n" +
                "2. 确认电磁阀接线;\n" +
                "3. 复位后重新运行;",
    10146, "WS2 Pull: Size Cylinder IO Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string ChangeSizeCylinderFailed = "PROC_WS2P_ACT_003";

            /// <summary>尺寸切换气缸超时</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-尺寸切换气缸动作超时", "WS2 Pull: Size Cylinder Timeout", AlarmSeverity.Error,
    "1. 检查气源压力是否正常;\n" +
                "2. 确认磁性开关信号;\n" +
                "3. 复位后重新运行;",
    10147, "WS2 Pull: Size Cylinder Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string ChangeSizeCylinderTimeout = "PROC_WS2P_ACT_004";

            /// <summary>夹爪张开气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪张开气缸操作失败", "WS2 Pull: Gripper Open Cyl Failed", AlarmSeverity.Error,
    "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;",
    10148, "WS2 Pull: Gripper Open Cyl Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperOpenCylinderFailed = "PROC_WS2P_ACT_005";

            /// <summary>夹爪张开超时</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪张开超时，未感应到张开信号", "WS2 Pull: Gripper Open Timeout", AlarmSeverity.Error,
    "1. 检查气源压力;\n" +
                "2. 确认气缸张开传感器信号;\n" +
                "3. 复位后重新运行;",
    10149, "WS2 Pull: Gripper Open Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperOpenTimeout = "PROC_WS2P_ACT_006";

            /// <summary>夹爪闭合气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪闭合气缸操作失败", "WS2 Pull: Gripper Close Cyl Failed", AlarmSeverity.Error,
    "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;",
    10150, "WS2 Pull: Gripper Close Cyl Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseCylinderFailed = "PROC_WS2P_ACT_007";

            /// <summary>夹爪闭合超时</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪闭合超时，未感应到闭合信号", "WS2 Pull: Gripper Close Timeout", AlarmSeverity.Error,
    "1. 检查气源压力;\n" +
                "2. 确认气缸闭合传感器信号;\n" +
                "3. 复位后重新运行;",
    10151, "WS2 Pull: Gripper Close Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseTimeout = "PROC_WS2P_ACT_008";

            /// <summary>夹爪闭合后未检测到铁环</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-夹爪闭合后未检测到铁环（空夹）", "WS2 Pull: No Ring Detected (Empty)", AlarmSeverity.Error,
    "1. 确认上料工站已将物料正确推送至拉料位;\n" +
                "2. 确认晶圆铁环是否在正确位置;\n" +
                "3. 检查铁环检测传感器;\n" +
                "4. 复位后重新运行;",
    10152, "WS2 Pull: No Ring Detected (Empty)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string GripperCloseNoRing = "PROC_WS2P_SEN_001";

            /// <summary>移动到待机位失败（带余料防呆）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到待机位失败", "WS2 Pull: move to standby pos fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10153, "WS2 Pull: move to standby pos fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string MoveInitialFailed = "PROC_WS2P_MOT_007";

            /// <summary>待机位检测到残留物料</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-待机位检测到残留物料", "WS2 Pull: residue at standby", AlarmSeverity.Error,
    "1. 人工确认夹爪内是否有残留物料;\n" +
                "2. 清除残留物料后复位;",
    10154, "WS2 Pull: residue at standby",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string MoveInitialResidualMaterial = "PROC_WS2P_SEN_002";

            /// <summary>移动到待机位失败（无检测模式）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到待机位失败（强制复位）", "WS2 Pull: standby move fail (RST)", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10155, "WS2 Pull: standby move fail (RST)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string MoveInitialNoScanFailed = "PROC_WS2P_MOT_008";

            /// <summary>移动到取出安全位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到取出安全位置失败", "WS2 Pull: Move to Safe Pos Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10156, "WS2 Pull: Move to Safe Pos Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PutOverMoveFailed = "PROC_WS2P_MOT_009";

            /// <summary>卸料后物料粘连未脱落</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-卸料后夹爪物料粘连未脱落", "WS2 Pull: Gripper Material Sticking", AlarmSeverity.Error,
    "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 复位后重新运行;",
    10157, "WS2 Pull: Gripper Material Sticking",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PutOverMaterialStuck = "PROC_WS2P_SEN_003";




            /// <summary>8寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-8寸晶圆放反", "WS2 Pull: 8\" Wafer Placed Reversed", AlarmSeverity.Error,
    "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;",
    10158, "WS2 Pull: 8\" Wafer Placed Reversed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string Wafer8InchReversed = "PROC_WS2P_SEN_004";
            /// <summary>12寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-12寸晶圆放反", "WS2 Pull: 12\" Wafer Reversed", AlarmSeverity.Error,
    "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;",
    10159, "WS2 Pull: 12\" Wafer Reversed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string Wafer12InchReversed = "PROC_WS2P_SEN_005";



            /// <summary>移动到取料位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到取料位置失败", "WS2 Pull: Move to Pick Pos Failed", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10160, "WS2 Pull: Move to Pick Pos Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string InitialMoveFeedingFailed = "PROC_WS2P_MOT_010";

            /// <summary>拉出运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出运动触发失败", "WS2 Pull: Pull Motion Trigger Fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;",
    10161, "WS2 Pull: Pull Motion Trigger Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutTriggerFailed = "PROC_WS2P_MOT_011";

            /// <summary>拉出过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出过程卡料报警，已紧急停止", "WS2 Pull: pull jam, E-stop on", AlarmSeverity.Fatal,
    "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;",
    10162, "WS2 Pull: pull jam, E-stop on",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutJamAlarm = "PROC_WS2P_MOT_012";

            /// <summary>拉出过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出过程丢料报警，已紧急停止", "WS2 Pull: pull drop, E-stop on", AlarmSeverity.Fatal,
    "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;",
    10163, "WS2 Pull: pull drop, E-stop on",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutDropAlarm = "PROC_WS2P_MOT_013";

            /// <summary>拉出运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-Y轴拉出运动超时", "WS2 Pull: Y pull motion timeout", AlarmSeverity.Error,
    "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10164, "WS2 Pull: Y pull motion timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PullOutTimeout = "PROC_WS2P_MOT_014";

            /// <summary>送入运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入运动触发失败", "WS2 Pull: Feed Motion Trigger Fail", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;",
    10165, "WS2 Pull: Feed Motion Trigger Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackTriggerFailed = "PROC_WS2P_MOT_015";

            /// <summary>送入过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入过程卡料报警，已紧急刹停", "WS2 Pull: feed jam, E-stop on", AlarmSeverity.Fatal,
    "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;",
    10166, "WS2 Pull: feed jam, E-stop on",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackJamAlarm = "PROC_WS2P_MOT_016";

            /// <summary>送入过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入过程丢料报警，已紧急刹停", "WS2 Pull: feed drop, E-stop on", AlarmSeverity.Fatal,
    "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;",
    10167, "WS2 Pull: feed drop, E-stop on",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackDropAlarm = "PROC_WS2P_MOT_017";

            /// <summary>送入运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入运动超时", "WS2 Pull: Feed Motion Timeout", AlarmSeverity.Error,
    "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10168, "WS2 Pull: Feed Motion Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string PushBackTimeout = "PROC_WS2P_MOT_018";

            /// <summary>扫码失败</summary>
            [AlarmInfo("流程异常/相机", "工位2拉料-扫码失败或校验不合法", "WS2 Pull: Scan or Verify Failed", AlarmSeverity.Error,
    "1. 检查扫码枪连接;\n" +
                "2. 确认光源亮度;\n" +
                "3. 清洁扫码枪镜头;\n" +
                "4. 复位后重新运行;",
    10169, "WS2 Pull: Scan or Verify Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/夹爪-成品.png")]
            public const string CodeScanFailed = "PROC_WS2P_CAM_001";
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
            [AlarmInfo("流程异常/信号", "OCR检测-等待工位检测信号任务池异常中断", "OCR: Task Pool Abnormal Interrupt", AlarmSeverity.Error,
    "1. 检查上游拉料工站信号是否正常发出;\n" +
                "2. 确认工站间同步配置是否正确;\n" +
                "3. 查看关联工站报警信息;\n" +
                "4. 复位后重新运行;",
    10170, "OCR: Task Pool Abnormal Interrupt",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string SignalWaitFault = "PROC_DET_SIG_001";

            /// <summary>龙门模组移动到检测位置失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-龙门模组移动到检测位置失败", "OCR: gantry inspect pos move fail", AlarmSeverity.Error,
    "1. 检查龙门X/Y/Z轴是否报警;\n" +
                "2. 确认配方中目标坐标是否正确;\n" +
                "3. 手动点动确认龙门运动;\n" +
                "4. 复位后重试定位;",
    10171, "OCR: gantry inspect pos move fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string GantryMoveFailed = "PROC_DET_MOT_001";

            /// <summary>相机握手失败（光源或相机掉线）</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-相机握手失败（光源或相机掉线）", "OCR: Cam Handshake Fail (Light/Cam)", AlarmSeverity.Error,
    "1. 检查相机连接状态;\n" +
                "2. 检查光源控制器通讯状态;\n" +
                "3. 确认相机触发参数配置正确;\n" +
                "4. 手动触发相机确认功能;\n" +
                "5. 复位后重新运行;",
    10172, "OCR: Cam Handshake Fail (Light/Cam)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string CameraTriggerFailed = "PROC_DET_CAM_001";

            /// <summary>相机Z轴无法抬起避位（紧急锁死防撞）</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-相机Z轴无法抬起避位（紧急锁死防撞）", "OCR: Z-Axis Lift Fail (Crash Lock)", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认是否能抬起;\n" +
                "3. 确认无机械干涉;\n" +
                "4. 复位后重试;",
    10173, "OCR: Z-Axis Lift Fail (Crash Lock)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string ZAxisRetractAfterScan = "PROC_DET_MOT_002";

            /// <summary>检测数据写入失败</summary>
            [AlarmInfo("流程异常/数据", "OCR检测-检测数据写入失败", "OCR: Data Write Failed", AlarmSeverity.Error,
    "1. 检查数据库连接与磁盘空间;\n" +
                "2. 查看日志中具体写入错误信息;\n" +
                "3. 尝试重启数据服务后复位;\n" +
                "4. 联系维护人员检查数据库;",
    10174, "OCR: Data Write Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string DataWriteFailed = "PROC_DET_DATA_001";

         

            // ── 模组内部方法级错误码 ──

            /// <summary>移动到待机位失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到待机位失败（Z轴或XY轴运动失败）", "OCR: Move Standby Fail (Z/XY Axis)", AlarmSeverity.Error,
    "1. 检查XYZ轴伺服是否报警;\n" +
                "2. 手动点动确认各轴运动正常;\n" +
                "3. 复位后重新运行;",
    10175, "OCR: Move Standby Fail (Z/XY Axis)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveInitialFailed = "PROC_DET_MOT_003";

            /// <summary>Z轴安全位移动失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-Z轴移动到安全位置失败", "OCR: Z-Axis Move to Safe Pos Fail", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 复位后重新运行;",
    10176, "OCR: Z-Axis Move to Safe Pos Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveZSafePosFailed = "PROC_DET_MOT_004";

            /// <summary>工位1配方为空，无法定位</summary>
            [AlarmInfo("流程异常/数据", "OCR检测-工位1配方为空，无法获取目标坐标", "OCR: WS1 Recipe Empty, No Coords", AlarmSeverity.Error,
    "1. 确认工位1配方已正确下发;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 重新下发配方后复位;",
    10177, "OCR: WS1 Recipe Empty, No Coords",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation1RecipeNull = "PROC_DET_DATA_002";

            /// <summary>移动到工位1轴运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位1轴运动触发失败", "OCR: WS1 Axis Trigger Fail", AlarmSeverity.Error,
    "1. 检查XYZ轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;",
    10178, "OCR: WS1 Axis Trigger Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation1MoveFailed = "PROC_DET_MOT_005";

            /// <summary>工位1 OCR相机配方切换失败</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-切换到工位1的OCR配方失败", "OCR: Switch to WS1 Recipe Fail", AlarmSeverity.Error,
    "1. 检查相机通讯连接;\n" +
                "2. 确认配方名称是否正确;\n" +
                "3. 复位后重新运行;",
    10179, "OCR: Switch to WS1 Recipe Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation1RecipeSwitchFailed = "PROC_DET_CAM_002";

            /// <summary>移动到工位1轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位1 XYZ轴运动超时", "OCR: WS1 XYZ Axis Motion Timeout", AlarmSeverity.Error,
    "1. 检查各轴是否卡在中途;\n" +
                "2. 手动点动确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10180, "OCR: WS1 XYZ Axis Motion Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation1MoveTimeout = "PROC_DET_MOT_006";

            /// <summary>工位2配方为空，无法定位</summary>
            [AlarmInfo("流程异常/数据", "OCR检测-工位2配方为空，无法获取目标坐标", "OCR: WS2 Recipe Empty, No Coords", AlarmSeverity.Error,
    "1. 确认工位2配方已正确下发;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 重新下发配方后复位;",
    10181, "OCR: WS2 Recipe Empty, No Coords",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation2RecipeNull = "PROC_DET_DATA_003";

            /// <summary>移动到工位2轴运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位2轴运动触发失败", "OCR: WS2 Axis Trigger Fail", AlarmSeverity.Error,
    "1. 检查XYZ轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;",
    10182, "OCR: WS2 Axis Trigger Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation2MoveFailed = "PROC_DET_MOT_007";

            /// <summary>工位2 OCR相机配方切换失败</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-切换到工位2的OCR配方失败", "OCR: Switch to WS2 Recipe Fail", AlarmSeverity.Error,
    "1. 检查相机通讯连接;\n" +
                "2. 确认配方名称是否正确;\n" +
                "3. 复位后重新运行;",
    10183, "OCR: Switch to WS2 Recipe Fail",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation2RecipeSwitchFailed = "PROC_DET_CAM_003";

            /// <summary>移动到工位2轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位2 XYZ轴运动超时", "OCR: WS2 XYZ Axis Motion Timeout", AlarmSeverity.Error,
    "1. 检查各轴是否卡在中途;\n" +
                "2. 手动点动确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;",
    10184, "OCR: WS2 XYZ Axis Motion Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string MoveToStation2MoveTimeout = "PROC_DET_MOT_008";

            /// <summary>相机拍照触发失败</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-相机拍照触发失败或通讯异常", "OCR: Cam Trigger Fail / Comm Error", AlarmSeverity.Error,
    "1. 检查相机连接状态;\n" +
                "2. 确认相机触发参数;\n" +
                "3. 手动触发相机确认功能;\n" +
                "4. 复位后重新运行;",
    10185, "OCR: Cam Trigger Fail / Comm Error",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string CameraCaptureFailed = "PROC_DET_CAM_004";

            // ── 初始化流程 ──

            /// <summary>初始化Z轴回零失败</summary>
            [AlarmInfo("流程异常/初始化", "OCR检测-初始化Z轴回零失败", "OCR: Z-Axis Homing Fail (Init)", AlarmSeverity.Error,
    "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 确认Z轴原点传感器信号;\n" +
                "4. 复位后重新初始化;",
    10186, "OCR: Z-Axis Homing Fail (Init)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string InitHomeZFailed = "PROC_DET_MOT_009";

            /// <summary>初始化X轴回零失败</summary>
            [AlarmInfo("流程异常/初始化", "OCR检测-初始化X轴回零失败", "OCR: X-Axis Homing Fail (Init)", AlarmSeverity.Error,
    "1. 检查X轴伺服是否报警;\n" +
                "2. 手动点动X轴确认运动正常;\n" +
                "3. 确认X轴原点传感器信号;\n" +
                "4. 复位后重新初始化;",
    10187, "OCR: X-Axis Homing Fail (Init)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string InitHomeXFailed = "PROC_DET_MOT_010";

            /// <summary>初始化Y轴回零失败</summary>
            [AlarmInfo("流程异常/初始化", "OCR检测-初始化Y轴回零失败", "OCR: Y-Axis Homing Fail (Init)", AlarmSeverity.Error,
    "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 确认Y轴原点传感器信号;\n" +
                "4. 复位后重新初始化;",
    10188, "OCR: Y-Axis Homing Fail (Init)",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string InitHomeYFailed = "PROC_DET_MOT_011";

            /// <summary>初始化异常（非预期故障统一归口）</summary>
            [AlarmInfo("流程异常/初始化", "OCR检测-初始化异常", "OCR: Initialization Abnormal", AlarmSeverity.Error,
    "1. 查看日志中具体异常信息;\n" +
                "2. 根据异常详情排查对应硬件或配置;\n" +
                "3. 复位后重新初始化;",
    10189, "OCR: Initialization Abnormal",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/视觉龙门-成品.png")]
            public const string InitException = "PROC_DET_INIT_001";
        }

        // ─────────────────────────────────────────────────────────────────────
        // 数据模组 (PROC_DATA_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 数据模组报警代码
        /// </summary>
        public static class DataModule
        {
            /// <summary>MES查询失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-MES查询失败", "Data: MES Query Failed", AlarmSeverity.Error,
    "1. 检查MES服务器连接状态;\n" +
                "2. 确认网络配置正确;\n" +
                "3. 检查MES接口参数;\n" +
                "4. 联系MES维护人员;",
    10190, "Data: MES Query Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string MesQueryFailed = "PROC_DATA_MES_001";

            /// <summary>配方更新失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-配方更新失败", "Data: Recipe Update Failed", AlarmSeverity.Error,
    "1. 检查配方数据格式;\n" +
                "2. 确认配方版本兼容性;\n" +
                "3. 检查存储空间;\n" +
                "4. 重新下发配方;",
    10191, "Data: Recipe Update Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string RecipeUpdateFailed = "PROC_DATA_REC_001";

            /// <summary>OCR校验失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-OCR校验失败", "Data: OCR Verification Failed", AlarmSeverity.Error,
    "1. 检查OCR识别结果;\n" +
                "2. 确认校验规则配置;\n" +
                "3. 调整OCR参数后重试;",
    10192, "Data: OCR Verification Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string OcrValidationFailed = "PROC_DATA_OCR_001";

            /// <summary>数据持久化失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-数据持久化失败", "Data: Persistence Failed", AlarmSeverity.Error,
    "1. 检查数据库连接;\n" +
                "2. 确认磁盘空间充足;\n" +
                "3. 检查文件读写权限;\n" +
                "4. 重启数据服务;",
    10193, "Data: Persistence Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string DataPersistenceFailed = "PROC_DATA_DB_001";

            /// <summary>批次数据不完整</summary>
            [AlarmInfo("流程异常/数据", "数据模组-批次数据不完整", "Data: Batch Data Incomplete", AlarmSeverity.Error,
    "1. 检查MES下发的批次数据;\n" +
                "2. 确认所有必填字段已填充;\n" +
                "3. 重新请求批次数据;",
    10194, "Data: Batch Data Incomplete",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string BatchDataIncomplete = "PROC_DATA_BAT_001";

            /// <summary>MES批次信息更新失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-MES批次信息更新失败", "Data: MES Batch Update Failed", AlarmSeverity.Error,
    "1. 检查工位标识是否合法;\n" +
                "2. 确认MES数据格式正确;\n" +
                "3. 重新尝试切换批次;",
    10195, "Data: MES Batch Update Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string MesInfoUpdateFailed = "PROC_DATA_MES_002";

            /// <summary>条码校验失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-条码校验失败", "Data: Barcode Verify Failed", AlarmSeverity.Error,
    "1. 检查扫码枪读取结果;\n" +
                "2. 确认条码格式与配方规则匹配;\n" +
                "3. 核对MES下发的客户批次名单;",
    10196, "Data: Barcode Verify Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string CodeValidationFailed = "PROC_DATA_CODE_001";
        }

        // ─────────────────────────────────────────────────────────────────────
        // SECS/GEM 模组 (PROC_SECS_*)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// SECS/GEM模组报警代码
        /// </summary>
        public static class SecsGemModule
        {
            /// <summary>SECS/GEM初始化失败</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-初始化失败", "SECS/GEM: Init Failed", AlarmSeverity.Error,
    "1. 检查通讯板卡连接;\n" +
                "2. 确认IP地址配置;\n" +
                "3. 检查端口占用情况;\n" +
                "4. 重启通讯服务;",
    10197, "SECS/GEM: Init Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string InitializationFailed = "PROC_SECS_INIT_001";

            /// <summary>协议处理失败</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-协议处理失败", "SECS/GEM: Protocol Proc Failed", AlarmSeverity.Error,
    "1. 检查消息格式;\n" +
                "2. 确认协议版本兼容性;\n" +
                "3. 查看通讯日志;\n" +
                "4. 重新建立连接;",
    10198, "SECS/GEM: Protocol Proc Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string ProtocolProcessingFailed = "PROC_SECS_PROT_001";

            /// <summary>消息发送失败</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-消息发送失败", "SECS/GEM: Message Send Failed", AlarmSeverity.Error,
    "1. 检查网络连接;\n" +
                "2. 确认目标主机可达;\n" +
                "3. 检查防火墙设置;\n" +
                "4. 重试发送消息;",
    10199, "SECS/GEM: Message Send Failed",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string MessageSendFailed = "PROC_SECS_SEND_001";

            /// <summary>消息接收超时</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-消息接收超时", "SECS/GEM: Message Receive Timeout", AlarmSeverity.Error,
    "1. 检查主机响应状态;\n" +
                "2. 确认消息处理逻辑;\n" +
                "3. 调整超时时间;\n" +
                "4. 重新发送请求;",
    10200, "SECS/GEM: Message Receive Timeout",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string MessageReceiveTimeout = "PROC_SECS_RECV_001";

            /// <summary>连接断开</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-连接断开", "SECS/GEM: Connection Lost", AlarmSeverity.Error,
    "1. 检查物理连接;\n" +
                "2. 确认主机状态;\n" +
                "3. 自动重连或手动复位;",
    10201, "SECS/GEM: Connection Lost",
    "/PF.WorkStation.AutoOcr.UI;component/ModelImages/整机-成品.png")]
            public const string ConnectionLost = "PROC_SECS_CONN_001";
        }
    }
}
