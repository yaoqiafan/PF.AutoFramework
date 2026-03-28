using Microsoft.Win32;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.SecsGem.Tools;
using PF.SecsGem.DataBase.Entities.Variable;
using PF.Modules.SecsGem.Views;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PF.Core.Interfaces.SecsGem.Command;

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
            SaveParamCommand = new DelegateCommand(ExecuteSaveParam);
            ClearLogCommand = new DelegateCommand(() => TransactionLogs.Clear());

            SelectCommandLeafCommand = new DelegateCommand<CommandLeafViewModel>(OnCommandLeafSelected);
            AddNewCommandCommand = new DelegateCommand(ExecuteAddNewCommand);
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

        private void ExecuteSaveParam()
        {
            var paramType = _selectedParamIndex switch
            {
                1 => ParamType.Validate,
                2 => ParamType.Formula,
                _ => ParamType.System
            };

            // 如果是 System 参数，先将编辑中的 ParamRows 写回 SystemParam 对象
            if (paramType == ParamType.System)
                ApplySystemParamEdits();

            _manager.ParamsManager.SaveParam(paramType);
            AppendLog(null, $"参数已保存 ({paramType})", isSystem: true);
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

                bool removed = await commandStore.RemoveCommand(vm.Command.ID);
                if (!removed)
                {
                    await MessageService.ShowMessageAsync(
                        $"删除命令 [{vm.Command.Name}] 失败，请稍后重试。",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

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
            });
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
    }
}
