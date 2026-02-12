using Microsoft.Extensions.DependencyInjection;
using PF.Application.Shell.CustomConfiguration.Logging;
using PF.Common.Core.PrismBase;
using PF.Core.Constants;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using PF.UI.Controls;
using PF.UI.Shared.Data;
using Prism.Navigation.Regions;
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

            
           
        }

        private void OnNavigated(FunctionEventArgs<object> args)
        {
            if (args != null)
            {
                if (args.Info is SideMenuItem sideMenuItem)
                {
                    RegionManager.RegisterViewWithRegion(NavigationConstants.Regions.SoftwareViewRegion, sideMenuItem.Tag.ToString());
                } 
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



        public ICommand LoadCommand { get; set; }
        public ICommand SwitchItemCmd { get; set; }

        private async void OnLoading()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();

            _dbLogger = CategoryLoggerFactory.Database(_logService);
            _systemLogger = CategoryLoggerFactory.System(_logService);
            _custom = CategoryLoggerFactory.Custom(_logService);

            Assembly assembly = Assembly.GetEntryAssembly();
            string name = $"{await _paramService.GetParamAsync<string>("SoftWareName")}_V{assembly.GetName().Version}";
            SoftWareName = name;

            name = await _paramService.GetParamAsync<string>("COName");
            CoName = name;

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
