namespace PF.Vision.Halcon.Packaging;

/// <summary>视觉资产包读写过程中的业务错误，<see cref="Exception.Message"/> 是可以直接给用户看的中文说明。</summary>
public class VisionPackageException : Exception
{
    /// <summary>用中文说明构造。</summary>
    public VisionPackageException(string message) : base(message) { }

    /// <summary>用中文说明 + 内部异常构造。</summary>
    public VisionPackageException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>保存前校验不通过：<see cref="Validation"/> 列出全部原因。</summary>
public sealed class VisionPackageValidationException : VisionPackageException
{
    /// <summary>校验结果。</summary>
    public PackageValidation Validation { get; }

    /// <summary>用校验结果构造，消息为全部错误的汇总。</summary>
    public VisionPackageValidationException(PackageValidation validation)
        : base("视觉资产包校验未通过，不能保存：" + string.Join("；", validation.Errors.Select(i => i.ToString())))
        => Validation = validation;
}
