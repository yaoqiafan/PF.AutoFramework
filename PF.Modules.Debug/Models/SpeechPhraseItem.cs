namespace PF.Modules.Debug.Models
{
    /// <summary>语音播报调试页「常用语」列表的一行。</summary>
    public sealed class SpeechPhraseItem : BindableBase
    {
        private bool _isHighPriority;

        /// <summary>创建常用语行。</summary>
        public SpeechPhraseItem(string text, bool isHighPriority)
        {
            Text = text;
            _isHighPriority = isHighPriority;
        }

        /// <summary>播报文字</summary>
        public string Text { get; }

        /// <summary>是否以高优先级（插队）播报</summary>
        public bool IsHighPriority
        {
            get => _isHighPriority;
            set => SetProperty(ref _isHighPriority, value);
        }
    }
}
