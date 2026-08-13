using PF.Core.Enums.Hardware.Vision;

namespace PF.Core.Entities.Hardware.Vision
{
    /// <summary>
    /// GenICam 属性树中的一个节点快照（名称 + 类型 + 权限 + 当前值）。
    ///
    /// <para>用于调试面板把设备的全部可调参数列出来，替代"手填节点名"——
    /// 现场排查时最花时间的往往不是改值，而是先猜出节点叫什么、
    /// 以及它此刻到底是不存在、还是存在但不可写。</para>
    ///
    /// <para>这是**取快照那一刻**的状态：设备状态一变（开流、改模式），
    /// 同一节点的 <see cref="AccessMode"/> 和取值范围都可能变，需重新枚举。</para>
    /// </summary>
    public sealed class GenICamNode
    {
        /// <summary>节点名（GenICam 特征名，写入时用的就是它）。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>界面显示名。设备 XML 未提供时回退为 <see cref="Name"/>。</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>所属分类（属性树中的父分类名）。未归类时为空。</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>节点类型。</summary>
        public GenICamNodeType NodeType { get; init; }

        /// <summary>取快照时的访问模式。</summary>
        public GenICamAccessMode AccessMode { get; init; }

        /// <summary>取快照时的当前值。不可读或读取失败时为 null。</summary>
        public string? Value { get; init; }

        /// <summary>枚举节点的可选项（symbolic 名）。非枚举节点为空列表。</summary>
        public IReadOnlyList<string> EnumEntries { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 节点说明。优先为框架内置的中文释义，没有对应条目时回退设备 XML 自带的英文 ToolTip。
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// <see cref="Description"/> 是否来自框架的中文释义表。
        /// false 表示是设备自带的英文原文——界面可据此区别显示，
        /// 也便于发现哪些节点还缺中文条目。
        /// </summary>
        public bool IsDescriptionLocalized { get; init; }

        /// <summary>当前是否可写。</summary>
        public bool IsWritable => AccessMode is GenICamAccessMode.ReadWrite or GenICamAccessMode.WriteOnly;

        /// <summary>是否为命令节点（用"执行"而不是"写入"）。</summary>
        public bool IsCommand => NodeType == GenICamNodeType.Command;

        /// <summary>权限的简短文本，供界面直接显示。</summary>
        public string AccessText => AccessMode switch
        {
            GenICamAccessMode.ReadWrite => "读写",
            GenICamAccessMode.ReadOnly => "只读",
            GenICamAccessMode.WriteOnly => "只写",
            GenICamAccessMode.NotAvailable => "不可用",
            GenICamAccessMode.NotImplemented => "未实现",
            _ => "未知",
        };
    }
}
