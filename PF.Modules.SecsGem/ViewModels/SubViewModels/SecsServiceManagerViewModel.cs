using PF.CommonTools.ServeTool;
using PF.UI.Infrastructure.PrismBase;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels.SubViewModels
{
    /// <summary>
    /// 负责 Windows 服务的安装、卸载、启动和状态刷新。
    /// </summary>
    public class SecsServiceManagerViewModel : ViewModelBase
    {
        private readonly SecsLogViewModel _log;

        /// <summary>初始化实例</summary>
        public SecsServiceManagerViewModel(SecsLogViewModel log)
        {
            _log = log;

            RefreshServiceStatusCommand = new DelegateCommand(ExecuteRefreshServiceStatus);
            InstallServiceCommand       = new DelegateCommand(ExecuteInstallService);
            UninstallServiceCommand     = new DelegateCommand(async () => await ExecuteUninstallServiceAsync());
            StartServiceCommand         = new DelegateCommand(ExecuteStartService);
        }

        // ── 服务状态属性 ───────────────────────────────────────────────────────

        private string _serviceStatusText = "未知";
        /// <summary>获取或设置服务状态文本</summary>
        public string ServiceStatusText
        {
            get => _serviceStatusText;
            set => SetProperty(ref _serviceStatusText, value);
        }

        private string _serviceStatusColor = "#9E9E9E";
        /// <summary>获取或设置服务状态颜色</summary>
        public string ServiceStatusColor
        {
            get => _serviceStatusColor;
            set => SetProperty(ref _serviceStatusColor, value);
        }

        private string _serviceExePath = string.Empty;
        /// <summary>获取或设置服务可执行文件路径</summary>
        public string ServiceExePath
        {
            get => _serviceExePath;
            set => SetProperty(ref _serviceExePath, value);
        }

        private string _serviceNameForManagement = "MyDotNet8Service";
        /// <summary>获取或设置服务管理名称</summary>
        public string ServiceNameForManagement
        {
            get => _serviceNameForManagement;
            set => SetProperty(ref _serviceNameForManagement, value);
        }

        // ── 命令 ───────────────────────────────────────────────────────────────

        /// <summary>刷新服务状态命令</summary>
        public DelegateCommand RefreshServiceStatusCommand { get; }
        /// <summary>安装服务命令</summary>
        public DelegateCommand InstallServiceCommand       { get; }
        /// <summary>卸载服务命令</summary>
        public DelegateCommand UninstallServiceCommand     { get; }
        /// <summary>启动服务命令</summary>
        public DelegateCommand StartServiceCommand         { get; }

        // ── 命令实现 ───────────────────────────────────────────────────────────

        [SupportedOSPlatform("windows")]
        private void ExecuteRefreshServiceStatus()
        {
            try
            {
                bool installed = ServerMangerTool.IsWindowsServiceInstalled(ServiceNameForManagement);
                if (!installed)
                {
                    ServiceStatusText  = "未安装";
                    ServiceStatusColor = "#9E9E9E";
                    return;
                }
                bool running = ServerMangerTool.IsServiceRunning(ServiceNameForManagement);
                ServiceStatusText  = running ? "运行中" : "已停止";
                ServiceStatusColor = running ? "#4CAF50" : "#F44336";
            }
            catch (Exception ex)
            {
                ServiceStatusText  = $"查询失败: {ex.Message}";
                ServiceStatusColor = "#9E9E9E";
            }
        }

        [SupportedOSPlatform("windows")]
        private void ExecuteInstallService()
        {
            if (string.IsNullOrWhiteSpace(ServiceExePath))
            {
                MessageBox.Show("请先填写服务 EXE 文件路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ServerMangerTool.IsAdministrator())
            {
                MessageBox.Show("需要管理员权限才能安装服务，请以管理员身份运行程序。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            bool ok = ServerMangerTool.InstallService(ServiceNameForManagement, ServiceNameForManagement, ServiceExePath);
            _log.Append(null, ok ? $"服务 [{ServiceNameForManagement}] 安装成功" : $"服务 [{ServiceNameForManagement}] 安装失败", isSystem: true);
            ExecuteRefreshServiceStatus();
        }

        [SupportedOSPlatform("windows")]
        private async Task ExecuteUninstallServiceAsync()
        {
            var confirm = await MessageService.ShowMessageAsync(
                $"确定要卸载服务 [{ServiceNameForManagement}] 吗？",
                "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != ButtonResult.Yes) return;

            if (!ServerMangerTool.IsAdministrator())
            {
                MessageBox.Show("需要管理员权限才能卸载服务，请以管理员身份运行程序。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (ServerMangerTool.IsServiceRunning(ServiceNameForManagement))
                {
                    using var sc = new ServiceController(ServiceNameForManagement);
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
            }
            catch { /* 停止失败时仍尝试卸载 */ }

            bool ok = ServerMangerTool.UninstallService(ServiceNameForManagement);
            _log.Append(null, ok ? $"服务 [{ServiceNameForManagement}] 卸载成功" : $"服务 [{ServiceNameForManagement}] 卸载失败", isSystem: true);
            ExecuteRefreshServiceStatus();
        }

        [SupportedOSPlatform("windows")]
        private void ExecuteStartService()
        {
            if (!ServerMangerTool.IsAdministrator())
            {
                MessageBox.Show("需要管理员权限才能启动服务，请以管理员身份运行程序。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            bool ok = ServerMangerTool.StartWindowsService(ServiceNameForManagement);
            _log.Append(null, ok ? $"服务 [{ServiceNameForManagement}] 已启动" : $"服务 [{ServiceNameForManagement}] 启动失败", isSystem: true);
            ExecuteRefreshServiceStatus();
        }
    }
}
