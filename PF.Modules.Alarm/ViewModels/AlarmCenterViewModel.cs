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
using System.Windows;

namespace PF.Modules.Alarm.ViewModels
{
    /// <summary>
    /// 报警中心 ViewModel。
    /// 负责活跃报警集合维护、历史查询、SOP看板联动以及复位命令。
    /// 遵守架构规范：不在 ViewModel 中直接写 EF Core 查询，所有 DB 操作委托给 IAlarmService。
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

            // 订阅服务事件（后台线程触发，需 Dispatcher 调度到 UI 线程）
            _alarmService.AlarmTriggered += OnAlarmTriggered;
            _alarmService.AlarmCleared  += OnAlarmCleared;

            // 加载当前活跃报警快照
            foreach (var record in _alarmService.ActiveAlarms)
                ActiveAlarms.Add(record);
        }

        // ── 导航生命周期 ──────────────────────────────────────────────────────

        /// <summary>
        /// 复用同一实例以保持事件订阅和集合状态（否则每次导航都会创建新 ViewModel）。
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

        // ── 服务事件回调 ──────────────────────────────────────────────────────

        private void OnAlarmTriggered(object? sender, AlarmRecord record)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // 移除同一 source 的旧记录（如有）再插入最新的到顶部
                var existing = ActiveAlarms.FirstOrDefault(r => r.Source == record.Source);
                if (existing != null) ActiveAlarms.Remove(existing);

                ActiveAlarms.Insert(0, record);
            });
        }

        private void OnAlarmCleared(object? sender, AlarmRecord record)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var existing = ActiveAlarms.FirstOrDefault(r => r.Source == record.Source);
                if (existing != null) ActiveAlarms.Remove(existing);
            });
        }

        // ── 命令实现 ──────────────────────────────────────────────────────────

        private void OnClearAll()
        {
            _alarmService.ClearAllActiveAlarms();
            // ActiveAlarms 集合通过 AlarmCleared 事件回调自动清空
        }

        private void OnClearSelected()
        {
            if (_selectedAlarm == null) return;
            _alarmService.ClearAlarm(_selectedAlarm.Source);
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

            Application.Current?.Dispatcher.Invoke(() =>
            {
                HistoricalAlarms.Clear();
                foreach (var r in results)
                    HistoricalAlarms.Add(r);
            });
        }

        // ── 清理 ─────────────────────────────────────────────────────────────

        public override void Destroy()
        {
            _alarmService.AlarmTriggered -= OnAlarmTriggered;
            _alarmService.AlarmCleared  -= OnAlarmCleared;
            base.Destroy();
        }
    }
}
