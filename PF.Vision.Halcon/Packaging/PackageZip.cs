using System.IO.Compression;

namespace PF.Vision.Halcon.Packaging;

/// <summary>包内路径与压缩方式的公共规则。</summary>
internal static class PackageZip
{
    private static readonly HashSet<string> AlreadyCompressed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gz", ".zip", ".7z",
    };

    /// <summary>已经是压缩格式的不再压缩；大文件用 Fastest，省保存时间。</summary>
    public static CompressionLevel LevelFor(string path, long size)
    {
        if (AlreadyCompressed.Contains(System.IO.Path.GetExtension(path))) return CompressionLevel.NoCompression;
        return size > 10 * 1024 * 1024 ? CompressionLevel.Fastest : CompressionLevel.Optimal;
    }

    /// <summary>校验包内相对路径：正斜杠、不能是绝对路径、不能含 <c>..</c>（防止解包写到临时目录外）。</summary>
    public static string CheckRelative(string rel, string displayName)
    {
        if (string.IsNullOrWhiteSpace(rel) || rel.Contains('\\') || rel.StartsWith('/') || rel.Contains(':')
            || rel.Split('/').Any(p => p is ".." or "." or ""))
            throw new VisionPackageException($"视觉资产包 [{displayName}] 含非法的条目路径 \"{rel}\"，包可能已损坏。");
        return rel;
    }
}
