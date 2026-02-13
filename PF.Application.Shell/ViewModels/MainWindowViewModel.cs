using Microsoft.Extensions.DependencyInjection;
using PF.Application.Shell.CustomConfiguration.Logging;
using PF.Core.Constants;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using PF.UI.Controls;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using System.Reflection;
using System.Windows.Input;

namespace PF.Application.Shell.ViewModels
{
    public class MainWindowViewModel : RegionViewModelBase
    {
        private readonly IParamService _paramService;
        private ILogService _logService;

        private CategoryLogger _dbLogger;
        private CategoryLogger _systemLogger;
        private CategoryLogger _custom;
        private CancellationTokenSource _cts;
        private Task _runningTask;

        public MainWindowViewModel(IParamService paramService)
        {
            _paramService = paramService;
            LoadCommand = new DelegateCommand(OnLoading);
            SwitchItemCmd = new DelegateCommand<FunctionEventArgs<object>>(OnNavigated);
            ChangeExpandCmd = new DelegateCommand<string>((e) => 
            {
                if (Enum.TryParse<ExpandMode>(e,out ExpandMode result))
                {
                    Expand = result;
                }
            });
        }

        private void OnNavigated(FunctionEventArgs<object> args)
        {
            if (args != null && args.Info is SideMenuItem sideMenuItem)
            {
                if (sideMenuItem.Tag != null)
                {
                    string viewName = sideMenuItem.Tag.ToString();

                    // 检查是否为参数管理相关的视图名称
                    // 这里假设 SideMenu 的 Tag 设置为具体的参数实体类型名称，如 "CommonParam"
                    if (IsParameterView(viewName))
                    {
                        var parameters = new NavigationParameters();
                        parameters.Add("TargetParamType", viewName);

                        // 统一导航到 ParameterView_SystemConfigParam，并传递具体的参数类型
                        RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.ParameterView, NavigationComplete, parameters);
                        return;
                    }

                    string category = NavigationConstantMapper.GetCategory(viewName);
                    switch (category)
                    {
                        case nameof(NavigationConstants.Views):
                            RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, viewName, NavigationComplete);
                            break;
                        case nameof(NavigationConstants.Dialogs):
                            DialogService.ShowDialog(NavigationConstants.Dialogs.LoginView, OnLoginOverCallback);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 判断是否为参数配置页面类型
        /// </summary>
        private bool IsParameterView(string viewName)
        {
            return viewName == NavigationConstants.Views.ParameterView_SystemConfigParam ||
                   viewName == NavigationConstants.Views.ParameterView_CommonParam ||
                    viewName == NavigationConstants.Views.ParameterView_UserLoginParam ||
                   viewName == NavigationConstants.Views.ParameterView_HardWareParam;
        }

        private void OnLoginOverCallback()
        {

        }

        // 可选：添加导航回调以处理错误
        private void NavigationComplete(NavigationResult result)
        {
            if (result.Success == false && result.Exception != null)
            {
                // 这里可以记录日志：导航失败
                _logService.Error($"导航失败: {result.Exception.Message}", "System", result.Exception);
            }
        }

        private string _SoftWareName = string.Empty;

        public string SoftWareName
        {
            get
            {
                return _SoftWareName;
            }
            set
            {
                SetProperty(ref _SoftWareName, value);
            }
        }

        private string _CoName = string.Empty;

        public string CoName
        {
            get
            {
                return _CoName;
            }
            set { SetProperty(ref _CoName, value); }
        }

        private string _sysTime = string.Empty;
        public string SysTime
        {
            get { return _sysTime; }
            set { SetProperty(ref _sysTime, value); }
        }


        private ExpandMode _ExpandMode = ExpandMode.ShowAll;
        public ExpandMode Expand
        {
            get { return _ExpandMode; }
            set { SetProperty(ref _ExpandMode, value); }
        }

        public ICommand LoadCommand { get; set; }
        public ICommand SwitchItemCmd { get; set; }
        public ICommand ChangeExpandCmd { get; set; }

        private async void OnLoading()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();

            _dbLogger = CategoryLoggerFactory.Database(_logService);
            _systemLogger = CategoryLoggerFactory.System(_logService);
            _custom = CategoryLoggerFactory.Custom(_logService);

            Assembly assembly = Assembly.GetEntryAssembly();
            // 注意：这里需要确保 GetParamAsync 调用安全，或者在 Loaded 后调用
            try
            {
                string name = $"{await _paramService.GetParamAsync<string>("SoftWareName")}_V{assembly.GetName().Version}";
                SoftWareName = name;

                name = await _paramService.GetParamAsync<string>("COName");
                CoName = name;
            }
            catch
            {
                // 忽略初始化时的异常或记录日志
            }

            UPdataTime();
        }

        #region 公共
        public void UPdataTime()
        {
            _cts = new CancellationTokenSource();
            _runningTask = Task.Factory.StartNew(
                () => WorkerMethod(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default
            );
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_runningTask != null)
            {
                await _runningTask;
            }
            _cts?.Dispose();
        }

        private async Task WorkerMethod(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SysTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    await Task.Delay(500, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
        }
        #endregion
    }
}