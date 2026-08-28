using System;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 标记某个操作日志 Key 属于"关键操作"（启停/复位/保存/删除/确认/登录登出/下发指令等会改变状态或
    /// 有留痕价值的动作）：默认启用记录。未标记的字段视为非关键操作（刷新/筛选/切换 Tab 等日常交互），
    /// 默认不启用，需要工程师在项目级 Excel 配置里手动打开。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class OperationLogCriticalAttribute : Attribute
    {
    }
}
