namespace PF.Core.Interfaces.Vision;

public enum ProcedureParamKind { Control, Iconic }

public record ProcedureParam(string Name, ProcedureParamKind Kind);

public record ProcedureSignature(
    string ProcedureName,
    IReadOnlyList<ProcedureParam> InputParams,
    IReadOnlyList<ProcedureParam> OutputParams);
