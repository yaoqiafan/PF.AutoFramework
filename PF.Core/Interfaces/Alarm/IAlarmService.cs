using PF.Core.Enums;
using PF.Core.Models;
using System;

namespace PF.Core.Interfaces.Alarm
{
    /// <summary>
    /// 报警业务服务：提供报警触发、清除、历史查询能力。
    /// 所有 <c>TriggerAlarm</c> 调用的 <paramref name="errorCode"/> 参数
    /// 必须引用 <see cref="PF.Core.Constants.AlarmCodes"/> 中的常量，严禁魔法字符串。
    /// </summary>
    public interface IAlarmService : IAsyncDisposable
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
        ///   <item>以 (source, errorCode) 复合键幂等处理：相同复合键已存在时直接跳过，不重复落盘。</item>
        ///   <item>同一工站可同时持有多个不同 errorCode 的活跃报警，互不覆盖。</item>
        ///   <item>errorCode 不在字典中时，自动生成通用兜底记录确保故障不被吞噬。</item>
        /// </list>
        /// </summary>
        /// <param name="source">来源标识，建议使用机构名或工站名</param>
        /// <param name="errorCode">报警代码，必须引用 <c>AlarmCodes.*</c> 常量</param>
        /// <returns><c>true</c> 表示本次为首次触发（已记录）；<c>false</c> 表示复合键已存在被幂等跳过。</returns>
        bool TriggerAlarm(string source, string errorCode);

        /// <summary>
        /// 触发报警（携带运行时消息）。
        /// <paramref name="runtimeMessage"/> 非空时覆盖活跃报警的 Message 字段（如 "X轴位置偏差 3.5mm"），
        /// 运行时消息随报警历史一并持久化，历史查询时优先显示。
        /// </summary>
        /// <returns><c>true</c> 表示本次为首次触发（已记录）；<c>false</c> 表示复合键已存在被幂等跳过。</returns>
        bool TriggerAlarm(string source, string errorCode, string? runtimeMessage);

        /// <summary>
        /// 清除指定来源的所有活跃报警（人工清除语义：每条都会发出硬件复位请求，
        /// 主控收到后对处于报警态的同名工站执行局部复位）。
        /// </summary>
        void ClearAlarm(string source);

        /// <summary>精确清除指定来源下单条报警（按复合键匹配；人工清除语义，同上会发出硬件复位请求）</summary>
        void ClearAlarm(string source, string errorCode);

        /// <summary>
        /// 程序自动清除（硬件自恢复、安全门关闭、监控恢复等）：只撤销报警显示与活跃记录，
        /// <b>不发出硬件复位请求</b>——设备自己恢复了不代表可以无人值守地让工站执行复位动作，仍需操作员复位。
        /// <paramref name="errorCode"/> 为 null 时清除该来源全部报警。
        /// <para>默认实现退化为 <see cref="ClearAlarm(string)"/> / <see cref="ClearAlarm(string, string)"/>，兼容自行实现本接口的项目。</para>
        /// </summary>
        void ClearAlarmAutomatically(string source, string? errorCode = null)
        {
            if (errorCode == null) ClearAlarm(source);
            else ClearAlarm(source, errorCode);
        }

        /// <summary>一键清除所有活跃报警（复位成功后调用；系统已复位，不再逐条发出硬件复位请求）</summary>
        void ClearAllActiveAlarms();

        // ── 历史查询 ───────────────────────────────────────────────────────

        /// <summary>
        /// 查询历史报警记录（自动路由到年度分表，内存层多条件过滤）。
        /// </summary>
        /// <param name="year">查询年份，0 = 当前年</param>
        /// <param name="startTime">触发时间下限，null = 不过滤</param>
        /// <param name="endTime">触发时间上限，null = 不过滤</param>
        /// <param name="category">精确分类，null = 不过滤</param>
        /// <param name="severity">精确等级，null = 不过滤</param>
        /// <param name="source">精确来源，null = 不过滤</param>
        /// <param name="errorCode">代码包含匹配，null = 不过滤</param>
        /// <param name="descriptionKeyword">描述模糊匹配，null = 不过滤</param>
        /// <param name="pageSize">DB 层预取上限</param>
        /// <param name="page">DB 层分页页码（从 0 开始）</param>
        Task<IReadOnlyList<AlarmRecord>> QueryHistoricalAlarmsAsync(
            int year = 0,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string? category = null,
            AlarmSeverity? severity = null,
            string? source = null,
            string? errorCode = null,
            string? descriptionKeyword = null,
            int pageSize = 5000,
            int page = 0);
    }
}
