namespace PF.Vision.Halcon.Internal;

/// <summary>
/// 管线步骤间共享的变量黑板。
/// 仅在 Worker 线程上读写，无需任何线程同步。
/// HObject 值安全存储在此处，不会离开 Worker 线程。
/// </summary>
internal sealed class VisionContext
{
    private readonly Dictionary<string, object?> _bag = new();

    /// <summary>写入步骤输出，key 格式为 "stepId.paramName"</summary>
    public void Set(string stepId, string paramName, object? value)
        => _bag[$"{stepId}.{paramName}"] = value;

    /// <summary>注入外部值，在步骤 inputs 中用 "$__ext__.keyName" 引用</summary>
    public void InjectExternal(Dictionary<string, object?>? inputs)
    {
        if (inputs is null) return;
        foreach (var (k, v) in inputs)
            _bag[$"__ext__.{k}"] = v;
    }

    /// <summary>
    /// 解析单个输入值：string 以 "$" 开头时从 bag 取值，否则返回原值。
    /// 引用不存在时返回 null。
    /// </summary>
    public object? Resolve(object? raw)
    {
        if (raw is string s && s.StartsWith('$'))
            return _bag.TryGetValue(s[1..], out var v) ? v : null;
        return raw;
    }

    /// <summary>
    /// 评估条件表达式。null 或空字符串视为 true（无条件执行）。
    /// 表达式解析失败时返回 true 并由调用方记录警告。
    /// </summary>
    public bool EvaluateCondition(string? condition)
        => string.IsNullOrWhiteSpace(condition) || ConditionEvaluator.Evaluate(condition, this);

    /// <summary>按 pipeline_outputs 键列表收集最终输出</summary>
    public Dictionary<string, object?> Collect(IEnumerable<string> keys)
        => keys.ToDictionary(k => k, k => _bag.GetValueOrDefault(k));
}
