using PF.Core.Enums;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 报警信息特性，标注在 AlarmCodes 常量字段上，用于反射加载预设报警字典。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class AlarmInfoAttribute : Attribute
    {
        /// <summary>报警分类（如 "硬件异常"、"系统异常"、"工艺异常"）</summary>
        public string Category { get; }

        /// <summary>报警描述文本，用于日志和 UI 展示</summary>
        public string Message { get; }

        /// <summary>报警严重程度</summary>
        public AlarmSeverity Severity { get; }

        /// <summary>排故 SOP 指导文本，操作员可在报警中心查看逐步处理方案</summary>
        public string Solution { get; }

        /// <summary>图片路径，如果有</summary>
        public string? ImagePath { get; }

        /// <param name="category">分类</param>
        /// <param name="message">描述</param>
        /// <param name="severity">严重程度</param>
        /// <param name="solution">排故指导 SOP</param>
        public AlarmInfoAttribute(string category, string message, AlarmSeverity severity, string solution, string? imagePath=null)
        {
            Category = category;
            Message  = message;
            Severity = severity;
            Solution = solution;
            ImagePath = imagePath;
        }
    }
}
