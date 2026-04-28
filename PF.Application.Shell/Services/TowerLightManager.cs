using PF.Core.Enums;
using PF.Core.Interfaces.TowerLight;
using PF.UI.Infrastructure.PrismBase;
using Prism.Events;
using System.Collections.Generic;

namespace PF.Application.Shell.Services
{
    /// <summary>
    /// 三色灯管理器：订阅 <see cref="MachineStateChangedEvent"/>，根据机台状态映射灯光组合。
    /// 采用标准工业映射规范（绿灯常亮=全自动运行，黄灯常亮=待机，红灯闪烁+蜂鸣器闪烁=报警）。
    /// </summary>
    public class TowerLightManager
    {
        private readonly ITowerLightService _towerLight;

        /// <summary>
        /// 状态→灯光映射表（遵循标准工业约定）：
        /// - 黄灯常亮 = 待机就绪（Idle）
        /// - 绿灯常亮 = 全自动运行中（Running）
        /// - 红灯闪烁 + 蜂鸣器闪烁 = 报警（InitAlarm/RunAlarm）
        /// - 黄灯闪烁 = 复位/初始化中（Initializing/Resetting/Uninitialized）
        /// </summary>
        private static readonly Dictionary<MachineState, IReadOnlyDictionary<LightColor, LightState>> StateMap = new()
        {
            {
                MachineState.Uninitialized,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Off },
                    { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Initializing,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Off },
                    { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Idle,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Off },
                    { LightColor.Yellow, LightState.On },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Running,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Off },
                    { LightColor.Yellow, LightState.Off },
                    { LightColor.Green,  LightState.On },
                    { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.Paused,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Off },
                    { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Off }
                }
            },
            {
                MachineState.InitAlarm,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Blinking },
                    { LightColor.Yellow, LightState.Off },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Blinking }
                }
            },
            {
                MachineState.RunAlarm,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Blinking },
                    { LightColor.Yellow, LightState.Off },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Blinking }
                }
            },
            {
                MachineState.Resetting,
                new Dictionary<LightColor, LightState>
                {
                    { LightColor.Red,    LightState.Off },
                    { LightColor.Yellow, LightState.Blinking },
                    { LightColor.Green,  LightState.Off },
                    { LightColor.Buzzer, LightState.Off }
                }
            }
        };

        public TowerLightManager(ITowerLightService towerLight, IEventAggregator eventAggregator)
        {
            _towerLight = towerLight;

            // 订阅机台状态变更事件，后台线程避免阻塞 UI
            eventAggregator.GetEvent<MachineStateChangedEvent>()
                .Subscribe(OnMachineStateChanged, ThreadOption.BackgroundThread, keepSubscriberReferenceAlive: true);
        }

        private void OnMachineStateChanged(MachineState newState)
        {
            if (StateMap.TryGetValue(newState, out var pattern))
            {
                _towerLight.SetLights(pattern);
            }
        }
    }
}
