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

        /// <summary>报警描述文本，用于日志和 UI 展示（中文）</summary>
        public string Message { get; }

        /// <summary>报警描述文本英文，用于 SECS/GEM 上传（最长 35 字符）</summary>
        public string MessageEn { get; }

        /// <summary>报警严重程度</summary>
        public AlarmSeverity Severity { get; }

        /// <summary>排故 SOP 指导文本，操作员可在报警中心查看逐步处理方案</summary>
        public string Solution { get; }

        /// <summary>图片路径，如果有</summary>
        public string? ImagePath { get; }


        /// <summary>
        /// 报警信息ID(用于上传SECSGEM)，由开发人员定义，需确保唯一性和稳定性（不随代码变动而改变）。
        /// </summary>
        public int MessageID { get; }


        /// <summary>
        /// 报警信息英文文本(用于上传SECSGEM)，最长40 位。
        /// 当前实现中直接填写英文报警信息，与 <see cref="MessageEn"/> 保持一致。
        /// </summary>
        public string MessageIDHex { get; }

        /// <param name="category">分类</param>
        /// <param name="message">描述（中文）</param>
        /// <param name="messageEn">描述（英文，最长 35 字符）</param>
        /// <param name="severity">严重程度</param>
        /// <param name="solution">排故指导 SOP</param>
        /// <param name="messageID">报警信息ID</param>
        /// <param name="messageIDHex">报警信息英文文本</param>
        /// <param name="imagePath">图片路径</param>
        public AlarmInfoAttribute(string category, string message, string messageEn, AlarmSeverity severity, string solution, int messageID, string messageIDHex, string? imagePath=null)
        {
            Category = category;
            Message  = message;
            MessageEn = messageEn;
            Severity = severity;
            Solution = solution;
            ImagePath = imagePath;
            MessageID = messageID;
            MessageIDHex = messageIDHex;
        }
    }
}
