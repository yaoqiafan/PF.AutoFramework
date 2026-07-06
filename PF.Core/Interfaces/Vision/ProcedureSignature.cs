namespace PF.Core.Interfaces.Vision;

/// <summary>过程参数类型：控制量或图标量。</summary>
public enum ProcedureParamKind
{
    /// <summary>控制量（数值 / 字符串类型变量）。</summary>
    Control,
    /// <summary>图标量（图像 / 区域 / 轮廓类型变量）。</summary>
    Iconic
}

/// <summary>过程参数描述（名称 + 参数类型）。</summary>
public record ProcedureParam(string Name, ProcedureParamKind Kind);

/// <summary>过程签名（过程名称 + 输入参数列表 + 输出参数列表）。</summary>
public record ProcedureSignature(
    string ProcedureName,
    IReadOnlyList<ProcedureParam> InputParams,
    IReadOnlyList<ProcedureParam> OutputParams);
