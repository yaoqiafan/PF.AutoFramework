using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Application.Base
{
    /// <summary>
    /// PF.Application.Base 自身界面的操作日志键目录。内层嵌套类名 = 页面名（挂在对应 View 根节点的
    /// OperationLog.PageName 上），const 字段 = Key，字段上的 DescriptionAttribute = 默认描述。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysBase
    {
        /// <summary>主窗口</summary>
        public static class MainWindow
        {
            /// <summary>切换深色/浅色主题</summary>
            [Description("切换深色/浅色主题")]
            public const string ToggleTheme = nameof(ToggleTheme);
            [Description("切换导航页面"), OperationLogCritical]
            public const string SwitchNavigation = nameof(SwitchNavigation);
        }

        /// <summary>主显界面（对应 NavigationConstants.Views.MainView）</summary>
        public static class MainView
        {
            [Description("打开说明书")]
            public const string OpenManual = nameof(OpenManual);
            [Description("检查更新")]
            public const string CheckUpdate = nameof(CheckUpdate);
            [Description("联系技术支持")]
            public const string ContactSupport = nameof(ContactSupport);
            [Description("关于软件")]
            public const string About = nameof(About);
        }

        /// <summary>公共参数页（对应 NavigationConstants.Views.CommonParamView）</summary>
        public static class CommonParamView
        {
            [Description("保存公共参数"), OperationLogCritical]
            public const string Save = nameof(Save);
        }
    }
}
