using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    /// <summary>
    /// 硬件事件总线的默认实现 (需在 DI 容器中注册为 Singleton)
    /// </summary>
    public class PhysicalButtonEventBus
    {
        public event Action<PhysicalButtonType> PhysicalButtonPressed;

        public void PublishPhysicalButton(PhysicalButtonType buttonType)
        {
            // 线程安全地触发事件
            PhysicalButtonPressed?.Invoke(buttonType);
        }
    }
}