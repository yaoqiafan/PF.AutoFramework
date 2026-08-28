using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

// 默认启用规则（OperationLogInterceptor 决定）：登记了描述但没打 [OperationLogCritical] 的 Key，
// 默认 Enabled=false，需要工程师在项目级 Excel 配置里手动打开；只有下面显式打了
// [OperationLogCritical] 的才默认开——这些是真正会改变系统状态、值得默认审计的动作
// （运行/停止流程、保存/删除模板、清空未保存的编辑内容等）。

namespace PF.Modules.Halcon;

/// <summary>
/// PF.Modules.Halcon 界面的操作日志键目录。内层嵌套类名 = 页面名，必须和
/// <see cref="HalconNavigationConstants"/> 里对应的导航常量值完全一致（挂在对应 View
/// 根节点的 OperationLog.PageName 上）；const 字段 = Key，字段上的 DescriptionAttribute = 默认描述。
/// </summary>
[OperationLogKeyCatalog]
public static class OperationLogKeysHalcon
{
    /// <summary>Halcon 仪表盘（Tab 切换壳页）</summary>
    public static class HalconDashboardView
    {
        [Description("切换到过程调试标签")]
        public const string NavToDebug = nameof(NavToDebug);

        [Description("切换到管线运行标签")]
        public const string NavToPipeline = nameof(NavToPipeline);
    }

    /// <summary>过程调试页</summary>
    public static class HalconDebugView
    {
        [Description("选择过程文件")]
        public const string SelectProcedure = nameof(SelectProcedure);

        [Description("刷新过程列表")]
        public const string RefreshProcedures = nameof(RefreshProcedures);

        [Description("设置调试端口")]
        public const string SetPort = nameof(SetPort);

        // 调试密码字段不接入操作日志：LogValueChange 会把旧/新值原文写进日志，
        // 密码类输入没有安全的方式记录明文变化，宁可不追踪。

        [Description("启动调试服务器")]
        [OperationLogCritical]
        public const string EnableDebugServer = nameof(EnableDebugServer);

        [Description("停止调试服务器")]
        [OperationLogCritical]
        public const string DisableDebugServer = nameof(DisableDebugServer);

        [Description("启动 HDevelop（附加到进程调试）")]
        [OperationLogCritical]
        public const string LaunchHDevelop = nameof(LaunchHDevelop);

        [Description("取消调试")]
        [OperationLogCritical]
        public const string CancelDebug = nameof(CancelDebug);

        [Description("设置打开后直接运行")]
        public const string SetRunImmediately = nameof(SetRunImmediately);

        [Description("引擎耦合自检（会停止调试服务器并释放引擎）")]
        [OperationLogCritical]
        public const string SelfCheck = nameof(SelfCheck);

        [Description("设置输入控制量")]
        public const string SetInputControlValue = nameof(SetInputControlValue);

        [Description("切换为 ROI 模式")]
        public const string ToggleToRoiMode = nameof(ToggleToRoiMode);

        [Description("切回文件模式")]
        public const string ToggleToFileMode = nameof(ToggleToFileMode);

        [Description("编辑 ROI")]
        public const string EditRois = nameof(EditRois);

        [Description("浏览图像文件")]
        public const string BrowseFile = nameof(BrowseFile);
    }

    /// <summary>管线运行页</summary>
    public static class PipelineRunnerView
    {
        [Description("选择管线")]
        public const string SelectPipeline = nameof(SelectPipeline);

        [Description("执行管线")]
        [OperationLogCritical]
        public const string RunPipeline = nameof(RunPipeline);

        [Description("刷新管线列表")]
        public const string RefreshPipelineList = nameof(RefreshPipelineList);

        [Description("清除图像")]
        public const string ClearImage = nameof(ClearImage);
    }

    /// <summary>ROI 编辑弹窗（内嵌 HalconRoiEditor 控件，共用其下方一组 Key）</summary>
    public static class RoiEditorDialog
    {
        [Description("确认")]
        [OperationLogCritical]
        public const string Confirm = nameof(Confirm);

