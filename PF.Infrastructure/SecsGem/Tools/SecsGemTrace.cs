using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;

namespace PF.Infrastructure.SecsGem.Tools
{
    /// <summary>
    /// SECS/GEM 实现内部的共用日志入口。
    /// <para>此前命令管理、Excel 配置导入、VID 更新等处的诊断信息全部写 Console.WriteLine——
    /// WPF 主程序与 Windows 服务都没有控制台，配置导入时哪几行被跳过、命令更新为何失败，现场无从查起。
    /// 这些代码多为静态工具类，无法逐个注入 ILogService，故由持有日志服务的组件（InternalClient）在构造时
    /// 调用 <see cref="Attach"/> 接上；未接上时退化为 Trace 输出，不影响功能。</para>
    /// </summary>
    internal static class SecsGemTrace
    {
        private static CategoryLogger? _logger;

        /// <summary>接上系统日志（重复调用以最后一次为准）</summary>
        public static void Attach(ILogService? logService)
        {
            if (logService != null) _logger = CategoryLoggerFactory.SecsGem(logService);
        }

        /// <summary>失败、跳过、校验不通过等需要现场关注的信息</summary>
        public static void Warn(string message)
        {
            if (_logger != null) _logger.Warn(message);
            else System.Diagnostics.Trace.WriteLine($"[SecsGem][WARN] {message}");
        }

        /// <summary>过程信息（加载完成、保存路径等）</summary>
        public static void Debug(string message)
        {
            if (_logger != null) _logger.Debug(message);
            else System.Diagnostics.Trace.WriteLine($"[SecsGem] {message}");
        }
    }
}
