namespace PF.Core.Enums
{
    /// <summary><see cref="UserLevel"/> 扩展方法</summary>
    public static class UserLevelExtensions
    {
        /// <summary>中文等级名（与 Identity 模块 UserLevelToDisplayConverter 一致），用于提示文本与日志</summary>
        public static string ToDisplayName(this UserLevel level) => level switch
        {
            UserLevel.Operator      => "操作员",
            UserLevel.Engineer      => "工程师",
            UserLevel.Administrator => "管理员",
            UserLevel.SuperUser     => "超级用户",
            _                       => "未登录"
        };
    }
}
