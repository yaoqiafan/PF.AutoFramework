using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Modules.SecsGem
{
    /// <summary>
    /// PF.Modules.SecsGem 界面的操作日志键目录。内层嵌套类名 = 页面名（挂在对应 View/Dialog 根节点的
    /// OperationLog.PageName 上），const 字段 = Key，字段上的 DescriptionAttribute = 默认描述，
    /// OperationLogCriticalAttribute 标记该操作首次出现时默认启用记录（未标记则默认关闭，工程师可在
    /// 项目级 Excel 配置里手动打开）。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysSecsGem
    {
        /// <summary>SecsGem 调试主界面（NavigationConstants.Views.SecsGemDebugView）</summary>
        public static class SecsGemDebugView
        {
            /// <summary>初始化 SECS/GEM 连接</summary>
            [OperationLogCritical]
            [Description("初始化 SECS/GEM 连接")]
            public const string InitializeConnection = nameof(InitializeConnection);

            /// <summary>断开 SECS/GEM 连接</summary>
            [OperationLogCritical]
            [Description("断开 SECS/GEM 连接")]
            public const string DisconnectConnection = nameof(DisconnectConnection);

            /// <summary>导入命令库</summary>
            [OperationLogCritical]
            [Description("导入命令库")]
            public const string ImportCommands = nameof(ImportCommands);

            /// <summary>导出命令库</summary>
            [Description("导出命令库")]
            public const string ExportCommands = nameof(ExportCommands);

            /// <summary>重载命令库</summary>
            [Description("重载命令库")]
            public const string ReloadCommands = nameof(ReloadCommands);

            /// <summary>新建命令</summary>
            [OperationLogCritical]
            [Description("新建命令")]
            public const string AddNewCommand = nameof(AddNewCommand);

            /// <summary>命令分组下添加命令</summary>
            [OperationLogCritical]
            [Description("命令分组下添加命令")]
            public const string AddCommandGroupItem = nameof(AddCommandGroupItem);

            /// <summary>删除命令</summary>
            [OperationLogCritical]
            [Description("删除命令")]
            public const string DeleteCommandLeaf = nameof(DeleteCommandLeaf);

            /// <summary>编辑报文节点值</summary>
            [OperationLogCritical]
            [Description("编辑报文节点值")]
            public const string EditMessageNodeValue = nameof(EditMessageNodeValue);

            /// <summary>切换节点变量绑定</summary>
            [OperationLogCritical]
            [Description("切换节点变量绑定")]
            public const string ToggleVariableNode = nameof(ToggleVariableNode);

            /// <summary>添加报文子节点</summary>
            [OperationLogCritical]
            [Description("添加报文子节点")]
            public const string AddChildNode = nameof(AddChildNode);

            /// <summary>删除报文节点</summary>
            [OperationLogCritical]
            [Description("删除报文节点")]
            public const string RemoveMessageNode = nameof(RemoveMessageNode);

            /// <summary>切换等待回复(W-Bit)</summary>
            [Description("切换等待回复(W-Bit)")]
            public const string ToggleWaitReply = nameof(ToggleWaitReply);

            /// <summary>保存报文</summary>
            [OperationLogCritical]
            [Description("保存报文")]
            public const string SaveMessage = nameof(SaveMessage);

            /// <summary>更新变量节点</summary>
            [Description("更新变量节点")]
            public const string UpdateVariableNodes = nameof(UpdateVariableNodes);

            /// <summary>发送报文</summary>
            [OperationLogCritical]
            [Description("发送报文")]
            public const string SendMessage = nameof(SendMessage);

            /// <summary>清空实时通信日志</summary>
            [OperationLogCritical]
            [Description("清空实时通信日志")]
            public const string ClearTransactionLog = nameof(ClearTransactionLog);

            /// <summary>选中通信日志条目</summary>
            [Description("选中通信日志条目")]
            public const string SelectTransactionLog = nameof(SelectTransactionLog);

            /// <summary>切换命令库主动/应答Tab</summary>
            [Description("切换命令库主动/应答Tab")]
            public const string SwitchCommandLibraryTab = nameof(SwitchCommandLibraryTab);

            /// <summary>切换主功能Tab</summary>
            [Description("切换主功能Tab")]
            public const string SwitchMainTab = nameof(SwitchMainTab);

            /// <summary>切换参数管理子Tab</summary>
            [Description("切换参数管理子Tab")]
            public const string SwitchParamSubTab = nameof(SwitchParamSubTab);

            /// <summary>编辑系统参数值</summary>
            [OperationLogCritical]
            [Description("编辑系统参数值")]
            public const string EditSystemParamValue = nameof(EditSystemParamValue);

            /// <summary>新增参数行</summary>
            [OperationLogCritical]
            [Description("新增参数行")]
            public const string AddParamRow = nameof(AddParamRow);

            /// <summary>删除参数行</summary>
            [OperationLogCritical]
            [Description("删除参数行")]
            public const string DeleteParamRow = nameof(DeleteParamRow);

            /// <summary>编辑VID数据类型</summary>
            [OperationLogCritical]
            [Description("编辑VID数据类型")]
            public const string EditVidDataType = nameof(EditVidDataType);

            /// <summary>编辑VID值</summary>
            [OperationLogCritical]
            [Description("编辑VID值")]
            public const string EditVidValue = nameof(EditVidValue);

            /// <summary>编辑CEID关联ReportID</summary>
            [OperationLogCritical]
            [Description("编辑CEID关联ReportID")]
            public const string EditCeidLinkReportIDs = nameof(EditCeidLinkReportIDs);

            /// <summary>编辑ReportID关联VID</summary>
            [OperationLogCritical]
            [Description("编辑ReportID关联VID")]
            public const string EditReportIdLinkVIDs = nameof(EditReportIdLinkVIDs);

            /// <summary>编辑CommandID的RCMD</summary>
            [OperationLogCritical]
            [Description("编辑CommandID的RCMD")]
            public const string EditCommandIdRcmd = nameof(EditCommandIdRcmd);

            /// <summary>编辑CommandID关联VID</summary>
            [OperationLogCritical]
            [Description("编辑CommandID关联VID")]
            public const string EditCommandIdLinkVIDs = nameof(EditCommandIdLinkVIDs);

            /// <summary>导入系统参数(JSON)</summary>
            [OperationLogCritical]
            [Description("导入系统参数(JSON)")]
            public const string ImportSystemParam = nameof(ImportSystemParam);

            /// <summary>导入变量参数(Excel)</summary>
            [OperationLogCritical]
            [Description("导入变量参数(Excel)")]
            public const string ImportValidateParam = nameof(ImportValidateParam);

            /// <summary>导出参数</summary>
            [Description("导出参数")]
            public const string ExportParams = nameof(ExportParams);

            /// <summary>保存参数</summary>
            [OperationLogCritical]
            [Description("保存参数")]
            public const string SaveParam = nameof(SaveParam);

            /// <summary>编辑服务名称</summary>
            [OperationLogCritical]
            [Description("编辑服务名称")]
            public const string EditServiceName = nameof(EditServiceName);

            /// <summary>编辑服务EXE路径</summary>
            [OperationLogCritical]
            [Description("编辑服务EXE路径")]
            public const string EditServiceExePath = nameof(EditServiceExePath);

            /// <summary>安装SecsGem服务</summary>
            [OperationLogCritical]
            [Description("安装SecsGem服务")]
            public const string InstallService = nameof(InstallService);

            /// <summary>卸载SecsGem服务</summary>
            [OperationLogCritical]
            [Description("卸载SecsGem服务")]
            public const string UninstallService = nameof(UninstallService);

            /// <summary>启动SecsGem服务</summary>
            [OperationLogCritical]
            [Description("启动SecsGem服务")]
            public const string StartService = nameof(StartService);

            /// <summary>刷新服务状态</summary>
            [Description("刷新服务状态")]
            public const string RefreshServiceStatus = nameof(RefreshServiceStatus);
        }

        /// <summary>命令编辑弹窗</summary>
        public static class CommandEditDialog
        {
            /// <summary>编辑Stream</summary>
            [OperationLogCritical]
            [Description("编辑Stream")]
            public const string EditStream = nameof(EditStream);

            /// <summary>编辑Function</summary>
            [OperationLogCritical]
            [Description("编辑Function")]
            public const string EditFunction = nameof(EditFunction);

            /// <summary>编辑命令名称</summary>
            [OperationLogCritical]
            [Description("编辑命令名称")]
            public const string EditCommandName = nameof(EditCommandName);

            /// <summary>确认命令编辑</summary>
            [OperationLogCritical]
            [Description("确认命令编辑")]
            public const string Confirm = nameof(Confirm);

            /// <summary>取消命令编辑</summary>
            [Description("取消命令编辑")]
            public const string Cancel = nameof(Cancel);
        }

        /// <summary>报文节点配置弹窗</summary>
        public static class SecsNodeConfigDialog
        {
            /// <summary>选择数据类型</summary>
            [OperationLogCritical]
            [Description("选择数据类型")]
            public const string SelectDataType = nameof(SelectDataType);

            /// <summary>切换变量节点绑定</summary>
            [OperationLogCritical]
            [Description("切换变量节点绑定")]
            public const string ToggleVariableNode = nameof(ToggleVariableNode);

            /// <summary>选择VID</summary>
            [OperationLogCritical]
            [Description("选择VID")]
            public const string SelectVid = nameof(SelectVid);

            /// <summary>编辑节点初始值</summary>
            [OperationLogCritical]
            [Description("编辑节点初始值")]
            public const string EditNodeValue = nameof(EditNodeValue);

            /// <summary>确认节点配置</summary>
            [OperationLogCritical]
            [Description("确认节点配置")]
            public const string Confirm = nameof(Confirm);

            /// <summary>取消节点配置</summary>
            [Description("取消节点配置")]
            public const string Cancel = nameof(Cancel);
        }

        /// <summary>VID 选择弹窗</summary>
        public static class VidSelectDialog
        {
            /// <summary>选择VID变量</summary>
            [Description("选择VID变量")]
            public const string SelectVid = nameof(SelectVid);

            /// <summary>确认VID选择</summary>
            [OperationLogCritical]
            [Description("确认VID选择")]
            public const string Confirm = nameof(Confirm);

            /// <summary>取消VID选择</summary>
            [Description("取消VID选择")]
            public const string Cancel = nameof(Cancel);
        }
    }
}
