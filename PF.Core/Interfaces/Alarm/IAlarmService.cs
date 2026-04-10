using PF.Core.Enums;
using PF.Core.Models;

namespace PF.Core.Interfaces.Alarm
{
    /// <summary>
    /// 报警业务服务：提供报警触发、清除、历史查询能力。
    /// 所有 <c>TriggerAlarm</c> 调用的 <paramref name="errorCode"/> 参数
    /// 必须引用 <see cref="PF.Core.Constants.AlarmCodes"/> 中的常量，严禁魔法字符串。
    /// </summary>
    public interface IAlarmService
    {
        // ── 活跃报警集合（线程安全快照） ────────────────────────────────────

        /// <summary>当前活跃报警快照（线程安全，可供 UI 初始化绑定）</summary>
        IReadOnlyList<AlarmRecord> ActiveAlarms { get; }

        // ── 事件（UI 层通过此接口订阅，在后台线程触发） ────────────────────

        /// <summary>有新报警触发时引发（含兜底未知报警）</summary>
        event EventHandler<AlarmRecord> AlarmTriggered;

        /// <summary>报警被清除时引发</summary>
        event EventHandler<AlarmRecord> AlarmCleared;

        // ── 触发与清除 ─────────────────────────────────────────────────────

        /// <summary>
        /// 触发报警。
        /// <list type="bullet">
        ///   <item>同一 source 在 2 秒内重复触发相同 errorCode 时，仅更新时间戳，不重复落盘。</item>
        ///   <item>errorCode 不在字典中时，自动生成通用兜底记录确保故障不被吞噬。</item>
        /// </list>
        /// </summary>
        /// <param name="source">来源标识，建议使用机构名或工站名</param>
        /// <param name="errorCode">报警代码，必须引用 <c>AlarmCodes.*</c> 常量</param>
        void TriggerAlarm(string source, string errorCode);

        /// <summary>清除指定来源的活跃报警</summary>
        void ClearAlarm(string source);

        /// <summary>一键清除所有活跃报警（关联【复位】按钮）</summary>
        void ClearAllActiveAlarms();

        // ── 历史查询 ───────────────────────────────────────────────────────

        /// <summary>
        /// 分页查询历史报警记录（自动跨年路由到对应分表）。
        /// </summary>
        /// <param name="year">查询年份，0 = 当前年</param>
        /// <param name="category">按分类过滤，null = 不过滤</param>
        /// <param name="minSeverity">最低严重程度过滤，null = 不过滤</param>
        /// <param name="pageSize">每页条数</param>
        /// <param name="page">页码（从 0 开始）</param>
        Task<IReadOnlyList<AlarmRecord>> QueryHistoricalAlarmsAsync(
            int year = 0,
            string? category = null,
            AlarmSeverity? minSeverity = null,
            int pageSize = 100,
            int page = 0);
    }
}
