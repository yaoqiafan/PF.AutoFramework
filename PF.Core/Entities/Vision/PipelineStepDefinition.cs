namespace PF.Core.Entities.Vision;

/// <summary>
/// 管线中单个步骤的定义（对应 JSON 中的一个 step 对象）。
/// </summary>
public sealed class PipelineStepDefinition
{
    /// <summary>步骤唯一标识，供后续步骤的 "$stepId.paramName" 引用使用</summary>
    public string Id { get; init; } = "";

    /// <summary>.hdev 过程文件名（不含路径和扩展名）</summary>
    public string Procedure { get; init; } = "";

    /// <summary>
    /// 输入参数字典。
    /// Value 为 string 且以 "$" 开头时表示上下文引用（格式 "$stepId.paramName" 或 "$__ext__.key"），
    /// 否则为静态字面量。
    /// </summary>
    public Dictionary<string, object?> Inputs { get; init; } = new();

    /// <summary>
    /// 声明要从本步骤输出中保存到上下文的参数名列表。
    /// 框架自动从 ControlOutputs 和 IconicOutputs 中匹配并存储。
    /// </summary>
    public List<string> Outputs { get; init; } = new();

    /// <summary>
    /// 执行条件表达式（null 或空字符串表示无条件执行）。
    /// 支持格式："$stepId.varName > 0.5"、"$stepId.flag == true"、"$stepId.count != 0"
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>true = 本步骤失败后继续执行后续步骤；false = 立即终止管线</summary>
    public bool SkipOnError { get; init; } = false;
}
