namespace PF.Core.Enums.Hardware.Vision
{
    /// <summary>
    /// GenICam 节点类型。决定该节点用什么方式编辑：数值框、开关、下拉框还是按钮。
    /// </summary>
    public enum GenICamNodeType
    {
        /// <summary>未识别类型。</summary>
        Unknown = 0,

        /// <summary>整数节点。</summary>
        Integer,

        /// <summary>浮点节点。</summary>
        Float,

        /// <summary>布尔节点。</summary>
        Boolean,

        /// <summary>枚举节点，取值来自一组 symbolic 名。</summary>
        Enumeration,

        /// <summary>字符串节点。</summary>
        String,

        /// <summary>命令节点，无参执行。</summary>
        Command,

        /// <summary>分类节点，只用于组织属性树，本身不带值。</summary>
        Category,
    }
}
