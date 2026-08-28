using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Modules.Alarm
{
    /// <summary>
    /// 报警模块的操作日志键目录。内层嵌套类名 = 页面名（挂在对应 View 根节点的
    /// OperationLog.PageName 上），const 字段 = Key，字段上的 DescriptionAttribute = 默认描述。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysAlarm
    {
        /// <summary>报警中心视图（对应 NavigationConstantsAlarm.AlarmCenterView）</summary>
        public static class AlarmCenterView
        {
            /// <summary>历史查询-等级筛选</summary>
            [Description("历史报警查询-等级筛选")]
            public const string SelectedSeverity = nameof(SelectedSeverity);

            /// <summary>历史查询-分类筛选</summary>
            [Description("历史报警查询-分类筛选")]
            public const string SelectedCategory = nameof(SelectedCategory);

            /// <summary>历史查询-来源筛选</summary>
            [Description("历史报警查询-来源筛选")]
            public const string SelectedSource = nameof(SelectedSource);

            /// <summary>历史查询-代码筛选</summary>
            [Description("历史报警查询-代码筛选")]
            public const string QueryErrorCode = nameof(QueryErrorCode);

            /// <summary>历史查询-描述模糊搜索</summary>
            [Description("历史报警查询-描述模糊搜索")]
            public const string QueryDescription = nameof(QueryDescription);

            /// <summary>执行历史报警查询</summary>
            [Description("查询历史报警")]
            public const string QueryHistory = nameof(QueryHistory);

            /// <summary>重置历史查询筛选条件</summary>
            [Description("重置历史报警查询筛选条件")]
            public const string ClearHistoryFilters = nameof(ClearHistoryFilters);

            /// <summary>切换历史报警每页显示条数</summary>
            [Description("切换历史报警每页显示条数")]
            public const string HistoryPageSize = nameof(HistoryPageSize);

            /// <summary>选中一条活跃报警</summary>
            [Description("选中活跃报警")]
            public const string SelectActiveAlarm = nameof(SelectActiveAlarm);

            /// <summary>确认全部活跃报警（软确认，不触发硬件复位）</summary>
            [Description("确认全部活跃报警")]
            [OperationLogCritical]
            public const string ClearAllAlarms = nameof(ClearAllAlarms);

            /// <summary>确认/清除选中的单条活跃报警</summary>
            [Description("确认/清除选中的活跃报警")]
            [OperationLogCritical]
            public const string ClearSelectedAlarm = nameof(ClearSelectedAlarm);

            /// <summary>触发全线系统复位</summary>
            [Description("触发系统复位")]
            [OperationLogCritical]
            public const string SystemReset = nameof(SystemReset);

            /// <summary>在活跃报警卡片上单条清除</summary>
            [Description("清除单条活跃报警")]
            [OperationLogCritical]
            public const string ClearSingleAlarm = nameof(ClearSingleAlarm);
        }

        /// <summary>报警详情弹窗</summary>
        public static class AlarmDetailCardView
        {
            /// <summary>弹窗内异常复位</summary>
            [Description("报警详情弹窗-异常复位")]
            [OperationLogCritical]
            public const string ConfirmAlarm = nameof(ConfirmAlarm);
        }
    }
}
