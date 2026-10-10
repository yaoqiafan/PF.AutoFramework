namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// <c>Data&lt;T&gt;</c> 条目的可选自校验。<c>Data&lt;T&gt;</c> 反序列化是宽松的（缺字段取默认值、多字段忽略），
/// <typeparamref name="T"/> 实现本接口后，保存前和生产读取时都会调用 <see cref="Validate"/>，
/// 挡住宽松处理放进来的非法默认值（比如行列号为 0）。
/// </summary>
public interface IVisionPackageData
{
    /// <summary>返回全部错误（中文）；没有错误返回空序列。</summary>
    IEnumerable<string> Validate();
}
