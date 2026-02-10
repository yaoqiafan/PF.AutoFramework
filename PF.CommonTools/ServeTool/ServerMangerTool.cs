
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;

namespace PF.Infrastructure.Utilities.ServeTool
{
    public class ServerMangerTool
    {

        #region 软件启动权限

        /// <summary>
        /// 检查是否为管理员权限
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }



        /// <summary>
        /// 以管理员权限启动当前程序
        /// </summary>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        public static bool TryRestartAsAdministrator()
        {
            try
            {
                if (IsAdministrator())
                {
                    return true; // 已经是管理员权限，无需重启
                }
                var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exeName))
                {
                    return false;
                }
                var startInfo = new System.Diagnostics.ProcessStartInfo(exeName)
                {
                    UseShellExecute = true,
                    Verb = "runas" // 提升权限为管理员
                };
                try
                {
                    Process.Start(startInfo);
                    Environment.Exit(0); // 关闭当前普通权限进程
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // 用户取消UAC授权时触发
                    if (ex.NativeErrorCode == 1223)
                    {
                        LogService.Instance.Info("你取消了管理员权限授权，程序将以普通模式运行");
                    }
                    else
                    {
                        LogService.Instance.Info($"重启程序失败：{ex.Message}");
                    }
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        #endregion 软件启动权限



        #region 服务管控方法

        /// <summary>
        /// 判断指定Windows服务是否处于运行状态
        /// </summary>
        /// <param name="serviceName">服务名称（不是显示名！）</param>
        /// <returns>true=运行中，false=未运行/不存在/无权限</returns>
        [SupportedOSPlatform("windows")]
        public static bool IsServiceRunning(string serviceName)
        {
            // 空值校验
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName), "服务名称不能为空");
            }

            ServiceController service = null;
            try
            {
                // 实例化ServiceController，指定服务名
                service = new ServiceController(serviceName);

                // 获取服务状态，判断是否为Running
                return service.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException ex)
            {
                // 异常：服务不存在/权限不足/服务控制器未运行
                LogService.Instance.Error($"查询服务失败：{ex.Message}");
                return false;
            }
            finally
            {
                // 释放资源
                service?.Dispose();
            }
        }


        /// <summary>
        /// 安装指定路径的Windows服务
        /// </summary>
        /// <param name="serviceName">服务名称（唯一标识）</param>
        /// <param name="displayName">服务显示名</param>
        /// <param name="serviceExePath">服务可执行文件完整路径（如D:\MyService\MyService.exe）</param>
        /// <param name="startType">启动类型：auto（自动）、manual（手动）、disabled（禁用）</param>
        /// <returns>安装是否成功</returns>
        [SupportedOSPlatform("windows")]
        public static bool InstallService(string serviceName, string displayName, string serviceExePath, string startType = "auto")
        {
            // 1. 权限检查
            if (!IsAdministrator())
            {
                LogService.Instance.Warn("错误：需要管理员权限才能安装服务！");
                return false;
            }

            // 2. 路径合法性检查
            if (!File.Exists(serviceExePath))
            {
                LogService.Instance.Warn($"错误：指定的服务程序不存在！路径：{serviceExePath}");
                return false;
            }

            // 3. 构造sc create命令（注意参数格式：等号后必须加空格）
            string arguments = $"create \"{serviceName}\" binPath= \"{serviceExePath}\" displayname= \"{displayName}\" start= {startType}";

            try
            {
                // 4. 执行sc命令
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    CreateNoWindow = true, // 不显示命令窗口
                    UseShellExecute = false,
                    RedirectStandardOutput = true, // 捕获输出
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    // 5. 检查执行结果
                    if (process.ExitCode == 0)
                    {
                        LogService.Instance.Info($"服务【{serviceName}】安装成功！");
                        LogService.Instance.Info($"输出信息：{output}");
                        return true;
                    }
                    else
                    {
                        LogService.Instance.Info($"服务安装失败！错误码：{process.ExitCode}");
                        LogService.Instance.Info($"错误信息：{error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Info($"安装服务时发生异常：{ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 卸载指定名称的Windows服务
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>卸载是否成功</returns>
        [SupportedOSPlatform("windows")]
        public static bool UninstallService(string serviceName)
        {
            if (!IsAdministrator())
            {
                LogService.Instance.Warn("错误：需要管理员权限才能卸载服务！");
                return false;
            }

            // 构造sc delete命令
            string arguments = $"delete \"{serviceName}\"";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        LogService.Instance.Info($"服务【{serviceName}】卸载成功！");
                        LogService.Instance.Info($"输出信息：{output}");
                        return true;
                    }
                    else
                    {
                        LogService.Instance.Info($"服务卸载失败！错误码：{process.ExitCode}");
                        LogService.Instance.Info($"错误信息：{error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"卸载服务时发生异常：{ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 启动指定的Windows服务
        /// </summary>
        /// <param name="serviceName">服务名称（服务名，非显示名）</param>
        /// <param name="timeoutSeconds">等待服务启动的超时时间（秒）</param>
        /// <returns>true=启动成功/已运行，false=启动失败</returns>
        [SupportedOSPlatform("windows")]
        public static bool StartWindowsService(string serviceName, int timeoutSeconds = 30)
        {
            // 空值校验
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName), "服务名称不能为空");
            }

            ServiceController service = null;
            try
            {
                // 实例化服务控制器
                service = new ServiceController(serviceName);
                // 刷新最新状态
                service.Refresh();

                // 1. 校验当前状态，避免无效操作
                switch (service.Status)
                {
                    case ServiceControllerStatus.Running:
                        LogService.Instance.Info($"服务【{serviceName}】已处于运行状态，无需启动");
                        return true;
                    case ServiceControllerStatus.StartPending:
                        LogService.Instance.Info($"服务【{serviceName}】正在启动中");
                        return true;
                    case ServiceControllerStatus.Stopped:
                        // 2. 停止状态下执行启动
                        LogService.Instance.Info($"开始启动服务【{serviceName}】...");
                        service.Start();

                        // 3. 等待服务启动完成（超时则判定失败）
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(timeoutSeconds));

                        // 再次刷新状态，确认启动成功
                        service.Refresh();
                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            LogService.Instance.Info($"服务【{serviceName}】启动成功");
                            return true;
                        }
                        else
                        {
                            LogService.Instance.Info($"服务【{serviceName}】启动超时/状态异常，当前状态：{service.Status}");
                            return false;
                        }
                    default:
                        // 暂停、停止中、暂停中等状态
                        LogService.Instance.Info($"服务【{serviceName}】当前状态为{service.Status}，无法直接启动");
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                // 异常：服务不存在/服务已标记为删除/服务无法启动
                LogService.Instance.Warn($"启动服务失败：{ex.Message}");
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 异常：权限不足（需管理员权限）/服务依赖缺失
                LogService.Instance.Warn($"系统权限不足/服务依赖缺失：{ex.Message}");
                return false;
            }
            catch (System.TimeoutException ex)
            {
                // 异常：启动超时
                LogService.Instance.Warn($"服务启动超时（{timeoutSeconds}秒）：{ex.Message}");
                return false;
            }
            finally
            {
                // 释放资源
                service?.Dispose();
            }
        }


        /// <summary>
        /// 判断指定名称的Windows服务是否安装
        /// </summary>
        /// <param name="serviceName">服务名称（非显示名称）</param>
        /// <returns>已安装返回true，未安装返回false</returns>
       [SupportedOSPlatform("windows")]
        public static bool IsWindowsServiceInstalled(string serviceName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    return false;
                }

                // 获取系统中所有已安装的服务
                ServiceController[] allServices = ServiceController.GetServices();

                // 遍历查找匹配的服务名称
                foreach (ServiceController service in allServices)
                {
                    if (service.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"获取电脑服务失败",null, ex);
                return false;
            }
            // 空值校验

        }

        #endregion 服务管控方法



    }
}
