using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 信号量调试树的 Scope（工站）节点
    /// </summary>
    public class ScopeTreeNode : BindableBase
    {
        /// <summary>获取作用域名称</summary>
        public string ScopeName { get; }

        private bool _isExpanded = true;
        /// <summary>获取或设置是否展开</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>获取作用域下的信号列表</summary>
        public ObservableCollection<SignalTreeNode> Signals { get; } = new();

        /// <summary>初始化作用域节点</summary>
        public ScopeTreeNode(string scopeName) => ScopeName = scopeName;
    }
}
