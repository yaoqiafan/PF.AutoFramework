using Prism.Mvvm;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 信号量调试树的叶子节点（单个具名信号量）
    /// </summary>
    public class SignalTreeNode : BindableBase
    {
        public string SignalName { get; }
        public string ParentScope { get; }
        public int InitialCount { get; }

        private int _currentCount;
        public int CurrentCount
        {
            get => _currentCount;
            private set
            {
                if (SetProperty(ref _currentCount, value))
                    RaisePropertyChanged(nameof(StatusText));
            }
        }

        /// <summary> 显示文本，例如 "允许拉料  [1 / 1]" </summary>
        public string StatusText => $"{SignalName}  [{CurrentCount} / {InitialCount}]";

        public SignalTreeNode(string signalName, string parentScope, int initialCount, int currentCount)
        {
            SignalName   = signalName;
            ParentScope  = parentScope;
            InitialCount = initialCount;
            _currentCount = currentCount;
        }

        public void Update(int currentCount) => CurrentCount = currentCount;
    }
}
