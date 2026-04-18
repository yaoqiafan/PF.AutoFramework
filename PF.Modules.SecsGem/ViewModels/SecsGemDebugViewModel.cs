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
        private readonly ISecsGemManager _manager;

        // ── 子 ViewModel（公开供 XAML 绑定）──────────────────────────────────
        /// <summary>获取日志视图模型</summary>
        public SecsLogViewModel            Log            { get; }
        /// <summary>获取连接视图模型</summary>
        public SecsConnectionViewModel     Connection     { get; }
        /// <summary>获取命令构建器视图模型</summary>
        public SecsCommandBuilderViewModel CommandBuilder { get; }
        /// <summary>获取参数视图模型</summary>
        public SecsParameterViewModel      Parameter      { get; }
        /// <summary>获取服务管理视图模型</summary>
        public SecsServiceManagerViewModel ServiceManager { get; }

        // ── 构造 ───────────────────────────────────────────────────────────────

        /// <summary>初始化实例</summary>
        public SecsGemDebugViewModel(ISecsGemManager manager, ISecsGemDataBase db)
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

        /// <summary>导航进入时调用</summary>
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

        /// <summary>导航离开时调用</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            Connection.StopMonitoring();
            _manager.SecsGemClient.MessageReceived         -= OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError    -= OnFormulaValidateError;
        }

        /// <summary>销毁视图模型</summary>
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
