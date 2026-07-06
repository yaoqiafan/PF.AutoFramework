using HalconDotNet;

namespace PF.Vision.Halcon.Internal;

/// <summary>
/// 管线步骤间共享的变量黑板。
/// 仅在 Worker 线程上读写，无需任何线程同步。
/// <para>
/// HObject 所有权契约：黑板对存入的所有 HObject 持有独立句柄副本（CopyObj，
/// HALCON 内部引用计数共享图像数据，复制句柄代价极低），并在 <see cref="Dispose"/> 时统一释放；
/// <see cref="CollectOwned"/> 返回新克隆，所有权转移给调用方。
/// </para>
/// </summary>
internal sealed class VisionContext : IDisposable
{
    private readonly Dictionary<string, object?> _bag = new();
    private bool _disposed;

    /// <summary>
    /// 写入步骤输出（黑板持有独立副本，原对象所有权仍归调用方），key 格式为 "stepId.paramName"。
    /// 覆盖同名旧值时释放旧 HObject。
    /// </summary>
    public void Set(string stepId, string paramName, object? value)
    {
        var key = $"{stepId}.{paramName}";
        if (_bag.TryGetValue(key, out var old))
            (old as HObject)?.Dispose();
        _bag[key] = OwnCopy(value);
    }

    /// <summary>
    /// 注入外部值（黑板持有独立副本，原对象所有权仍归调用方），
    /// 在步骤 inputs 中用 "$__ext__.keyName" 引用。
    /// </summary>
    public void InjectExternal(Dictionary<string, object?>? inputs)
    {
        if (inputs is null) return;
        foreach (var (k, v) in inputs)
            _bag[$"__ext__.{k}"] = OwnCopy(v);
    }

    /// <summary>
    /// 解析单个输入值：string 以 "$" 开头时视为黑板引用，否则原样返回。
    /// 引用不存在时返回 null（由调用方决定是否告警）。
    /// </summary>
    public object? Resolve(object? raw) => Resolve(raw, out _, out _);

    /// <summary>
    /// 解析单个输入值并报告解析详情。
    /// </summary>
    /// <param name="raw">原始输入值</param>
    /// <param name="isReference">true = raw 是 "$" 开头的黑板引用</param>
    /// <param name="found">仅在 <paramref name="isReference"/> 为 true 时有意义：黑板中是否存在该 key</param>
    public object? Resolve(object? raw, out bool isReference, out bool found)
    {
        isReference = false;
        found       = false;
        if (raw is string s && s.StartsWith('$'))
        {
            isReference = true;
            found       = _bag.TryGetValue(s[1..], out var v);
            return found ? v : null;
        }
        return raw;
    }

    /// <summary>
    /// 评估条件表达式。null 或空字符串视为 true（无条件执行）。
    /// 表达式解析失败时抛出异常，由调用方决定处置策略。
    /// </summary>
    public bool EvaluateCondition(string? condition)
        => string.IsNullOrWhiteSpace(condition) || ConditionEvaluator.Evaluate(condition, this);

    /// <summary>
    /// 按 pipeline_outputs 键列表收集最终输出。
    /// HObject 返回独立克隆，所有权转移给调用方；黑板中的原件仍由 <see cref="Dispose"/> 释放。
    /// </summary>
    public Dictionary<string, object?> CollectOwned(IEnumerable<string> keys)
        => keys.ToDictionary(k => k, k => OwnCopy(_bag.GetValueOrDefault(k)));

    /// <summary>释放黑板持有的所有 HObject</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var v in _bag.Values)
            (v as HObject)?.Dispose();
        _bag.Clear();
    }

    /// <summary>非 HObject 原样返回；HObject 返回独立句柄副本（未初始化对象返回新的空对象）</summary>
    private static object? OwnCopy(object? value)
    {
        if (value is not HObject h) return value;
        if (!h.IsInitialized()) return new HObject();
        HOperatorSet.CopyObj(h, out HObject copy, 1, -1);
        return copy;
    }
}
