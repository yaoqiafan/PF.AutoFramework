using Prism.Mvvm;
using System;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 光源控制器调试页单个通道的滑块/文本框数据项。
    /// 滑块与右侧文本框绑定同一个 <see cref="Value"/>，天然双向联动。
    /// </summary>
    public sealed class LightChannelViewModel : BindableBase
    {
        /// <summary>通道号（从 1 开始）</summary>
        public int Channel { get; }

        /// <summary>通道显示名，如“通道1”</summary>
        public string DisplayName => $"通道{Channel}";

        private int _value;
        /// <summary>当前亮度值。用户在滑块/文本框改动时触发 <see cref="ValueChangedByUser"/>。</summary>
        public int Value
        {
            get => _value;
            set => SetValueCore(value, notifyOwner: true);
        }

        /// <summary>用户操作导致的取值变化：(Channel, Value)。读回赋值不会触发本事件。</summary>
        public event Action<int, int> ValueChangedByUser;

        /// <summary>构造通道项</summary>
        public LightChannelViewModel(int channel)
        {
            Channel = channel;
        }

        /// <summary>把读回值写入通道，不触发下发（避免读到什么又原样发回去）。</summary>
        public void ApplyReadValue(int value) => SetValueCore(value, notifyOwner: false);

        private void SetValueCore(int value, bool notifyOwner)
        {
            if (SetProperty(ref _value, value) && notifyOwner)
            {
                ValueChangedByUser?.Invoke(Channel, value);
            }
        }
    }
}
