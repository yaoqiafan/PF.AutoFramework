using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 信号量调试树的 Scope（工站）节点
    /// </summary>
    public class ScopeTreeNode : BindableBase
    {
        public string ScopeName { get; }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ObservableCollection<SignalTreeNode> Signals { get; } = new();

        public ScopeTreeNode(string scopeName) => ScopeName = scopeName;
    }
}
