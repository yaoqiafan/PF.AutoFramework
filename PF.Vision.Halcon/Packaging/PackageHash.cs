using System.Security.Cryptography;
using System.Text;

namespace PF.Vision.Halcon.Packaging;

/// <summary>条目指纹：内容的 SHA-256，格式 <c>sha256:</c> + 小写十六进制。只用哈希判断"变没变"，不比时间戳。</summary>
internal static class PackageHash
{
    private const string Prefix = "sha256:";

    public static string OfFile(string path)
    {
        using var fs = File.OpenRead(path);
        return OfStream(fs);
    }

    public static string OfStream(Stream stream) => Prefix + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

    public static string OfText(string text) => Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
