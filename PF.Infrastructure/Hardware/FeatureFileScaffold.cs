using PF.Infrastructure.Logging;

namespace PF.Infrastructure.Hardware
{
    /// <summary>
    /// 固定路径属性文件（<c>HardwareFeatureFiles\</c>）缺失时的脚手架：建目录 + 写占位文件。
    /// 相机、采集卡各自的 <c>AutoImportFeatureFile</c> 共用，避免现场人员对着一个不存在的目录/
    /// 文件名发懵——目录建好、占位文件放好，照着路径导出真实参数覆盖即可。
    /// </summary>
    internal static class FeatureFileScaffold
    {
        /// <summary>
        /// 目录不存在则创建；文件不存在则写一份仅含注释行的占位文件（GenApi 属性文件格式，
        /// <c>#</c> 开头的行是注释，逐节点解析时会被跳过，即使误被当真实文件导入也不会写坏任何节点）。
        /// 任何 IO 异常只记 Warn，不向上抛——脚手架失败不该阻塞设备连接。
        /// </summary>
        public static void EnsurePlaceholder(string directory, string path, string deviceName, CategoryLogger logger)
        {
            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(path)) return;

                string content =
                    $"# 占位文件 —— 由 PF.AutoFramework 在 [{deviceName}] 首次连接时自动生成，尚未导入任何参数。{Environment.NewLine}"
                    + $"# 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}"
                    + $"# 请用厂商客户端（如海康 MVS）把本设备现场调好的接线/成像参数导出后覆盖本文件（文件名不变），{Environment.NewLine}"
                    + "# 然后重新连接/初始化设备即可生效。本文件在被覆盖前只含注释行，不会写入任何节点。"
                    + Environment.NewLine;

                File.WriteAllText(path, content);
                logger?.Info($"[{deviceName}] 已创建属性文件占位：{path}（请用厂商客户端导出真实参数覆盖）。");
            }
            catch (Exception ex)
            {
                logger?.Warn($"[{deviceName}] 创建属性文件占位失败：{ex.Message}", ex);
            }
        }
    }
}
