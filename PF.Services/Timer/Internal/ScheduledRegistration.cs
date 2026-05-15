using System;

namespace PF.Services.Timer.Internal
{
    internal enum ScheduleType { Daily, Weekly, Monthly }

    internal sealed class ScheduledRegistration
    {
        public string       Key            { get; }
        public ScheduleType Type           { get; }
        public TimeSpan     TimeOfDay      { get; }
        public DayOfWeek?   DayOfWeek      { get; }
        public int?         DayOfMonth     { get; }
        public Action       Callback       { get; }
        public bool         CatchUpOnStart { get; }
        public DateTime     LastFiredTime  { get; set; } = DateTime.MinValue;

        private ScheduledRegistration(
            string key, ScheduleType type, TimeSpan timeOfDay,
            DayOfWeek? dayOfWeek, int? dayOfMonth,
            Action callback, bool catchUpOnStart)
        {
            Key            = key;
            Type           = type;
            TimeOfDay      = timeOfDay;
            DayOfWeek      = dayOfWeek;
            DayOfMonth     = dayOfMonth;
            Callback       = callback;
            CatchUpOnStart = catchUpOnStart;
        }

        public static ScheduledRegistration Daily(
            string key, TimeSpan timeOfDay, Action callback, bool catchUp)
            => new(key, ScheduleType.Daily, timeOfDay, null, null, callback, catchUp);

        public static ScheduledRegistration Weekly(
            string key, DayOfWeek dayOfWeek, TimeSpan timeOfDay, Action callback, bool catchUp)
            => new(key, ScheduleType.Weekly, timeOfDay, dayOfWeek, null, callback, catchUp);

        public static ScheduledRegistration Monthly(
            string key, int dayOfMonth, TimeSpan timeOfDay, Action callback, bool catchUp)
            => new(key, ScheduleType.Monthly, timeOfDay, null, dayOfMonth, callback, catchUp);

        /// <summary>
        /// 判断当前时刻是否满足触发条件（时刻已到 + 周期类型匹配 + 今天尚未触发）。
        /// </summary>
        public bool ShouldFire(DateTime now)
        {
            if (now.Date <= LastFiredTime.Date) return false;
            if (now.TimeOfDay < TimeOfDay)      return false;

            return Type switch
            {
                ScheduleType.Daily   => true,
                ScheduleType.Weekly  => now.DayOfWeek == DayOfWeek!.Value,
                ScheduleType.Monthly => now.Day == DayOfMonth!.Value,
                _                    => false
            };
        }

        /// <summary>
        /// 判断今天的调度时刻是否已过（不考虑是否已触发，用于启动时防误触发初始化）。
        /// </summary>
        public bool WouldFireToday(DateTime now)
        {
            if (now.TimeOfDay < TimeOfDay) return false;

            return Type switch
            {
                ScheduleType.Daily   => true,
                ScheduleType.Weekly  => now.DayOfWeek == DayOfWeek!.Value,
                ScheduleType.Monthly => now.Day == DayOfMonth!.Value,
                _                    => false
            };
        }
    }
}
