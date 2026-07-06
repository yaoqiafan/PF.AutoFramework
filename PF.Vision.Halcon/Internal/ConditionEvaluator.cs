using System.Globalization;

namespace PF.Vision.Halcon.Internal;

/// <summary>
/// 管线步骤条件表达式解析器。
/// <para>
/// 支持格式（操作符两侧必须有空格）：
///   "$stepId.var > 0.5"
///   "$stepId.var >= 100"
///   "$stepId.flag == true"
///   "$stepId.count != 0"
///   "$stepId.label == OK"
/// 左侧必须是 "$" 上下文引用，右侧为字面量（数值 / bool / 字符串）。
/// </para>
/// </summary>
internal static class ConditionEvaluator
{
    // 按长度降序，确保 ">=" 先于 ">" 匹配
    private static readonly string[] Operators = [">=", "<=", "!=", ">", "<", "=="];

    public static bool Evaluate(string expression, VisionContext ctx)
    {
        foreach (var op in Operators)
        {
            var idx = expression.IndexOf(' ' + op + ' ', StringComparison.Ordinal);
            if (idx < 0) continue;

            var leftRaw  = expression[..idx].Trim();
            var rightRaw = expression[(idx + op.Length + 2)..].Trim();

            var leftValue = ctx.Resolve(leftRaw);
            return Compare(leftValue, rightRaw, op);
        }

        throw new InvalidOperationException($"无法解析条件表达式: \"{expression}\"");
    }

    private static bool Compare(object? left, string right, string op)
    {
        // bool 字面量
        if (right is "true" or "false")
        {
            var lb = ToBoolean(left);
            var rb = bool.Parse(right);
            return op switch { "==" => lb == rb, "!=" => lb != rb, _ => false };
        }

        // 数值字面量
        if (double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var rv))
        {
            var lv = ToDouble(left);
            return op switch
            {
                ">"  => lv > rv,
                ">=" => lv >= rv,
                "<"  => lv < rv,
                "<=" => lv <= rv,
                "==" => Math.Abs(lv - rv) < 1e-10,
                "!=" => Math.Abs(lv - rv) >= 1e-10,
                _    => false,
            };
        }

        // 字符串字面量
        var ls = left?.ToString() ?? "";
        return op switch { "==" => ls == right, "!=" => ls != right, _ => false };
    }

    private static bool ToBoolean(object? v) => v switch
    {
        bool b   => b,
        int  i   => i != 0,
        long l   => l != 0,
        double d => d != 0.0,
        string s => bool.TryParse(s, out var r) ? r : !string.IsNullOrEmpty(s),
        _        => v is not null,
    };

    private static double ToDouble(object? v) => v switch
    {
        double d => d,
        float  f => f,
        int    i => i,
        long   l => l,
        string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0.0,
        bool   b => b ? 1.0 : 0.0,
        _        => 0.0,
    };
}
