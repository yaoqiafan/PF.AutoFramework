namespace PF.Core.Enums
{
    /// <summary>语音播报优先级</summary>
    public enum SpeechPriority
    {
        /// <summary>普通：按入队顺序播报</summary>
        Normal,

        /// <summary>
        /// 高：插到所有普通播报之前（高优先级之间仍按入队顺序）；
        /// 若当前正在播普通播报，立即打断（被打断的那一句直接丢弃，不再续播）。
        /// </summary>
        High
    }
}
