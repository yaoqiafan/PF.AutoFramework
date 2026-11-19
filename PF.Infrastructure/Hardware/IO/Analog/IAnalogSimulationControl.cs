using PF.Core.Enums.Hardware;

namespace PF.Infrastructure.Hardware.IO.Analog
{
    /// <summary>
    /// 模拟量设备的仿真注入入口（供调试页使用）。所有修改只在内存，不落盘；
    /// 设备重连（含全局切换仿真触发的重载）后回到通道配置里的 SimValue / SimMode。
    /// </summary>
    public interface IAnalogSimulationControl
    {
        /// <summary>AI 通道当前生效的仿真模式</summary>
        AnalogSimMode GetSimMode(int inputChannel);

        /// <summary>临时切换 AI 通道仿真模式</summary>
        void SetSimMode(int inputChannel, AnalogSimMode mode);

        /// <summary>该 AI 通道是否配置了可用的跟随 AO（无则不能切到【跟随输出】）</summary>
        bool CanFollowOutput(int inputChannel);

        /// <summary>注入 AI 仿真值（工程值）</summary>
        void SetSimInput(int inputChannel, double value);
    }
}
