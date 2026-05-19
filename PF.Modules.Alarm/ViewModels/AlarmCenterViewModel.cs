using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using Prism.Commands;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PF.Modules.Alarm.ViewModels
{
    /// <summary>
    /// 报警中心 ViewModel。
    /// 负责活跃报警集合维护、历史查询、SOP看板联动以及复位命令。
    /// 遵守架构规范：不在 ViewModel 中直接写 EF Core 查询，所有 DB 操作委托给 IAlarmService。
    /// 报警通知通过 Prism EventAggregator 接收（ThreadOption.UIThread 保证线程安全）。
    /// </summary>
    public class AlarmCenterViewModel : RegionViewModelBase
    {
        private readonly IAlarmService _alarmService;

        // ── 集合 ────────────────────────────────────────────────────────────

        /// <summary>当前活跃报警列表</summary>
        public ObservableCollection<AlarmRecord> ActiveAlarms { get; } = new();

        // 历史查询三层结构：全量缓存 → 当前页显示
        private List<AlarmRecord> _allHistoricalAlarms = new();

        /// <summary>当前页历史报警（DataGrid 数据源）</summary>
        public ObservableCollection<AlarmRecord> HistoricalAlarms { get; } = new();

        // ── 历史查询过滤条件 ──────────────────────────────────────────────────

        private DateTime _startDate = new DateTime(DateTime.Now.Year, 1, 1);
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate = DateTime.Now.Date.AddDays(1).AddSeconds(-1);
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        /// <summary>等级下拉选项（全部 + 四个枚举值）</summary>
        public ObservableCollection<string> SeverityOptions { get; } =
            new() { "全部", "信息", "警告", "错误", "致命" };

        private string _selectedSeverity = "全部";
        public string SelectedSeverity
        {
            get => _selectedSeverity;
            set => SetProperty(ref _selectedSeverity, value);
        }

        /// <summary>分类下拉选项（查询后从结果中提取）</summary>
        public ObservableCollection<string> AvailableCategories { get; } = new() { "全部" };

        private string _selectedCategory = "全部";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        /// <summary>来源下拉选项（查询后从结果中提取）</summary>
        public ObservableCollection<string> AvailableSources { get; } = new() { "全部" };

        private string _selectedSource = "全部";
        public string SelectedSource
        {
            get => _selectedSource;
            set => SetProperty(ref _selectedSource, value);
        }

        private string? _queryErrorCode;
        public string? QueryErrorCode
        {
            get => _queryErrorCode;
            set => SetProperty(ref _queryErrorCode, value);
        }

        private string? _queryDescription;
        public string? QueryDescription
        {
            get => _queryDescription;
            set => SetProperty(ref _queryDescription, value);
        }

        // ── 分页 ─────────────────────────────────────────────────────────────

        private int _historyPageSize = 50;
        public int HistoryPageSize
        {
            get => _historyPageSize;
            set
            {
                if (SetProperty(ref _historyPageSize, value))
                    RecalculateHistoryPagination();
            }
        }

        private int _historyPageIndex = 1;
        public int HistoryPageIndex
        {
            get => _historyPageIndex;
            set => SetProperty(ref _historyPageIndex, value);
        }

        private int _historyMaxPageCount = 1;
        public int HistoryMaxPageCount
        {
            get => _historyMaxPageCount;
            set => SetProperty(ref _historyMaxPageCount, value);
        }

        public DelegateCommand<FunctionEventArgs<int>> HistoryPageUpdatedCmd { get; }

        // ── 状态 ─────────────────────────────────────────────────────────────

        private bool _isQuerying;
        public bool IsQuerying
        {
            get => _isQuerying;
            set => SetProperty(ref _isQuerying, value);
        }

        private string _historyStatusMessage = "";
        public string HistoryStatusMessage
        {
            get => _historyStatusMessage;
            set => SetProperty(ref _historyStatusMessage, value);
        }

        // ── 选中的报警（联动 SOP 看板） ─────────────────────────────────────

        private AlarmRecord? _selectedAlarm;
        /// <summary>获取或设置选中的报警记录</summary>
        public AlarmRecord? SelectedAlarm
        {
            get => _selectedAlarm;
            set
            {
                if (SetProperty(ref _selectedAlarm, value))
                {
                    RaisePropertyChanged(nameof(SelectedSolution));
                    RaisePropertyChanged(nameof(HasSelectedAlarm));
                    ClearSelectedCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>当前选中报警的 SOP 排故文本（直接绑定到 SOP 看板）</summary>
        public string SelectedSolution =>
            _selectedAlarm?.Solution ?? "← 请在左侧报警列表中选择一条报警，\n此处将显示对应的排故 SOP 指导方案。";

        /// <summary>是否有选中的活跃报警（控制"清除选中"按钮可用性）</summary>
        public bool HasSelectedAlarm => _selectedAlarm?.IsActive == true;

        // ── 命令 ─────────────────────────────────────────────────────────────

        /// <summary>清除所有报警命令</summary>
        public DelegateCommand ClearAllCommand            { get; }
        /// <summary>查询历史报警命令</summary>
        public DelegateCommand QueryHistoryCommand        { get; }
        /// <summary>清除历史查询筛选条件命令</summary>
        public DelegateCommand ClearHistoryFiltersCommand { get; }
        /// <summary>清除选中报警命令</summary>
        public DelegateCommand ClearSelectedCommand       { get; }
        /// <summary>从卡片内直接清除单条报警（CommandParameter 传入 AlarmRecord）</summary>
        public DelegateCommand<AlarmRecord> ClearSingleAlarmCommand { get; }
        /// <summary>
        /// 系统复位命令：发布 SystemResetRequestedEvent，由 Shell 桥接到 IMasterController.RequestSystemResetAsync()。
        /// 与"确认/清除"的区别：本命令触发全线硬件复位+状态机跳转，不仅仅是清除报警记录。
        /// </summary>
        public DelegateCommand SystemResetCommand         { get; }

        // ── 构造 ─────────────────────────────────────────────────────────────

        /// <summary>初始化报警中心 ViewModel</summary>
        public AlarmCenterViewModel(IAlarmService alarmService) : base()
        {
            _alarmService = alarmService;

            ClearAllCommand            = new DelegateCommand(OnClearAll);
            QueryHistoryCommand        = new DelegateCommand(async () => await OnQueryHistoryAsync(), () => !IsQuerying);
            ClearHistoryFiltersCommand = new DelegateCommand(OnClearHistoryFilters);
            ClearSelectedCommand       = new DelegateCommand(OnClearSelected, CanClearSelected);
            ClearSingleAlarmCommand    = new DelegateCommand<AlarmRecord>(r => { if (r != null) _alarmService.ClearAlarm(r.Source, r.ErrorCode); });
            SystemResetCommand         = new DelegateCommand(OnSystemReset);
            HistoryPageUpdatedCmd      = new DelegateCommand<FunctionEventArgs<int>>(OnHistoryPageUpdated);

            EventAggregator.GetEvent<AlarmTriggeredEvent>()
                .Subscribe(OnAlarmTriggered, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
            EventAggregator.GetEvent<AlarmClearedEvent>()
                .Subscribe(OnAlarmCleared, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);

            foreach (var record in _alarmService.ActiveAlarms)
                ActiveAlarms.Add(record);
        }

        // ── 导航生命周期 ──────────────────────────────────────────────────────

        public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

        // ── EventAggregator 回调（已在 UI 线程） ──────────────────────────────

        private void OnAlarmTriggered(AlarmRecord record)
        {
            var existing = ActiveAlarms.FirstOrDefault(
                r => r.Source == record.Source && r.ErrorCode == record.ErrorCode);
            if (existing != null) ActiveAlarms.Remove(existing);
            ActiveAlarms.Insert(0, record);
        }

        private void OnAlarmCleared(AlarmRecord record)
        {
            var existing = ActiveAlarms.FirstOrDefault(
                r => r.Source == record.Source && r.ErrorCode == record.ErrorCode);
            if (existing != null) ActiveAlarms.Remove(existing);
        }

        // ── 命令实现 ──────────────────────────────────────────────────────────

        private void OnClearAll()
        {
            _alarmService.ClearAllActiveAlarms();
        }

        private void OnClearSelected()
        {
            if (_selectedAlarm == null) return;
            _alarmService.ClearAlarm(_selectedAlarm.Source, _selectedAlarm.ErrorCode);
        }

        private bool CanClearSelected() => HasSelectedAlarm;

        private void OnSystemReset()
        {
            EventAggregator.GetEvent<SystemResetRequestedEvent>().Publish();
        }

        private void OnClearHistoryFilters()
        {
            StartDate        = new DateTime(DateTime.Now.Year, 1, 1);
            EndDate          = DateTime.Now.Date.AddDays(1).AddSeconds(-1);
            SelectedSeverity = "全部";
            SelectedCategory = "全部";
            SelectedSource   = "全部";
            QueryErrorCode   = null;
            QueryDescription = null;
        }

        private async Task OnQueryHistoryAsync()
        {
            IsQuerying = true;
            QueryHistoryCommand.RaiseCanExecuteChanged();
            HistoryStatusMessage = "查询中...";

            try
            {
                AlarmSeverity? severity = SelectedSeverity switch
                {
                    "信息" => AlarmSeverity.Information,
                    "警告" => AlarmSeverity.Warning,
                    "错误" => AlarmSeverity.Error,
                    "致命" => AlarmSeverity.Fatal,
                    _     => null
                };

                string? category    = SelectedCategory == "全部" ? null : SelectedCategory;
                string? source      = SelectedSource   == "全部" ? null : SelectedSource;
                string? errorCode   = string.IsNullOrWhiteSpace(QueryErrorCode)   ? null : QueryErrorCode;
                string? description = string.IsNullOrWhiteSpace(QueryDescription) ? null : QueryDescription;

                var results = await _alarmService.QueryHistoricalAlarmsAsync(
                    startTime:          StartDate,
                    endTime:            EndDate,
                    severity:           severity,
                    category:           category,
                    source:             source,
                    errorCode:          errorCode,
                    descriptionKeyword: description,
                    pageSize:           5000,
                    page:               0);

                _allHistoricalAlarms = results.ToList();
                RefreshFilterDropdowns(results);
                RecalculateHistoryPagination();
            }
            finally
            {
                IsQuerying = false;
                QueryHistoryCommand.RaiseCanExecuteChanged();
            }
        }

        // ── 分页方法 ──────────────────────────────────────────────────────────

        private void OnHistoryPageUpdated(FunctionEventArgs<int> e)
        {
            HistoryPageIndex = e.Info;
            ApplyHistoryPage(e.Info);
            UpdateHistoryStatusMessage();
        }

        private void RecalculateHistoryPagination()
        {
            int total = _allHistoricalAlarms.Count;
            HistoryMaxPageCount = Math.Max(1, (int)Math.Ceiling((double)total / HistoryPageSize));
            HistoryPageIndex    = 1;
            ApplyHistoryPage(1);
            UpdateHistoryStatusMessage();
        }

        private void ApplyHistoryPage(int pageIndex)
        {
            var page = _allHistoricalAlarms
                .Skip((pageIndex - 1) * HistoryPageSize)
                .Take(HistoryPageSize);

            HistoricalAlarms.Clear();
            foreach (var r in page)
                HistoricalAlarms.Add(r);
        }

        private void UpdateHistoryStatusMessage()
        {
            int total = _allHistoricalAlarms.Count;
            if (total == 0)
            {
                HistoryStatusMessage = "无记录";
                return;
            }
            int start = (_historyPageIndex - 1) * _historyPageSize + 1;
            int end   = Math.Min(_historyPageIndex * _historyPageSize, total);
            HistoryStatusMessage = $"共 {total} 条，显示第 {start}–{end} 条";
        }

        private void RefreshFilterDropdowns(IReadOnlyList<AlarmRecord> results)
        {
            var currentCategory = SelectedCategory;
            var currentSource   = SelectedSource;

            AvailableCategories.Clear();
            AvailableCategories.Add("全部");
            foreach (var cat in results.Select(r => r.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c))
                AvailableCategories.Add(cat!);

            AvailableSources.Clear();
            AvailableSources.Add("全部");
            foreach (var src in results.Select(r => r.Source).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s))
                AvailableSources.Add(src!);

            SelectedCategory = AvailableCategories.Contains(currentCategory) ? currentCategory : "全部";
            SelectedSource   = AvailableSources.Contains(currentSource)      ? currentSource   : "全部";
        }

        // ── 清理 ─────────────────────────────────────────────────────────────

        /// <summary>销毁 ViewModel 并取消事件订阅</summary>
        public override void Destroy()
        {
            EventAggregator.GetEvent<AlarmTriggeredEvent>().Unsubscribe(OnAlarmTriggered);
            EventAggregator.GetEvent<AlarmClearedEvent>().Unsubscribe(OnAlarmCleared);
            base.Destroy();
        }
    }
}
