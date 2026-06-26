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

        /// <summary>无操作超时事件，由 UI 线程触发。</summary>
        public event EventHandler? IdleTimeout;

        /// <summary>构造并配置空闲超时时长。</summary>
        public IdleMonitorService(TimeSpan timeout)
        {
            _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = timeout
            };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>启动监控：注册全局输入事件并开始计时。</summary>
        public void Start()
        {
            InputManager.Current.PreProcessInput += OnInputActivity;
            _timer.Start();
        }

        /// <summary>停止监控：取消计时并移除全局输入事件订阅。</summary>
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

        /// <summary>释放定时器资源并停止监控。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _timer.Tick -= OnTimerTick;
        }
    }
}
