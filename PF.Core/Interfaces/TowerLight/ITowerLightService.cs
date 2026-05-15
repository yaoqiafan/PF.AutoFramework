using PF.Core.Enums;
using System.Collections.Generic;

namespace PF.Core.Interfaces.TowerLight
{
    /// <summary>
    /// 三色灯逻辑控制服务。
    /// 负责管理各通道状态与软件频闪循环，通过 <see cref="ITowerLightDoWriter"/> 输出到物理 DO 点。
    /// </summary>
    public interface ITowerLightService
    {
        /// <summary>
        /// 全局蜂鸣器屏蔽开关。
        /// true = 静音：蜂鸣器请求的任何状态均以 Off 生效，已在运行的任务立即取消；
        /// false = 解除：按 RequestedState 重新评估并即时生效。
        /// </summary>
        bool IsBuzzerMuted { get; set; }

        /// <summary>设置单个通道的状态</summary>
        /// <param name="color">目标通道</param>
        /// <param name="state">目标状态</param>
        /// <param name="blinkIntervalMs">频闪半周期（仅 Blinking 时有效，默认 500 ms）</param>
        void SetLight(LightColor color, LightState state, int blinkIntervalMs = 500);

        /// <summary>批量设置多个通道状态（原子操作，避免多次调用的中间态）</summary>
        /// <param name="states">通道→状态映射</param>
        /// <param name="blinkIntervalMs">频闪半周期（仅 Blinking 通道有效，默认 500 ms）</param>
        void SetLights(IReadOnlyDictionary<LightColor, LightState> states, int blinkIntervalMs = 500);

        /// <summary>立即关闭所有通道并取消所有频闪任务，同时重置所有槽位的 RequestedState</summary>
        void TurnOffAll();
    }
}
