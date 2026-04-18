using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace PF.Application.Shell.Services
{
    /// <summary>
    /// 空闲超时监控服务。
    /// 在应用程序级别监听鼠标/键盘输入事件，若连续无操作时间超过设定阈值，
    /// 则触发 <see cref="IdleTimeout"/> 事件。
    /// </summary>
    public sealed class IdleMonitorService : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private bool _disposed;

        /// <summary>当无操作时长达到阈值时触发。</summary>
        public event EventHandler? IdleTimeout;

        /// <param name="timeout">空闲超时时长，默认 60 秒。</param>
        public IdleMonitorService(TimeSpan timeout)
        {
            _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = timeout
            };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>开始监听输入并启动倒计时。</summary>
        public void Start()
        {
            InputManager.Current.PreProcessInput += OnInputActivity;
            _timer.Start();
        }

        /// <summary>停止监听并重置倒计时（用户注销或权限已为 Operator 时调用）。</summary>
        public void Stop()
        {
            _timer.Stop();
            InputManager.Current.PreProcessInput -= OnInputActivity;
        }

        // 有任何鼠标或键盘事件 → 重置计时器
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

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _timer.Tick -= OnTimerTick;
        }
    }
}
