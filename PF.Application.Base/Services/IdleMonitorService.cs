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

        private bool _running;

        /// <summary>
        /// 启动监控：注册全局输入事件并开始计时。已在运行时只重新计时。
        /// <para>必须幂等：用户从工程师直接切到管理员等连续登录会重复调用 Start。此前每次都追加一份输入订阅，
        /// Stop 只摘掉一份，降回操作员后残留的订阅一有输入就把计时器重新拉起，
        /// 于是操作员停手 NoUseTime 秒后当前页面被自动关掉，且每次都记一条"无操作超时"。</para>
        /// </summary>
        public void Start()
        {
            if (_disposed) return;
            if (!_running)
            {
                InputManager.Current.PreProcessInput += OnInputActivity;
                _running = true;
            }
            _timer.Stop();
            _timer.Start();
        }

        /// <summary>停止监控：取消计时并移除全局输入事件订阅。</summary>
        public void Stop()
        {
            _timer.Stop();
            if (!_running) return;
            InputManager.Current.PreProcessInput -= OnInputActivity;
            _running = false;
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
