using PF.Core.Enums;

namespace PF.Core.Entities.Speech
{
    /// <summary>
    /// 一句语音播报的记录快照。状态每变化一次，服务就发出一个新的快照（<see cref="Id"/> 不变），
    /// 订阅方按 <see cref="Id"/> 替换旧行即可。
    /// </summary>
    public sealed record SpeechRecord
    {
        /// <summary>记录序号，进程内递增，同一句的各个快照相同</summary>
        public long Id { get; init; }

        /// <summary>播报文字</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>优先级</summary>
        public SpeechPriority Priority { get; init; }

        /// <summary>调用方传入的来源（如工站名）；未传时为 null</summary>
        public string? Source { get; init; }

        /// <summary>当前状态</summary>
        public SpeechStatus Status { get; init; }

        /// <summary>请求时间（入队或被丢弃的时间）</summary>
        public DateTime RequestTime { get; init; }

        /// <summary>开始播放时间；未开始播放为 null</summary>
        public DateTime? StartTime { get; init; }

        /// <summary>结束时间；尚未结束为 null</summary>
        public DateTime? EndTime { get; init; }

        /// <summary>失败原因（仅 <see cref="SpeechStatus.Failed"/> 时有值）</summary>
        public string? Error { get; init; }

        /// <summary>实际播放时长；未开始或未结束时为 null</summary>
        public TimeSpan? Duration => StartTime.HasValue && EndTime.HasValue ? EndTime - StartTime : null;

        /// <summary>是否已结束（不会再有后续快照）</summary>
        public bool IsFinished => Status is not (SpeechStatus.Queued or SpeechStatus.Playing);
    }
}
