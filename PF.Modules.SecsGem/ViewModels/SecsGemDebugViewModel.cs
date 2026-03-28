using Microsoft.Win32;
using PF.CommonTools.ServeTool;
using System.Collections.Concurrent;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.SecsGem.Tools;
using PF.Modules.SecsGem.Views;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.System;
using PF.SecsGem.DataBase.Entities.Variable;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// SECS/GEM 调试与配置中心主 ViewModel
    /// </summary>
    public class SecsGemDebugViewModel : RegionViewModelBase
    {
        private readonly ISecsGemManger _manager;
        private readonly ISecsGemDataBase _db;

        // ──────────────────────────────────────────────
        // 构造
        // ──────────────────────────────────────────────

        public SecsGemDebugViewModel(ISecsGemManger manager, ISecsGemDataBase db)
        {
            _manager = manager;
            _db = db;

            IncentiveCommandsTree = new ObservableCollection<CommandGroupViewModel>();
            ResponseCommandsTree = new ObservableCollection<CommandGroupViewModel>();
            CurrentMessageNodes = new ObservableCollection<SecsNodeViewModel>();
            TransactionLogs = new ObservableCollection<TransactionLogEntry>();
            ParamRows = new ObservableCollection<ParamRowViewModel>();

            VidRows = new ObservableCollection<VidRowViewModel>();
            CeidRows = new ObservableCollection<CeidRowViewModel>();
            ReportIdRows = new ObservableCollection<ReportIdRowViewModel>();
            CommandIdRows = new ObservableCollection<CommandIdRowViewModel>();

            DbSystemRows = new ObservableCollection<ParamRowViewModel>();
            DbCommandIdRows = new ObservableCollection<CommandIdRowViewModel>();
            DbCeidRows = new ObservableCollection<CeidRowViewModel>();
            DbReportIdRows = new ObservableCollection<ReportIdRowViewModel>();
            DbVidRows = new ObservableCollection<VidRowViewModel>();
            DbIncentiveRows = new ObservableCollection<ParamRowViewModel>();
            DbResponseRows = new ObservableCollection<ParamRowViewModel>();

            InitializeCommand = new DelegateCommand(async () => await ExecuteInitializeAsync(), () => !IsInitializing)
                .ObservesProperty(() => IsInitializing);

            ConnectCommand = new DelegateCommand(async () => await ExecuteConnectAsync(), () => !IsConnected && !IsConnecting)
                .ObservesProperty(() => IsConnected)
                .ObservesProperty(() => IsConnecting);

            DisconnectCommand = new DelegateCommand(async () => await ExecuteDisconnectAsync(), () => IsConnected)
                .ObservesProperty(() => IsConnected);

            ImportCommandsCommand = new DelegateCommand(async () => await ExecuteImportCommandsAsync());
            ExportCommandsCommand = new DelegateCommand(async () => await ExecuteExportCommandsAsync());
            ReloadCommandsCommand = new DelegateCommand(async () => await ExecuteReloadCommandsAsync());

            UpdateVariablesCommand = new DelegateCommand(ExecuteUpdateVariables);

            SendCommand = new DelegateCommand(async () => await ExecuteSendAsync(),
                () => IsConnected)
                .ObservesProperty(() => IsConnected);

            ImportParamsCommand = new DelegateCommand(async () => await ExecuteImportParamsAsync());
            ExportParamsCommand = new DelegateCommand(async () => await ExecuteExportParamsAsync());
            SaveParamCommand = new DelegateCommand(async () => await ExecuteSaveParamAsync());
            ClearLogCommand = new DelegateCommand(() => TransactionLogs.Clear());

            SelectCommandLeafCommand = new DelegateCommand<CommandLeafViewModel>(OnCommandLeafSelected);
            AddNewCommandCommand = new DelegateCommand(ExecuteAddNewCommand);

            RefreshServiceStatusCommand = new DelegateCommand(ExecuteRefreshServiceStatus);
            InstallServiceCommand = new DelegateCommand(ExecuteInstallService);
            UninstallServiceCommand = new DelegateCommand(async () => await ExecuteUninstallServiceAsync());
            StartServiceCommand = new DelegateCommand(ExecuteStartService);

            ImportSystemParamCommand = new DelegateCommand(async () => await ExecuteImportSystemParamToDbAsync());
            ImportValidateParamCommand = new DelegateCommand(async () => await ExecuteImportValidateParamToDbAsync());
            ImportFormulaParamCommand = new DelegateCommand(async () => await ExecuteImportFormulaParamToDbAsync());
            RefreshDbViewCommand = new DelegateCommand(async () => await ExecuteRefreshDbViewAsync());

            SaveMessageCommand = new DelegateCommand(async () => await ExecuteSaveMessageAsync());

            AddVidCommand = new DelegateCommand(() => VidRows.Add(new VidRowViewModel { Description = "新VID", DataType = "U4", Value = "0", Comment = string.Empty }));
            DeleteVidCommand = new DelegateCommand(() => { if (SelectedVidRow != null) VidRows.Remove(SelectedVidRow); });
            AddCeidCommand = new DelegateCommand(() => CeidRows.Add(new CeidRowViewModel { Description = "新CEID", LinkReportIDs = string.Empty, Comment = string.Empty }));
            DeleteCeidCommand = new DelegateCommand(() => { if (SelectedCeidRow != null) CeidRows.Remove(SelectedCeidRow); });
            AddReportIdCommand = new DelegateCommand(() => ReportIdRows.Add(new ReportIdRowViewModel { Description = "新ReportID", LinkVIDs = string.Empty, Comment = string.Empty }));
            DeleteReportIdCommand = new DelegateCommand(() => { if (SelectedReportIdRow != null) ReportIdRows.Remove(SelectedReportIdRow); });
            AddCommandIdCommand = new DelegateCommand(() => CommandIdRows.Add(new CommandIdRowViewModel { Description = "新CommandID", RCMD = string.Empty, LinkVIDs = string.Empty, Comment = string.Empty }));
            DeleteCommandIdCommand = new DelegateCommand(() => { if (SelectedCommandIdRow != null) CommandIdRows.Remove(SelectedCommandIdRow); });
        }

        // ──────────────────────────────────────────────
        // 顶部工具栏
        // ──────────────────────────────────────────────

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RaisePropertyChanged(nameof(StatusColor));
                    RaisePropertyChanged(nameof(ConnectionStatusText));
                }
            }
        }

        private bool _isInitializing;
        public bool IsInitializing
        {
            get => _isInitializing;
            set => SetProperty(ref _isInitializing, value);
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }

        public string StatusColor => IsConnected ? "#4CAF50" : "#F44336";
        public string ConnectionStatusText => IsConnected ? "已连接 (Connected)" : "未连接 (Disconnected)";

        private bool _isDbEmpty;
        public bool IsDbEmpty
        {
            get => _isDbEmpty;
            set => SetProperty(ref _isDbEmpty, value);
        }

        private string _dbEmptyMessage;
        public string DbEmptyMessage
        {
            get => _dbEmptyMessage;
            set => SetProperty(ref _dbEmptyMessage, value);
        }

        // ──────────────────────────────────────────────
        // 左侧命令树
        // ──────────────────────────────────────────────

        public ObservableCollection<CommandGroupViewModel> IncentiveCommandsTree { get; }
        public ObservableCollection<CommandGroupViewModel> ResponseCommandsTree { get; }

        // ──────────────────────────────────────────────
        // 中间报文编辑器
        // ──────────────────────────────────────────────

        private string _currentCommandHeader = "当前选中: (请从左侧选择命令)";
        public string CurrentCommandHeader
        {
            get => _currentCommandHeader;
            set => SetProperty(ref _currentCommandHeader, value);
        }

        public ObservableCollection<SecsNodeViewModel> CurrentMessageNodes { get; }

        private bool _waitReply = true;
        public bool WaitReply
        {
            get => _waitReply;
            set => SetProperty(ref _waitReply, value);
        }

        private bool _isWBitWarningVisible;
        public bool IsWBitWarningVisible
        {
            get => _isWBitWarningVisible;
            set => SetProperty(ref _isWBitWarningVisible, value);
        }

        private SFCommand _currentCommand;

        // ──────────────────────────────────────────────
        // 右侧参数配置
        // ──────────────────────────────────────────────

        private int _selectedParamIndex;
        public int SelectedParamIndex
        {
            get => _selectedParamIndex;
            set
            {
                if (SetProperty(ref _selectedParamIndex, value))
                    LoadParamRows(value);
            }
        }

        /// <summary>
        /// 参数管理 Tab 内嵌 TabControl 的选中索引：
        /// 0=系统参数, 1=VID, 2=CEID, 3=ReportID, 4=CommandID
        /// </summary>
        private int _selectedParamTabIndex;
        public int SelectedParamTabIndex
        {
            get => _selectedParamTabIndex;
            set
            {
                SetProperty(ref _selectedParamTabIndex, value);
                // 0 → System(0), 1~4 → Validate(1)
                SelectedParamIndex = value == 0 ? 0 : 1;
            }
        }

        public ObservableCollection<ParamRowViewModel> ParamRows { get; }

        // ──────────────────────────────────────────────
        // 底部日志
        // ──────────────────────────────────────────────

        public ObservableCollection<TransactionLogEntry> TransactionLogs { get; }

        private bool _autoScrollLog = true;
        public bool AutoScrollLog
        {
            get => _autoScrollLog;
            set => SetProperty(ref _autoScrollLog, value);
        }

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        // ──────────────────────────────────────────────
        // 参数分类视图集合
        // ──────────────────────────────────────────────

        public ObservableCollection<VidRowViewModel> VidRows { get; }
        public ObservableCollection<CeidRowViewModel> CeidRows { get; }
        public ObservableCollection<ReportIdRowViewModel> ReportIdRows { get; }
        public ObservableCollection<CommandIdRowViewModel> CommandIdRows { get; }

        private VidRowViewModel _selectedVidRow;
        public VidRowViewModel SelectedVidRow { get => _selectedVidRow; set => SetProperty(ref _selectedVidRow, value); }

        private CeidRowViewModel _selectedCeidRow;
        public CeidRowViewModel SelectedCeidRow { get => _selectedCeidRow; set => SetProperty(ref _selectedCeidRow, value); }

        private ReportIdRowViewModel _selectedReportIdRow;
        public ReportIdRowViewModel SelectedReportIdRow { get => _selectedReportIdRow; set => SetProperty(ref _selectedReportIdRow, value); }

        private CommandIdRowViewModel _selectedCommandIdRow;
        public CommandIdRowViewModel SelectedCommandIdRow { get => _selectedCommandIdRow; set => SetProperty(ref _selectedCommandIdRow, value); }

        // ──────────────────────────────────────────────
        // 外围服务管理：Windows 服务状态
        // ──────────────────────────────────────────────

        private string _serviceStatusText = "未知";
        public string ServiceStatusText
        {
            get => _serviceStatusText;
            set => SetProperty(ref _serviceStatusText, value);
        }

        private string _serviceStatusColor = "#9E9E9E";
        public string ServiceStatusColor
        {
            get => _serviceStatusColor;
            set => SetProperty(ref _serviceStatusColor, value);
        }

        private string _serviceExePath = string.Empty;
        public string ServiceExePath
        {
            get => _serviceExePath;
            set => SetProperty(ref _serviceExePath, value);
        }

        private string _serviceNameForManagement = "SecsGemService";
        public string ServiceNameForManagement
        {
            get => _serviceNameForManagement;
            set => SetProperty(ref _serviceNameForManagement, value);
        }

        // ──────────────────────────────────────────────
        // 外围服务管理：数据库视图集合
        // ──────────────────────────────────────────────

        public ObservableCollection<ParamRowViewModel> DbSystemRows { get; }
        public ObservableCollection<CommandIdRowViewModel> DbCommandIdRows { get; }
        public ObservableCollection<CeidRowViewModel> DbCeidRows { get; }
        public ObservableCollection<ReportIdRowViewModel> DbReportIdRows { get; }
        public ObservableCollection<VidRowViewModel> DbVidRows { get; }
        public ObservableCollection<ParamRowViewModel> DbIncentiveRows { get; }
        public ObservableCollection<ParamRowViewModel> DbResponseRows { get; }

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        public DelegateCommand InitializeCommand { get; }
        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand ImportCommandsCommand { get; }
        public DelegateCommand ExportCommandsCommand { get; }
        public DelegateCommand ReloadCommandsCommand { get; }
        public DelegateCommand UpdateVariablesCommand { get; }
        public DelegateCommand SendCommand { get; }
        public DelegateCommand ImportParamsCommand { get; }
        public DelegateCommand ExportParamsCommand { get; }
        public DelegateCommand SaveParamCommand { get; }
        public DelegateCommand ClearLogCommand { get; }
        public DelegateCommand<CommandLeafViewModel> SelectCommandLeafCommand { get; }
        public DelegateCommand AddNewCommandCommand { get; }

        public DelegateCommand RefreshServiceStatusCommand { get; }
        public DelegateCommand InstallServiceCommand { get; }
        public DelegateCommand UninstallServiceCommand { get; }
        public DelegateCommand StartServiceCommand { get; }

        public DelegateCommand ImportSystemParamCommand { get; }
        public DelegateCommand ImportValidateParamCommand { get; }
        public DelegateCommand ImportFormulaParamCommand { get; }
        public DelegateCommand RefreshDbViewCommand { get; }

        public DelegateCommand SaveMessageCommand { get; }

        public DelegateCommand AddVidCommand { get; }
        public DelegateCommand DeleteVidCommand { get; }
        public DelegateCommand AddCeidCommand { get; }
        public DelegateCommand DeleteCeidCommand { get; }
        public DelegateCommand AddReportIdCommand { get; }
        public DelegateCommand DeleteReportIdCommand { get; }
        public DelegateCommand AddCommandIdCommand { get; }
        public DelegateCommand DeleteCommandIdCommand { get; }

        // ──────────────────────────────────────────────
        // 导航生命周期
        // ──────────────────────────────────────────────

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            // 同步连接状态
            IsConnected = _manager.IsConnected;

            // 订阅报文接收事件
            _manager.SecsGemClient.MessageReceived += OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError += OnFormulaValidateError;

            // 加载命令树 & 检查数据库
            _ = Task.Run(async () =>
            {
                await LoadCommandTreesAsync();
                await CheckDbEmptyAsync();
                LoadParamRows(_selectedParamIndex);
            });
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _manager.SecsGemClient.MessageReceived -= OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError -= OnFormulaValidateError;
        }

        public override void Destroy()
        {
            base.Destroy();
            _manager.SecsGemClient.MessageReceived -= OnMessageReceived;
            _manager.ParamsManager.FormulaValidateError -= OnFormulaValidateError;
        }

        // ──────────────────────────────────────────────
        // 命令实现：顶部工具栏
        // ──────────────────────────────────────────────

        private async Task ExecuteInitializeAsync()
        {
            IsInitializing = true;
            try
            {
                bool ok = await _manager.InitializeAsync();
                AppendLog(null, ok ? "初始化成功" : "初始化失败", isSystem: true);
                IsConnected = _manager.IsConnected;
            }
            catch (Exception ex)
            {
                AppendLog(null, $"初始化异常: {ex.Message}", isSystem: true);
            }
            finally
            {
                IsInitializing = false;
            }
        }

        private async Task ExecuteConnectAsync()
        {
            IsConnecting = true;
            try
            {
                bool ok = await _manager.ConnectAsync();
                IsConnected = ok;
                AppendLog(null, ok ? "连接成功" : "连接失败", isSystem: true);
            }
            catch (Exception ex)
            {
                AppendLog(null, $"连接异常: {ex.Message}", isSystem: true);
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task ExecuteDisconnectAsync()
        {
            try
            {
                await _manager.DisconnectAsync();
                IsConnected = false;
                AppendLog(null, "已断开连接", isSystem: true);
            }
            catch (Exception ex)
            {
                AppendLog(null, $"断开异常: {ex.Message}", isSystem: true);
            }
        }

        // ──────────────────────────────────────────────
        // 命令实现：左侧命令库
        // ──────────────────────────────────────────────

        private async Task ExecuteImportCommandsAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "导入命令配置 (Excel)",
                Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var formula = _manager.CommandManager.FormulaConfiguration;
                await NPOIHelper.LoadIncentiveCommandFromExcel(dlg.FileName, formula.IncentiveCommandDictionary);
                await NPOIHelper.LoadResponseCommandFromExcel(dlg.FileName, formula.ResponseCommandDictionary);
                await _manager.CommandManager.UPDataCommondCollection(formula);
                await LoadCommandTreesAsync();
                AppendLog(null, $"命令导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteExportCommandsAsync()
        {
            var dlg = new SaveFileDialog
            {
                Title = "导出命令配置 (Excel)",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"SecsGemCommands_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                await _manager.CommandManager.SaveIncentiveCommandsToExcelAsync(dlg.FileName);
                await _manager.CommandManager.SaveResponseCommandsToExcelAsync(dlg.FileName);
                AppendLog(null, $"命令导出完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteReloadCommandsAsync()
        {
            try
            {
                var formula = _manager.CommandManager.FormulaConfiguration;
                await _manager.CommandManager.ReloadAllCommandsAsync(formula);
                await LoadCommandTreesAsync();
                AppendLog(null, "命令库已重新加载", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────
        // 命令实现：中间报文编辑器
        // ──────────────────────────────────────────────

        private void ExecuteUpdateVariables()
        {
            if (_currentCommand?.Message == null) return;
            try
            {
                // 将 ViewModel 树序列化回报文
                var msg = BuildSecsGemMessage();
                // 用 MessageUpdater 查表填充变量节点
                _manager.MessageUpdater.UpdateVariableNodesWithVIDValues(msg);
                // 将更新后的报文同步回 ViewModel 树
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentMessageNodes.Clear();
                    if (msg.RootNode != null)
                    {
                        var rootVm = SecsNodeViewModel.FromNodeMessage(msg.RootNode);
                        SubscribeVidEvents(rootVm);
                        CurrentMessageNodes.Add(rootVm);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新变量失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteSendAsync()
        {
            if (_currentCommand == null) return;

            try
            {
                var msg = BuildSecsGemMessage();

                // W-Bit 校验
                if (msg.Function % 2 == 1 && WaitReply)
                    msg.WBit = true;

                IsWBitWarningVisible = msg.Function % 2 == 1 && !WaitReply;

                // 记录发送日志
                AppendLog(msg, direction: "→", isSystem: false);

                if (WaitReply && msg.WBit)
                {
                    string sysHex = BitConverter.ToString(msg.SystemBytes.ToArray()).Replace("-", "");
                    bool sent = await _manager.WaitSendMessageAsync(msg, sysHex);
                    if (!sent) AppendLog(null, "等待回复超时", isSystem: true);
                }
                else
                {
                    await _manager.SendMessageAsync(msg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteSaveMessageAsync()
        {
            if (_currentCommand == null) return;
            try
            {
                var updatedMsg = BuildSecsGemMessage();
                updatedMsg.WBit = WaitReply;
                _currentCommand.Message = updatedMsg;

                bool isIncentive = _currentCommand.Function % 2 == 1;
                var commandStore = isIncentive
                    ? _manager.CommandManager.IncentiveCommands
                    : _manager.CommandManager.ResponseCommands;

                await commandStore.UpdateCommandInfo(_currentCommand.Key, _currentCommand.Key, _currentCommand);

                if (isIncentive)
                {
                    var repo = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                    var all = (await repo.GetAllAsync()).ToList();
                    var entity = all.FirstOrDefault(e => e.ID == _currentCommand.ID);
                    if (entity != null)
                        await repo.RemoveAsync(entity);
                    await repo.AddAsync(_currentCommand.GetIncentiveEntityFormSFCommand());
                }
                else
                {
                    var repo = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                    var all = (await repo.GetAllAsync()).ToList();
                    var entity = all.FirstOrDefault(e => e.ID == _currentCommand.ID);
                    if (entity != null)
                        await repo.RemoveAsync(entity);
                    await repo.AddAsync(_currentCommand.GetResponseEntityFormSFCommand());
                }

                await _db.SaveChangesAsync();
                AppendLog(null, $"报文已保存: {_currentCommand.Key} {_currentCommand.Name}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存报文失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────
        // 命令实现：右侧参数配置
        // ──────────────────────────────────────────────

        private async Task ExecuteImportParamsAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "导入参数配置 (Excel)",
                Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var validateConfig = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(
                    ParamType.Validate, null);
                if (validateConfig == null)
                {
                    validateConfig = new ValidateConfiguration();
                    _manager.ParamsManager.SetParam(ParamType.Validate, validateConfig);
                }

                await NPOIHelper.LoadValidateFromExcel(dlg.FileName, validateConfig);
                await SaveValidateToDbAsync(validateConfig);
                LoadParamRows(_selectedParamIndex);
                await CheckDbEmptyAsync();
                AppendLog(null, $"参数导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteExportParamsAsync()
        {
            var dlg = new SaveFileDialog
            {
                Title = "导出参数配置 (Excel)",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"SecsGemParams_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var paramType = _selectedParamIndex switch
                {
                    1 => ParamType.Validate,
                    2 => ParamType.Formula,
                    _ => ParamType.System
                };

                if (paramType == ParamType.Validate)
                {
                    var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
                    if (cfg != null)
                        await Task.Run(() => NPOIHelper.SaveValidate(dlg.FileName, cfg));
                }
                else if (paramType == ParamType.Formula)
                {
                    await _manager.CommandManager.SaveAllCommandsToExcelAsync();
                }
                AppendLog(null, $"参数导出完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteSaveParamAsync()
        {
            var paramType = _selectedParamIndex switch
            {
                1 => ParamType.Validate,
                2 => ParamType.Formula,
                _ => ParamType.System
            };

            try
            {
                if (paramType == ParamType.System)
                {
                    ApplySystemParamEdits();
                    _manager.ParamsManager.SaveParam(paramType);
                    var sys = _manager.ParamsManager.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
                    if (sys != null) await SaveSystemToDbAsync(sys);
                }
                else if (paramType == ParamType.Validate)
                {
                    SyncValidateRowsToConfig();
                    var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
                    if (cfg != null) await SaveValidateToDbAsync(cfg);
                }
                else if (paramType == ParamType.Formula)
                {
                    _manager.ParamsManager.SaveParam(paramType);
                    await SaveFormulaToDbAsync();
                }

                AppendLog(null, $"参数已保存 ({paramType})", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存参数失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyncValidateRowsToConfig()
        {
            var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
            if (cfg == null) return;

            cfg.VIDS = new ConcurrentDictionary<string, VID>(
                VidRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Description))
                    .Select(r =>
                    {
                        var dt = Enum.TryParse<DataType>(r.DataType, out var parsed) ? parsed : DataType.U4;
                        var vid = new VID(r.Code, r.Description, dt) { Comment = r.Comment ?? string.Empty };
                        if (!string.IsNullOrWhiteSpace(r.Value)) vid.SetValue(r.Value);
                        return vid;
                    })
                    .ToDictionary(v => v.Description));

            cfg.CEIDS = new ConcurrentDictionary<string, CEID>(
                CeidRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Description))
                    .Select(r =>
                    {
                        var links = (r.LinkReportIDs ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => uint.TryParse(s.Trim(), out var n) ? n : 0u)
                            .Where(n => n > 0).ToArray();
                        return new CEID(r.Code, r.Description, links) { Comment = r.Comment ?? string.Empty };
                    })
                    .ToDictionary(c => c.Description));

            cfg.ReportIDS = new ConcurrentDictionary<string, ReportID>(
                ReportIdRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Description))
                    .Select(r =>
                    {
                        var links = (r.LinkVIDs ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => uint.TryParse(s.Trim(), out var n) ? n : 0u)
                            .Where(n => n > 0).ToArray();
                        return new ReportID(r.Code, r.Description, links) { Comment = r.Comment ?? string.Empty };
                    })
                    .ToDictionary(r => r.Description));

            cfg.CommandIDS = new ConcurrentDictionary<string, CommandID>(
                CommandIdRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Description))
                    .Select(r =>
                    {
                        var links = (r.LinkVIDs ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => uint.TryParse(s.Trim(), out var n) ? n : 0u)
                            .Where(n => n > 0).ToArray();
                        return new CommandID(r.Code, r.Description, links, r.RCMD ?? string.Empty, r.Description)
                               { Comment = r.Comment ?? string.Empty };
                    })
                    .ToDictionary(c => c.Description));
        }

        // ──────────────────────────────────────────────
        // 命令树加载与命令选中
        // ──────────────────────────────────────────────

        private async Task LoadCommandTreesAsync()
        {
            try
            {
                var incentiveList = await _manager.CommandManager.IncentiveCommands.GetAllCommands();
                var responseList = await _manager.CommandManager.ResponseCommands.GetAllCommands();

                var incentiveGroups = BuildCommandGroups(incentiveList, _manager.CommandManager.IncentiveCommands);
                var responseGroups = BuildCommandGroups(responseList, _manager.CommandManager.ResponseCommands);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IncentiveCommandsTree.Clear();
                    foreach (var g in incentiveGroups)
                        IncentiveCommandsTree.Add(g);

                    ResponseCommandsTree.Clear();
                    foreach (var g in responseGroups)
                        ResponseCommandsTree.Add(g);
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                    AppendLog(null, $"命令树加载失败: {ex.Message}", isSystem: true));
            }
        }

        private List<CommandGroupViewModel> BuildCommandGroups(
            List<SFCommand> commands,
            ISFCommand commandStore)
        {
            return commands
                .GroupBy(c => $"S{c.Stream}F{c.Function}")
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var first = g.First();
                    var group = new CommandGroupViewModel(first.Stream, first.Function, commandStore);
                    SubscribeGroupEvents(group);
                    foreach (var cmd in g)
                    {
                        var leaf = new CommandLeafViewModel(cmd);
                        var tree = commandStore == _manager.CommandManager.IncentiveCommands
                            ? IncentiveCommandsTree : ResponseCommandsTree;
                        SubscribeLeafEvents(leaf, group, tree);
                        group.Children.Add(leaf);
                    }
                    return group;
                })
                .ToList();
        }

        // ──────────────────────────────────────────────
        // 命令编辑 & 路由
        // ──────────────────────────────────────────────

        private void ExecuteAddNewCommand()
        {
            var p = new DialogParameters();
            p.Add("DefaultStream",   (uint)1);
            p.Add("DefaultFunction", (uint)1);
            p.Add("LockSF",         false);

            DialogService.ShowDialog("CommandEditDialog", p, result =>
            {
                if (result.Result != ButtonResult.OK) return;
                var stream = result.Parameters.GetValue<uint>("Stream");
                var func   = result.Parameters.GetValue<uint>("Function");
                var name   = result.Parameters.GetValue<string>("CommandName");
                _ = RouteAndAddCommandAsync(stream, func, name);
            });
        }

        private void SubscribeGroupEvents(CommandGroupViewModel group)
        {
            group.AddCommandFromGroupRequested += (defaultStream, defaultFunc) =>
            {
                var p = new DialogParameters();
                p.Add("DefaultStream",   defaultStream);
                p.Add("DefaultFunction", defaultFunc);
                p.Add("LockSF",         false);

                DialogService.ShowDialog("CommandEditDialog", p, result =>
                {
                    if (result.Result != ButtonResult.OK) return;
                    var stream = result.Parameters.GetValue<uint>("Stream");
                    var func   = result.Parameters.GetValue<uint>("Function");
                    var name   = result.Parameters.GetValue<string>("CommandName");
                    _ = RouteAndAddCommandAsync(stream, func, name);
                });
            };
        }

        private void SubscribeLeafEvents(
            CommandLeafViewModel leaf,
            CommandGroupViewModel parentGroup,
            ObservableCollection<CommandGroupViewModel> tree)
        {
            leaf.DeleteRequested += async vm =>
            {
                var confirm = await MessageService.ShowMessageAsync(
                    $"确定要删除命令 [{vm.Command.Name}] 吗？",
                    "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != ButtonResult.Yes) return;

                var commandStore = tree == IncentiveCommandsTree
                    ? _manager.CommandManager.IncentiveCommands
                    : _manager.CommandManager.ResponseCommands;

                // 使用 Key (S{n}F{n}) 从内存字典中删除
                bool removed = await commandStore.RemoveCommand(vm.Command.Key);
                if (!removed)
                {
                    await MessageService.ShowMessageAsync(
                        $"删除命令 [{vm.Command.Name}] 失败，请稍后重试。",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 从数据库删除
                bool isIncentive = tree == IncentiveCommandsTree;
                await PersistRemoveCommandFromDbAsync(vm.Command.ID, isIncentive);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    parentGroup.Children.Remove(vm);
                    if (parentGroup.Children.Count == 0)
                        tree.Remove(parentGroup);
                });
            };
        }

        private async Task RouteAndAddCommandAsync(uint stream, uint function, string name)
        {
            var commandStore = function % 2 == 1
                ? _manager.CommandManager.IncentiveCommands
                : _manager.CommandManager.ResponseCommands;
            var tree = function % 2 == 1
                ? IncentiveCommandsTree
                : ResponseCommandsTree;

            var newCommand = new SFCommand
            {
                Stream   = stream,
                Function = function,
                Name     = name,
                ID       = Guid.NewGuid().ToString("N")[..8],
                Message  = new SecsGemMessage
                {
                    Stream      = (int)stream,
                    Function    = (int)function,
                    WBit        = function % 2 == 1,
                    SystemBytes = new List<byte> { 0, 0, 0, 0 },
                    MessageId   = Guid.NewGuid().ToString(),
                    IsIncoming  = false,
                    RootNode    = new SecsGemNodeMessage
                    {
                        DataType = DataType.LIST,
                        Length   = 0,
                        SubNode  = new List<SecsGemNodeMessage>()
                    }
                }
            };

            bool added = await commandStore.AddCommand(newCommand);
            if (!added)
            {
                await MessageService.ShowMessageAsync(
                    $"添加命令失败，该命令可能已存在。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 持久化到数据库
            await PersistAddCommandToDbAsync(newCommand, function % 2 == 1);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var group = tree.FirstOrDefault(g => g.Stream == stream && g.Function == function);
                if (group == null)
                {
                    group = new CommandGroupViewModel(stream, function, commandStore);
                    SubscribeGroupEvents(group);
                    tree.Add(group);
                }
                var leaf = new CommandLeafViewModel(newCommand);
                SubscribeLeafEvents(leaf, group, tree);
                group.Children.Add(leaf);
            });
        }

        /// <summary>
        /// 点击叶子节点：将命令的报文结构加载到中间编辑器
        /// </summary>
        public void OnCommandLeafSelected(CommandLeafViewModel leaf)
        {
            if (leaf?.Command == null) return;
            _currentCommand = leaf.Command;
            CurrentCommandHeader = $"当前选中: S{leaf.Stream}F{leaf.Function}  {leaf.Command.Name}";

            // 自动设置 WaitReply
            WaitReply = leaf.IsRequest;

            // 重建节点树
            CurrentMessageNodes.Clear();
            var msg = leaf.Command.Message;
            if (msg?.RootNode != null)
            {
                var rootVm = SecsNodeViewModel.FromNodeMessage(msg.RootNode);
                SubscribeVidEvents(rootVm);
                CurrentMessageNodes.Add(rootVm);
            }

            // 刷新 W-Bit 警告
            IsWBitWarningVisible = false;
        }

        // ──────────────────────────────────────────────
        // 报文构建
        // ──────────────────────────────────────────────

        private SecsGemMessage BuildSecsGemMessage()
        {
            var msg = new SecsGemMessage
            {
                Stream = _currentCommand?.Message?.Stream ?? 1,
                Function = _currentCommand?.Message?.Function ?? 1,
                WBit = WaitReply,
                MessageId = Guid.NewGuid().ToString(),
                SystemBytes = new List<byte> { 0, 0, 0, 0 },
                IsIncoming = false,
            };

            if (CurrentMessageNodes.Count > 0)
                msg.RootNode = CurrentMessageNodes[0].ToNodeMessage();

            return msg;
        }

        // ──────────────────────────────────────────────
        // VID 事件订阅与处理
        // ──────────────────────────────────────────────

        private void SubscribeVidEvents(SecsNodeViewModel vm)
        {
            if (vm == null) return;
            vm.VidSelectionRequested += OnVidSelectionRequested;
            vm.NodeAddRequested      += OnNodeAddRequested;
            foreach (var child in vm.Children)
                SubscribeVidEvents(child);
        }

        private void OnNodeAddRequested(object sender, EventArgs e)
        {
            if (sender is not SecsNodeViewModel parentNode) return;

            List<VIDEntity> vids;
            try
            {
                var vidRepo = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                vids = vidRepo.GetAllAsync().GetAwaiter().GetResult().ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取变量库失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var p = new DialogParameters();
            p.Add("Vids", (IEnumerable<VIDEntity>)vids);

            DialogService.ShowDialog("SecsNodeConfigDialog", p, result =>
            {
                if (result.Result != ButtonResult.OK) return;

                var dt    = result.Parameters.GetValue<DataType>("DataType");
                var isVar = result.Parameters.GetValue<bool>("IsVariableNode");
                var code  = result.Parameters.GetValue<uint>("VariableCode");
                var desc  = result.Parameters.GetValue<string>("VariableDescription");
                var val   = result.Parameters.GetValue<string>("Value");

                var newNode = new SecsNodeViewModel { DataType = dt, Value = val };
                if (isVar)
                    newNode.SetVariableBinding(code, desc);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SubscribeVidEvents(newNode);
                    parentNode.Children.Add(newNode);
                });
            });
        }

        private void OnVidSelectionRequested(object sender, EventArgs e)
        {
            if (sender is not SecsNodeViewModel nodeVm) return;

            try
            {
                // 从数据库获取所有 VID
                var vidRepo = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                var vids = vidRepo.GetAllAsync().GetAwaiter().GetResult().ToList();

                if (!vids.Any())
                {
                    MessageBox.Show("变量库 (VID) 为空，请先导入参数配置。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    nodeVm.IsVariableNode = false;
                    return;
                }

                var p = new DialogParameters();
                p.Add("Vids", (IEnumerable<VIDEntity>)vids);

                DialogService.ShowDialog("VidSelectDialog", p, result =>
                {
                    if (result.Result != ButtonResult.OK)
                    {
                        nodeVm.IsVariableNode = false;
                        return;
                    }
                    

                    var selectedVid = result.Parameters.GetValue<VIDEntity>("SelectedVid");
                    if (selectedVid != null)
                    {
                        nodeVm.VariableCode = selectedVid.Code;
                        nodeVm.VariableDescription = $"VID:{selectedVid.Code} [{selectedVid.Description}]";
                    }
                    else 
                    {
                        nodeVm.IsVariableNode = false;
                    }
                   
                });

                

               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"VID 选择失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                nodeVm.IsVariableNode = false;
            }
        }

        // ──────────────────────────────────────────────
        // 参数行加载
        // ──────────────────────────────────────────────

        private void LoadParamRows(int index)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ParamRows.Clear();
                try
                {
                    switch (index)
                    {
                        case 0: LoadSystemParamRows(); break;
                        case 1: LoadValidateParamRows(); break;
                        case 2: LoadFormulaParamRows(); break;
                    }
                }
                catch (Exception ex)
                {
                    ParamRows.Add(new ParamRowViewModel
                    {
                        Name = "错误",
                        Value = ex.Message,
                        DataType = "-",
                        Description = "加载参数失败"
                    });
                }

                // 同步刷新分类集合
                var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
                LoadVidRows(cfg);
                LoadCeidRows(cfg);
                LoadReportIdRows(cfg);
                LoadCommandIdRows(cfg);
            });
        }

        private void LoadVidRows(ValidateConfiguration cfg)
        {
            VidRows.Clear();
            if (cfg == null) return;
            foreach (var vid in cfg.VIDS?.Values ?? Enumerable.Empty<VID>())
            {
                VidRows.Add(new VidRowViewModel
                {
                    Code = vid.ID,
                    Description = vid.Description,
                    DataType = vid.DataType.ToString(),
                    Value = vid.Value?.ToString() ?? string.Empty,
                    Comment = vid.Comment ?? string.Empty
                });
            }
        }

        private void LoadCeidRows(ValidateConfiguration cfg)
        {
            CeidRows.Clear();
            if (cfg == null) return;
            foreach (var ceid in cfg.CEIDS?.Values ?? Enumerable.Empty<CEID>())
            {
                CeidRows.Add(new CeidRowViewModel
                {
                    Code = ceid.ID,
                    Description = ceid.Description,
                    LinkReportIDs = string.Join(", ", ceid.LinkReportID),
                    Comment = ceid.Comment ?? string.Empty
                });
            }
        }

        private void LoadReportIdRows(ValidateConfiguration cfg)
        {
            ReportIdRows.Clear();
            if (cfg == null) return;
            foreach (var report in cfg.ReportIDS?.Values ?? Enumerable.Empty<ReportID>())
            {
                ReportIdRows.Add(new ReportIdRowViewModel
                {
                    Code = report.ID,
                    Description = report.Description,
                    LinkVIDs = string.Join(", ", report.LinkVID),
                    Comment = report.Comment ?? string.Empty
                });
            }
        }

        private void LoadCommandIdRows(ValidateConfiguration cfg)
        {
            CommandIdRows.Clear();
            if (cfg == null) return;
            foreach (var cmd in cfg.CommandIDS?.Values ?? Enumerable.Empty<CommandID>())
            {
                CommandIdRows.Add(new CommandIdRowViewModel
                {
                    Code = cmd.ID,
                    Description = cmd.Description,
                    RCMD = cmd.RCMD ?? string.Empty,
                    LinkVIDs = string.Join(", ", cmd.LinkVID),
                    Comment = cmd.Comment ?? string.Empty
                });
            }
        }

        private void LoadSystemParamRows()
        {
            var sys = _manager.ParamsManager.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
            if (sys == null)
            {
                ParamRows.Add(new ParamRowViewModel { Name = "提示", Value = "系统参数为空，请先初始化或导入", DataType = "-", Description = "" });
                return;
            }

            ParamRows.Add(new ParamRowViewModel { Name = "ServiceName", Value = sys.ServiceName, DataType = "String", Description = "服务名称" });
            ParamRows.Add(new ParamRowViewModel { Name = "IPAddress", Value = sys.IPAddress, DataType = "String", Description = "连接 IP 地址" });
            ParamRows.Add(new ParamRowViewModel { Name = "Port", Value = sys.Port.ToString(), DataType = "Int", Description = "端口号" });
            ParamRows.Add(new ParamRowViewModel { Name = "DeviceID", Value = sys.DeviceID, DataType = "String", Description = "设备 ID" });
            ParamRows.Add(new ParamRowViewModel { Name = "MDLN", Value = sys.MDLN, DataType = "String", Description = "设备型号" });
            ParamRows.Add(new ParamRowViewModel { Name = "SOFTREV", Value = sys.SOFTREV, DataType = "String", Description = "软件版本" });
            ParamRows.Add(new ParamRowViewModel { Name = "AutoStart", Value = sys.AutoStart.ToString(), DataType = "Bool", Description = "自动启动" });
            ParamRows.Add(new ParamRowViewModel { Name = "T3 (ms)", Value = sys.T3.ToString(), DataType = "Int", Description = "回复等待超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T5 (ms)", Value = sys.T5.ToString(), DataType = "Int", Description = "连接超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T6 (ms)", Value = sys.T6.ToString(), DataType = "Int", Description = "控制消息超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T7 (ms)", Value = sys.T7.ToString(), DataType = "Int", Description = "未连接超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T8 (ms)", Value = sys.T8.ToString(), DataType = "Int", Description = "网络字节超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "BeatInterval (ms)", Value = sys.BeatInterval.ToString(), DataType = "Int", Description = "心跳间隔" });
        }

        private void LoadValidateParamRows()
        {
            var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
            if (cfg == null)
            {
                ParamRows.Add(new ParamRowViewModel { Name = "提示", Value = "Validate 参数为空，请先导入", DataType = "-", Description = "" });
                return;
            }

            foreach (var vid in cfg.VIDS?.Values ?? Enumerable.Empty<VID>())
            {
                ParamRows.Add(new ParamRowViewModel
                {
                    Name = $"VID:{vid.ID} {vid.Description}",
                    Value = vid.Value?.ToString() ?? string.Empty,
                    DataType = vid.DataType.ToString(),
                    Description = vid.Comment ?? string.Empty
                });
            }

            foreach (var ceid in cfg.CEIDS?.Values ?? Enumerable.Empty<CEID>())
            {
                ParamRows.Add(new ParamRowViewModel
                {
                    Name = $"CEID:{ceid.ID} {ceid.Description}",
                    Value = ceid.Key ?? string.Empty,
                    DataType = "CEID",
                    Description = ceid.Comment ?? string.Empty
                });
            }
        }

        private void LoadFormulaParamRows()
        {
            var formula = _manager.CommandManager.FormulaConfiguration;
            if (formula == null)
            {
                ParamRows.Add(new ParamRowViewModel { Name = "提示", Value = "Formula 参数为空", DataType = "-", Description = "" });
                return;
            }

            foreach (var kvp in formula.IncentiveCommandDictionary ?? new System.Collections.Concurrent.ConcurrentDictionary<string, SFCommand>())
            {
                ParamRows.Add(new ParamRowViewModel
                {
                    Name = kvp.Key,
                    Value = kvp.Value.Name,
                    DataType = "Incentive",
                    Description = $"S{kvp.Value.Stream}F{kvp.Value.Function}"
                });
            }

            foreach (var kvp in formula.ResponseCommandDictionary ?? new System.Collections.Concurrent.ConcurrentDictionary<string, SFCommand>())
            {
                ParamRows.Add(new ParamRowViewModel
                {
                    Name = kvp.Key,
                    Value = kvp.Value.Name,
                    DataType = "Response",
                    Description = $"S{kvp.Value.Stream}F{kvp.Value.Function}"
                });
            }
        }

        private void ApplySystemParamEdits()
        {
            var sys = _manager.ParamsManager.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
            if (sys == null) return;

            foreach (var row in ParamRows)
            {
                try
                {
                    switch (row.Name)
                    {
                        case "IPAddress": sys.IPAddress = row.Value; break;
                        case "Port": if (int.TryParse(row.Value, out int port)) sys.Port = port; break;
                        case "DeviceID": sys.DeviceID = row.Value; break;
                        case "MDLN": sys.MDLN = row.Value; break;
                        case "SOFTREV": sys.SOFTREV = row.Value; break;
                        case "ServiceName": sys.ServiceName = row.Value; break;
                        case "AutoStart": if (bool.TryParse(row.Value, out bool autoStart)) sys.AutoStart = autoStart; break;
                        case "T3 (ms)": if (int.TryParse(row.Value, out int t3)) sys.T3 = t3; break;
                        case "T5 (ms)": if (int.TryParse(row.Value, out int t5)) sys.T5 = t5; break;
                        case "T6 (ms)": if (int.TryParse(row.Value, out int t6)) sys.T6 = t6; break;
                        case "T7 (ms)": if (int.TryParse(row.Value, out int t7)) sys.T7 = t7; break;
                        case "T8 (ms)": if (int.TryParse(row.Value, out int t8)) sys.T8 = t8; break;
                        case "BeatInterval (ms)": if (int.TryParse(row.Value, out int bi)) sys.BeatInterval = bi; break;
                    }
                }
                catch { /* 单个字段解析失败不影响其他字段 */ }
            }
        }

        // ──────────────────────────────────────────────
        // 数据库空库检测
        // ──────────────────────────────────────────────

        private async Task CheckDbEmptyAsync()
        {
            try
            {
                var vidRepo = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                int vidCount = await vidRepo.CountAsync();

                var sysParam = _manager.ParamsManager.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
                bool sysEmpty = sysParam == null || string.IsNullOrEmpty(sysParam.IPAddress);

                bool isEmpty = vidCount == 0 || sysEmpty;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsDbEmpty = isEmpty;
                    DbEmptyMessage = isEmpty
                        ? "⚠  系统参数或变量库 (VID) 为空，请先从右侧面板导入配置文件"
                        : string.Empty;
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsDbEmpty = true;
                    DbEmptyMessage = $"⚠  数据库检测异常: {ex.Message}";
                });
            }
        }

        // ──────────────────────────────────────────────
        // 事件处理
        // ──────────────────────────────────────────────

        private void OnMessageReceived(object sender, SecsMessageReceivedEventArgs e)
        {
            if (e?.Message == null) return;
            var msg = e.Message;
            var entry = CreateLogEntry(msg, "←");
            Application.Current?.Dispatcher.Invoke(() => TransactionLogs.Add(entry));
        }

        private void OnFormulaValidateError(object sender, FormulaValidateErrorEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                AppendLog(null,
                    $"⚠ Formula 校验错误: {e.ErrorMessage}",
                    isSystem: true);
            });
        }

        // ──────────────────────────────────────────────
        // 日志工具
        // ──────────────────────────────────────────────

        private void AppendLog(SecsGemMessage msg, string direction = null, bool isSystem = false)
        {
            TransactionLogEntry entry;
            if (isSystem || msg == null)
            {
                entry = new TransactionLogEntry
                {
                    Timestamp = DateTime.Now,
                    Direction = direction ?? "ℹ",
                    Header = direction ?? "SYS",
                    RawHex = string.Empty,
                    SmlText = msg?.ToString() ?? direction,
                    IsIncoming = false
                };
            }
            else
            {
                entry = CreateLogEntry(msg, direction ?? "→");
            }
            TransactionLogs.Add(entry);
        }

        private static TransactionLogEntry CreateLogEntry(SecsGemMessage msg, string direction)
        {
            string rawHex = msg.SystemBytes != null
                ? BitConverter.ToString(msg.SystemBytes.ToArray()).Replace("-", " ")
                : string.Empty;

            string header = $"S{msg.Stream}F{msg.Function}" + (msg.WBit ? " W" : string.Empty);

            return new TransactionLogEntry
            {
                Timestamp = DateTime.Now,
                Direction = direction,
                Header = header,
                RawHex = rawHex,
                SmlText = msg.ToString(),
                IsIncoming = msg.IsIncoming,
            };
        }

        // ──────────────────────────────────────────────
        // 数据库持久化辅助方法
        // ──────────────────────────────────────────────

        private async Task PersistAddCommandToDbAsync(SFCommand cmd, bool isIncentive)
        {
            try
            {
                if (isIncentive)
                {
                    var repo = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                    await repo.AddAsync(cmd.GetIncentiveEntityFormSFCommand());
                }
                else
                {
                    var repo = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                    await repo.AddAsync(cmd.GetResponseEntityFormSFCommand());
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppendLog(null, $"命令持久化失败: {ex.Message}", isSystem: true);
            }
        }

        private async Task PersistRemoveCommandFromDbAsync(string cmdId, bool isIncentive)
        {
            try
            {
                if (isIncentive)
                {
                    var repo = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                    var entity = (await repo.FindAsync(e => e.ID == cmdId)).FirstOrDefault();
                    if (entity != null)
                    {
                        await repo.RemoveAsync(entity);
                        await _db.SaveChangesAsync();
                    }
                }
                else
                {
                    var repo = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                    var entity = (await repo.FindAsync(e => e.ID == cmdId)).FirstOrDefault();
                    if (entity != null)
                    {
                        await repo.RemoveAsync(entity);
                        await _db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog(null, $"命令删除持久化失败: {ex.Message}", isSystem: true);
            }
        }

        private async Task SaveValidateToDbAsync(ValidateConfiguration cfg)
        {
            try
            {
                var vidRepo = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                var allVids = await vidRepo.GetAllAsync();
                await vidRepo.RemoveRangeAsync(allVids);
                await vidRepo.AddRangeAsync(cfg.VIDS.Values.Select(v => v.ToEntity()));

                var ceidRepo = _db.GetRepository<CEIDEntity>(SecsDbSet.CEIDs);
                var allCeids = await ceidRepo.GetAllAsync();
                await ceidRepo.RemoveRangeAsync(allCeids);
                await ceidRepo.AddRangeAsync(cfg.CEIDS.Values.Select(c => c.ToEntity()));

                var reportRepo = _db.GetRepository<ReportIDEntity>(SecsDbSet.ReportIDs);
                var allReports = await reportRepo.GetAllAsync();
                await reportRepo.RemoveRangeAsync(allReports);
                await reportRepo.AddRangeAsync(cfg.ReportIDS.Values.Select(r => r.ToEntity()));

                var cmdRepo = _db.GetRepository<CommandIDEntity>(SecsDbSet.CommnadIDs);
                var allCmds = await cmdRepo.GetAllAsync();
                await cmdRepo.RemoveRangeAsync(allCmds);
                await cmdRepo.AddRangeAsync(cfg.CommandIDS.Values.Select(c => c.ToEntity()));

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppendLog(null, $"Validate 参数持久化失败: {ex.Message}", isSystem: true);
            }
        }

        private async Task SaveSystemToDbAsync(SecsGemSystemParam sys)
        {
            try
            {
                var repo = _db.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                var all = await repo.GetAllAsync();
                await repo.RemoveRangeAsync(all);
                await repo.AddAsync(sys.ToEntity());
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppendLog(null, $"System 参数持久化失败: {ex.Message}", isSystem: true);
            }
        }

        private async Task SaveFormulaToDbAsync()
        {
            try
            {
                var formula = _manager.CommandManager.FormulaConfiguration;
                if (formula == null) return;

                var incRepo = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                var allInc = await incRepo.GetAllAsync();
                await incRepo.RemoveRangeAsync(allInc);
                await incRepo.AddRangeAsync(formula.IncentiveCommandDictionary.Values
                    .Select(c => c.GetIncentiveEntityFormSFCommand()));

                var resRepo = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                var allRes = await resRepo.GetAllAsync();
                await resRepo.RemoveRangeAsync(allRes);
                await resRepo.AddRangeAsync(formula.ResponseCommandDictionary.Values
                    .Select(c => c.GetResponseEntityFormSFCommand()));

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppendLog(null, $"Formula 参数持久化失败: {ex.Message}", isSystem: true);
            }
        }

        // ──────────────────────────────────────────────
        // 外围服务管理：Windows 服务操作
        // ──────────────────────────────────────────────

        [SupportedOSPlatform("windows")]
        private void ExecuteRefreshServiceStatus()
        {
            try
            {
                bool installed = ServerMangerTool.IsWindowsServiceInstalled(ServiceNameForManagement);
                if (!installed)
                {
                    ServiceStatusText = "未安装";
                    ServiceStatusColor = "#9E9E9E";
                    return;
                }
                bool running = ServerMangerTool.IsServiceRunning(ServiceNameForManagement);
                ServiceStatusText = running ? "运行中" : "已停止";
                ServiceStatusColor = running ? "#4CAF50" : "#F44336";
            }
            catch (Exception ex)
            {
                ServiceStatusText = $"查询失败: {ex.Message}";
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
            AppendLog(null, ok ? $"服务 [{ServiceNameForManagement}] 安装成功" : $"服务 [{ServiceNameForManagement}] 安装失败", isSystem: true);
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

            // 若服务正在运行，先停止
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
            AppendLog(null, ok ? $"服务 [{ServiceNameForManagement}] 卸载成功" : $"服务 [{ServiceNameForManagement}] 卸载失败", isSystem: true);
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
            AppendLog(null, ok ? $"服务 [{ServiceNameForManagement}] 已启动" : $"服务 [{ServiceNameForManagement}] 启动失败", isSystem: true);
            ExecuteRefreshServiceStatus();
        }

        // ──────────────────────────────────────────────
        // 外围服务管理：数据库导入操作
        // ──────────────────────────────────────────────

        private async Task ExecuteImportSystemParamToDbAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "导入系统参数 (JSON)",
                Filter = "JSON 文件 (*.json)|*.json"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var param = new SecsGemSystemParam();
                if (await param.Load(dlg.FileName))
                {
                    _manager.ParamsManager.SetParam(ParamType.System, param);
                    await SaveSystemToDbAsync(param);
                    await ExecuteRefreshDbViewAsync();
                    AppendLog(null, $"系统参数导入完成: {dlg.FileName}", isSystem: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteImportValidateParamToDbAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "导入变量参数 (Excel)",
                Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var cfg = new ValidateConfiguration();
                await NPOIHelper.LoadValidateFromExcel(dlg.FileName, cfg);
                _manager.ParamsManager.SetParam(ParamType.Validate, cfg);
                await SaveValidateToDbAsync(cfg);
                LoadParamRows(_selectedParamIndex);
                await ExecuteRefreshDbViewAsync();
                AppendLog(null, $"变量参数导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteImportFormulaParamToDbAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "导入配方参数 (Excel)",
                Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var formula = _manager.CommandManager.FormulaConfiguration ?? new FormulaConfiguration();
                await NPOIHelper.LoadIncentiveCommandFromExcel(dlg.FileName, formula.IncentiveCommandDictionary);
                await NPOIHelper.LoadResponseCommandFromExcel(dlg.FileName, formula.ResponseCommandDictionary);
                await _manager.CommandManager.UPDataCommondCollection(formula);
                await SaveFormulaToDbAsync();
                await LoadCommandTreesAsync();
                await ExecuteRefreshDbViewAsync();
                AppendLog(null, $"配方参数导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteRefreshDbViewAsync()
        {
            try
            {
                var sysRepo = _db.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                var sysEntities = await sysRepo.GetAllAsync();

                var cmdIdRepo = _db.GetRepository<CommandIDEntity>(SecsDbSet.CommnadIDs);
                var cmdIdEntities = await cmdIdRepo.GetAllAsync();

                var ceidRepo = _db.GetRepository<CEIDEntity>(SecsDbSet.CEIDs);
                var ceidEntities = await ceidRepo.GetAllAsync();

                var reportRepo = _db.GetRepository<ReportIDEntity>(SecsDbSet.ReportIDs);
                var reportEntities = await reportRepo.GetAllAsync();

                var vidRepo = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                var vidEntities = await vidRepo.GetAllAsync();

                var incRepo = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                var incEntities = await incRepo.GetAllAsync();

                var resRepo = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                var resEntities = await resRepo.GetAllAsync();

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    DbSystemRows.Clear();
                    foreach (var e in sysEntities)
                    {
                        var p = e.ToParam();
                        DbSystemRows.Add(new ParamRowViewModel { Name = "ServiceName", Value = p.ServiceName, DataType = "String", Description = "服务名称" });
                        DbSystemRows.Add(new ParamRowViewModel { Name = "IPAddress", Value = p.IPAddress, DataType = "String", Description = "IP 地址" });
                        DbSystemRows.Add(new ParamRowViewModel { Name = "Port", Value = p.Port.ToString(), DataType = "Int", Description = "端口" });
                        DbSystemRows.Add(new ParamRowViewModel { Name = "DeviceID", Value = p.DeviceID, DataType = "String", Description = "设备ID" });
                    }

                    DbCommandIdRows.Clear();
                    foreach (var e in cmdIdEntities)
                    {
                        var c = e.ToCommandID();
                        DbCommandIdRows.Add(new CommandIdRowViewModel { Code = c.ID, Description = c.Description, RCMD = c.RCMD, LinkVIDs = string.Join(", ", c.LinkVID), Comment = c.Comment ?? string.Empty });
                    }

                    DbCeidRows.Clear();
                    foreach (var e in ceidEntities)
                    {
                        var c = e.ToCEID();
                        DbCeidRows.Add(new CeidRowViewModel { Code = c.ID, Description = c.Description, LinkReportIDs = string.Join(", ", c.LinkReportID), Comment = c.Comment ?? string.Empty });
                    }

                    DbReportIdRows.Clear();
                    foreach (var e in reportEntities)
                    {
                        var r = e.ToReportID();
                        DbReportIdRows.Add(new ReportIdRowViewModel { Code = r.ID, Description = r.Description, LinkVIDs = string.Join(", ", r.LinkVID), Comment = r.Comment ?? string.Empty });
                    }

                    DbVidRows.Clear();
                    foreach (var e in vidEntities)
                    {
                        var v = e.ToVID();
                        DbVidRows.Add(new VidRowViewModel { Code = v.ID, Description = v.Description, DataType = v.DataType.ToString(), Value = v.Value?.ToString() ?? string.Empty, Comment = v.Comment ?? string.Empty });
                    }

                    DbIncentiveRows.Clear();
                    foreach (var e in incEntities)
                    {
                        var c = e.GetSFCommandFormIncentiveEntity();
                        DbIncentiveRows.Add(new ParamRowViewModel { Name = c.Key, Value = c.Name, DataType = "Incentive", Description = c.ID });
                    }

                    DbResponseRows.Clear();
                    foreach (var e in resEntities)
                    {
                        var c = e.GetSFCommandFormResponseEntity();
                        DbResponseRows.Add(new ParamRowViewModel { Name = c.Key, Value = c.Name, DataType = "Response", Description = c.ID });
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog(null, $"数据库视图刷新失败: {ex.Message}", isSystem: true);
            }
        }
    }
}
