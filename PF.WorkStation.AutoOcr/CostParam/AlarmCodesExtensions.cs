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

            

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化上料状态失败（Z/X轴运动到待机位失败）</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-初始化上料状态失败（Z/X轴运动到待机位失败）", AlarmSeverity.Error,
                "1. 检查Z轴和X轴是否处于报警状态;\n" +
                "2. 手动点动确认各轴运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string InitFeedingStateFailed = "PROC_WS1F_MOT_005";

            /// <summary>切换阵列配方尺寸失败（SwitchProductionStateAsync 执行失败）</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-切换阵列配方尺寸失败", AlarmSeverity.Error,
                "1. 检查机构是否卡阻或存在异物;\n" +
                "2. 手动点动确认尺寸切换机构运动正常;\n" +
                "3. 确认当前配方尺寸与料盒规格一致;\n" +
                "4. 复位后重新运行;")]
            public const string SwitchArrayRecipeSizeFailed = "PROC_WS1F_MOT_007";

            /// <summary>料盒公用底座未检测到物体</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-料盒公用底座未检测到物体", AlarmSeverity.Error,
                "1. 确认料盒是否正确放入;\n" +
                "2. 检查底座光电传感器是否正常;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;")]
            public const string BoxBaseNotDetected = "PROC_WS1F_SEN_003";

            /// <summary>8寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-8寸晶圆放反", AlarmSeverity.Error,
                "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;")]
            public const string Wafer8InchReversed = "PROC_WS1F_SEN_004";

            /// <summary>12寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-12寸晶圆放反", AlarmSeverity.Error,
                "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;")]
            public const string Wafer12InchReversed = "PROC_WS1F_SEN_005";

            /// <summary>料盒尺寸传感器信号冲突</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发）", AlarmSeverity.Error,
                "1. 检查料盒是否倾斜或放歪;\n" +
                "2. 检查8寸和12寸传感器安装位置;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;")]
            public const string BoxSizeConflict = "PROC_WS1F_SEN_006";

            /// <summary>目标层数超出有效范围</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-目标层数超出有效范围", AlarmSeverity.Error,
                "1. 检查配方中最大层数设置;\n" +
                "2. 确认料盒规格;\n" +
                "3. 复位后重新运行;")]
            public const string LayerOutOfRange = "PROC_WS1F_ALG_003";

            /// <summary>未找到目标层的阵列点位</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-未找到目标层的阵列点位（可能未执行生产状态切换）", AlarmSeverity.Error,
                "1. 确认已执行切换生产状态步骤;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;")]
            public const string LayerPointNotFound = "PROC_WS1F_ALG_004";

            /// <summary>Z轴切换层运动失败</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴切换层运动失败", AlarmSeverity.Error,
                "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string LayerMoveFailed = "PROC_WS1F_MOT_006";

            /// <summary>Z轴互锁失败：料盒未到位禁止升降</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-Z轴互锁失败：料盒未到位禁止升降", AlarmSeverity.Error,
                "1. 确认料盒已完全落座;\n" +
                "2. 检查底座到位传感器;\n" +
                "3. 复位后重新检查;")]
            public const string ZAxisBoxNotInPlace = "PROC_WS1F_MOT_007";

            /// <summary>X轴互锁失败：存在铁环突片</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-X轴互锁失败：存在铁环突片", AlarmSeverity.Error,
                "1. 检查铁环突片检测传感器;\n" +
                "2. 确认铁环安装方向;\n" +
                "3. 复位后重新检查;")]
            public const string XAxisTabDetected = "PROC_WS1F_MOT_008";

            /// <summary>拉料互锁失败：晶圆盒挡杆未打开</summary>
            [AlarmInfo("流程异常/执行器", "工位1上下料-拉料互锁失败：晶圆盒挡杆未打开", AlarmSeverity.Error,
                "1. 检查挡杆驱动气缸状态;\n" +
                "2. 确认挡杆传感器信号;\n" +
                "3. 复位后重新检查;")]
            public const string PullOutLeverNotOpen = "PROC_WS1F_ACT_001";

            /// <summary>寻层扫描移动到起点失败</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-寻层扫描移动到起点失败", AlarmSeverity.Error,
                "1. 检查Z轴是否卡在中途;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string ScanMoveToStartFailed = "PROC_WS1F_MOT_009";

            /// <summary>寻层扫描硬件锁存配置失败</summary>
            [AlarmInfo("流程异常/传感器", "工位1上下料-寻层扫描硬件锁存配置失败", AlarmSeverity.Error,
                "1. 检查运动控制卡连接;\n" +
                "2. 确认传感器接线;\n" +
                "3. 复位后重新运行;")]
            public const string ScanLatchConfigFailed = "PROC_WS1F_SEN_007";

            /// <summary>寻层扫描移动到终点失败</summary>
            [AlarmInfo("流程异常/运动", "工位1上下料-寻层扫描移动到终点失败", AlarmSeverity.Error,
                "1. 检查Z轴是否卡在中途;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string ScanMoveToEndFailed = "PROC_WS1F_MOT_010";

            /// <summary>寻层算法理论层坐标未初始化</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法理论层坐标未初始化", AlarmSeverity.Error,
                "1. 确认已执行切换生产状态;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;")]
            public const string AlgorithmNotInitialized = "PROC_WS1F_ALG_005";

            /// <summary>寻层算法传感器原始数据不足</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法传感器原始数据不足", AlarmSeverity.Error,
                "1. 检查传感器信号线连接;\n" +
                "2. 确认料盒位置正确;\n" +
                "3. 复位后重新运行;")]
            public const string AlgorithmRawDataMissing = "PROC_WS1F_ALG_006";

            /// <summary>寻层算法双传感器识别数量差异过大</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法双传感器识别数量差异过大（疑似斜片或传感器失效）", AlarmSeverity.Error,
                "1. 检查左右传感器信号;\n" +
                "2. 确认物料摆放无倾斜;\n" +
                "3. 复位后重新运行;")]
            public const string AlgorithmCountMismatch = "PROC_WS1F_ALG_007";

            /// <summary>寻层算法检测到严重斜片(Cross-slot)</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法检测到严重斜片(Cross-slot)", AlarmSeverity.Error,
                "1. 人工检查料盒内物料状态;\n" +
                "2. 小心处理斜片物料;\n" +
                "3. 复位后重新执行寻层;")]
            public const string AlgorithmCrossSlot = "PROC_WS1F_ALG_008";

            /// <summary>寻层算法检测到重叠片(Double-wafer)</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法检测到重叠片(Double-wafer)", AlarmSeverity.Error,
                "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离重叠物料;\n" +
                "3. 复位后重新执行寻层;")]
            public const string AlgorithmDoubleWafer = "PROC_WS1F_ALG_009";

            /// <summary>寻层算法晶圆偏离标准槽位</summary>
            [AlarmInfo("流程异常/算法", "工位1上下料-寻层算法晶圆严重偏离标准槽位（可能未插到底）", AlarmSeverity.Error,
                "1. 检查物料是否正确插入槽位;\n" +
                "2. 确认料盒无损坏;\n" +
                "3. 复位后重新执行寻层;")]
            public const string AlgorithmSlotMismatch = "PROC_WS1F_ALG_010";

            /// <summary>断点续跑：重启后实际物料层数与记忆不一致</summary>
            [AlarmInfo("断点续跑", "工位1上下料-重启后物料状态与记忆不一致", AlarmSeverity.Fatal,
                "1. 人工核查料盒内物料数量是否与系统记忆一致;\n" +
                "2. 若物料已被取走，请清空批次后重新下发;\n" +
                "3. 若物料仍在，检查传感器或算法是否异常;\n" +
                "4. 确认状态后手动复位重启;")]
            public const string ResumeConsistencyFailed = "PROC_WS1F_RSM_001";
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

          

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化上料状态失败（Z/X轴运动到待机位失败）</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-初始化上料状态失败（Z/X轴运动到待机位失败）", AlarmSeverity.Error,
                "1. 检查Z轴和X轴是否处于报警状态;\n" +
                "2. 手动点动确认各轴运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string InitFeedingStateFailed = "PROC_WS2F_MOT_005";

            /// <summary>切换阵列配方尺寸失败（SwitchProductionStateAsync 执行失败）</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-切换阵列配方尺寸失败", AlarmSeverity.Error,
                "1. 检查机构是否卡阻或存在异物;\n" +
                "2. 手动点动确认尺寸切换机构运动正常;\n" +
                "3. 确认当前配方尺寸与料盒规格一致;\n" +
                "4. 复位后重新运行;")]
            public const string SwitchArrayRecipeSizeFailed = "PROC_WS2F_MOT_007";

            /// <summary>料盒公用底座未检测到物体</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-料盒公用底座未检测到物体", AlarmSeverity.Error,
                "1. 确认料盒是否正确放入;\n" +
                "2. 检查底座光电传感器是否正常;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;")]
            public const string BoxBaseNotDetected = "PROC_WS2F_SEN_003";

            /// <summary>8寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-8寸晶圆放反", AlarmSeverity.Error,
                "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;")]
            public const string Wafer8InchReversed = "PROC_WS2F_SEN_004";

            /// <summary>12寸晶圆放反</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-12寸晶圆放反", AlarmSeverity.Error,
                "1. 取出料盒检查晶圆放置方向;\n" +
                "2. 确认防反传感器信号正常;\n" +
                "3. 正确放置后复位;")]
            public const string Wafer12InchReversed = "PROC_WS2F_SEN_005";

            /// <summary>料盒尺寸传感器信号冲突</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发）", AlarmSeverity.Error,
                "1. 检查料盒是否倾斜或放歪;\n" +
                "2. 检查8寸和12寸传感器安装位置;\n" +
                "3. 清洁传感器感应面;\n" +
                "4. 复位后重新检测;")]
            public const string BoxSizeConflict = "PROC_WS2F_SEN_006";

            /// <summary>目标层数超出有效范围</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-目标层数超出有效范围", AlarmSeverity.Error,
                "1. 检查配方中最大层数设置;\n" +
                "2. 确认料盒规格;\n" +
                "3. 复位后重新运行;")]
            public const string LayerOutOfRange = "PROC_WS2F_ALG_003";

            /// <summary>未找到目标层的阵列点位</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-未找到目标层的阵列点位（可能未执行生产状态切换）", AlarmSeverity.Error,
                "1. 确认已执行切换生产状态步骤;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;")]
            public const string LayerPointNotFound = "PROC_WS2F_ALG_004";

            /// <summary>Z轴切换层运动失败</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴切换层运动失败", AlarmSeverity.Error,
                "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string LayerMoveFailed = "PROC_WS2F_MOT_006";

            /// <summary>Z轴互锁失败：料盒未到位禁止升降</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-Z轴互锁失败：料盒未到位禁止升降", AlarmSeverity.Error,
                "1. 确认料盒已完全落座;\n" +
                "2. 检查底座到位传感器;\n" +
                "3. 复位后重新检查;")]
            public const string ZAxisBoxNotInPlace = "PROC_WS2F_MOT_007";

            /// <summary>X轴互锁失败：存在铁环突片</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-X轴互锁失败：存在铁环突片", AlarmSeverity.Error,
                "1. 检查铁环突片检测传感器;\n" +
                "2. 确认铁环安装方向;\n" +
                "3. 复位后重新检查;")]
            public const string XAxisTabDetected = "PROC_WS2F_MOT_008";

            /// <summary>拉料互锁失败：晶圆盒挡杆未打开</summary>
            [AlarmInfo("流程异常/执行器", "工位2上下料-拉料互锁失败：晶圆盒挡杆未打开", AlarmSeverity.Error,
                "1. 检查挡杆驱动气缸状态;\n" +
                "2. 确认挡杆传感器信号;\n" +
                "3. 复位后重新检查;")]
            public const string PullOutLeverNotOpen = "PROC_WS2F_ACT_001";

            /// <summary>寻层扫描移动到起点失败</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-寻层扫描移动到起点失败", AlarmSeverity.Error,
                "1. 检查Z轴是否卡在中途;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string ScanMoveToStartFailed = "PROC_WS2F_MOT_009";

            /// <summary>寻层扫描硬件锁存配置失败</summary>
            [AlarmInfo("流程异常/传感器", "工位2上下料-寻层扫描硬件锁存配置失败", AlarmSeverity.Error,
                "1. 检查运动控制卡连接;\n" +
                "2. 确认传感器接线;\n" +
                "3. 复位后重新运行;")]
            public const string ScanLatchConfigFailed = "PROC_WS2F_SEN_007";

            /// <summary>寻层扫描移动到终点失败</summary>
            [AlarmInfo("流程异常/运动", "工位2上下料-寻层扫描移动到终点失败", AlarmSeverity.Error,
                "1. 检查Z轴是否卡在中途;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string ScanMoveToEndFailed = "PROC_WS2F_MOT_010";

            /// <summary>寻层算法理论层坐标未初始化</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法理论层坐标未初始化", AlarmSeverity.Error,
                "1. 确认已执行切换生产状态;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 复位后重新运行;")]
            public const string AlgorithmNotInitialized = "PROC_WS2F_ALG_005";

            /// <summary>寻层算法传感器原始数据不足</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法传感器原始数据不足", AlarmSeverity.Error,
                "1. 检查传感器信号线连接;\n" +
                "2. 确认料盒位置正确;\n" +
                "3. 复位后重新运行;")]
            public const string AlgorithmRawDataMissing = "PROC_WS2F_ALG_006";

            /// <summary>寻层算法双传感器识别数量差异过大</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法双传感器识别数量差异过大（疑似斜片或传感器失效）", AlarmSeverity.Error,
                "1. 检查左右传感器信号;\n" +
                "2. 确认物料摆放无倾斜;\n" +
                "3. 复位后重新运行;")]
            public const string AlgorithmCountMismatch = "PROC_WS2F_ALG_007";

            /// <summary>寻层算法检测到严重斜片(Cross-slot)</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法检测到严重斜片(Cross-slot)", AlarmSeverity.Error,
                "1. 人工检查料盒内物料状态;\n" +
                "2. 小心处理斜片物料;\n" +
                "3. 复位后重新执行寻层;")]
            public const string AlgorithmCrossSlot = "PROC_WS2F_ALG_008";

            /// <summary>寻层算法检测到重叠片(Double-wafer)</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法检测到重叠片(Double-wafer)", AlarmSeverity.Error,
                "1. 人工检查料盒内物料状态;\n" +
                "2. 小心分离重叠物料;\n" +
                "3. 复位后重新执行寻层;")]
            public const string AlgorithmDoubleWafer = "PROC_WS2F_ALG_009";

            /// <summary>寻层算法晶圆偏离标准槽位</summary>
            [AlarmInfo("流程异常/算法", "工位2上下料-寻层算法晶圆严重偏离标准槽位（可能未插到底）", AlarmSeverity.Error,
                "1. 检查物料是否正确插入槽位;\n" +
                "2. 确认料盒无损坏;\n" +
                "3. 复位后重新执行寻层;")]
            public const string AlgorithmSlotMismatch = "PROC_WS2F_ALG_010";

            /// <summary>断点续跑：重启后实际物料层数与记忆不一致</summary>
            [AlarmInfo("断点续跑", "工位2上下料-重启后物料状态与记忆不一致", AlarmSeverity.Fatal,
                "1. 人工核查料盒内物料数量是否与系统记忆一致;\n" +
                "2. 若物料已被取走，请清空批次后重新下发;\n" +
                "3. 若物料仍在，检查传感器或算法是否异常;\n" +
                "4. 确认状态后手动复位重启;")]
            public const string ResumeConsistencyFailed = "PROC_WS2F_RSM_001";
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

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化拉料流程失败（Y轴运动到待机位失败）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-初始化拉料流程失败（Y轴运动到待机位失败）", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string InitPullingFailed = "PROC_WS1P_MOT_006";

            /// <summary>轨道有物料阻止尺寸切换</summary>
            [AlarmInfo("流程异常/物料", "工位1拉料-轨道有物料，无法执行尺寸切换", AlarmSeverity.Error,
                "1. 清除轨道上的残留物料;\n" +
                "2. 确认轨道无料后复位;")]
            public const string ChangeSizeTrackHasMaterial = "PROC_WS1P_MAT_003";

            /// <summary>尺寸切换气缸IO操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-尺寸切换气缸IO操作失败", AlarmSeverity.Error,
                "1. 检查IO模块输出信号;\n" +
                "2. 确认电磁阀接线;\n" +
                "3. 复位后重新运行;")]
            public const string ChangeSizeCylinderFailed = "PROC_WS1P_ACT_003";

            /// <summary>尺寸切换气缸超时</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-尺寸切换气缸动作超时", AlarmSeverity.Error,
                "1. 检查气源压力是否正常;\n" +
                "2. 确认磁性开关信号;\n" +
                "3. 复位后重新运行;")]
            public const string ChangeSizeCylinderTimeout = "PROC_WS1P_ACT_004";

            /// <summary>夹爪张开气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪张开气缸操作失败", AlarmSeverity.Error,
                "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;")]
            public const string GripperOpenCylinderFailed = "PROC_WS1P_ACT_005";

            /// <summary>夹爪张开超时</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪张开超时，未感应到张开信号", AlarmSeverity.Error,
                "1. 检查气源压力;\n" +
                "2. 确认气缸张开传感器信号;\n" +
                "3. 复位后重新运行;")]
            public const string GripperOpenTimeout = "PROC_WS1P_ACT_006";

            /// <summary>夹爪闭合气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪闭合气缸操作失败", AlarmSeverity.Error,
                "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;")]
            public const string GripperCloseCylinderFailed = "PROC_WS1P_ACT_007";

            /// <summary>夹爪闭合超时</summary>
            [AlarmInfo("流程异常/执行器", "工位1拉料-夹爪闭合超时，未感应到闭合信号", AlarmSeverity.Error,
                "1. 检查气源压力;\n" +
                "2. 确认气缸闭合传感器信号;\n" +
                "3. 复位后重新运行;")]
            public const string GripperCloseTimeout = "PROC_WS1P_ACT_008";

            /// <summary>夹爪闭合后未检测到铁环</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-夹爪闭合后未检测到铁环（空夹）", AlarmSeverity.Error,
                "1. 确认晶圆铁环是否在正确位置;\n" +
                "2. 检查铁环检测传感器;\n" +
                "3. 复位后重新运行;")]
            public const string GripperCloseNoRing = "PROC_WS1P_SEN_001";

            /// <summary>移动到待机位失败（带余料防呆）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到待机位失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string MoveInitialFailed = "PROC_WS1P_MOT_007";

            /// <summary>待机位检测到残留物料</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-待机位检测到残留物料", AlarmSeverity.Error,
                "1. 人工确认夹爪内是否有残留物料;\n" +
                "2. 清除残留物料后复位;")]
            public const string MoveInitialResidualMaterial = "PROC_WS1P_SEN_002";

            /// <summary>移动到待机位失败（无检测模式）</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到待机位失败（强制复位）", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string MoveInitialNoScanFailed = "PROC_WS1P_MOT_008";

            /// <summary>移动到取出安全位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到取出安全位置失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string PutOverMoveFailed = "PROC_WS1P_MOT_009";

            /// <summary>卸料后物料粘连未脱落</summary>
            [AlarmInfo("流程异常/传感器", "工位1拉料-卸料后夹爪物料粘连未脱落", AlarmSeverity.Error,
                "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 复位后重新运行;")]
            public const string PutOverMaterialStuck = "PROC_WS1P_SEN_003";

            /// <summary>移动到取料位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-移动到取料位置失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string InitialMoveFeedingFailed = "PROC_WS1P_MOT_010";

            /// <summary>拉出运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出运动触发失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;")]
            public const string PullOutTriggerFailed = "PROC_WS1P_MOT_011";

            /// <summary>拉出过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出过程卡料报警，已紧急停止", AlarmSeverity.Fatal,
                "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;")]
            public const string PullOutJamAlarm = "PROC_WS1P_MOT_012";

            /// <summary>拉出过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-拉出过程丢料报警，已紧急停止", AlarmSeverity.Fatal,
                "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;")]
            public const string PullOutDropAlarm = "PROC_WS1P_MOT_013";

            /// <summary>拉出运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-Y轴拉出运动超时", AlarmSeverity.Error,
                "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string PullOutTimeout = "PROC_WS1P_MOT_014";

            /// <summary>送入运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入运动触发失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;")]
            public const string PushBackTriggerFailed = "PROC_WS1P_MOT_015";

            /// <summary>送入过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入过程卡料报警，已紧急刹停", AlarmSeverity.Fatal,
                "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;")]
            public const string PushBackJamAlarm = "PROC_WS1P_MOT_016";

            /// <summary>送入过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入过程丢料报警，已紧急刹停", AlarmSeverity.Fatal,
                "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;")]
            public const string PushBackDropAlarm = "PROC_WS1P_MOT_017";

            /// <summary>送入运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位1拉料-送入运动超时", AlarmSeverity.Error,
                "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string PushBackTimeout = "PROC_WS1P_MOT_018";

            /// <summary>扫码失败</summary>
            [AlarmInfo("流程异常/相机", "工位1拉料-扫码失败或校验不合法", AlarmSeverity.Error,
                "1. 检查扫码枪连接;\n" +
                "2. 确认光源亮度;\n" +
                "3. 清洁扫码枪镜头;\n" +
                "4. 复位后重新运行;")]
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

            // ── 模组内部方法级错误码 ──

            /// <summary>初始化拉料流程失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-初始化拉料流程失败（Y轴运动到待机位失败）", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string InitPullingFailed = "PROC_WS2P_MOT_006";

            /// <summary>轨道有物料阻止尺寸切换</summary>
            [AlarmInfo("流程异常/物料", "工位2拉料-轨道有物料，无法执行尺寸切换", AlarmSeverity.Error,
                "1. 清除轨道上的残留物料;\n" +
                "2. 确认轨道无料后复位;")]
            public const string ChangeSizeTrackHasMaterial = "PROC_WS2P_MAT_003";

            /// <summary>尺寸切换气缸IO操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-尺寸切换气缸IO操作失败", AlarmSeverity.Error,
                "1. 检查IO模块输出信号;\n" +
                "2. 确认电磁阀接线;\n" +
                "3. 复位后重新运行;")]
            public const string ChangeSizeCylinderFailed = "PROC_WS2P_ACT_003";

            /// <summary>尺寸切换气缸超时</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-尺寸切换气缸动作超时", AlarmSeverity.Error,
                "1. 检查气源压力是否正常;\n" +
                "2. 确认磁性开关信号;\n" +
                "3. 复位后重新运行;")]
            public const string ChangeSizeCylinderTimeout = "PROC_WS2P_ACT_004";

            /// <summary>夹爪张开气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪张开气缸操作失败", AlarmSeverity.Error,
                "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;")]
            public const string GripperOpenCylinderFailed = "PROC_WS2P_ACT_005";

            /// <summary>夹爪张开超时</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪张开超时，未感应到张开信号", AlarmSeverity.Error,
                "1. 检查气源压力;\n" +
                "2. 确认气缸张开传感器信号;\n" +
                "3. 复位后重新运行;")]
            public const string GripperOpenTimeout = "PROC_WS2P_ACT_006";

            /// <summary>夹爪闭合气缸操作失败</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪闭合气缸操作失败", AlarmSeverity.Error,
                "1. 检查IO模块输出信号;\n" +
                "2. 确认气缸接线;\n" +
                "3. 复位后重新运行;")]
            public const string GripperCloseCylinderFailed = "PROC_WS2P_ACT_007";

            /// <summary>夹爪闭合超时</summary>
            [AlarmInfo("流程异常/执行器", "工位2拉料-夹爪闭合超时，未感应到闭合信号", AlarmSeverity.Error,
                "1. 检查气源压力;\n" +
                "2. 确认气缸闭合传感器信号;\n" +
                "3. 复位后重新运行;")]
            public const string GripperCloseTimeout = "PROC_WS2P_ACT_008";

            /// <summary>夹爪闭合后未检测到铁环</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-夹爪闭合后未检测到铁环（空夹）", AlarmSeverity.Error,
                "1. 确认晶圆铁环是否在正确位置;\n" +
                "2. 检查铁环检测传感器;\n" +
                "3. 复位后重新运行;")]
            public const string GripperCloseNoRing = "PROC_WS2P_SEN_001";

            /// <summary>移动到待机位失败（带余料防呆）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到待机位失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string MoveInitialFailed = "PROC_WS2P_MOT_007";

            /// <summary>待机位检测到残留物料</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-待机位检测到残留物料", AlarmSeverity.Error,
                "1. 人工确认夹爪内是否有残留物料;\n" +
                "2. 清除残留物料后复位;")]
            public const string MoveInitialResidualMaterial = "PROC_WS2P_SEN_002";

            /// <summary>移动到待机位失败（无检测模式）</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到待机位失败（强制复位）", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string MoveInitialNoScanFailed = "PROC_WS2P_MOT_008";

            /// <summary>移动到取出安全位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到取出安全位置失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string PutOverMoveFailed = "PROC_WS2P_MOT_009";

            /// <summary>卸料后物料粘连未脱落</summary>
            [AlarmInfo("流程异常/传感器", "工位2拉料-卸料后夹爪物料粘连未脱落", AlarmSeverity.Error,
                "1. 人工排查夹爪是否粘连带料;\n" +
                "2. 小心取下残留物料;\n" +
                "3. 复位后重新运行;")]
            public const string PutOverMaterialStuck = "PROC_WS2P_SEN_003";

            /// <summary>移动到取料位置失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-移动到取料位置失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string InitialMoveFeedingFailed = "PROC_WS2P_MOT_010";

            /// <summary>拉出运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出运动触发失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;")]
            public const string PullOutTriggerFailed = "PROC_WS2P_MOT_011";

            /// <summary>拉出过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出过程卡料报警，已紧急停止", AlarmSeverity.Fatal,
                "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;")]
            public const string PullOutJamAlarm = "PROC_WS2P_MOT_012";

            /// <summary>拉出过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-拉出过程丢料报警，已紧急停止", AlarmSeverity.Fatal,
                "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;")]
            public const string PullOutDropAlarm = "PROC_WS2P_MOT_013";

            /// <summary>拉出运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-Y轴拉出运动超时", AlarmSeverity.Error,
                "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string PullOutTimeout = "PROC_WS2P_MOT_014";

            /// <summary>送入运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入运动触发失败", AlarmSeverity.Error,
                "1. 检查Y轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;")]
            public const string PushBackTriggerFailed = "PROC_WS2P_MOT_015";

            /// <summary>送入过程卡料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入过程卡料报警，已紧急刹停", AlarmSeverity.Fatal,
                "1. 人工检查是否有物料卡阻;\n" +
                "2. 确认轨道无异物;\n" +
                "3. 处理后复位;")]
            public const string PushBackJamAlarm = "PROC_WS2P_MOT_016";

            /// <summary>送入过程丢料报警</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入过程丢料报警，已紧急刹停", AlarmSeverity.Fatal,
                "1. 人工检查物料是否脱落;\n" +
                "2. 小心回收脱落的物料;\n" +
                "3. 处理后复位;")]
            public const string PushBackDropAlarm = "PROC_WS2P_MOT_017";

            /// <summary>送入运动超时</summary>
            [AlarmInfo("流程异常/运动", "工位2拉料-送入运动超时", AlarmSeverity.Error,
                "1. 检查Y轴是否卡在中途;\n" +
                "2. 手动点动Y轴确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string PushBackTimeout = "PROC_WS2P_MOT_018";

            /// <summary>扫码失败</summary>
            [AlarmInfo("流程异常/相机", "工位2拉料-扫码失败或校验不合法", AlarmSeverity.Error,
                "1. 检查扫码枪连接;\n" +
                "2. 确认光源亮度;\n" +
                "3. 清洁扫码枪镜头;\n" +
                "4. 复位后重新运行;")]
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

         

            // ── 模组内部方法级错误码 ──

            /// <summary>移动到待机位失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到待机位失败（Z轴或XY轴运动失败）", AlarmSeverity.Error,
                "1. 检查XYZ轴伺服是否报警;\n" +
                "2. 手动点动确认各轴运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string MoveInitialFailed = "PROC_DET_MOT_003";

            /// <summary>Z轴安全位移动失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-Z轴移动到安全位置失败", AlarmSeverity.Error,
                "1. 检查Z轴伺服是否报警;\n" +
                "2. 手动点动Z轴确认运动正常;\n" +
                "3. 复位后重新运行;")]
            public const string MoveZSafePosFailed = "PROC_DET_MOT_004";

            /// <summary>工位1配方为空，无法定位</summary>
            [AlarmInfo("流程异常/数据", "OCR检测-工位1配方为空，无法获取目标坐标", AlarmSeverity.Error,
                "1. 确认工位1配方已正确下发;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 重新下发配方后复位;")]
            public const string MoveToStation1RecipeNull = "PROC_DET_DATA_002";

            /// <summary>移动到工位1轴运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位1轴运动触发失败", AlarmSeverity.Error,
                "1. 检查XYZ轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;")]
            public const string MoveToStation1MoveFailed = "PROC_DET_MOT_005";

            /// <summary>工位1 OCR相机配方切换失败</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-切换到工位1的OCR配方失败", AlarmSeverity.Error,
                "1. 检查相机通讯连接;\n" +
                "2. 确认配方名称是否正确;\n" +
                "3. 复位后重新运行;")]
            public const string MoveToStation1RecipeSwitchFailed = "PROC_DET_CAM_002";

            /// <summary>移动到工位1轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位1 XYZ轴运动超时", AlarmSeverity.Error,
                "1. 检查各轴是否卡在中途;\n" +
                "2. 手动点动确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string MoveToStation1MoveTimeout = "PROC_DET_MOT_006";

            /// <summary>工位2配方为空，无法定位</summary>
            [AlarmInfo("流程异常/数据", "OCR检测-工位2配方为空，无法获取目标坐标", AlarmSeverity.Error,
                "1. 确认工位2配方已正确下发;\n" +
                "2. 检查配方参数是否完整;\n" +
                "3. 重新下发配方后复位;")]
            public const string MoveToStation2RecipeNull = "PROC_DET_DATA_003";

            /// <summary>移动到工位2轴运动触发失败</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位2轴运动触发失败", AlarmSeverity.Error,
                "1. 检查XYZ轴伺服是否报警;\n" +
                "2. 确认运动控制卡连接;\n" +
                "3. 复位后重新运行;")]
            public const string MoveToStation2MoveFailed = "PROC_DET_MOT_007";

            /// <summary>工位2 OCR相机配方切换失败</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-切换到工位2的OCR配方失败", AlarmSeverity.Error,
                "1. 检查相机通讯连接;\n" +
                "2. 确认配方名称是否正确;\n" +
                "3. 复位后重新运行;")]
            public const string MoveToStation2RecipeSwitchFailed = "PROC_DET_CAM_003";

            /// <summary>移动到工位2轴运动超时</summary>
            [AlarmInfo("流程异常/运动", "OCR检测-移动到工位2 XYZ轴运动超时", AlarmSeverity.Error,
                "1. 检查各轴是否卡在中途;\n" +
                "2. 手动点动确认运动正常;\n" +
                "3. 检查运动参数;\n" +
                "4. 复位后重新运行;")]
            public const string MoveToStation2MoveTimeout = "PROC_DET_MOT_008";

            /// <summary>相机拍照触发失败</summary>
            [AlarmInfo("流程异常/相机", "OCR检测-相机拍照触发失败或通讯异常", AlarmSeverity.Error,
                "1. 检查相机连接状态;\n" +
                "2. 确认相机触发参数;\n" +
                "3. 手动触发相机确认功能;\n" +
                "4. 复位后重新运行;")]
            public const string CameraTiggerFailed = "PROC_DET_CAM_004";
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
            [AlarmInfo("流程异常/数据", "数据模组-MES查询失败", AlarmSeverity.Error,
                "1. 检查MES服务器连接状态;\n" +
                "2. 确认网络配置正确;\n" +
                "3. 检查MES接口参数;\n" +
                "4. 联系MES维护人员;")]
            public const string MesQueryFailed = "PROC_DATA_MES_001";

            /// <summary>配方更新失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-配方更新失败", AlarmSeverity.Error,
                "1. 检查配方数据格式;\n" +
                "2. 确认配方版本兼容性;\n" +
                "3. 检查存储空间;\n" +
                "4. 重新下发配方;")]
            public const string RecipeUpdateFailed = "PROC_DATA_REC_001";

            /// <summary>OCR校验失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-OCR校验失败", AlarmSeverity.Error,
                "1. 检查OCR识别结果;\n" +
                "2. 确认校验规则配置;\n" +
                "3. 调整OCR参数后重试;")]
            public const string OcrValidationFailed = "PROC_DATA_OCR_001";

            /// <summary>数据持久化失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-数据持久化失败", AlarmSeverity.Error,
                "1. 检查数据库连接;\n" +
                "2. 确认磁盘空间充足;\n" +
                "3. 检查文件读写权限;\n" +
                "4. 重启数据服务;")]
            public const string DataPersistenceFailed = "PROC_DATA_DB_001";

            /// <summary>批次数据不完整</summary>
            [AlarmInfo("流程异常/数据", "数据模组-批次数据不完整", AlarmSeverity.Error,
                "1. 检查MES下发的批次数据;\n" +
                "2. 确认所有必填字段已填充;\n" +
                "3. 重新请求批次数据;")]
            public const string BatchDataIncomplete = "PROC_DATA_BAT_001";

            /// <summary>MES批次信息更新失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-MES批次信息更新失败", AlarmSeverity.Error,
                "1. 检查工位标识是否合法;\n" +
                "2. 确认MES数据格式正确;\n" +
                "3. 重新尝试切换批次;")]
            public const string MesInfoUpdateFailed = "PROC_DATA_MES_002";

            /// <summary>条码校验失败</summary>
            [AlarmInfo("流程异常/数据", "数据模组-条码校验失败", AlarmSeverity.Error,
                "1. 检查扫码枪读取结果;\n" +
                "2. 确认条码格式与配方规则匹配;\n" +
                "3. 核对MES下发的客户批次名单;")]
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
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-初始化失败", AlarmSeverity.Error,
                "1. 检查通讯板卡连接;\n" +
                "2. 确认IP地址配置;\n" +
                "3. 检查端口占用情况;\n" +
                "4. 重启通讯服务;")]
            public const string InitializationFailed = "PROC_SECS_INIT_001";

            /// <summary>协议处理失败</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-协议处理失败", AlarmSeverity.Error,
                "1. 检查消息格式;\n" +
                "2. 确认协议版本兼容性;\n" +
                "3. 查看通讯日志;\n" +
                "4. 重新建立连接;")]
            public const string ProtocolProcessingFailed = "PROC_SECS_PROT_001";

            /// <summary>消息发送失败</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-消息发送失败", AlarmSeverity.Error,
                "1. 检查网络连接;\n" +
                "2. 确认目标主机可达;\n" +
                "3. 检查防火墙设置;\n" +
                "4. 重试发送消息;")]
            public const string MessageSendFailed = "PROC_SECS_SEND_001";

            /// <summary>消息接收超时</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-消息接收超时", AlarmSeverity.Error,
                "1. 检查主机响应状态;\n" +
                "2. 确认消息处理逻辑;\n" +
                "3. 调整超时时间;\n" +
                "4. 重新发送请求;")]
            public const string MessageReceiveTimeout = "PROC_SECS_RECV_001";

            /// <summary>连接断开</summary>
            [AlarmInfo("流程异常/通讯", "SECS/GEM模组-连接断开", AlarmSeverity.Error,
                "1. 检查物理连接;\n" +
                "2. 确认主机状态;\n" +
                "3. 自动重连或手动复位;")]
            public const string ConnectionLost = "PROC_SECS_CONN_001";
        }
    }
}
