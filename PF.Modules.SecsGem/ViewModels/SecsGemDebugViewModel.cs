using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Modules.SecsGem.ViewModels.SubViewModels;
using PF.UI.Infrastructure.PrismBase;
using Prism.Navigation.Regions;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// SECS/GEM 调试与配置中心主 ViewModel。
    /// 作为组合根，持有并暴露各单职责子 ViewModel 供 XAML 绑定；
    /// 自身仅保留导航生命周期和跨 VM 事件桥接逻辑。
    /// </summary>
    public class SecsGemDebugViewModel : RegionViewModelBase
    {
        private readonly ISecsGemManger _manager;

        // ── 子 ViewModel（公开供 XAML 绑定）──────────────────────────────────
        public SecsLogViewModel            Log            { get; }
        public SecsConnectionViewModel     Connection     { get; }
        public SecsCommandBuilderViewModel CommandBuilder { get; }
        public SecsParameterViewModel      Parameter      { get; }
        public SecsServiceManagerViewModel ServiceManager { get; }

        // ── 构造 ───────────────────────────────────────────────────────────────

        public SecsGemDebugViewModel(ISecsGemManger manager, ISecsGemDataBase db)
        {
            _manager = manager;

            Log            = new SecsLogViewModel();
            Connection     = new SecsConnectionViewModel(manager, db, Log);
            CommandBuilder = new SecsCommandBuilderViewModel(manager, db, Log, Connection);
            Parameter      = new SecsParameterViewModel(manager, db, Log,
                                 () => CommandBuilder.LoadCommandTreesAsync());
            ServiceManager = new SecsServiceManagerViewModel(Log);
        }

        // ── 导航生命周期 ───────────────────────────────────────────────────────

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            Connection.StartMonitoring();
            _manager.SecsGemClient.MessageReceived         += OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError    += OnFormulaValidateError;

            _ = Task.Run(async () =>
            {
                await CommandBuilder.LoadCommandTreesAsync();
                await Connection.CheckDbEmptyAsync();
                Parameter.LoadParamRows(0);
            });
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            Connection.StopMonitoring();
            _manager.SecsGemClient.MessageReceived         -= OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError    -= OnFormulaValidateError;
        }

        public override void Destroy()
        {
            base.Destroy();
            Connection.StopMonitoring();
            _manager.SecsGemClient.MessageReceived         -= OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError    -= OnFormulaValidateError;
        }

        // ── 跨 VM 事件桥接 ─────────────────────────────────────────────────────

        private void OnMessageReceived(object sender, SecsMessageReceivedEventArgs e)
        {
            if (e?.Message != null)
                Application.Current?.Dispatcher.Invoke(() => Log.AppendReceived(e.Message));
        }

        private void OnFormulaValidateError(object sender, FormulaValidateErrorEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
                Log.Append(null, $"⚠ Formula 校验错误: {e.ErrorMessage}", isSystem: true));
        }
    }
}
