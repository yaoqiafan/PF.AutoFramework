using PF.Core.Events;
using PF.Core.Interfaces.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Mechanisms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Mechanisms
{
    public abstract class BaseMechanism : IMechanism, IDisposable
    {
        public string MechanismName { get; }
        public bool IsInitialized { get; protected set; }
        public bool HasAlarm { get; protected set; }

        // 实现接口事件
        public event EventHandler<MechanismAlarmEventArgs> AlarmTriggered;

        protected readonly ILogService _logger;
        private readonly List<IHardwareDevice> _internalHardwares;

        // 构造函数：删除了 IEventAggregator
        protected BaseMechanism(string name, ILogService logger, params IHardwareDevice[] hardwares)
        {
            MechanismName = name;
            _logger = logger;
            _internalHardwares = hardwares?.ToList() ?? new List<IHardwareDevice>();

            // 自动订阅所有注入硬件的报警事件
            foreach (var hw in _internalHardwares)
            {
                if (hw != null)
                {
                    hw.AlarmTriggered += OnHardwareAlarmTriggered;
                }
            }
        }

        /// <summary>
        /// 拦截底层硬件报警，转换为模组报警抛出
        /// </summary>
        private void OnHardwareAlarmTriggered(object sender, DeviceAlarmEventArgs e)
        {
            var device = sender as IHardwareDevice;
            HasAlarm = true;
            IsInitialized = false;

            _logger?.Error($"[模组 {MechanismName}] 内部硬件 [{device?.DeviceName}] 发生报警: {e.ErrorMessage}");

            // 触发标准 C# 事件，通知上层（如状态机或业务控制器）
            AlarmTriggered?.Invoke(this, new MechanismAlarmEventArgs
            {
                MechanismName = this.MechanismName,
                HardwareName = device?.DeviceName ?? "未知硬件",
                ErrorMessage = e.ErrorMessage,
                InternalException = e.InternalException
            });
        }

        public async Task<bool> InitializeAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 开始初始化...");
            HasAlarm = false;

            try
            {
                IsInitialized = await InternalInitializeAsync(token);
                if (IsInitialized)
                    _logger?.Success($"[模组 {MechanismName}] 初始化完成！");
                else
                    _logger?.Warn($"[模组 {MechanismName}] 初始化未成功完成。");

                return IsInitialized;
            }
            catch (Exception ex)
            {
                HasAlarm = true;
                _logger?.Error($"[模组 {MechanismName}] 初始化过程发生异常: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ResetAsync(CancellationToken token = default)
        {
            _logger?.Info($"[模组 {MechanismName}] 正在复位清除报警...");

            bool allResetOk = true;
            foreach (var hw in _internalHardwares)
            {
                if (!await hw.ResetAsync(token))
                {
                    allResetOk = false;
                }
            }

            if (allResetOk)
            {
                allResetOk = await InternalResetAsync(token);
            }

            if (allResetOk) HasAlarm = false;
            return allResetOk;
        }

        public async Task StopAsync()
        {
            _logger?.Warn($"[模组 {MechanismName}] 触发紧急停止！");
            await InternalStopAsync();
        }

        protected abstract Task<bool> InternalInitializeAsync(CancellationToken token);
        protected abstract Task InternalStopAsync();
        protected virtual Task<bool> InternalResetAsync(CancellationToken token) => Task.FromResult(true);

        protected void CheckReady()
        {
            if (HasAlarm) throw new Exception($"模组 [{MechanismName}] 处于报警状态，禁止动作！");
            if (!IsInitialized) throw new Exception($"模组 [{MechanismName}] 未初始化，禁止动作！");
        }

        public virtual void Dispose()
        {
            foreach (var hw in _internalHardwares)
            {
                if (hw != null) hw.AlarmTriggered -= OnHardwareAlarmTriggered;
            }
        }
    }
}
