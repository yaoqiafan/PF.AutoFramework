using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Modules.Parameter
{
    /// <summary>
    /// PF.Modules.Parameter 界面的操作日志键目录。内层嵌套类名 = 页面名（挂在对应 View 根节点的
    /// OperationLog.PageName 上），const 字段 = Key，字段上的 DescriptionAttribute = 默认描述，
    /// OperationLogCriticalAttribute 标记的字段默认启用记录，未标记的默认不启用（工程师可在项目级
    /// Excel 配置里手动打开）。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysParameter
    {
        /// <summary>参数管理主视图</summary>
        public static class ParameterView
        {
            /// <summary>刷新参数列表（日常交互，非关键）</summary>
            [Description("刷新参数列表")]
            public const string RefreshParameters = nameof(RefreshParameters);

            /// <summary>重置为默认参数（会覆盖当前参数值，关键操作）</summary>
            [Description("重置为默认参数")]
            [OperationLogCritical]
            public const string ResetDefaults = nameof(ResetDefaults);

            /// <summary>切换参数分类（日常交互，非关键）</summary>
            [Description("切换参数分类")]
            public const string SelectCategory = nameof(SelectCategory);

            /// <summary>选中参数行（日常交互，非关键）</summary>
            [Description("选中参数行")]
            public const string SelectParameterRow = nameof(SelectParameterRow);

            /// <summary>编辑参数数据类型（改变参数定义，关键操作）</summary>
            [Description("编辑参数数据类型")]
            [OperationLogCritical]
            public const string EditParamDataType = nameof(EditParamDataType);

            /// <summary>打开参数值编辑弹窗（只是导航到弹窗，非关键；真正改值的动作在弹窗的 Confirm 上）</summary>
            [Description("打开参数值编辑弹窗")]
            public const string OpenChangeValueDialog = nameof(OpenChangeValueDialog);

            /// <summary>添加新参数（关键操作）</summary>
            [Description("添加新参数")]
            [OperationLogCritical]
            public const string AddParameter = nameof(AddParameter);

            /// <summary>删除选中参数（关键操作）</summary>
            [Description("删除选中参数")]
            [OperationLogCritical]
            public const string DeleteParameter = nameof(DeleteParameter);

            /// <summary>保存所有修改（关键操作）</summary>
            [Description("保存所有修改")]
            [OperationLogCritical]
            public const string SaveParameters = nameof(SaveParameters);
        }

        /// <summary>参数值编辑弹窗</summary>
        public static class CommonChangeParamDialog
        {
            /// <summary>取消参数值修改（不产生状态变化，非关键）</summary>
            [Description("取消参数值修改")]
            public const string Cancel = nameof(Cancel);

            /// <summary>确认参数值修改（真正提交改值，关键操作）</summary>
            [Description("确认参数值修改")]
            [OperationLogCritical]
            public const string Confirm = nameof(Confirm);
        }
    }
}
