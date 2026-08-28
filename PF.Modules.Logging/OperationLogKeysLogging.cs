using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

// OperationLogCriticalAttribute 控制"注册但未在 Excel 里出现过的键"首次出现时是否默认 Enabled=true：
// 只标在会改变状态/审计价值高的动作上（清空日志、导出日志），筛选/刷新/查看详情等常规动作不标，
// 默认 Enabled=false，工程师需要时自己去 Excel 里打开。

namespace PF.Modules.Logging
{
    /// <summary>
    /// PF.Modules.Logging 界面的操作日志键目录。内层嵌套类名 = 页面名（对应
    /// NavigationConstants.Views.LoggingListView / LogManagementView，挂在对应 View
    /// 根节点的 OperationLog.PageName 上），const 字段 = Key，字段上的
    /// DescriptionAttribute = 默认描述。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysLogging
    {
        /// <summary>实时日志列表视图</summary>
        public static class LoggingListView
        {
            /// <summary>按级别筛选</summary>
            [Description("按级别筛选日志")]
            public const string SelectedLogLevel = nameof(SelectedLogLevel);

            /// <summary>按时间范围筛选</summary>
            [Description("按时间范围筛选日志")]
            public const string SelectedDateRange = nameof(SelectedDateRange);

            /// <summary>自动滚动开关</summary>
            [Description("切换自动滚动")]
            public const string AutoScroll = nameof(AutoScroll);

            /// <summary>选中一条日志查看详情</summary>
            [Description("选中日志条目")]
            public const string SelectedLogEntry = nameof(SelectedLogEntry);

            /// <summary>右键菜单：复制选中内容</summary>
            [Description("复制选中日志内容")]
            public const string CopySelected = nameof(CopySelected);

            /// <summary>右键菜单：复制全部显示内容</summary>
            [Description("复制全部显示日志内容")]
            public const string CopyAll = nameof(CopyAll);

            /// <summary>右键菜单：清空日志</summary>
            [Description("清空日志")]
            [OperationLogCritical]
            public const string ClearLogs = nameof(ClearLogs);

            /// <summary>右键菜单：刷新</summary>
            [Description("刷新日志列表")]
            public const string Refresh = nameof(Refresh);
        }

        /// <summary>日志归档管理视图</summary>
        public static class LogManagementView
        {
            /// <summary>从磁盘读取历史日志</summary>
            [Description("从磁盘读取历史日志")]
            public const string QueryHistory = nameof(QueryHistory);

            /// <summary>按级别筛选</summary>
            [Description("按级别筛选历史日志")]
            public const string SelectedFilterLevel = nameof(SelectedFilterLevel);

            /// <summary>按分类筛选</summary>
            [Description("按分类筛选历史日志")]
            public const string SelectedFilterCategory = nameof(SelectedFilterCategory);

            /// <summary>导出当前筛选结果</summary>
            [Description("导出当前筛选结果为 CSV")]
            [OperationLogCritical]
            public const string ExportLogs = nameof(ExportLogs);

            /// <summary>切换每页显示条数</summary>
            [Description("切换每页显示条数")]
            public const string PageSize = nameof(PageSize);
        }
    }
}
