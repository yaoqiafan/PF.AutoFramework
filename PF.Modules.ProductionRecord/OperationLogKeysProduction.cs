using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Modules.Production
{
    /// <summary>
    /// PF.Modules.ProductionRecord 界面的操作日志键目录。内层嵌套类名 = 页面名（对应
    /// NavigationConstants.Views 里的同名常量，挂在对应 View 根节点的 OperationLog.PageName 上），
    /// const 字段 = Key，字段上的 DescriptionAttribute = 默认描述。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysProduction
    {
        /// <summary>生产数据历史查询视图</summary>
        public static class ProductionHistoryView
        {
            /// <summary>按记录类型过滤</summary>
            [Description("按记录类型过滤")]
            public const string RecordTypeFilter = nameof(RecordTypeFilter);

            /// <summary>按关键词过滤</summary>
            [Description("按关键词过滤")]
            public const string KeywordFilter = nameof(KeywordFilter);

            /// <summary>查询历史记录</summary>
            [Description("查询历史记录")]
            public const string Query = nameof(Query);

            /// <summary>重置查询条件</summary>
            [Description("重置查询条件")]
            public const string Reset = nameof(Reset);

            /// <summary>选中记录查看详情</summary>
            [Description("选中记录查看详情")]
            public const string ResultList = nameof(ResultList);

            /// <summary>导出历史记录为 Excel</summary>
            [Description("导出历史记录为 Excel")]
            [OperationLogCritical]
            public const string ExportExcel = nameof(ExportExcel);

            /// <summary>导出历史记录为 CSV</summary>
            [Description("导出历史记录为 CSV")]
            [OperationLogCritical]
            public const string ExportCsv = nameof(ExportCsv);
        }

        /// <summary>生产数据实时监控视图</summary>
        public static class ProductionMonitorView
        {
            /// <summary>按记录类型过滤实时数据</summary>
            [Description("按记录类型过滤实时数据")]
            public const string RecordTypeFilter = nameof(RecordTypeFilter);

            /// <summary>清空实时记录</summary>
            [Description("清空实时记录")]
            [OperationLogCritical]
            public const string Clear = nameof(Clear);

            /// <summary>选中实时记录</summary>
            [Description("选中实时记录")]
            public const string RecordList = nameof(RecordList);

            /// <summary>导出实时记录为 Excel</summary>
            [Description("导出实时记录为 Excel")]
            [OperationLogCritical]
            public const string ExportExcel = nameof(ExportExcel);

            /// <summary>导出实时记录为 CSV</summary>
            [Description("导出实时记录为 CSV")]
            [OperationLogCritical]
            public const string ExportCsv = nameof(ExportCsv);
        }
    }
}
