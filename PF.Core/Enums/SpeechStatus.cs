namespace PF.Core.Enums
{
    /// <summary>一句语音播报的状态（播报记录用）</summary>
    public enum SpeechStatus
    {
        /// <summary>排队中</summary>
        Queued,

        /// <summary>播报中</summary>
        Playing,

        /// <summary>已播完</summary>
        Completed,

        /// <summary>播放中被打断（高优先级插队、停止全部、开启静音）</summary>
        Interrupted,

        /// <summary>已取消（排队中被停止全部清掉，或调用方的取消令牌触发）</summary>
        Cancelled,

        /// <summary>静音期间的播报请求，直接丢弃未入队</summary>
        Muted,

        /// <summary>普通优先级排队超过上限，被丢弃</summary>
        Dropped,

        /// <summary>播放失败（如没有音频设备）</summary>
        Failed
    }
}
