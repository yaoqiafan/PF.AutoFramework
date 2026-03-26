using System;

namespace PF.Core.Events
{
    /// <summary>
    /// 硬件输入事件总线（需在 DI 容器中注册为 Singleton）。
    /// 接收来自 HardwareInputMonitor 的字符串类型事件，
    /// 广播给 BaseMasterController 等订阅者进行业务路由。
    /// </summary>
    public class HardwareInputEventBus
    {
        /// <summary>任意硬件输入被触发时广播，参数为 HardwareInputType 常量或自定义字符串。</summary>
        public event Action<string> HardwareInputTriggered;

        /// <summary>线程安全地发布一次硬件输入事件。</summary>
        public void PublishInputEvent(string inputType)
        {
            HardwareInputTriggered?.Invoke(inputType);
        }
    }
}