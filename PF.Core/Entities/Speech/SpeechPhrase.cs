namespace PF.Core.Entities.Speech
{
    /// <summary>语音播报常用语（随语音设置保存在 speech_config.json）</summary>
    public sealed class SpeechPhrase
    {
        /// <summary>播报文字</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>是否以高优先级（插队）播报</summary>
        public bool IsHighPriority { get; set; }
    }
}
