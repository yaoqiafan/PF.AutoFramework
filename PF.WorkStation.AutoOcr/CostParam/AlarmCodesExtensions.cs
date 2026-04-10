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
        }
    }
}
