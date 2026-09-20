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

        private string _readStatus = "未读取";
        /// <summary>
        /// 该通道亮度值的来源状态。"未读取"/"读取失败：…"时 <see cref="Value"/> 只是界面初值 0，
        /// 并不代表硬件真是 0——没有这个提示，"没读到"和"读到 0"在界面上完全看不出区别。
        /// 读回成功或用户改值后为空串。
        /// </summary>
        public string ReadStatus
        {
            get => _readStatus;
            private set
            {
                if (SetProperty(ref _readStatus, value))
                    RaisePropertyChanged(nameof(HasReadProblem));
            }
        }

        /// <summary>当前显示的值是否不可信（未读取或读取失败），供界面高亮。</summary>
        public bool HasReadProblem => !string.IsNullOrEmpty(_readStatus);

        /// <summary>把读回值写入通道，不触发下发（避免读到什么又原样发回去）。</summary>
        public void ApplyReadValue(int value)
        {
            SetValueCore(value, notifyOwner: false);
            ReadStatus = string.Empty;
        }

        /// <summary>标记读回失败：数值保持原样不动，只把"不可信"状态亮给界面。</summary>
        public void MarkReadFailed(string message) => ReadStatus = $"读取失败：{message}";

        private void SetValueCore(int value, bool notifyOwner)
        {
            // 必须显式给属性名：SetProperty 默认取 [CallerMemberName]，在这个辅助方法里取到的是
            // "SetValueCore"，界面永远收不到 Value 的变更通知——读回的值赋进去了，滑块和数值框
            // 却一直停在 0（读回日志里明明是 165）
            if (SetProperty(ref _value, value, nameof(Value)) && notifyOwner)
            {
                // 用户主动给了值，这个值就是权威的，不再是"没读到的初值"
                ReadStatus = string.Empty;
                ValueChangedByUser?.Invoke(Channel, value);
            }
        }
    }
}
