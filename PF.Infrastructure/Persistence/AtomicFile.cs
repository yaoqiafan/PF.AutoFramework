using System.Text;

namespace PF.Infrastructure.Persistence
{
    /// <summary>
    /// 配置/数据文件的安全读写工具：原子写入 + 损坏文件备份。
    ///
    /// <para>为什么需要：直接 <c>File.WriteAllText</c> 覆盖时若遇到断电/崩溃，文件会只写了一半；
    /// 下次读取失败按空数据处理，再被保存一次，原数据就永久丢失（点位表就是这样丢的）。</para>
    /// <list type="bullet">
    ///   <item><see cref="WriteAllText"/>：先写同目录临时文件并刷盘，再用 File.Replace 替换正式文件。
    ///     任一时刻磁盘上要么是完整的旧文件，要么是完整的新文件。</item>
    ///   <item><see cref="BackupCorrupt"/>：读取/解析失败时先把坏文件另存一份，后续即使被覆盖也能人工恢复。</item>
    /// </list>
    /// </summary>
    public static class AtomicFile
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>原子写入文本（UTF-8 无 BOM）。目录不存在时自动创建；失败抛异常，原文件保持不变。</summary>
        public static void WriteAllText(string path, string content)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tempPath = path + ".tmp";
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = Utf8NoBom.GetBytes(content);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                catch (IOException)
                {
                    // 个别文件系统（网络盘、部分 U 盘）不支持 Replace：退化为覆盖复制，临时文件仍是完整的
                    File.Copy(tempPath, path, overwrite: true);
                    File.Delete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        /// <summary>
        /// 把读取失败的文件另存为 <c>{原名}.corrupt-yyyyMMdd-HHmmss</c>，返回备份路径；
        /// 文件不存在或备份失败返回 null（不抛异常，调用方已在处理读取失败）。
        /// </summary>
        public static string? BackupCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(path, backup, overwrite: true);
                return backup;
            }
            catch
            {
                return null;
            }
        }
    }
}
