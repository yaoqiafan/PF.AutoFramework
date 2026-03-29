using Microsoft.Win32;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Infrastructure.SecsGem.Tools;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.Variable;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels.SubViewModels
{
    /// <summary>
    /// 负责左侧命令树的加载/导入/导出，以及中间报文编辑器的所有逻辑。
    /// </summary>
    public class SecsCommandBuilderViewModel : ViewModelBase
    {
        private readonly ISecsGemManger _manager;
        private readonly ISecsGemDataBase _db;
        private readonly SecsLogViewModel _log;
        private readonly SecsConnectionViewModel _connection;

        private SFCommand _currentCommand;

        public SecsCommandBuilderViewModel(
            ISecsGemManger manager,
            ISecsGemDataBase db,
            SecsLogViewModel log,
            SecsConnectionViewModel connection)
        {
            _manager    = manager;
            _db         = db;
            _log        = log;
            _connection = connection;

            IncentiveCommandsTree = new ObservableCollection<CommandGroupViewModel>();
            ResponseCommandsTree  = new ObservableCollection<CommandGroupViewModel>();
            CurrentMessageNodes   = new ObservableCollection<SecsNodeViewModel>();

            ImportCommandsCommand  = new DelegateCommand(async () => await ExecuteImportCommandsAsync());
            ExportCommandsCommand  = new DelegateCommand(async () => await ExecuteExportCommandsAsync());
            ReloadCommandsCommand  = new DelegateCommand(async () => await ExecuteReloadCommandsAsync());
            UpdateVariablesCommand = new DelegateCommand(ExecuteUpdateVariables);
            SaveMessageCommand     = new DelegateCommand(async () => await ExecuteSaveMessageAsync());
            AddNewCommandCommand   = new DelegateCommand(ExecuteAddNewCommand);

            SendCommand = new DelegateCommand(
                async () => await ExecuteSendAsync(),
                () => _connection.IsConnected);

            _connection.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SecsConnectionViewModel.IsConnected))
                    SendCommand.RaiseCanExecuteChanged();
            };

            SelectCommandLeafCommand = new DelegateCommand<CommandLeafViewModel>(OnCommandLeafSelected);
        }

        // ── 左侧命令树 ─────────────────────────────────────────────────────────
        public ObservableCollection<CommandGroupViewModel> IncentiveCommandsTree { get; }
        public ObservableCollection<CommandGroupViewModel> ResponseCommandsTree  { get; }

        // ── 中间报文编辑器 ─────────────────────────────────────────────────────

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

        // ── 命令 ───────────────────────────────────────────────────────────────
        public DelegateCommand ImportCommandsCommand  { get; }
        public DelegateCommand ExportCommandsCommand  { get; }
        public DelegateCommand ReloadCommandsCommand  { get; }
        public DelegateCommand UpdateVariablesCommand { get; }
        public DelegateCommand SendCommand            { get; }
        public DelegateCommand SaveMessageCommand     { get; }
        public DelegateCommand AddNewCommandCommand   { get; }
        public DelegateCommand<CommandLeafViewModel> SelectCommandLeafCommand { get; }

        // ── 命令树加载 ─────────────────────────────────────────────────────────

        public async Task LoadCommandTreesAsync()
        {
            try
            {
                var incentiveList = await _manager.CommandManager.IncentiveCommands.GetAllCommands();
                var responseList  = await _manager.CommandManager.ResponseCommands.GetAllCommands();

                var incentiveGroups = BuildCommandGroups(incentiveList, _manager.CommandManager.IncentiveCommands);
                var responseGroups  = BuildCommandGroups(responseList,  _manager.CommandManager.ResponseCommands);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IncentiveCommandsTree.Clear();
                    foreach (var g in incentiveGroups) IncentiveCommandsTree.Add(g);

                    ResponseCommandsTree.Clear();
                    foreach (var g in responseGroups) ResponseCommandsTree.Add(g);
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                    _log.Append(null, $"命令树加载失败: {ex.Message}", isSystem: true));
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

        // ── 命令选中与叶子事件 ─────────────────────────────────────────────────

        public void OnCommandLeafSelected(CommandLeafViewModel leaf)
        {
            if (leaf?.Command == null) return;
            _currentCommand      = leaf.Command;
            CurrentCommandHeader = $"当前选中: S{leaf.Stream}F{leaf.Function}  {leaf.Command.Name}";
            WaitReply            = leaf.IsRequest;

            CurrentMessageNodes.Clear();
            var msg = leaf.Command.Message;
            if (msg?.RootNode != null)
            {
                var rootVm = SecsNodeViewModel.FromNodeMessage(msg.RootNode);
                SubscribeVidEvents(rootVm);
                CurrentMessageNodes.Add(rootVm);
            }
            IsWBitWarningVisible = false;
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
                    "添加命令失败，该命令可能已存在。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

        // ── 命令导入/导出/重载 ─────────────────────────────────────────────────

        private async Task ExecuteImportCommandsAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "导入命令配置 (Excel)",
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
                _log.Append(null, $"命令导入完成: {dlg.FileName}", isSystem: true);
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
                Title    = "导出命令配置 (Excel)",
                Filter   = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"SecsGemCommands_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                await _manager.CommandManager.SaveIncentiveCommandsToExcelAsync(dlg.FileName);
                await _manager.CommandManager.SaveResponseCommandsToExcelAsync(dlg.FileName);
                _log.Append(null, $"命令导出完成: {dlg.FileName}", isSystem: true);
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
                _log.Append(null, "命令库已重新加载", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        // ── 报文编辑器 ─────────────────────────────────────────────────────────

        private void ExecuteUpdateVariables()
        {
            if (_currentCommand?.Message == null) return;
            try
            {
                var msg = BuildSecsGemMessage();
                _manager.MessageUpdater.UpdateVariableNodesWithVIDValues(msg);
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
                if (msg.Function % 2 == 1 && WaitReply) msg.WBit = true;
                IsWBitWarningVisible = msg.Function % 2 == 1 && !WaitReply;

                _log.Append(msg, "→", isSystem: false);

                if (WaitReply && msg.WBit)
                {
                    string sysHex =SecsGemMessageTools.ByteArrayToHexStringWithSeparator(msg.SystemBytes.ToArray());
                    bool sent = await _manager.WaitSendMessageAsync(msg, sysHex);
                    if (!sent) _log.Append(null, "等待回复超时", isSystem: true);
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
                    var repo   = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                    var all    = (await repo.GetAllAsync()).ToList();
                    var entity = all.FirstOrDefault(e => e.ID == _currentCommand.ID);
                    if (entity != null) await repo.RemoveAsync(entity);
                    await repo.AddAsync(_currentCommand.GetIncentiveEntityFormSFCommand());
                }
                else
                {
                    var repo   = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                    var all    = (await repo.GetAllAsync()).ToList();
                    var entity = all.FirstOrDefault(e => e.ID == _currentCommand.ID);
                    if (entity != null) await repo.RemoveAsync(entity);
                    await repo.AddAsync(_currentCommand.GetResponseEntityFormSFCommand());
                }

                await _db.SaveChangesAsync();
                _log.Append(null, $"报文已保存: {_currentCommand.Key} {_currentCommand.Name}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存报文失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private SecsGemMessage BuildSecsGemMessage()
        {
            var msg = new SecsGemMessage
            {
                Stream      = _currentCommand?.Message?.Stream   ?? 1,
                Function    = _currentCommand?.Message?.Function ?? 1,
                WBit        = WaitReply,
                MessageId   = Guid.NewGuid().ToString(),
                SystemBytes = SecsGemMessageTools.GenerateSystemBytes(),
                IsIncoming  = false
            };
            if (CurrentMessageNodes.Count > 0)
                msg.RootNode = CurrentMessageNodes[0].ToNodeMessage();
            return msg;
        }

        // ── VID 事件 ───────────────────────────────────────────────────────────

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
                vids = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs)
                          .GetAllAsync().GetAwaiter().GetResult().ToList();
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
                if (isVar) newNode.SetVariableBinding(code, desc);

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
                var vids = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs)
                              .GetAllAsync().GetAwaiter().GetResult().ToList();

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
                        nodeVm.VariableCode        = selectedVid.Code;
                        nodeVm.VariableDescription = $"{selectedVid.Code}";
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

        // ── 数据库持久化辅助 ───────────────────────────────────────────────────

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
                _log.Append(null, $"命令持久化失败: {ex.Message}", isSystem: true);
            }
        }

        private async Task PersistRemoveCommandFromDbAsync(string cmdId, bool isIncentive)
        {
            try
            {
                if (isIncentive)
                {
                    var repo   = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                    var entity = (await repo.FindAsync(e => e.ID == cmdId)).FirstOrDefault();
                    if (entity != null) { await repo.RemoveAsync(entity); await _db.SaveChangesAsync(); }
                }
                else
                {
                    var repo   = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                    var entity = (await repo.FindAsync(e => e.ID == cmdId)).FirstOrDefault();
                    if (entity != null) { await repo.RemoveAsync(entity); await _db.SaveChangesAsync(); }
                }
            }
            catch (Exception ex)
            {
                _log.Append(null, $"命令删除持久化失败: {ex.Message}", isSystem: true);
            }
        }
    }
}
