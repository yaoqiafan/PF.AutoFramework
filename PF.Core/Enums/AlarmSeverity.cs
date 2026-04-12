namespace PF.Core.Enums
{
    /// <summary>
    /// 报警严重程度枚举，从低到高排列
    /// </summary>
    public enum AlarmSeverity
    {
        /// <summary>信息：用于记录正常事件，无需操作员干预</summary>
        Information = 0,

        /// <summary>警告：异常情况，但设备仍可运行，建议尽快处理</summary>
        Warning = 1,

        /// <summary>错误：设备局部功能失常，影响生产，需要处理</summary>
        Error = 2,

        /// <summary>致命：设备无法继续运行，必须立即处理才可复位</summary>
        Fatal = 3
    }
}
