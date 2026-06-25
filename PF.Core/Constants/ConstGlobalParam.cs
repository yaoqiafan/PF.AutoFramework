using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Constants
{
    /// <summary>
    /// 全局常量参数
    /// </summary>
    public static class ConstGlobalParam
    {
        /// <summary>整体配置的配置文件路径</summary>
        public static string ConfigPath => Path.Combine("D://PFConfig", "PFAutoFrameWork");

        /// <summary>视觉系统根目录</summary>
        public static string VisionPath           => Path.Combine(ConfigPath, "Vision");

        /// <summary>.hdev 算法文件目录（FileSystemWatcher 监听，支持热重载）</summary>
        public static string VisionProceduresPath => Path.Combine(VisionPath, "Procedures");

        /// <summary>管线 JSON 配置目录（FileSystemWatcher 监听，修改后自动刷新列表）</summary>
        public static string VisionWorkflowsPath  => Path.Combine(VisionPath, "Workflows");
    }
}
