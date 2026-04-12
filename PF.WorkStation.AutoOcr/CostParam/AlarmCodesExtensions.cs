using PF.Core.Attributes;
using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static class Process
        {
            [AlarmInfo("工艺异常", "OCR 识别连续失败，超过阈值", AlarmSeverity.Warning,
                "1. 检查相机焦距是否正确（参数页面调整）;\n" +
                "2. 调整光源亮度配方;\n" +
                "3. 清洁镜头表面;\n" +
                "4. 检查产品定位是否准确;\n" +
                "5. 联系工艺工程师调整识别参数;")]
            public const string OcrFailure = "PRC_OCR_001";

            [AlarmInfo("工艺异常", "批次产品数量与预期不符", AlarmSeverity.Error,
                "1. 手动核对当前产品数量;\n" +
                "2. 检查上料是否符合批次要求;\n" +
                "3. 检查计数传感器状态;\n" +
                "4. 确认无误后手动纠正批次记录并复位;")]
            public const string BatchCountError = "PRC_BCH_001";

            [AlarmInfo("工艺异常", "工站运动超时，轴未到达目标位", AlarmSeverity.Fatal,
                "1. 检查运动轴是否被卡死或碰到异物;\n" +
                "2. 检查限位传感器指示状态;\n" +
                "3. 在调试模式下手动点动轴到安全位置;\n" +
                "4. 确认无机械障碍后执行回原点;\n" +
                "5. 点击【复位】继续生产;")]
            public const string StationMotionTimeout = "PRC_MOT_001";

            [AlarmInfo("工艺异常", "上料工站送料超时", AlarmSeverity.Error,
                "1. 检查料盘是否有料;\n" +
                "2. 检查送料机构是否被卡住;\n" +
                "3. 手动清除卡料后复位;\n" +
                "4. 检查送料气缸传感器状态;")]
            public const string FeedingTimeout = "PRC_FEED_001";

            [AlarmInfo("工艺异常", "拉料工站取料超时", AlarmSeverity.Error,
                "1. 检查拉料机构是否被卡住;\n" +
                "2. 检查真空吸力是否正常;\n" +
                "3. 手动清除异常后复位;\n" +
                "4. 检查真空传感器及气路;")]
            public const string PullingTimeout = "PRC_PULL_001";



            
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
                    "4. 重新下发数据后复位重启;",
                @"C:\Users\12434\source\repos\PF.AutoFramework\PF.UI.Resources\Images\PNG\1.png")]
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
    }
}
