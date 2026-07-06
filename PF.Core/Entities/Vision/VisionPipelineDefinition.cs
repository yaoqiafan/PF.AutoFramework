namespace PF.Core.Entities.Vision;

/// <summary>
/// 视觉管线定义（对应一个 JSON 配置文件）。
/// <para>
/// 修改流程只需编辑 JSON，不需要改动任何 C# 代码。
/// </para>
/// </summary>
public sealed class VisionPipelineDefinition
{
    /// <summary>管线唯一标识，对应 IVisionResult.ProcedureName 字段</summary>
    public string PipelineId { get; init; } = "";

    /// <summary>人类可读名称（UI 列表显示用）</summary>
    public string Name { get; init; } = "";

    /// <summary>配置文件版本号，用于兼容性追踪</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>按声明顺序执行的步骤列表</summary>
    public List<PipelineStepDefinition> Steps { get; init; } = new();

    /// <summary>
    /// 管线最终对外暴露的输出键列表，格式为 "stepId.paramName"。
    /// 框架从上下文中收集这些键作为 IVisionResult 的输出。
    /// </summary>
    public List<string> PipelineOutputs { get; init; } = new();
}
