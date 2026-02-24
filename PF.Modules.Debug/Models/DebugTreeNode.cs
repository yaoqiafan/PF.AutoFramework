using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace PF.Modules.Debug.Models
{
    /// <summary>
    /// 调试界面的通用树节点
    /// </summary>
    public class DebugTreeNode : BindableBase
    {
        private string _nodeName;
        /// <summary> 显示的名称 (例如 "硬件调试", "X轴") </summary>
        public string NodeName
        {
            get => _nodeName;
            set => SetProperty(ref _nodeName, value);
        }

        private bool _isExpanded = true; // 默认展开
        /// <summary> 是否默认展开 </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private ObservableCollection<DebugTreeNode> _children = new ObservableCollection<DebugTreeNode>();
        /// <summary> 子节点集合 </summary>
        public ObservableCollection<DebugTreeNode> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        private object _payload;
        /// <summary> 
        /// 实际挂载的对象（核心！）
        /// 如果是分类文件夹，此属性为 null；
        /// 如果是叶子节点，这里存放具体的 IHardwareDevice 或 IMechanism。
        /// </summary>
        public object Payload
        {
            get => _payload;
            set => SetProperty(ref _payload, value);
        }
    }
}