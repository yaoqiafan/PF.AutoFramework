using System.Windows.Input;
using System.Windows.Threading;

namespace PF.Application.Base.Services
{
    /// <summary>
    /// 空闲超时监控：鼠标/键盘无操作超过阈值后触发 IdleTimeout 事件
    /// </summary>
    public sealed class IdleMonitorService : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        public event EventHandler? IdleTimeout;

        public IdleMonitorService(TimeSpan timeout)
        {
            _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = timeout
            };
            _timer.Tick += OnTimerTick;
        }

        public void Start()
        {
            InputManager.Current.PreProcessInput += OnInputActivity;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            InputManager.Current.PreProcessInput -= OnInputActivity;
        }

        private void OnInputActivity(object sender, PreProcessInputEventArgs e)
        {
            var input = e.StagingItem.Input;
            if (input is MouseEventArgs || input is KeyboardEventArgs || input is StylusEventArgs)
            {
                _timer.Stop();
                _timer.Start();
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            IdleTimeout?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _timer.Tick -= OnTimerTick;
        }
    }
}
