using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// 启动时对 .hdev 算法文件做 MD5 完整性校验。
/// 首次运行写入锁文件；后续运行对比并对变更文件发出 Warn 日志（不阻断启动）。
/// 校验完成后总是用当前状态刷新锁文件，确保下次基准与当前一致。
/// </summary>
internal static class AlgorithmVerifier
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented         = true,
        PropertyNamingPolicy  = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 扫描 <paramref name="procedureDirectory"/> 下所有 .hdev 文件，
    /// 对比或创建同级目录中的 <c>algorithms.lock.json</c>，变更时写 Warn 日志。
    /// </summary>
    public static void VerifyAndUpdate(string procedureDirectory, ILogService logger)
    {
        try
        {
            var lockPath = Path.Combine(procedureDirectory, "..", "algorithms.lock.json");
            lockPath = Path.GetFullPath(lockPath);

            var current = ComputeHashes(procedureDirectory);

            if (!File.Exists(lockPath))
            {
                WriteLock(lockPath, current);
                logger.Info($"[Vision] 算法文件锁初始化，共 {current.Count} 个文件 → {lockPath}", LogCategories.Vision);
                return;
            }

            var previous = ReadLock(lockPath);
            if (previous is null)
            {
                WriteLock(lockPath, current);
                logger.Warn("[Vision] algorithms.lock.json 解析失败，已重建", LogCategories.Vision);
                return;
            }

            var changed  = new List<string>();
            var added    = new List<string>();
            var removed  = new List<string>();

            foreach (var (name, hash) in current)
            {
                if (!previous.TryGetValue(name, out var prev))
                    added.Add(name);
                else if (prev != hash)
                    changed.Add(name);
            }
            foreach (var name in previous.Keys)
            {
                if (!current.ContainsKey(name))
                    removed.Add(name);
            }

            if (changed.Count > 0)
                logger.Warn($"[Vision] 算法文件内容已变更: {string.Join(", ", changed)}", LogCategories.Vision);
            if (added.Count > 0)
                logger.Warn($"[Vision] 新增算法文件: {string.Join(", ", added)}", LogCategories.Vision);
            if (removed.Count > 0)
                logger.Warn($"[Vision] 算法文件已删除: {string.Join(", ", removed)}", LogCategories.Vision);

            if (changed.Count == 0 && added.Count == 0 && removed.Count == 0)
                logger.Info($"[Vision] 算法文件校验通过，共 {current.Count} 个文件", LogCategories.Vision);

            // 总是刷新锁文件，以当前状态为新基准
            WriteLock(lockPath, current);
        }
        catch (Exception ex)
        {
            // 校验本身不应阻断启动
            logger.Warn($"[Vision] 算法文件校验时发生异常: {ex.Message}", LogCategories.Vision);
        }
    }

    // ── 私有 ─────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> ComputeHashes(string directory)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory)) return result;

        foreach (var file in Directory.GetFiles(directory, "*.hdev", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(directory, file).Replace('\\', '/');
            var hash = ComputeMd5(file);
            result[name] = hash;
        }
        return result;
    }

    private static string ComputeMd5(string filePath)
    {
        var bytes = MD5.HashData(File.ReadAllBytes(filePath));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void WriteLock(string lockPath, Dictionary<string, string> hashes)
    {
        var model = new LockFile(DateTime.UtcNow, hashes);
        var json  = JsonSerializer.Serialize(model, JsonOpts);
        File.WriteAllText(lockPath, json, System.Text.Encoding.UTF8);
    }

    private static Dictionary<string, string>? ReadLock(string lockPath)
    {
        try
        {
            var json  = File.ReadAllText(lockPath, System.Text.Encoding.UTF8);
            var model = JsonSerializer.Deserialize<LockFile>(json, JsonOpts);
            return model?.Files;
        }
        catch { return null; }
    }

    private sealed record LockFile(
        [property: JsonPropertyName("generatedAt")] DateTime              GeneratedAt,
        [property: JsonPropertyName("files")]       Dictionary<string, string> Files);
}