        [Description("取消")]
        public const string Cancel = nameof(Cancel);

        [Description("设为包含区域模式")]
        public const string SetIncludeMode = nameof(SetIncludeMode);

        [Description("设为排除区域模式")]
        public const string SetExcludeMode = nameof(SetExcludeMode);

        [Description("新增矩形 ROI")]
        public const string AddRect = nameof(AddRect);

        [Description("新增旋转矩形 ROI")]
        public const string AddRotatedRect = nameof(AddRotatedRect);

        [Description("新增圆形 ROI")]
        public const string AddCircle = nameof(AddCircle);

        [Description("新增椭圆 ROI")]
        public const string AddEllipse = nameof(AddEllipse);

        [Description("新增扇形 ROI")]
        public const string AddSector = nameof(AddSector);

        [Description("预览检测范围")]
        public const string PreviewRegion = nameof(PreviewRegion);

        [Description("退出预览")]
        public const string ExitPreview = nameof(ExitPreview);

        [Description("清空 ROI")]
        [OperationLogCritical]
        public const string ClearRois = nameof(ClearRois);

        [Description("ROI 名称")]
        public const string RoiName = nameof(RoiName);

        [Description("ROI 操作类型选择")]
        public const string RoiOperation = nameof(RoiOperation);

        [Description("删除 ROI")]
        [OperationLogCritical]
        public const string DeleteRoi = nameof(DeleteRoi);
    }

    /// <summary>ROI 形状模板编辑弹窗（同样内嵌 HalconRoiEditor 控件，复制一份同款 Key）</summary>
    public static class ShapeTemplateEditorDialog
    {
        [Description("选参考图")]
        public const string SelectReferenceImage = nameof(SelectReferenceImage);

        [Description("设置模板名称")]
        public const string TemplateName = nameof(TemplateName);

        [Description("加载模板文件")]
        public const string LoadTemplateFile = nameof(LoadTemplateFile);

        [Description("保存并关闭")]
        [OperationLogCritical]
        public const string SaveAndClose = nameof(SaveAndClose);

        [Description("取消")]
        public const string Cancel = nameof(Cancel);

        [Description("设为包含区域模式")]
        public const string SetIncludeMode = nameof(SetIncludeMode);

        [Description("设为排除区域模式")]
        public const string SetExcludeMode = nameof(SetExcludeMode);

        [Description("新增矩形 ROI")]
        public const string AddRect = nameof(AddRect);

        [Description("新增旋转矩形 ROI")]
        public const string AddRotatedRect = nameof(AddRotatedRect);

        [Description("新增圆形 ROI")]
        public const string AddCircle = nameof(AddCircle);

        [Description("新增椭圆 ROI")]
        public const string AddEllipse = nameof(AddEllipse);

        [Description("新增扇形 ROI")]
        public const string AddSector = nameof(AddSector);

        [Description("预览检测范围")]
        public const string PreviewRegion = nameof(PreviewRegion);

        [Description("退出预览")]
        public const string ExitPreview = nameof(ExitPreview);

        [Description("清空 ROI")]
        [OperationLogCritical]
        public const string ClearRois = nameof(ClearRois);

        [Description("ROI 名称")]
        public const string RoiName = nameof(RoiName);

        [Description("ROI 操作类型选择")]
        public const string RoiOperation = nameof(RoiOperation);

        [Description("删除 ROI")]
        [OperationLogCritical]
        public const string DeleteRoi = nameof(DeleteRoi);
    }

    /// <summary>ROI 形状模板验证弹窗</summary>
    public static class ShapeTemplateVerifyDialog
    {
        [Description("查找匹配")]
        public const string FindMatch = nameof(FindMatch);

        [Description("选择匹配结果")]
        public const string SelectMatchResult = nameof(SelectMatchResult);

        [Description("选图")]
        public const string SelectImage = nameof(SelectImage);

        [Description("关闭")]
        public const string Close = nameof(Close);
    }
}
