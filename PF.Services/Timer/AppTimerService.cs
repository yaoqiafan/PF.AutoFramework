using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Timer;
using PF.Services.Timer.Internal;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Services.Timer
{
    /// <summary>
    /// 集中定时服务实现。
    /// <list type="bullet">
    ///   <item>内部使用单个 <see cref="System.Threading.Timer"/>（线程池），基础 tick = 100ms。</item>
    ///   <item>时间边界事件：秒/分/时/天，在对应边界跨越时触发一次。</item>
    ///   <item>间隔注册：<see cref="Register"/> 返回 <see cref="IDisposable"/>，Dispose 即取消。</item>
    ///   <item>定点调度：DailyAt / WeeklyAt / MonthlyAt，末次执行时间持久化到 JSON，支持启动补偿。</item>
    ///   <item>所有回调在线程池触发，更新 UI 属性须自行 Dispatcher.Invoke。</item>
    /// </list>
    /// </summary>
    internal sealed class AppTimerService : IAppTimerService, IDisposable
    {
        private const int BaseIntervalMs = 100;

        private readonly TimerPersistence   _persistence;
        private readonly ILogService?       _logger;
        private readonly object             _lock                  = new();
        private readonly List<IntervalRegistration>   _intervals   = new();
        private readonly List<ScheduledRegistration>  _schedules   = new();

        private System.Threading.Timer? _timer;
        private DateTime _lastTick;
        private bool     _disposed;

        public DateTime CurrentTime { get; private set; } = DateTime.Now;

        public event EventHandler<DateTime>? SecondElapsed;
        public event EventHandler<DateTime>? MinuteElapsed;
        public event EventHandler<DateTime>? HourElapsed;
        public event EventHandler<DateTime>? DayElapsed;

        public AppTimerService(TimerPersistence persistence, ILogService? logger = null)
        {
            _persistence = persistence;
            _logger      = logger;
        }

        // ── 生命周期 ─────────────────────────────────────────────────────

        public void Start()
        {
            if (_timer != null) return;
            _lastTick   = DateTime.Now;
            CurrentTime = _lastTick;
            _timer      = new System.Threading.Timer(OnTick, null, BaseIntervalMs, BaseIntervalMs);
            _logger?.Info("[AppTimerService] 已启动");
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _logger?.Info("[AppTimerService] 已停止");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        // ── 核心 Tick ────────────────────────────────────────────────────

        private void OnTick(object? state)
        {
            var now = DateTime.Now;

            if (now.Second != _lastTick.Second)
            {
                CurrentTime = now;
                SecondElapsed?.Invoke(this, now);
            }

            if (now.Minute != _lastTick.Minute)
            {
                MinuteElapsed?.Invoke(this, now);
                CheckSchedules(now);
            }

            if (now.Hour != _lastTick.Hour) HourElapsed?.Invoke(this, now);
            if (now.Date != _lastTick.Date) DayElapsed?.Invoke(this, now);

            CheckIntervals(now);

            _lastTick = now;
        }

        private void CheckIntervals(DateTime now)
        {
            List<IntervalRegistration> snapshot;
            lock (_lock)
                snapshot = new List<IntervalRegistration>(_intervals);

            foreach (var reg in snapshot)
            {
                if (now < reg.NextFire) continue;
                reg.NextFire = now.AddMilliseconds(reg.IntervalMs);
                FireSafe(reg.Callback, null);
            }
        }

        private void CheckSchedules(DateTime now)
        {
            List<ScheduledRegistration> snapshot;
            lock (_lock)
                snapshot = new List<ScheduledRegistration>(_schedules);

            foreach (var s in snapshot)
            {
                if (!s.ShouldFire(now)) continue;
                s.LastFiredTime = now;
                _persistence.Save(s.Key, now);
                FireSafe(s.Callback, s.Key);
            }
        }

        private void FireSafe(Action callback, string? key)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    _logger?.Error($"[AppTimerService] 调度回调异常{(key != null ? $"（{key}）" : string.Empty)}", exception:ex);
                }
            });
        }

        // ── IDisposable 注册 ─────────────────────────────────────────────

        public IDisposable Register(int intervalMs, Action callback)
        {
            var reg = new IntervalRegistration(intervalMs, callback);
            lock (_lock) _intervals.Add(reg);
            return new DisposableAction(() => { lock (_lock) _intervals.Remove(reg); });
        }

        public IDisposable RegisterDailyAt(string key, TimeSpan timeOfDay, Action callback, bool catchUpOnStart = false)
            => RegisterScheduled(ScheduledRegistration.Daily(key, timeOfDay, callback, catchUpOnStart));

        public IDisposable RegisterWeeklyAt(string key, DayOfWeek dayOfWeek, TimeSpan timeOfDay, Action callback, bool catchUpOnStart = false)
            => RegisterScheduled(ScheduledRegistration.Weekly(key, dayOfWeek, timeOfDay, callback, catchUpOnStart));

        public IDisposable RegisterMonthlyAt(string key, int dayOfMonth, TimeSpan timeOfDay, Action callback, bool catchUpOnStart = false)
            => RegisterScheduled(ScheduledRegistration.Monthly(key, dayOfMonth, timeOfDay, callback, catchUpOnStart));

        private IDisposable RegisterScheduled(ScheduledRegistration schedule)
        {
            schedule.LastFiredTime = _persistence.Load(schedule.Key);
            var now = DateTime.Now;

            if (schedule.CatchUpOnStart && schedule.ShouldFire(now))
            {
                _logger?.Info($"[AppTimerService] 启动补偿触发：{schedule.Key}");
                schedule.LastFiredTime = now;
                _persistence.Save(schedule.Key, now);
                FireSafe(schedule.Callback, schedule.Key);
            }
            else if (!schedule.CatchUpOnStart
                     && schedule.LastFiredTime.Date < DateTime.Today
                     && schedule.WouldFireToday(now))
            {
                // 非补偿模式：今天的时刻已过但持久化记录是旧日期，标记为已执行防止误触发
                schedule.LastFiredTime = DateTime.Today;
            }

            lock (_lock) _schedules.Add(schedule);
            return new DisposableAction(() => { lock (_lock) _schedules.Remove(schedule); });
        }

        // ── 内部辅助 ─────────────────────────────────────────────────────

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action _action;
            private bool _disposed;

            public DisposableAction(Action action) => _action = action;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _action();
            }
        }
    }
}
