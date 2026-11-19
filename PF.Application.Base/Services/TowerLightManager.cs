using PF.Core.Enums;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.TowerLight;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Application.Base.Services
{
    /// <summary>
    /// 三色灯管理器：订阅 MachineStateChangedEvent，按工业标准将机台状态映射为灯光组合
    /// </summary>
    public class TowerLightManager
    {
        private readonly ITowerLightService _towerLight;

        private static readonly Dictionary<MachineState, IReadOnlyDictionary<LightColor, LightState>> StateMap = new()
        {
            {
                MachineState.Uninitialized,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Off }, { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Initializing,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Off }, { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Idle,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Off }, { LightColor.Yellow, LightState.On },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Running,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Off }, { LightColor.Yellow, LightState.Off },
                    { LightColor.Green, LightState.On }, { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Paused,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Off }, { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.InitAlarm,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Blinking }, { LightColor.Yellow, LightState.Off },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Blinking }
                }
            },
            {
                MachineState.RunAlarm,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Blinking }, { LightColor.Yellow, LightState.Off },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Blinking }
                }
            },
            {
                MachineState.Resetting,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red, LightState.Off }, { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green, LightState.Off }, { LightColor.Buzzer, LightState.Off }
                }
            }
        };

        private readonly IMasterController _masterController;
        private readonly object _applyLock = new();

        /// <summary>构造并订阅 <see cref="MachineStateChangedEvent"/>，状态变化时自动更新三色灯。</summary>
        public TowerLightManager(ITowerLightService towerLight, IEventAggregator eventAggregator, IMasterController masterController)
        {
            _towerLight = towerLight;
            _masterController = masterController;
            eventAggregator.GetEvent<MachineStateChangedEvent>()
                .Subscribe(OnMachineStateChanged, ThreadOption.BackgroundThread, keepSubscriberReferenceAlive: true);
        }

        /// <summary>
        /// 不用事件里携带的状态，而是在锁内读主控的当前状态。
        /// <para>状态事件先经主控 Task.Run、再经 Prism BackgroundThread 两次派发到线程池，先后顺序没有保证。
        /// 按事件参数设灯时，Running → RunAlarm 快速相继发生，"Running"那条可能后到，
        /// 结果机台在报警、灯却是绿灯常亮、蜂鸣器不响。改为读当前状态后，最后一个执行的回调
        /// 必然晚于最后一次状态切换，读到的就是最终状态，灯不会停在过期的状态上。</para>
        /// </summary>
        private void OnMachineStateChanged(MachineState _)
        {
            lock (_applyLock)
            {
                if (StateMap.TryGetValue(_masterController.CurrentState, out var pattern))
                    _towerLight.SetLights(pattern);
            }
        }
    }
}
