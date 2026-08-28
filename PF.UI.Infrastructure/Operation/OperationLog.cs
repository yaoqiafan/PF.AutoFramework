using System.Windows;

namespace PF.UI.Infrastructure.Operation
{
    /// <summary>
    /// 界面操作日志标记：挂在需要留痕的控件上（Button/ComboBox/DataGridColumn 等任意 DependencyObject），
    /// <see cref="OperationLogInterceptor"/> 只处理挂了 <see cref="KeyProperty"/> 的控件，未挂的控件不产生任何日志与开销。
    /// </summary>
    public static class OperationLog
    {
        /// <summary>
        /// 操作键：唯一标识这一个操作点，建议引用 OperationLogKeyCatalog 特性标记的常量类里的字段，
        /// 而不是手写字符串。展示用的描述文本不在这里配置，来自 OperationLogKeyRegistry 里对应的登记。
        /// </summary>
        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.RegisterAttached(
                "Key",
                typeof(string),
                typeof(OperationLog),
                new PropertyMetadata(null));

        /// <summary>设置操作键</summary>
        public static void SetKey(DependencyObject element, string value) =>
            element.SetValue(KeyProperty, value);

        /// <summary>获取操作键；未标记时返回 null</summary>
        public static string GetKey(DependencyObject element) =>
            (string)element.GetValue(KeyProperty);

        /// <summary>
        /// 页面名：在每个 View 的根节点挂一次即可，子孙控件通过 WPF 附加属性继承自动拿到同一个值，
        /// 不用逐控件设置。建议直接复用该页面对应的 NavigationConstants.Views.XXX 常量值。
        /// </summary>
        public static readonly DependencyProperty PageNameProperty =
            DependencyProperty.RegisterAttached(
                "PageName",
                typeof(string),
                typeof(OperationLog),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        /// <summary>设置页面名</summary>
        public static void SetPageName(DependencyObject element, string value) =>
            element.SetValue(PageNameProperty, value);

        /// <summary>获取页面名（沿可视/逻辑树继承而来）；未标记时返回 null</summary>
        public static string GetPageName(DependencyObject element) =>
            (string)element.GetValue(PageNameProperty);
    }
}
