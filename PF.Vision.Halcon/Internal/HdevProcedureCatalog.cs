namespace PF.Vision.Halcon.Internal;

/// <summary>
/// HDevelop 过程文件的目录查找与枚举（纯文件系统操作，不涉及 HDevEngine）。
/// <para>
/// 供 <c>HalconVisionService</c>（引擎内缓存列表）与 <c>HalconDebugService</c>
/// （无引擎的调试面板查询）共用，保证两侧 .hdvp/.hdev 优先级规则一致。
/// </para>
/// </summary>
internal static class HdevProcedureCatalog
{
    /// <summary>递归枚举目录下所有过程名（不含扩展名），.hdvp 与 .hdev 同名时去重，按名称排序。</summary>
    public static List<string> Scan(string directory)
    {
        if (!Directory.Exists(directory)) return new List<string>();

        // .hdvp 优先（同名时覆盖 .hdev 条目）
        var hdvp = Directory.GetFiles(directory, "*.hdvp", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f)!);
        var hdev = Directory.GetFiles(directory, "*.hdev", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f)!);

        return hdvp.Concat(hdev)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// 查找过程文件，递归覆盖子目录；两种扩展名均不存在时返回 null。
    /// <para>
    /// 查找优先级：顶层 .hdvp → 子目录 .hdvp → 顶层 .hdev → 子目录 .hdev。
    /// 必须递归，否则子目录里的过程能被 <see cref="Scan"/> 列出来却取不到签名。
    /// .hdvp 全局优先于 .hdev，与 <see cref="Scan"/> 的去重规则保持一致。
    /// </para>
    /// </summary>
    public static string? FindProcedureFile(string directory, string procedureName)
    {
        // 顶层快路径：绝大多数过程直接放在根目录，命中即免去整树枚举
        var hdvp = Path.Combine(directory, procedureName + ".hdvp");
        if (File.Exists(hdvp)) return hdvp;

        if (FindNested(directory, procedureName, ".hdvp") is { } nestedHdvp) return nestedHdvp;

        var hdev = Path.Combine(directory, procedureName + ".hdev");
        if (File.Exists(hdev)) return hdev;

        return FindNested(directory, procedureName, ".hdev");
    }

    /// <summary>在子目录中递归查找指定扩展名的同名过程文件。</summary>
    private static string? FindNested(string directory, string procedureName, string extension)
    {
        if (!Directory.Exists(directory)) return null;

        // 不把过程名塞进 searchPattern：名字里的 * 和 ? 会被当通配符解释，
        // 改为枚举扩展名后逐个精确比对；多处同名时按路径排序取首个，保证结果稳定
        return Directory.EnumerateFiles(directory, "*" + extension, SearchOption.AllDirectories)
            .Where(f => string.Equals(Path.GetFileNameWithoutExtension(f), procedureName,
                                      StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
