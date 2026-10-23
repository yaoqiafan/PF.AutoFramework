using PF.Core.Enums;
using PF.Core.Models;

namespace PF.Core.Interfaces.Alarm
{
    /// <summary>
    /// 报警清除/复位的权限判定服务：以当前登录用户等级对照报警码配置的清除等级。
    /// <para>
    /// 只用于<b>人为操作入口</b>（报警中心按钮、报警弹窗、复位按钮、物理复位按钮）。
    /// 程序自动清除（安全门关闭、硬件自恢复等）直接调用 <see cref="IAlarmService"/>，不经过本服务。
    /// </para>
    /// </summary>
    public interface IAlarmPermissionService
    {
        /// <summary>当前登录用户等级（未登录为 <see cref="UserLevel.Null"/>）</summary>
        UserLevel CurrentUserLevel { get; }

        /// <summary>当前登录用户名（未登录为空字符串），用于日志</summary>
        string CurrentUserName { get; }

        /// <summary>手动清除该报警码所需的最低等级</summary>
        UserLevel GetRequiredLevel(string errorCode);

        /// <summary>当前用户是否有权清除该条报警</summary>
        bool CanClear(AlarmRecord record);

        /// <summary>
        /// 复位所需的最低等级 = 当前所有活跃报警中要求最高的等级；无活跃报警时为 <see cref="UserLevel.Operator"/>。
        /// 复位会清除全部活跃报警，所以必须覆盖要求最高的那一条。
        /// </summary>
        UserLevel GetResetRequiredLevel();

        /// <summary>当前用户是否有权复位；无权时 <paramref name="requiredLevel"/> 给出所需等级</summary>
        bool CanReset(out UserLevel requiredLevel);

        /// <summary>
        /// 清除当前用户有权清除的全部活跃报警，跳过权限不足的条目。
        /// </summary>
        /// <returns>(已清除条数, 因权限不足跳过的条数)</returns>
        (int Cleared, int Skipped) ClearAllAuthorized();

        /// <summary>当前用户变化或报警码清除等级配置变化时引发（可能在后台线程），UI 据此刷新按钮可用状态</summary>
        event EventHandler? PermissionChanged;
    }
}
