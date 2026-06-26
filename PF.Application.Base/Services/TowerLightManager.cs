using PF.Core.Enums;
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

        public TowerLightManager(ITowerLightService towerLight, IEventAggregator eventAggregator)
        {
            _towerLight = towerLight;
            eventAggregator.GetEvent<MachineStateChangedEvent>()
                .Subscribe(OnMachineStateChanged, ThreadOption.BackgroundThread, keepSubscriberReferenceAlive: true);
        }

        private void OnMachineStateChanged(MachineState newState)
        {
            if (StateMap.TryGetValue(newState, out var pattern))
                _towerLight.SetLights(pattern);
        }
    }
}
