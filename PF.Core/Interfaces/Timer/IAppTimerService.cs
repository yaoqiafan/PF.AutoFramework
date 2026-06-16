using System;

namespace PF.Core.Interfaces.Timer
{
    /// <summary>
    /// 集中定时服务。提供时间边界事件和可注销的定点/间隔调度能力。
    /// <para>
    /// 注意：所有事件与回调均在<b>线程池线程</b>触发。
    /// 若需更新 UI 属性，订阅方须自行 <c>Application.Current.Dispatcher.Invoke</c>。
    /// 纯后台操作（调用 Service、写文件等）无需 Dispatch。
    /// </para>
    /// </summary>
    public interface IAppTimerService
    {
        /// <summary>当前本地时间，每整秒更新一次。</summary>
        DateTime CurrentTime { get; }

        // ── 方式一：时间边界事件（跨越边界时触发一次）────────────────────

        /// <summary>每整秒触发。</summary>
        event EventHandler<DateTime> SecondElapsed;

        /// <summary>每分钟 :00 触发。</summary>
        event EventHandler<DateTime> MinuteElapsed;

        /// <summary>每小时 :00:00 触发。</summary>
        event EventHandler<DateTime> HourElapsed;

        /// <summary>每天 00:00:00 触发。</summary>
        event EventHandler<DateTime> DayElapsed;

        // ── 方式二：IDisposable 注册（Dispose 返回值即取消）─────────────

        /// <summary>
        /// 注册自定义毫秒间隔回调。不持久化，不支持补偿。
        /// </summary>
        IDisposable Register(int intervalMs, Action callback);

        /// <summary>
        /// 每天在指定时刻执行一次。
        /// </summary>
        /// <param name="key">调度唯一标识，用于持久化末次执行时间，建议格式：daily_HH:mm_描述。同一 key 跨重启可恢复状态。</param>
        /// <param name="timeOfDay">每天触发的时刻（相对于当天零点的偏移量）。</param>
        /// <param name="callback">到达指定时刻时执行的回调方法。</param>
        /// <param name="catchUpOnStart">
        /// true：启动时若发现当天该时刻已过且未执行，立即补跑一次。<br/>
        /// false：错过则等待下一天，不补跑。
        /// </param>
        IDisposable RegisterDailyAt(string key, TimeSpan timeOfDay, Action callback, bool catchUpOnStart = false);

        /// <summary>
        /// 每周在指定星期+时刻执行一次。
        /// </summary>
        /// <param name="key">调度唯一标识，建议格式：weekly_星期_HH:mm_描述。</param>
        /// <param name="dayOfWeek">每周触发的星期几。</param>
        /// <param name="timeOfDay">触发当天的具体时刻（相对于零点的偏移量）。</param>
        /// <param name="callback">到达指定时刻时执行的回调方法。</param>
        /// <param name="catchUpOnStart">true：启动时若本周应执行时刻已过且未执行，立即补跑一次。</param>
        IDisposable RegisterWeeklyAt(string key, DayOfWeek dayOfWeek, TimeSpan timeOfDay, Action callback, bool catchUpOnStart = false);

        /// <summary>
        /// 每月在指定日+时刻执行一次。若当月不存在该日（如 2 月 31 日），则跳过该月。
        /// </summary>
        /// <param name="key">调度唯一标识，建议格式：monthly_DD_HH:mm_描述。</param>
        /// <param name="dayOfMonth">每月触发的日（1-31），若当月不存在则跳过。</param>
        /// <param name="timeOfDay">触发当天的具体时刻（相对于零点的偏移量）。</param>
        /// <param name="callback">到达指定时刻时执行的回调方法。</param>
        /// <param name="catchUpOnStart">true：启动时若本月应执行时刻已过且未执行，立即补跑一次。</param>
        IDisposable RegisterMonthlyAt(string key, int dayOfMonth, TimeSpan timeOfDay, Action callback, bool catchUpOnStart = false);

        /// <summary>启动定时服务，开始计时与调度。</summary>
        void Start();
        /// <summary>停止定时服务，暂停所有计时与调度。</summary>
        void Stop();
    }
}
