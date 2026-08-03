using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Constants
{
    /// <summary>
    /// 全局常量参数。
    ///
    /// 配置路径按项目隔离：所有项目共用 <see cref="ConfigRoot"/> 作为根，各自的配置落在
    /// <c>{ConfigRoot}\{ProjectName}\</c> 下。这样同一台机器上换项目运行时，
    /// 上一个项目的参数、配方、点表不会被新项目的默认参数种子清空。
    ///
    /// 使用前必须调用一次 <see cref="Initialize"/>：
    /// <list type="bullet">
    /// <item>WPF 主程序由 <c>PFApplicationBase</c> 构造函数自动完成，项目侧无需关心；</item>
    /// <item><c>PF.SecsGem.Service</c> 是独立进程，从自身 <c>appsettings.json</c> 的
    /// <c>ProjectName</c> 键读取后自行调用。</item>
    /// </list>
    /// </summary>
    public static class ConstGlobalParam
    {
        private static string _projectName = string.Empty;

        /// <summary>
        /// 所有项目共用的配置根目录（不含项目名）。
        /// 仅供迁移工具和框架内部使用，业务代码一律用 <see cref="ConfigPath"/>。
        /// </summary>
        public static string ConfigRoot => Path.Combine("D://PFConfig", "PFAutoFrameWork");

        /// <summary>当前项目名，即 <see cref="ConfigRoot"/> 下的隔离子目录名。</summary>
        public static string ProjectName => _projectName;

        /// <summary>是否已完成项目名初始化。</summary>
        public static bool IsInitialized => !string.IsNullOrEmpty(_projectName);

        /// <summary>
        /// 初始化项目名。必须在任何配置读写之前调用，且整个进程生命周期内只能调用一次。
        /// </summary>
        /// <param name="projectName">项目名，将作为配置目录名，不得为空或含非法路径字符。</param>
        /// <exception cref="InvalidOperationException">已经初始化过。</exception>
        /// <exception cref="ArgumentException">项目名为空或含非法路径字符。</exception>
        public static void Initialize(string projectName)
        {
            if (IsInitialized)
                throw new InvalidOperationException(
                    $"项目名已初始化为 [{_projectName}]，不允许重复设置。");

            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("项目名不能为空。", nameof(projectName));

            if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(
                    $"项目名 [{projectName}] 含非法路径字符。", nameof(projectName));

            _projectName = projectName;
            Directory.CreateDirectory(ConfigPath);
        }

        /// <summary>
        /// 当前项目的配置目录：<c>{ConfigRoot}\{ProjectName}\</c>。
        /// 未初始化时直接抛出，不回退到根目录——任何漏调 <see cref="Initialize"/> 的宿主
        /// 必须在首次运行时立刻暴露，而不是静默地把配置写到共享根目录去。
        /// </summary>
        /// <exception cref="InvalidOperationException">尚未调用 <see cref="Initialize"/>。</exception>
        public static string ConfigPath => IsInitialized
            ? Path.Combine(ConfigRoot, _projectName)
            : throw new InvalidOperationException(
                "ConstGlobalParam 尚未初始化，无法解析配置路径。" +
                "WPF 主程序应由 PFApplicationBase 构造函数自动完成；" +
                "PF.SecsGem.Service 需在 appsettings.json 中配置 ProjectName。");

        /// <summary>视觉系统根目录</summary>
        public static string VisionPath           => Path.Combine(ConfigPath, "Vision");

        /// <summary>.hdev 算法文件目录（FileSystemWatcher 监听，支持热重载）</summary>
        public static string VisionProceduresPath => Path.Combine(VisionPath, "Procedures");

        /// <summary>管线 JSON 配置目录（FileSystemWatcher 监听，修改后自动刷新列表）</summary>
        public static string VisionWorkflowsPath  => Path.Combine(VisionPath, "Workflows");
    }
}
