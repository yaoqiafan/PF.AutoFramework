using System;

namespace PF.Services.Timer.Internal
{
    internal sealed class IntervalRegistration
    {
        public int    IntervalMs { get; }
        public Action Callback   { get; }
        public DateTime NextFire { get; set; }

        public IntervalRegistration(int intervalMs, Action callback)
        {
            IntervalMs = intervalMs;
            Callback   = callback;
            NextFire   = DateTime.Now.AddMilliseconds(intervalMs);
        }
    }
}
