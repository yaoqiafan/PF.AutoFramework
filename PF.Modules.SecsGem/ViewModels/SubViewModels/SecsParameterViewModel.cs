using Microsoft.Win32;
using PF.Core.Entities.SecsGem.Command;
using PF.Core.Entities.SecsGem.Params;
using PF.Core.Entities.SecsGem.Params.FormulaParam;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.SecsGem.Tools;
using PF.Modules.SecsGem.ViewModels.Models;
using PF.Modules.SecsGem.ViewModels.Models.RowViewModel;
using PF.SecsGem.DataBase.Entities.Command;
using PF.SecsGem.DataBase.Entities.System;
using PF.SecsGem.DataBase.Entities.Variable;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.SecsGem.ViewModels.SubViewModels
{
    /// <summary>
    /// 负责右侧系统参数、VID/CEID/ReportID/CommandID 的加载、增删改查以及数据库持久化。
    /// </summary>
    public class SecsParameterViewModel : BindableBase
    {
        private readonly ISecsGemManger _manager;
        private readonly ISecsGemDataBase _db;
        private readonly SecsLogViewModel _log;
        private readonly Func<Task> _reloadCommandTrees;

        private int _selectedParamIndex;

        public SecsParameterViewModel(
            ISecsGemManger manager,
            ISecsGemDataBase db,
            SecsLogViewModel log,
            Func<Task> reloadCommandTrees)
        {
            _manager            = manager;
            _db                 = db;
            _log                = log;
            _reloadCommandTrees = reloadCommandTrees;

            ParamRows     = new ObservableCollection<ParamRowViewModel>();
            VidRows       = new ObservableCollection<VidRowViewModel>();
            CeidRows      = new ObservableCollection<CeidRowViewModel>();
            ReportIdRows  = new ObservableCollection<ReportIdRowViewModel>();
            CommandIdRows = new ObservableCollection<CommandIdRowViewModel>();

            DbSystemRows    = new ObservableCollection<ParamRowViewModel>();
            DbCommandIdRows = new ObservableCollection<CommandIdRowViewModel>();
            DbCeidRows      = new ObservableCollection<CeidRowViewModel>();
            DbReportIdRows  = new ObservableCollection<ReportIdRowViewModel>();
            DbVidRows       = new ObservableCollection<VidRowViewModel>();
            DbIncentiveRows = new ObservableCollection<ParamRowViewModel>();
            DbResponseRows  = new ObservableCollection<ParamRowViewModel>();

            ImportParamsCommand       = new DelegateCommand(async () => await ExecuteImportParamsAsync());
            ExportParamsCommand       = new DelegateCommand(async () => await ExecuteExportParamsAsync());
            SaveParamCommand          = new DelegateCommand(async () => await ExecuteSaveParamAsync());
            ImportSystemParamCommand  = new DelegateCommand(async () => await ExecuteImportSystemParamToDbAsync());
            ImportValidateParamCommand= new DelegateCommand(async () => await ExecuteImportValidateParamToDbAsync());
            ImportFormulaParamCommand = new DelegateCommand(async () => await ExecuteImportFormulaParamToDbAsync());
            RefreshDbViewCommand      = new DelegateCommand(async () => await ExecuteRefreshDbViewAsync());

            AddVidCommand       = new DelegateCommand(() => VidRows.Add(new VidRowViewModel      { Description = "新VID",       DataType = "U4",          Value = "0",          Comment = string.Empty }));
            DeleteVidCommand    = new DelegateCommand(() => { if (SelectedVidRow       != null) VidRows.Remove(SelectedVidRow); });
            AddCeidCommand      = new DelegateCommand(() => CeidRows.Add(new CeidRowViewModel     { Description = "新CEID",      LinkReportIDs = string.Empty, Comment = string.Empty }));
            DeleteCeidCommand   = new DelegateCommand(() => { if (SelectedCeidRow      != null) CeidRows.Remove(SelectedCeidRow); });
            AddReportIdCommand  = new DelegateCommand(() => ReportIdRows.Add(new ReportIdRowViewModel  { Description = "新ReportID",  LinkVIDs = string.Empty,      Comment = string.Empty }));
            DeleteReportIdCommand = new DelegateCommand(() => { if (SelectedReportIdRow  != null) ReportIdRows.Remove(SelectedReportIdRow); });
            AddCommandIdCommand = new DelegateCommand(() => CommandIdRows.Add(new CommandIdRowViewModel { Description = "新CommandID", RCMD = string.Empty,          LinkVIDs = string.Empty, Comment = string.Empty }));
            DeleteCommandIdCommand = new DelegateCommand(() => { if (SelectedCommandIdRow != null) CommandIdRows.Remove(SelectedCommandIdRow); });
        }

        // ── 参数 Tab 索引 ──────────────────────────────────────────────────────

        private int _selectedParamTabIndex;
        public int SelectedParamTabIndex
        {
            get => _selectedParamTabIndex;
            set
            {
                SetProperty(ref _selectedParamTabIndex, value);
                _selectedParamIndex = value == 0 ? 0 : 1;
                LoadParamRows(_selectedParamIndex);
            }
        }

        // ── 系统参数行集合 ─────────────────────────────────────────────────────
        public ObservableCollection<ParamRowViewModel> ParamRows { get; }

        // ── 分类参数集合 ───────────────────────────────────────────────────────
        public ObservableCollection<VidRowViewModel>       VidRows       { get; }
        public ObservableCollection<CeidRowViewModel>      CeidRows      { get; }
        public ObservableCollection<ReportIdRowViewModel>  ReportIdRows  { get; }
        public ObservableCollection<CommandIdRowViewModel> CommandIdRows { get; }

        private VidRowViewModel       _selectedVidRow;
        public  VidRowViewModel       SelectedVidRow       { get => _selectedVidRow;       set => SetProperty(ref _selectedVidRow,       value); }

        private CeidRowViewModel      _selectedCeidRow;
        public  CeidRowViewModel      SelectedCeidRow      { get => _selectedCeidRow;      set => SetProperty(ref _selectedCeidRow,      value); }

        private ReportIdRowViewModel  _selectedReportIdRow;
        public  ReportIdRowViewModel  SelectedReportIdRow  { get => _selectedReportIdRow;  set => SetProperty(ref _selectedReportIdRow,  value); }

        private CommandIdRowViewModel _selectedCommandIdRow;
        public  CommandIdRowViewModel SelectedCommandIdRow { get => _selectedCommandIdRow; set => SetProperty(ref _selectedCommandIdRow, value); }

        // ── 数据库镜像集合 ─────────────────────────────────────────────────────
        public ObservableCollection<ParamRowViewModel>     DbSystemRows    { get; }
        public ObservableCollection<CommandIdRowViewModel> DbCommandIdRows { get; }
        public ObservableCollection<CeidRowViewModel>      DbCeidRows      { get; }
        public ObservableCollection<ReportIdRowViewModel>  DbReportIdRows  { get; }
        public ObservableCollection<VidRowViewModel>       DbVidRows       { get; }
        public ObservableCollection<ParamRowViewModel>     DbIncentiveRows { get; }
        public ObservableCollection<ParamRowViewModel>     DbResponseRows  { get; }

        // ── 命令 ───────────────────────────────────────────────────────────────
        public DelegateCommand ImportParamsCommand        { get; }
        public DelegateCommand ExportParamsCommand        { get; }
        public DelegateCommand SaveParamCommand           { get; }
        public DelegateCommand ImportSystemParamCommand   { get; }
        public DelegateCommand ImportValidateParamCommand { get; }
        public DelegateCommand ImportFormulaParamCommand  { get; }
        public DelegateCommand RefreshDbViewCommand       { get; }
        public DelegateCommand AddVidCommand              { get; }
        public DelegateCommand DeleteVidCommand           { get; }
        public DelegateCommand AddCeidCommand             { get; }
        public DelegateCommand DeleteCeidCommand          { get; }
        public DelegateCommand AddReportIdCommand         { get; }
        public DelegateCommand DeleteReportIdCommand      { get; }
        public DelegateCommand AddCommandIdCommand        { get; }
        public DelegateCommand DeleteCommandIdCommand     { get; }

        // ── 参数行加载（供主 VM 在导航时调用）─────────────────────────────────

        public void LoadParamRows(int index)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ParamRows.Clear();
                try
                {
                    switch (index)
                    {
                        case 0: LoadSystemParamRows();   break;
                        case 1: LoadValidateParamRows(); break;
                        case 2: LoadFormulaParamRows();  break;
                    }
                }
                catch (Exception ex)
                {
                    ParamRows.Add(new ParamRowViewModel
                    {
                        Name        = "错误",
                        Value       = ex.Message,
                        DataType    = "-",
                        Description = "加载参数失败"
                    });
                }

                var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
                LoadVidRows(cfg);
                LoadCeidRows(cfg);
                LoadReportIdRows(cfg);
                LoadCommandIdRows(cfg);
            });
        }

        // ── 私有行加载辅助 ─────────────────────────────────────────────────────

        private void LoadSystemParamRows()
        {
            var sys = _manager.ParamsManager.GetParamOrDefault<SecsGemSystemParam>(ParamType.System, null);
            if (sys == null)
            {
                ParamRows.Add(new ParamRowViewModel { Name = "提示", Value = "系统参数为空，请先初始化或导入", DataType = "-", Description = "" });
                return;
            }
            ParamRows.Add(new ParamRowViewModel { Name = "ServiceName",         Value = sys.ServiceName,          DataType = "String", Description = "服务名称" });
            ParamRows.Add(new ParamRowViewModel { Name = "IPAddress",           Value = sys.IPAddress,            DataType = "String", Description = "连接 IP 地址" });
            ParamRows.Add(new ParamRowViewModel { Name = "Port",                Value = sys.Port.ToString(),      DataType = "Int",    Description = "端口号" });
            ParamRows.Add(new ParamRowViewModel { Name = "DeviceID",            Value = sys.DeviceID,             DataType = "String", Description = "设备 ID" });
            ParamRows.Add(new ParamRowViewModel { Name = "MDLN",                Value = sys.MDLN,                 DataType = "String", Description = "设备型号" });
            ParamRows.Add(new ParamRowViewModel { Name = "SOFTREV",             Value = sys.SOFTREV,              DataType = "String", Description = "软件版本" });
            ParamRows.Add(new ParamRowViewModel { Name = "AutoStart",           Value = sys.AutoStart.ToString(), DataType = "Bool",   Description = "自动启动" });
            ParamRows.Add(new ParamRowViewModel { Name = "T3 (ms)",             Value = sys.T3.ToString(),        DataType = "Int",    Description = "回复等待超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T5 (ms)",             Value = sys.T5.ToString(),        DataType = "Int",    Description = "连接超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T6 (ms)",             Value = sys.T6.ToString(),        DataType = "Int",    Description = "控制消息超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T7 (ms)",             Value = sys.T7.ToString(),        DataType = "Int",    Description = "未连接超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "T8 (ms)",             Value = sys.T8.ToString(),        DataType = "Int",    Description = "网络字节超时" });
            ParamRows.Add(new ParamRowViewModel { Name = "BeatInterval (ms)",   Value = sys.BeatInterval.ToString(), DataType = "Int", Description = "心跳间隔" });
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
                ParamRows.Add(new ParamRowViewModel { Name = $"VID:{vid.ID} {vid.Description}", Value = vid.Value?.ToString() ?? string.Empty, DataType = vid.DataType.ToString(), Description = vid.Comment ?? string.Empty });
            foreach (var ceid in cfg.CEIDS?.Values ?? Enumerable.Empty<CEID>())
                ParamRows.Add(new ParamRowViewModel { Name = $"CEID:{ceid.ID} {ceid.Description}", Value = ceid.Key ?? string.Empty, DataType = "CEID", Description = ceid.Comment ?? string.Empty });
        }

        private void LoadFormulaParamRows()
        {
            var formula = _manager.CommandManager.FormulaConfiguration;
            if (formula == null)
            {
                ParamRows.Add(new ParamRowViewModel { Name = "提示", Value = "Formula 参数为空", DataType = "-", Description = "" });
                return;
            }
            foreach (var kvp in formula.IncentiveCommandDictionary ?? new ConcurrentDictionary<string, SFCommand>())
                ParamRows.Add(new ParamRowViewModel { Name = kvp.Key, Value = kvp.Value.Name, DataType = "Incentive", Description = $"S{kvp.Value.Stream}F{kvp.Value.Function}" });
            foreach (var kvp in formula.ResponseCommandDictionary ?? new ConcurrentDictionary<string, SFCommand>())
                ParamRows.Add(new ParamRowViewModel { Name = kvp.Key, Value = kvp.Value.Name, DataType = "Response",  Description = $"S{kvp.Value.Stream}F{kvp.Value.Function}" });
        }

        private void LoadVidRows(ValidateConfiguration cfg)
        {
            VidRows.Clear();
            if (cfg == null) return;
            foreach (var vid in cfg.VIDS?.Values ?? Enumerable.Empty<VID>())
                VidRows.Add(new VidRowViewModel { Code = vid.ID, Description = vid.Description, DataType = vid.DataType.ToString(), Value = vid.Value?.ToString() ?? string.Empty, Comment = vid.Comment ?? string.Empty });
        }

        private void LoadCeidRows(ValidateConfiguration cfg)
        {
            CeidRows.Clear();
            if (cfg == null) return;
            foreach (var ceid in cfg.CEIDS?.Values ?? Enumerable.Empty<CEID>())
                CeidRows.Add(new CeidRowViewModel { Code = ceid.ID, Description = ceid.Description, LinkReportIDs = string.Join(", ", ceid.LinkReportID), Comment = ceid.Comment ?? string.Empty });
        }

        private void LoadReportIdRows(ValidateConfiguration cfg)
        {
            ReportIdRows.Clear();
            if (cfg == null) return;
            foreach (var report in cfg.ReportIDS?.Values ?? Enumerable.Empty<ReportID>())
                ReportIdRows.Add(new ReportIdRowViewModel { Code = report.ID, Description = report.Description, LinkVIDs = string.Join(", ", report.LinkVID), Comment = report.Comment ?? string.Empty });
        }

        private void LoadCommandIdRows(ValidateConfiguration cfg)
        {
            CommandIdRows.Clear();
            if (cfg == null) return;
            foreach (var cmd in cfg.CommandIDS?.Values ?? Enumerable.Empty<CommandID>())
                CommandIdRows.Add(new CommandIdRowViewModel { Code = cmd.ID, Description = cmd.Description, RCMD = cmd.RCMD ?? string.Empty, LinkVIDs = string.Join(", ", cmd.LinkVID), Comment = cmd.Comment ?? string.Empty });
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
                        case "IPAddress":           sys.IPAddress   = row.Value; break;
                        case "Port":                if (int.TryParse(row.Value, out int port))    sys.Port          = port;      break;
                        case "DeviceID":            sys.DeviceID    = row.Value; break;
                        case "MDLN":                sys.MDLN        = row.Value; break;
                        case "SOFTREV":             sys.SOFTREV     = row.Value; break;
                        case "ServiceName":         sys.ServiceName = row.Value; break;
                        case "AutoStart":           if (bool.TryParse(row.Value, out bool ab))    sys.AutoStart     = ab;        break;
                        case "T3 (ms)":             if (int.TryParse(row.Value, out int t3))      sys.T3            = t3;        break;
                        case "T5 (ms)":             if (int.TryParse(row.Value, out int t5))      sys.T5            = t5;        break;
                        case "T6 (ms)":             if (int.TryParse(row.Value, out int t6))      sys.T6            = t6;        break;
                        case "T7 (ms)":             if (int.TryParse(row.Value, out int t7))      sys.T7            = t7;        break;
                        case "T8 (ms)":             if (int.TryParse(row.Value, out int t8))      sys.T8            = t8;        break;
                        case "BeatInterval (ms)":   if (int.TryParse(row.Value, out int bi))      sys.BeatInterval  = bi;        break;
                    }
                }
                catch { /* 单字段解析失败不影响其他字段 */ }
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
                        var dt  = Enum.TryParse<DataType>(r.DataType, out var parsed) ? parsed : DataType.U4;
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

        // ── 命令实现：参数导入/导出/保存 ──────────────────────────────────────

        private async Task ExecuteImportParamsAsync()
        {
            var dlg = new OpenFileDialog { Title = "导入参数配置 (Excel)", Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var validateConfig = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null)
                                     ?? new ValidateConfiguration();
                _manager.ParamsManager.SetParam(ParamType.Validate, validateConfig);
                await NPOIHelper.LoadValidateFromExcel(dlg.FileName, validateConfig);
                await SaveValidateToDbAsync(validateConfig);
                LoadParamRows(_selectedParamIndex);
                _log.Append(null, $"参数导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteExportParamsAsync()
        {
            var dlg = new SaveFileDialog { Title = "导出参数配置 (Excel)", Filter = "Excel 文件 (*.xlsx)|*.xlsx", FileName = $"SecsGemParams_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var paramType = _selectedParamIndex switch { 1 => ParamType.Validate, 2 => ParamType.Formula, _ => ParamType.System };
                if (paramType == ParamType.Validate)
                {
                    var cfg = _manager.ParamsManager.GetParamOrDefault<ValidateConfiguration>(ParamType.Validate, null);
                    if (cfg != null) await Task.Run(() => NPOIHelper.SaveValidate(dlg.FileName, cfg));
                }
                else if (paramType == ParamType.Formula)
                {
                    await _manager.CommandManager.SaveAllCommandsToExcelAsync();
                }
                _log.Append(null, $"参数导出完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteSaveParamAsync()
        {
            var paramType = _selectedParamIndex switch { 1 => ParamType.Validate, 2 => ParamType.Formula, _ => ParamType.System };
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
                _log.Append(null, $"参数已保存 ({paramType})", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存参数失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── 命令实现：数据库导入操作 ───────────────────────────────────────────

        private async Task ExecuteImportSystemParamToDbAsync()
        {
            var dlg = new OpenFileDialog { Title = "导入系统参数 (JSON)", Filter = "JSON 文件 (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var param = new SecsGemSystemParam();
                if (await param.Load(dlg.FileName))
                {
                    _manager.ParamsManager.SetParam(ParamType.System, param);
                    await SaveSystemToDbAsync(param);
                    await ExecuteRefreshDbViewAsync();
                    _log.Append(null, $"系统参数导入完成: {dlg.FileName}", isSystem: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteImportValidateParamToDbAsync()
        {
            var dlg = new OpenFileDialog { Title = "导入变量参数 (Excel)", Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var cfg = new ValidateConfiguration();
                await NPOIHelper.LoadValidateFromExcel(dlg.FileName, cfg);
                _manager.ParamsManager.SetParam(ParamType.Validate, cfg);
                await SaveValidateToDbAsync(cfg);
                LoadParamRows(_selectedParamIndex);
                await ExecuteRefreshDbViewAsync();
                _log.Append(null, $"变量参数导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteImportFormulaParamToDbAsync()
        {
            var dlg = new OpenFileDialog { Title = "导入配方参数 (Excel)", Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var formula = _manager.CommandManager.FormulaConfiguration ?? new FormulaConfiguration();
                await NPOIHelper.LoadIncentiveCommandFromExcel(dlg.FileName, formula.IncentiveCommandDictionary);
                await NPOIHelper.LoadResponseCommandFromExcel(dlg.FileName, formula.ResponseCommandDictionary);
                await _manager.CommandManager.UPDataCommondCollection(formula);
                await SaveFormulaToDbAsync();
                await _reloadCommandTrees();
                await ExecuteRefreshDbViewAsync();
                _log.Append(null, $"配方参数导入完成: {dlg.FileName}", isSystem: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task ExecuteRefreshDbViewAsync()
        {
            try
            {
                var sysEntities    = await _db.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs).GetAllAsync();
                var cmdIdEntities  = await _db.GetRepository<CommandIDEntity>(SecsDbSet.CommnadIDs).GetAllAsync();
                var ceidEntities   = await _db.GetRepository<CEIDEntity>(SecsDbSet.CEIDs).GetAllAsync();
                var reportEntities = await _db.GetRepository<ReportIDEntity>(SecsDbSet.ReportIDs).GetAllAsync();
                var vidEntities    = await _db.GetRepository<VIDEntity>(SecsDbSet.VIDs).GetAllAsync();
                var incEntities    = await _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands).GetAllAsync();
                var resEntities    = await _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands).GetAllAsync();

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    DbSystemRows.Clear();
                    foreach (var e in sysEntities)
                    {
                        var p = e.ToParam();
                        DbSystemRows.Add(new ParamRowViewModel { Name = "ServiceName", Value = p.ServiceName,        DataType = "String", Description = "服务名称" });
                        DbSystemRows.Add(new ParamRowViewModel { Name = "IPAddress",   Value = p.IPAddress,          DataType = "String", Description = "IP 地址" });
                        DbSystemRows.Add(new ParamRowViewModel { Name = "Port",        Value = p.Port.ToString(),    DataType = "Int",    Description = "端口" });
                        DbSystemRows.Add(new ParamRowViewModel { Name = "DeviceID",    Value = p.DeviceID,           DataType = "String", Description = "设备ID" });
                    }
                    DbCommandIdRows.Clear();
                    foreach (var e in cmdIdEntities)  { var c = e.ToCommandID(); DbCommandIdRows.Add(new CommandIdRowViewModel { Code = c.ID, Description = c.Description, RCMD = c.RCMD, LinkVIDs = string.Join(", ", c.LinkVID), Comment = c.Comment ?? string.Empty }); }
                    DbCeidRows.Clear();
                    foreach (var e in ceidEntities)   { var c = e.ToCEID();      DbCeidRows.Add(     new CeidRowViewModel     { Code = c.ID, Description = c.Description, LinkReportIDs = string.Join(", ", c.LinkReportID), Comment = c.Comment ?? string.Empty }); }
                    DbReportIdRows.Clear();
                    foreach (var e in reportEntities) { var r = e.ToReportID();  DbReportIdRows.Add( new ReportIdRowViewModel { Code = r.ID, Description = r.Description, LinkVIDs = string.Join(", ", r.LinkVID), Comment = r.Comment ?? string.Empty }); }
                    DbVidRows.Clear();
                    foreach (var e in vidEntities)    { var v = e.ToVID();       DbVidRows.Add(      new VidRowViewModel      { Code = v.ID, Description = v.Description, DataType = v.DataType.ToString(), Value = v.Value?.ToString() ?? string.Empty, Comment = v.Comment ?? string.Empty }); }
                    DbIncentiveRows.Clear();
                    foreach (var e in incEntities)    { var c = e.GetSFCommandFormIncentiveEntity(); DbIncentiveRows.Add(new ParamRowViewModel { Name = c.Key, Value = c.Name, DataType = "Incentive", Description = c.ID }); }
                    DbResponseRows.Clear();
                    foreach (var e in resEntities)    { var c = e.GetSFCommandFormResponseEntity();  DbResponseRows.Add( new ParamRowViewModel { Name = c.Key, Value = c.Name, DataType = "Response",  Description = c.ID }); }
                });
            }
            catch (Exception ex)
            {
                _log.Append(null, $"数据库视图刷新失败: {ex.Message}", isSystem: true);
            }
        }

        // ── 数据库持久化辅助 ───────────────────────────────────────────────────

        private async Task SaveValidateToDbAsync(ValidateConfiguration cfg)
        {
            try
            {
                var vidRepo = _db.GetRepository<VIDEntity>(SecsDbSet.VIDs);
                await vidRepo.RemoveRangeAsync(await vidRepo.GetAllAsync());
                await vidRepo.AddRangeAsync(cfg.VIDS.Values.Select(v => v.ToEntity()));

                var ceidRepo = _db.GetRepository<CEIDEntity>(SecsDbSet.CEIDs);
                await ceidRepo.RemoveRangeAsync(await ceidRepo.GetAllAsync());
                await ceidRepo.AddRangeAsync(cfg.CEIDS.Values.Select(c => c.ToEntity()));

                var reportRepo = _db.GetRepository<ReportIDEntity>(SecsDbSet.ReportIDs);
                await reportRepo.RemoveRangeAsync(await reportRepo.GetAllAsync());
                await reportRepo.AddRangeAsync(cfg.ReportIDS.Values.Select(r => r.ToEntity()));

                var cmdRepo = _db.GetRepository<CommandIDEntity>(SecsDbSet.CommnadIDs);
                await cmdRepo.RemoveRangeAsync(await cmdRepo.GetAllAsync());
                await cmdRepo.AddRangeAsync(cfg.CommandIDS.Values.Select(c => c.ToEntity()));

                await _db.SaveChangesAsync();
            }
            catch (Exception ex) { _log.Append(null, $"Validate 参数持久化失败: {ex.Message}", isSystem: true); }
        }

        private async Task SaveSystemToDbAsync(SecsGemSystemParam sys)
        {
            try
            {
                var repo = _db.GetRepository<SecsGemSystemEntity>(SecsDbSet.SystemConfigs);
                await repo.RemoveRangeAsync(await repo.GetAllAsync());
                await repo.AddAsync(sys.ToEntity());
                await _db.SaveChangesAsync();
            }
            catch (Exception ex) { _log.Append(null, $"System 参数持久化失败: {ex.Message}", isSystem: true); }
        }

        private async Task SaveFormulaToDbAsync()
        {
            try
            {
                var formula = _manager.CommandManager.FormulaConfiguration;
                if (formula == null) return;

                var incRepo = _db.GetRepository<IncentiveEntity>(SecsDbSet.IncentiveCommands);
                await incRepo.RemoveRangeAsync(await incRepo.GetAllAsync());
                await incRepo.AddRangeAsync(formula.IncentiveCommandDictionary.Values.Select(c => c.GetIncentiveEntityFormSFCommand()));

                var resRepo = _db.GetRepository<ResponseEntity>(SecsDbSet.ResponseCommands);
                await resRepo.RemoveRangeAsync(await resRepo.GetAllAsync());
                await resRepo.AddRangeAsync(formula.ResponseCommandDictionary.Values.Select(c => c.GetResponseEntityFormSFCommand()));

                await _db.SaveChangesAsync();
            }
            catch (Exception ex) { _log.Append(null, $"Formula 参数持久化失败: {ex.Message}", isSystem: true); }
        }
    }
}
