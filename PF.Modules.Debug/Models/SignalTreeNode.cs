using Prism.Mvvm;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 信号量调试树的叶子节点（单个具名信号量）
    /// </summary>
    public class SignalTreeNode : BindableBase
    {

        
        /// <summary>获取信号名称</summary>
        public string SignalName { get; }
        /// <summary>获取父级作用域</summary>
        public string ParentScope { get; }
        /// <summary>获取初始计数</summary>
        public int InitialCount { get; }

        private bool _isExpanded = true;
        /// <summary>获取或设置是否展开</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }


        private int _currentCount;
        /// <summary>获取或设置当前计数</summary>
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

        /// <summary>初始化信号节点</summary>
        public SignalTreeNode(string signalName, string parentScope, int initialCount, int currentCount)
        {
            SignalName   = signalName;
            ParentScope  = parentScope;
            InitialCount = initialCount;
            _currentCount = currentCount;
        }

        /// <summary>更新当前计数</summary>
        public void Update(int currentCount) => CurrentCount = currentCount;
    }
}
