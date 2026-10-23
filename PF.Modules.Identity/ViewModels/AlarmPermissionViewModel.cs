using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Infrastructure.Logging;
using PF.UI.Infrastructure.PrismBase;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Identity.ViewModels
{
    /// <summary>
    /// 异常权限配置 ViewModel：按权限等级配置每个报警码的手动清除/复位等级，写入 AlarmHistory.db。
    /// <para>
    /// 表格只读，修改统一走底部「将选中项设为 → 应用到选中」/「选中项恢复默认」，操作后立即保存生效，不存在未保存状态。
    /// </para>
    /// <para>
    /// 编辑约束：当前用户须为管理员及以上；只能修改当前等级不高于自己的报警码，且只能设置不高于自己的等级（超级用户不受限）。
    /// </para>
    /// </summary>
    public class AlarmPermissionViewModel : RegionViewModelBase
    {
        private const string AllText = "全部";

        private readonly IAlarmDictionaryService _dictionary;
        private readonly IUserService _userService;
        private readonly CategoryLogger _uiLogger;

        /// <summary>初始化异常权限配置 ViewModel</summary>
        public AlarmPermissionViewModel(
            IAlarmDictionaryService dictionary,
            IUserService userService,
            ILogService logService)
        {
            _dictionary  = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _uiLogger    = CategoryLoggerFactory.UI(logService ?? throw new ArgumentNullException(nameof(logService)));

            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.Filter = FilterItem;

            LevelFilters = new List<LevelOption> { new(AllText, null) };
            LevelFilters.AddRange(AllLevels.Select(l => new LevelOption(l.ToDisplayName(), l)));
            _selectedLevelFilter = LevelFilters[0];

            RefreshCommand        = new DelegateCommand(LoadItems);
            ApplyBatchCommand     = new DelegateCommand<IList>(async items => await ApplyBatchAsync(items), _ => CanEdit && !IsLoading)
                                    .ObservesProperty(() => CanEdit)
                                    .ObservesProperty(() => IsLoading);
            RestoreDefaultCommand = new DelegateCommand<IList>(async items => await RestoreDefaultAsync(items), _ => CanEdit && !IsLoading)
                                    .ObservesProperty(() => CanEdit)
                                    .ObservesProperty(() => IsLoading);

            // 用户切换会改变可编辑范围：重新加载
            _userService.CurrentUserChanged += OnCurrentUserChanged;
            // 其它入口修改了等级：同步刷新
            _dictionary.RequiredLevelsChanged += OnRequiredLevelsChanged;

            LoadItems();
        }

        #region 数据

        /// <summary>可配置的全部等级（Operator ~ SuperUser）</summary>
        private static readonly UserLevel[] AllLevels =
            { UserLevel.Operator, UserLevel.Engineer, UserLevel.Administrator, UserLevel.SuperUser };

        /// <summary>报警码条目</summary>
        public ObservableCollection<AlarmPermissionItem> Items { get; } = new();

        /// <summary>带筛选的条目视图（DataGrid 数据源）</summary>
        public ICollectionView ItemsView { get; }

        /// <summary>分类筛选选项</summary>
        public ObservableCollection<string> Categories { get; } = new() { AllText };

        /// <summary>清除等级筛选选项</summary>
        public List<LevelOption> LevelFilters { get; }

        private IReadOnlyList<UserLevel> _assignableLevels = Array.Empty<UserLevel>();
        /// <summary>当前用户可分配的等级（不高于自己）</summary>
        public IReadOnlyList<UserLevel> AssignableLevels
        {
            get => _assignableLevels;
            private set => SetProperty(ref _assignableLevels, value);
        }

        #endregion

        #region 状态

        private UserLevel _currentLevel = UserLevel.Null;
        /// <summary>当前用户等级</summary>
        public UserLevel CurrentLevel
        {
            get => _currentLevel;
            private set => SetProperty(ref _currentLevel, value);
        }

        private string _currentUserDisplay = string.Empty;
        /// <summary>当前用户显示文本</summary>
        public string CurrentUserDisplay
        {
            get => _currentUserDisplay;
            private set => SetProperty(ref _currentUserDisplay, value);
        }

        private bool _canEdit;
        /// <summary>当前用户是否允许编辑（管理员及以上）</summary>
        public bool CanEdit
        {
            get => _canEdit;
            private set => SetProperty(ref _canEdit, value);
        }

        private bool _isLoading;
        /// <summary>是否正在保存</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _filteredCount;
        /// <summary>筛选后的条目数</summary>
        public int FilteredCount
        {
            get => _filteredCount;
            private set => SetProperty(ref _filteredCount, value);
        }

        private UserLevel _batchLevel = UserLevel.Operator;
        /// <summary>批量设置的目标等级</summary>
        public UserLevel BatchLevel
        {
            get => _batchLevel;
            set => SetProperty(ref _batchLevel, value);
        }

        #endregion

        #region 筛选

        private string _selectedCategory = AllText;
        /// <summary>选中的分类筛选</summary>
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) RefreshView(); }
        }

        private LevelOption _selectedLevelFilter;
        /// <summary>选中的清除等级筛选</summary>
        public LevelOption SelectedLevelFilter
        {
            get => _selectedLevelFilter;
            set { if (SetProperty(ref _selectedLevelFilter, value)) RefreshView(); }
        }

        private string _keyword = string.Empty;
        /// <summary>关键字（匹配报警码 / 描述 / 分类）</summary>
        public string Keyword
        {
            get => _keyword;
            set { if (SetProperty(ref _keyword, value)) RefreshView(); }
        }

        private bool FilterItem(object obj)
        {
            if (obj is not AlarmPermissionItem item) return false;

            if (SelectedCategory != AllText && item.Category != SelectedCategory)
                return false;

            if (SelectedLevelFilter?.Level is UserLevel level && item.Level != level)
                return false;

            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                var kw = Keyword.Trim();
                return item.ErrorCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || item.Message.Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || item.Category.Contains(kw, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void RefreshView()
        {
            ItemsView.Refresh();
            FilteredCount = ItemsView.Cast<object>().Count();
        }

        #endregion

        #region 命令

        /// <summary>重新加载</summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>把选中条目设为 <see cref="BatchLevel"/> 并立即保存（参数为 DataGrid.SelectedItems）</summary>
        public DelegateCommand<IList> ApplyBatchCommand { get; }

        /// <summary>把选中条目恢复为代码默认等级并立即保存（参数为 DataGrid.SelectedItems）</summary>
        public DelegateCommand<IList> RestoreDefaultCommand { get; }

        #endregion

        #region 导航

        /// <summary>每次进入页面时按最新字典刷新</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            LoadItems();
        }

        /// <summary>页面销毁时解除全局事件订阅</summary>
        public override void Destroy()
        {
            _userService.CurrentUserChanged -= OnCurrentUserChanged;
            _dictionary.RequiredLevelsChanged -= OnRequiredLevelsChanged;
            base.Destroy();
        }

        #endregion

        #region 加载与修改

        private void OnCurrentUserChanged(object? sender, Core.Entities.Identity.UserInfo? user)
            => Application.Current?.Dispatcher.InvokeAsync(LoadItems);

        // 本页保存时已先行更新条目，与字典一致则不重载（保留表格选中项）
        private void OnRequiredLevelsChanged(object? sender, EventArgs e)
            => Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (Items.Any(i => i.Level != _dictionary.GetRequiredLevel(i.ErrorCode))) LoadItems();
            });

        /// <summary>从报警字典（内存）重建条目，并按当前用户等级计算可编辑范围</summary>
        private void LoadItems()
        {
            var user = _userService.CurrentUser;
            CurrentLevel = user?.Root ?? UserLevel.Null;
            CurrentUserDisplay = user == null ? "未登录" : $"{user.UserName}（{CurrentLevel.ToDisplayName()}）";
            CanEdit = CurrentLevel >= UserLevel.Administrator;
            AssignableLevels = CanEdit ? AllLevels.Where(l => l <= CurrentLevel).ToList() : Array.Empty<UserLevel>();
            if (!AssignableLevels.Contains(BatchLevel)) BatchLevel = UserLevel.Operator;

            Items.Clear();
            foreach (var info in _dictionary.GetAll().Values
                         .OrderBy(i => i.Category)
                         .ThenBy(i => i.ErrorCode, StringComparer.OrdinalIgnoreCase))
            {
                Items.Add(new AlarmPermissionItem(info, IsEditableLevel(info.RequiredLevel)));
            }

            var keepCategory = SelectedCategory;
            Categories.Clear();
            Categories.Add(AllText);
            foreach (var category in Items.Select(i => i.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct())
                Categories.Add(category);
            _selectedCategory = Categories.Contains(keepCategory) ? keepCategory : AllText;
            RaisePropertyChanged(nameof(SelectedCategory));

            RefreshView();
        }

        /// <summary>原等级不高于当前用户才可编辑（超级用户为最高级，自然全部可编辑）</summary>
        private bool IsEditableLevel(UserLevel level) => CanEdit && level <= CurrentLevel;

        private async Task ApplyBatchAsync(IList selectedItems)
        {
            var targets = GetEditableSelection(selectedItems, out int skipped);
            if (targets.Count == 0)
            {
                ShowNoSelection(skipped, "要修改");
                return;
            }

            var level = BatchLevel;
            if (level > CurrentLevel) return; // AssignableLevels 已限制，防御性判断

            var changes = targets.Where(i => i.Level != level).ToDictionary(i => i, _ => level);
            await SaveChangesAsync(changes, skipped, $"设为「{level.ToDisplayName()}」");
        }

        private async Task RestoreDefaultAsync(IList selectedItems)
        {
            var targets = GetEditableSelection(selectedItems, out int skipped);
            if (targets.Count == 0)
            {
                ShowNoSelection(skipped, "要恢复默认");
                return;
            }

            var changes = new Dictionary<AlarmPermissionItem, UserLevel>();
            foreach (var item in targets)
            {
                // 默认等级高于自己的不能恢复（否则等于把等级提到自己之上）
                if (item.DefaultLevel > CurrentLevel) skipped++;
                else if (item.Level != item.DefaultLevel) changes[item] = item.DefaultLevel;
            }
            await SaveChangesAsync(changes, skipped, "恢复为默认等级");
        }

        private List<AlarmPermissionItem> GetEditableSelection(IList selectedItems, out int skipped)
        {
            var selected = selectedItems?.OfType<AlarmPermissionItem>().ToList() ?? new List<AlarmPermissionItem>();
            var editable = selected.Where(i => i.IsEditable).ToList();
            skipped = selected.Count - editable.Count;
            return editable;
        }

        private void ShowNoSelection(int skipped, string action)
        {
            if (skipped > 0)
            {
                MessageService.ShowMessage(
                    $"选中的 {skipped} 条报警码等级均高于当前用户（{CurrentLevel.ToDisplayName()}），无法修改。",
                    "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MessageService.ShowMessage($"请先在表格中选中{action}的报警码（可按 Ctrl / Shift 多选）。", "提示");
        }

        /// <summary>
        /// 立即保存等级修改。先更新条目再写库：字典的 RequiredLevelsChanged 回调发现条目已一致就不会整表重载，
        /// 表格选中项得以保留；写库失败则回滚条目。
        /// </summary>
        private async Task SaveChangesAsync(Dictionary<AlarmPermissionItem, UserLevel> changes, int skipped, string actionText)
        {
            string skippedText = skipped > 0
                ? $"\n有 {skipped} 条报警码的等级高于当前用户（{CurrentLevel.ToDisplayName()}），已跳过。"
                : string.Empty;

            if (changes.Count == 0)
            {
                MessageService.ShowMessage($"选中的报警码已是目标等级，无需修改。{skippedText}", "提示");
                return;
            }

            var confirm = await MessageService.ShowMessageAsync(
                $"确认将 {changes.Count} 条报警码的清除等级{actionText}？\n确认后立即保存生效。{skippedText}",
                "确认修改", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != ButtonResult.Yes) return;

            var originals = changes.Keys.ToDictionary(i => i, i => i.Level);

            IsLoading = true;
            try
            {
                foreach (var (item, level) in changes) item.SetLevel(level, IsEditableLevel(level));

                var levels = changes.ToDictionary(c => c.Key.ErrorCode, c => c.Value);
                if (!await _dictionary.UpdateRequiredLevelsAsync(levels, CurrentUserName))
                {
                    Rollback(originals);
                    MessageService.ShowMessage("保存失败，请检查报警数据库（AlarmHistory.db）是否可写。", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                RefreshView();
                _uiLogger.Info($"用户 [{CurrentUserName}] 将 {changes.Count} 条报警码的清除等级{actionText}");
                MessageService.ShowMessage($"已将 {changes.Count} 条报警码{actionText}，立即生效。", "保存成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Rollback(originals);
                _uiLogger.Error($"保存报警清除等级异常: {ex.Message}", ex);
                MessageService.ShowMessage($"保存出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Rollback(Dictionary<AlarmPermissionItem, UserLevel> originals)
        {
            foreach (var (item, level) in originals) item.SetLevel(level, IsEditableLevel(level));
            RefreshView();
        }

        #endregion
    }

    /// <summary>等级筛选选项（Level 为 null 表示全部）</summary>
    public sealed record LevelOption(string Name, UserLevel? Level);

    /// <summary>异常权限配置条目</summary>
    public class AlarmPermissionItem : BindableBase
    {
        /// <summary>从字典条目创建</summary>
        public AlarmPermissionItem(AlarmInfo info, bool isEditable)
        {
            ErrorCode     = info.ErrorCode;
            Category      = info.Category ?? string.Empty;
            Message       = info.Message ?? string.Empty;
            Severity      = info.Severity;
            DefaultLevel  = info.DefaultRequiredLevel;
            _level        = info.RequiredLevel;
            _isEditable   = isEditable;
        }

        /// <summary>报警码</summary>
        public string ErrorCode { get; }

        /// <summary>分类</summary>
        public string Category { get; }

        /// <summary>中文描述</summary>
        public string Message { get; }

        /// <summary>严重程度</summary>
        public AlarmSeverity Severity { get; }

        /// <summary>严重程度中文</summary>
        public string SeverityText => Severity switch
        {
            AlarmSeverity.Information => "信息",
            AlarmSeverity.Warning     => "警告",
            AlarmSeverity.Error       => "错误",
            AlarmSeverity.Fatal       => "致命",
            _                         => Severity.ToString()
        };

        /// <summary>代码中定义的默认等级</summary>
        public UserLevel DefaultLevel { get; }

        private UserLevel _level;
        /// <summary>当前清除等级</summary>
        public UserLevel Level
        {
            get => _level;
            private set
            {
                if (SetProperty(ref _level, value))
                    RaisePropertyChanged(nameof(IsCustomized));
            }
        }

        private bool _isEditable;
        /// <summary>当前用户是否可修改该条目</summary>
        public bool IsEditable
        {
            get => _isEditable;
            private set => SetProperty(ref _isEditable, value);
        }

        /// <summary>是否与代码默认等级不同（现场自定义）</summary>
        public bool IsCustomized => Level != DefaultLevel;

        /// <summary>更新等级及可修改状态</summary>
        public void SetLevel(UserLevel level, bool isEditable)
        {
            Level = level;
            IsEditable = isEditable;
        }
    }
}
