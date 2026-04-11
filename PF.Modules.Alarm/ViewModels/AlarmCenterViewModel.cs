using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System;
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

        public ObservableCollection<AlarmRecord> ActiveAlarms     { get; } = new();
        public ObservableCollection<AlarmRecord> HistoricalAlarms { get; } = new();

        // ── 过滤条件 ─────────────────────────────────────────────────────────

        private int _queryYear = DateTime.Now.Year;
        public int QueryYear
        {
            get => _queryYear;
            set => SetProperty(ref _queryYear, value);
        }

        private string? _queryCategory;
        public string? QueryCategory
        {
            get => _queryCategory;
            set => SetProperty(ref _queryCategory, value);
        }

        // ── 选中的报警（联动 SOP 看板） ─────────────────────────────────────

        private AlarmRecord? _selectedAlarm;
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

        public DelegateCommand ClearAllCommand      { get; }
        public DelegateCommand QueryHistoryCommand  { get; }
        public DelegateCommand ClearSelectedCommand { get; }

        // ── 构造 ─────────────────────────────────────────────────────────────

        public AlarmCenterViewModel(IAlarmService alarmService) : base()
        {
            _alarmService = alarmService;

            ClearAllCommand      = new DelegateCommand(OnClearAll);
            QueryHistoryCommand  = new DelegateCommand(async () => await OnQueryHistoryAsync());
            ClearSelectedCommand = new DelegateCommand(OnClearSelected, CanClearSelected);

            // 通过 EventAggregator 订阅（ThreadOption.UIThread 确保回调在 UI 线程执行，无需手动 Dispatcher）
            EventAggregator.GetEvent<AlarmTriggeredEvent>()
                .Subscribe(OnAlarmTriggered, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
            EventAggregator.GetEvent<AlarmClearedEvent>()
                .Subscribe(OnAlarmCleared, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);

            // 加载当前活跃报警快照（初始同步）
            foreach (var record in _alarmService.ActiveAlarms)
                ActiveAlarms.Add(record);
        }

        // ── 导航生命周期 ──────────────────────────────────────────────────────

        /// <summary>
        /// 复用同一实例以保持事件订阅和集合状态（否则每次导航都会创建新 ViewModel）。
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

        // ── EventAggregator 回调（已在 UI 线程） ──────────────────────────────

        private void OnAlarmTriggered(AlarmRecord record)
        {
            // 按复合键 (Source, ErrorCode) 匹配，避免同站不同故障互相覆盖
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
            // ActiveAlarms 集合通过 AlarmClearedEvent 回调自动逐条清空
        }

        private void OnClearSelected()
        {
            if (_selectedAlarm == null) return;
            // 使用精确重载，只清除选中的单条报警
            _alarmService.ClearAlarm(_selectedAlarm.Source, _selectedAlarm.ErrorCode);
        }

        private bool CanClearSelected() => HasSelectedAlarm;

        private async Task OnQueryHistoryAsync()
        {
            var results = await _alarmService.QueryHistoricalAlarmsAsync(
                year:        QueryYear,
                category:    QueryCategory,
                minSeverity: null,
                pageSize:    200,
                page:        0);

            HistoricalAlarms.Clear();
            foreach (var r in results)
                HistoricalAlarms.Add(r);
        }

        // ── 清理 ─────────────────────────────────────────────────────────────

        public override void Destroy()
        {
            // 显式取消订阅，防止僵尸订阅残留
            EventAggregator.GetEvent<AlarmTriggeredEvent>().Unsubscribe(OnAlarmTriggered);
            EventAggregator.GetEvent<AlarmClearedEvent>().Unsubscribe(OnAlarmCleared);
            base.Destroy();
        }
    }
}
