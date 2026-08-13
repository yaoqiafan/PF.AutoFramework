namespace PF.Core.Enums.Hardware.Vision
{
    /// <summary>
    /// GenICam 节点访问模式。
    /// <para>区分 <see cref="NotImplemented"/>（设备没有这个节点）与 <see cref="ReadOnly"/>
    /// （节点存在但当前状态下不可写）尤其重要——两者都表现为"写入失败"，
    /// 但前者要改节点名、后者要改操作时机。</para>
    /// </summary>
    public enum GenICamAccessMode
    {
        /// <summary>无法判定。</summary>
        Unknown = 0,

        /// <summary>本设备未实现该节点。</summary>
        NotImplemented,

        /// <summary>节点存在但当前不可访问（前置条件未满足）。</summary>
        NotAvailable,

        /// <summary>只读。</summary>
        ReadOnly,

        /// <summary>只写。</summary>
        WriteOnly,

        /// <summary>可读可写。</summary>
        ReadWrite,
    }
}
