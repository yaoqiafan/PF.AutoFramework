using System;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 操作日志键目录特性：标记在静态类上，表示该类下按页面分组的嵌套静态类里的
    /// public const string 字段都是操作日志的 Key（字段上的 <see cref="System.ComponentModel.DescriptionAttribute"/>
    /// 是对应的默认描述）。供 OperationLogKeyRegistry 反射扫描发现。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class OperationLogKeyCatalogAttribute : Attribute
    {
    }
}
