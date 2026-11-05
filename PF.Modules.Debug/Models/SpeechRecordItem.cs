using PF.Core.Entities.Speech;
using PF.Core.Enums;

namespace PF.Modules.Debug.Models
{
    /// <summary>语音播报调试页「播报记录」表格的一行。同一句播报的后续快照通过 <see cref="Update"/> 原地刷新。</summary>
    public sealed class SpeechRecordItem : BindableBase
    {
        private SpeechRecord _record;

        /// <summary>由一条播报记录快照创建表格行。</summary>
        public SpeechRecordItem(SpeechRecord record) => _record = record;

        /// <summary>记录序号（同一句播报不变）</summary>
        public long Id => _record.Id;

        /// <summary>请求时间</summary>
        public string TimeText => _record.RequestTime.ToString("HH:mm:ss.fff");

        /// <summary>是否高优先级</summary>
        public bool IsHigh => _record.Priority == SpeechPriority.High;

        /// <summary>优先级文字</summary>
        public string PriorityText => IsHigh ? "高" : "普通";

        /// <summary>播报文字</summary>
        public string Text => _record.Text;

        /// <summary>当前状态（XAML 按它切换标签配色）</summary>
        public SpeechStatus Status => _record.Status;

        /// <summary>状态文字</summary>
        public string StatusText => _record.Status switch
        {
            SpeechStatus.Queued => "排队中",
            SpeechStatus.Playing => "播报中",
            SpeechStatus.Completed => "已播完",
            SpeechStatus.Interrupted => "被打断",
            SpeechStatus.Cancelled => "已取消",
            SpeechStatus.Muted => "静音丢弃",
            SpeechStatus.Dropped => "积压丢弃",
            SpeechStatus.Failed => "失败",
            _ => _record.Status.ToString()
        };

        /// <summary>是否属于异常（「仅异常」筛选用）：被打断 / 取消 / 静音丢弃 / 积压丢弃 / 失败</summary>
        public bool IsAbnormal => _record.Status is SpeechStatus.Interrupted or SpeechStatus.Cancelled
                                  or SpeechStatus.Muted or SpeechStatus.Dropped or SpeechStatus.Failed;

        /// <summary>播放耗时</summary>
        public string DurationText => _record.Duration is { } d ? $"{d.TotalSeconds:0.0} s" : "-";

        /// <summary>来源</summary>
        public string SourceText => _record.Source ?? "-";

        /// <summary>悬停提示：失败原因，或完整文字</summary>
        public string ToolTipText => string.IsNullOrEmpty(_record.Error) ? _record.Text : $"{_record.Text}\n失败原因：{_record.Error}";

        /// <summary>用同一句播报的新快照刷新本行。</summary>
        public void Update(SpeechRecord record)
        {
            _record = record;
            RaisePropertyChanged(string.Empty);   // 全部属性都由快照派生，一次性通知
        }
    }
}
