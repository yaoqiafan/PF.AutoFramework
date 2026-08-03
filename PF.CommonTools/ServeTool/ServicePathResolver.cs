using Microsoft.Win32;
using PF.Core.Constants;
// WPF 项目（UseWPF=true）的隐式 using 集合不含 System.IO，需显式引入
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PF.CommonTools.ServeTool
{
    /// <summary>
    /// PF.SecsGem.Service 的部署位置与项目绑定解析。
    ///
    /// 主程序与该服务是两个独立进程，各自解析 <c>D:\PFConfig\PFAutoFrameWork\{项目名}\</c>
    /// 下的 SecsGemConfig.db：主程序取入口程序集名，服务读自身 appsettings.json 的
    /// ProjectName 键（由安装器写入）。两者一旦不一致就会操作不同的数据库，
    /// 故本类同时提供定位与校验能力，供主程序启动时比对。
    /// </summary>
    public static class ServicePathResolver
    {
        /// <summary>服务可执行文件名</summary>
        public const string SecsServiceExeName = "PF.SecsGem.Service.exe";

        /// <summary>服务安装子目录名（对应 installer.conf 的 SERVICE1_SUBDIR）</summary>
        public const string SecsServiceSubDir = "SecsGemService";

        /// <summary>
        /// 服务注册名。全机唯一，不按项目区分——服务的本机通道端口 6800 在服务端
        /// （Worker.LocationServer）和主程序端（InternalClient）都是硬编码的，HSMS 被动端口
        /// 也只有一个，一台机器上同时只可能有一个实例真正工作。一机多项目时由安装包覆盖注册，
        /// 服务归属最后安装的项目，主程序启动时校验归属。
        /// </summary>
        public const string SecsServiceName = "SecsGemService";

        /// <summary>
        /// 解析 PF.SecsGem.Service 的可执行文件完整路径。
        ///
        /// 优先级：
        /// <list type="number">
        /// <item><b>SCM 中实际注册的 ImagePath</b>——这是唯一能反映"当前生效的服务属于哪个项目"
        /// 的来源。一机多项目时该服务可能是别的项目装的，此时它的 appsettings.json 里
        /// 写的是别的项目名，校验才能查出来。</item>
        /// <item>本项目安装目录（注册表 InstallPath）下的默认位置——服务尚未注册时用。</item>
        /// <item>Program Files 默认位置。</item>
        /// </list>
        /// </summary>
        public static string ResolveSecsServiceExePath()
        {
            if (OperatingSystem.IsWindows())
            {
                var registered = TryReadRegisteredServiceImagePath();
                if (!string.IsNullOrEmpty(registered))
                    return registered;

                var installPath = TryReadInstallPath();
                if (!string.IsNullOrEmpty(installPath))
                    return Path.Combine(installPath, SecsServiceSubDir, SecsServiceExeName);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerFocus", "PFAutoFramework", SecsServiceSubDir, SecsServiceExeName);
        }

        /// <summary>
        /// 从 SCM 读取已注册服务的可执行文件路径，未注册或读不到返回 null。
        /// ImagePath 形如 <c>"C:\Program Files\...\PF.SecsGem.Service.exe"</c>（含引号，
        /// 也可能带命令行参数），需剥离引号与参数。
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static string? TryReadRegisteredServiceImagePath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{SecsServiceName}");
                if (key?.GetValue("ImagePath") is not string raw || string.IsNullOrWhiteSpace(raw))
                    return null;

                raw = raw.Trim();

                // 带引号：取第一对引号之间的内容，后面的都是参数
                if (raw.StartsWith("\""))
                {
                    var end = raw.IndexOf('"', 1);
                    return end > 1 ? raw.Substring(1, end - 1) : null;
                }

                // 未加引号：按 .exe 边界截断（路径本身不含空格时 sc 可以不加引号）
                var exeIdx = raw.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                return exeIdx > 0 ? raw.Substring(0, exeIdx + 4) : raw;
            }
            catch { /* 无权限或键不存在时按未注册处理 */ }

            return null;
        }

        /// <summary>
        /// 从注册表读取本项目的安装目录，读不到返回 null。
        ///
        /// 键路径按项目名分子键，与 Setup.iss 的 [Registry] 段一一对应。
        /// 注意键名是 PFAutoFramework（不含空格）而非 APP_NAME——后者是带空格的显示名，
        /// 旧版 Setup.iss 误用它作键名，导致这里的查找恒失败并静默回退到 Program Files。
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static string? TryReadInstallPath()
        {
            try
            {
                var subKey = $@"SOFTWARE\PowerFocus\PFAutoFramework\{ConstGlobalParam.ProjectName}";
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                if (key?.GetValue("InstallPath") is string installPath && !string.IsNullOrEmpty(installPath))
                    return installPath;
            }
            catch { /* 注册表不可读时按未安装处理，回退默认路径 */ }

            return null;
        }

        /// <summary>服务目录下 appsettings.json 的完整路径。</summary>
        public static string GetSecsServiceSettingsPath(string serviceExePath)
            => Path.Combine(Path.GetDirectoryName(serviceExePath) ?? string.Empty, "appsettings.json");

        /// <summary>
        /// 读取服务 appsettings.json 中的 ProjectName。
        /// 文件不存在、格式损坏或无该键时返回 null。
        /// </summary>
        public static string? TryReadServiceProjectName(string settingsPath)
        {
            try
            {
                if (!File.Exists(settingsPath)) return null;

                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("ProjectName", out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch { /* 损坏的配置等同未配置，由调用方告警 */ }

            return null;
        }

        /// <summary>
        /// 将当前进程的项目名写入服务的 appsettings.json，保留文件内其余配置。
        /// 供面板"安装服务"按钮使用——该路径绕过安装器，需自行保证服务的项目绑定正确。
        /// 需要对服务目录的写权限（通常在 Program Files 下，故调用方应先确认管理员权限）。
        /// </summary>
        /// <returns>写入成功返回 true。</returns>
        public static bool TryWriteServiceProjectName(string settingsPath, string projectName)
        {
            try
            {
                var dir = Path.GetDirectoryName(settingsPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;

                var map = new Dictionary<string, JsonElement>();
                if (File.Exists(settingsPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                            map[prop.Name] = prop.Value.Clone();
                    }
                }

                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("ProjectName", projectName);
                    foreach (var kv in map)
                    {
                        if (string.Equals(kv.Key, "ProjectName", StringComparison.Ordinal)) continue;
                        writer.WritePropertyName(kv.Key);
                        kv.Value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }

                File.WriteAllBytes(settingsPath, buffer.ToArray());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
