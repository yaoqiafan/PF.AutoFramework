using Microsoft.Win32;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Modules.Identity.Helpers;
using PF.Modules.Identity.Models;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Identity.ViewModels
{
    /// <summary>用户管理 ViewModel</summary>
    public class UserManagementViewModel : RegionViewModelBase
    {
        private readonly IUserService    _userService;
        private readonly IMessageService _messageService;
        private readonly ILogService     _logger;

        /// <summary>
        /// 系统内置账号名（与 UserService 内的保留名单一致），批量导入时用于跳过冲突，
        /// 真正的强制拦截仍在 UserService.SaveUserAsync 内完成，这里只是提前给出校验提示。
        /// </summary>
        private static readonly HashSet<string> _reservedBuiltInNames =
            new(StringComparer.OrdinalIgnoreCase) { "Operator", "Engineer", "Administrator", "SuperUser" };

        // ── 绑定属性 ──────────────────────────────────────────────────────────

        private ObservableCollection<SelectableUserItem> _selectableUsers = new();
        /// <summary>用户列表（卡片视图 CoverView 与列表视图 DataGrid 共用同一份数据源）</summary>
        public ObservableCollection<SelectableUserItem> SelectableUsers
        {
            get => _selectableUsers;
            set => SetProperty(ref _selectableUsers, value);
        }

        private bool _isCardView = true;
        /// <summary>是否为卡片视图（与 IsListView 互斥，驱动顶部视图切换 RadioButton）</summary>
        public bool IsCardView
        {
            get => _isCardView;
            set
            {
                if (SetProperty(ref _isCardView, value))
                    RaisePropertyChanged(nameof(IsListView));
            }
        }

        /// <summary>是否为列表视图，与 IsCardView 是同一份状态的两面</summary>
        public bool IsListView
        {
            get => !_isCardView;
            set => IsCardView = !value;
        }

        /// <summary>已勾选的用户数量（批量删除工具条展示 + 命令 CanExecute 依据）</summary>
        public int SelectedCount => SelectableUsers.Count(x => x.IsSelected);

        /// <summary>
        /// 全选/取消全选（仅作用于 CanSelect=true 的行，即权限等级严格低于当前登录用户的账号）。
        /// </summary>
        public bool SelectAllUsers
        {
            get
            {
                var selectable = SelectableUsers.Where(x => x.CanSelect).ToList();
                return selectable.Count > 0 && selectable.All(x => x.IsSelected);
            }
            set
            {
                foreach (var item in SelectableUsers.Where(x => x.CanSelect))
                    item.IsSelected = value;

                RaisePropertyChanged(nameof(SelectAllUsers));
                RaisePropertyChanged(nameof(SelectedCount));
                BatchDeleteCommand.RaiseCanExecuteChanged();
            }
        }

        private UserInfo? _selectedUser;
        /// <summary>当前选中的用户（右侧编辑面板数据上下文）</summary>
        public UserInfo? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    // 同步代理属性，使 ComboBox 选中值与 SelectedUser 保持一致
                    RaisePropertyChanged(nameof(EditingUserRoot));
                }
            }
        }

        /// <summary>
        /// 代理属性：绑定到权限等级 ComboBox。
        /// 当值改变时自动根据新等级重新生成默认 AccessibleViews，并通知 UI 刷新。
        /// </summary>
        public UserLevel EditingUserRoot
        {
            get => SelectedUser?.Root ?? UserLevel.Operator;
            set
            {
                if (SelectedUser == null || SelectedUser.Root == value) return;
                SelectedUser.Root            = value;
                SelectedUser.AccessibleViews = PermissionHelper.GetDefaultAccessibleViews(value);
                RaisePropertyChanged(nameof(EditingUserRoot));
                // 通知 ListToStringConverter 刷新 AccessibleViews 的中文显示
                RaisePropertyChanged(nameof(SelectedUser));
            }
        }

        /// <summary>
        /// 权限等级枚举列表（ComboBox 数据源）。
        /// 排除 Null 和 SuperUser（SuperUser 为唯一内置账号，不允许通过 UI 创建或修改）。
        /// 同时只列出小于等于当前登录用户权限的等级，防止越权提升。
        /// </summary>
        public IEnumerable<UserLevel> UserLevels =>
            Enum.GetValues(typeof(UserLevel))
                .Cast<UserLevel>()
                .Where(l => l != UserLevel.Null &&
                            l != UserLevel.SuperUser &&
                            (int)l <= (int)(_userService.CurrentUser?.Root ?? UserLevel.Null))
                .ToList();

        /// <summary>批量导入仅管理员（含 SuperUser）可用，防止操作员/工程师批量开设账号</summary>
        public bool CanImportUsers => _userService.IsAuthorized(UserLevel.Administrator);

        // ── 命令 ──────────────────────────────────────────────────────────────

        /// <summary>刷新用户列表</summary>
        public DelegateCommand LoadUsersCommand { get; }

        /// <summary>新增用户草稿（不立即持久化，需点击保存）</summary>
        public DelegateCommand AddCommand { get; }

        /// <summary>保存选中用户的修改（新增或编辑均通过此命令落盘）</summary>
        public DelegateCommand SaveCommand { get; }

        /// <summary>删除指定用户（带二次确认弹窗）</summary>
        public DelegateCommand<UserInfo> DeleteCommand { get; }

        /// <summary>从 CoverView 展开面板直接保存指定用户（CommandParameter 传入 UserInfo）</summary>
        public DelegateCommand<UserInfo> SaveUserCommand { get; }

        /// <summary>导出批量导入模板</summary>
        public DelegateCommand ExportTemplateCommand { get; }

        /// <summary>批量导入用户（仅工号 + 密码，权限固定为操作员）</summary>
        public DelegateCommand ImportUsersCommand { get; }

        /// <summary>批量删除已勾选的用户（卡片视图、列表视图共用）</summary>
        public DelegateCommand BatchDeleteCommand { get; }

        // ── 构造函数 ──────────────────────────────────────────────────────────

        /// <summary>初始化用户管理 ViewModel</summary>
        public UserManagementViewModel(
            IUserService    userService,
            IMessageService messageService,
            ILogService     logger)
        {
            _userService    = userService;
            _messageService = messageService;
            _logger         = logger;

            LoadUsersCommand = new DelegateCommand(async () => await LoadUsersAsync());
            AddCommand       = new DelegateCommand(ExecuteAdd);
            SaveCommand      = new DelegateCommand(
                async () => await SaveAsync(),
                () => SelectedUser != null);
            DeleteCommand    = new DelegateCommand<UserInfo>(
                async user => await DeleteAsync(user),
                user => user != null);
            SaveUserCommand  = new DelegateCommand<UserInfo>(
                async user => await SaveUserDirectAsync(user),
                user => user != null);
            ExportTemplateCommand = new DelegateCommand(ExecuteExportTemplate);
            ImportUsersCommand    = new DelegateCommand(
                async () => await ImportUsersAsync(),
                () => CanImportUsers);
            BatchDeleteCommand    = new DelegateCommand(
                async () => await BatchDeleteAsync(),
                () => SelectedCount > 0);

            _userService.CurrentUserChanged += OnCurrentUserChanged;
        }

        private void OnCurrentUserChanged(object? sender, UserInfo? user)
        {
            RaisePropertyChanged(nameof(CanImportUsers));
            ImportUsersCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Prism 框架生命周期钩子：Region 移除或导航离开时自动调用，解绑事件订阅以防内存泄漏。
        /// </summary>
        public override void Destroy()
        {
            _userService.CurrentUserChanged -= OnCurrentUserChanged;
            base.Destroy();
        }

        // ── Prism 导航生命周期 ────────────────────────────────────────────────

        /// <summary>
        /// 每次导航到页面时自动刷新用户列表，确保展示最新落盘数据。
        /// </summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            LoadUsersCommand.Execute();
        }

        // ── CRUD 实现 ─────────────────────────────────────────────────────────

        private async Task SaveUserDirectAsync(UserInfo user)
        {
            if (user == null) return;
            // 保存前根据当前 Root 重新计算可访问视图
            user.AccessibleViews = PermissionHelper.GetDefaultAccessibleViews(user.Root);
            SelectedUser = user;
            await SaveAsync();
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var list = await _userService.GetUserListAsync();

                // 越权可见性控制：只显示权限等级 ≤ 当前登录用户的账号
                var currentLevel = _userService.CurrentUser?.Root ?? UserLevel.Null;
                var filtered = (list ?? new ObservableCollection<UserInfo>())
                    .Where(u => (int)u.Root < (int)currentLevel || u.UserId == _userService.CurrentUser?.UserId)
                    .ToList();

                // 批量删除门槛：只能勾选权限等级严格低于自己的账号（同级/更高级不可选，天然排除自己）
                var items = filtered.Select(u => CreateSelectableItem(u, (int)u.Root < (int)currentLevel));
                SelectableUsers = new ObservableCollection<SelectableUserItem>(items);

                RaisePropertyChanged(nameof(SelectedCount));
                RaisePropertyChanged(nameof(SelectAllUsers));
                BatchDeleteCommand.RaiseCanExecuteChanged();

                _logger.Info($"[用户管理] 用户列表加载完成，共 {SelectableUsers.Count} 条（当前权限等级: {currentLevel}）。");
            }
            catch (Exception ex)
            {
                _logger.Error("[用户管理] 加载用户列表失败。", exception: ex);
                _messageService.ShowMessage(
                    "加载用户列表失败，请查看日志。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>创建勾选包装项，并订阅其 IsSelected 变化以联动"已选择 N 项"、全选状态与批量删除按钮</summary>
        private SelectableUserItem CreateSelectableItem(UserInfo user, bool canSelect)
        {
            var item = new SelectableUserItem(user, canSelect);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(SelectableUserItem.IsSelected)) return;
                RaisePropertyChanged(nameof(SelectedCount));
                RaisePropertyChanged(nameof(SelectAllUsers));
                BatchDeleteCommand.RaiseCanExecuteChanged();
            };
            return item;
        }

        private void ExecuteAdd()
        {
            var level   = UserLevel.Operator;
            var newUser = new UserInfo
            {
                UserId          = Guid.NewGuid().ToString("N")[..8],
                UserName        = "新用户",
                Password        = "PF111",
                Root            = level,
                AccessibleViews = PermissionHelper.GetDefaultAccessibleViews(level),
            };

            // 新草稿默认 Operator，是否可勾选批量删除同样按等级比较（当前用户是 Operator 时为不可选）
            var currentLevel = _userService.CurrentUser?.Root ?? UserLevel.Null;
            SelectableUsers.Add(CreateSelectableItem(newUser, (int)level < (int)currentLevel));
            SelectedUser = newUser;
            _logger.Info($"[用户管理] 已创建新用户草稿 UserId={newUser.UserId}，默认权限 {level}，请填写后点击\"保存更改\"落盘。");
        }

        private async Task SaveAsync()
        {
            if (SelectedUser == null) return;

            // SuperUser 为系统唯一内置账号，禁止通过 UI 创建或修改为该等级
            if (SelectedUser.Root == UserLevel.SuperUser)
            {
                _logger.Warn($"[用户管理] 拒绝保存：不允许创建或修改 SuperUser 等级的自定义账号。");
                _messageService.ShowMessage(
                    "SuperUser 为系统内置唯一账号，不允许创建或修改为该权限等级。",
                    "操作被拒绝",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var ok = await _userService.SaveUserAsync(SelectedUser);

                if (ok)
                {
                    _logger.Success($"[用户管理] 用户 '{SelectedUser.UserName}' 保存成功。");
                    _messageService.ShowMessage(
                        $"用户 {SelectedUser.UserName} 已保存。",
                        "成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    _logger.Warn($"[用户管理] 保存用户 '{SelectedUser.UserName}' 时服务返回 false，请检查用户信息。");
                    _messageService.ShowMessage(
                        "保存失败，请检查用户名是否重复或信息是否完整。",
                        "警告",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[用户管理] 保存用户 '{SelectedUser.UserName}' 时发生异常。", exception: ex);
                _messageService.ShowMessage(
                    $"保存时发生错误：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            LoadUsersCommand.Execute();
        }

        private async Task DeleteAsync(UserInfo user)
        {
            if (user == null) return;

            // 二次确认，防止误操作
            var result = await _messageService.ShowMessageAsync(
                $"确认要删除用户 {user.UserName} 吗？此操作不可撤销。",
                "删除确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != ButtonResult.OK) return;

            try
            {
                var ok = await _userService.DeleteUserAsync(user);

                if (ok)
                {
                    var item = SelectableUsers.FirstOrDefault(x => x.User == user);
                    if (item != null) SelectableUsers.Remove(item);
                    if (SelectedUser == user) SelectedUser = null;
                    RaisePropertyChanged(nameof(SelectedCount));
                    RaisePropertyChanged(nameof(SelectAllUsers));
                    BatchDeleteCommand.RaiseCanExecuteChanged();
                    _logger.Success($"[用户管理] 用户 '{user.UserName}' 已删除。");
                }
                else
                {
                    _logger.Warn($"[用户管理] 删除用户 '{user.UserName}' 时服务返回 false。");
                    _messageService.ShowMessage(
                        "删除失败，请查看日志。",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[用户管理] 删除用户 '{user.UserName}' 时发生异常。", exception: ex);
                _messageService.ShowMessage(
                    $"删除时发生错误：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ── 批量删除 ──────────────────────────────────────────────────────────

        private async Task BatchDeleteAsync()
        {
            // 防御性二次过滤：即便勾选框已按 CanSelect 禁用，命令层再确认一次等级门槛
            // （只能删除比自己权限等级低的账号，同级/更高级——含自己——一律排除）。
            var targets = SelectableUsers
                .Where(x => x.IsSelected && x.CanSelect)
                .Select(x => x.User)
                .ToList();

            if (targets.Count == 0) return;

            var confirmResult = await _messageService.ShowMessageAsync(
                $"确认要删除以下 {targets.Count} 个账号吗？此操作不可撤销。\n{string.Join("、", targets.Select(u => u.UserName))}",
                "批量删除确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (confirmResult != ButtonResult.OK) return;

            int successCount = 0;
            var failedNames = new List<string>();

            await _messageService.ExecuteWithWaitAsync(async () =>
            {
                foreach (var user in targets)
                {
                    try
                    {
                        if (await _userService.DeleteUserAsync(user)) successCount++;
                        else failedNames.Add(user.UserName);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[用户管理] 批量删除用户 '{user.UserName}' 失败。", exception: ex);
                        failedNames.Add(user.UserName);
                    }
                }
            }, "正在批量删除用户...", "请稍候");

            _logger.Success($"[用户管理] 批量删除完成：成功 {successCount} 条，失败 {failedNames.Count} 条。");

            var resultMessage = $"批量删除完成：成功 {successCount} 条。" +
                (failedNames.Count > 0 ? $"\n以下 {failedNames.Count} 个账号删除失败：{string.Join("、", failedNames)}" : string.Empty);
            _messageService.ShowMessage(
                resultMessage,
                "删除结果",
                MessageBoxButton.OK,
                failedNames.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            LoadUsersCommand.Execute();
        }

        // ── 批量导入 ──────────────────────────────────────────────────────────

        private void ExecuteExportTemplate()
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"用户批量导入模板_{DateTime.Now:yyyyMMdd}",
                Filter   = "Excel 文件 (*.xlsx)|*.xlsx",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                UserImportExcelHelper.ExportTemplate(dlg.FileName);
                _logger.Info($"[用户管理] 已导出批量导入模板: {dlg.FileName}");
                _messageService.ShowMessage(
                    $"模板已导出至：{dlg.FileName}",
                    "成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Error("[用户管理] 导出批量导入模板失败。", exception: ex);
                _messageService.ShowMessage(
                    $"导出失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ImportUsersAsync()
        {
            if (!CanImportUsers)
            {
                _logger.Warn("[用户管理] 拒绝批量导入：当前账号权限不足（需管理员及以上）。");
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls",
            };
            if (dlg.ShowDialog() != true) return;

            List<UserImportExcelHelper.ImportRow> rows;
            try
            {
                rows = UserImportExcelHelper.ParseImportFile(dlg.FileName);
            }
            catch (Exception ex)
            {
                _logger.Error("[用户管理] 解析批量导入文件失败。", exception: ex);
                _messageService.ShowMessage(
                    $"解析文件失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (rows.Count == 0)
            {
                _messageService.ShowMessage("文件内没有可导入的数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 已存在账号集合：全量自定义账号（不受当前登录者可见性过滤）+ 内置保留名
            var existingNames = new HashSet<string>(
                (await _userService.GetUserListAsync()).Select(u => u.UserName),
                StringComparer.OrdinalIgnoreCase);
            existingNames.UnionWith(_reservedBuiltInNames);

            var toImport    = new List<UserImportExcelHelper.ImportRow>();
            var seenInFile  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var failedRows  = new List<string>();
            var skippedRows = new List<string>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.UserName) || string.IsNullOrWhiteSpace(row.Password))
                {
                    failedRows.Add($"第 {row.RowNumber} 行：工号或密码为空");
                    continue;
                }

                if (!seenInFile.Add(row.UserName))
                {
                    failedRows.Add($"第 {row.RowNumber} 行：工号 '{row.UserName}' 在文件内重复");
                    continue;
                }

                if (existingNames.Contains(row.UserName))
                {
                    skippedRows.Add($"第 {row.RowNumber} 行：工号 '{row.UserName}' 已存在，跳过（不覆盖）");
                    continue;
                }

                toImport.Add(row);
            }

            var summary = $"校验完成：可导入 {toImport.Count} 条，跳过 {skippedRows.Count} 条，失败 {failedRows.Count} 条。";
            var detailLines = failedRows.Concat(skippedRows).ToList();
            var detail = detailLines.Count > 0 ? string.Join("\n", detailLines) + "\n" : string.Empty;

            if (toImport.Count == 0)
            {
                _messageService.ShowMessage($"{summary}\n{detail}没有可导入的新账号。", "批量导入", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmResult = await _messageService.ShowMessageAsync(
                $"{summary}\n{detail}是否继续导入 {toImport.Count} 个新账号（权限均为操作员）？",
                "批量导入确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (confirmResult != ButtonResult.OK) return;

            int successCount = 0;
            var saveFailedNames = new List<string>();

            await _messageService.ExecuteWithWaitAsync(async () =>
            {
                foreach (var row in toImport)
                {
                    var user = new UserInfo
                    {
                        UserId          = Guid.NewGuid().ToString("N")[..8],
                        UserName        = row.UserName,
                        Password        = row.Password,
                        Root            = UserLevel.Operator,
                        AccessibleViews = PermissionHelper.GetDefaultAccessibleViews(UserLevel.Operator),
                    };

                    try
                    {
                        if (await _userService.SaveUserAsync(user)) successCount++;
                        else saveFailedNames.Add(row.UserName);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[用户管理] 批量导入用户 '{row.UserName}' 失败。", exception: ex);
                        saveFailedNames.Add(row.UserName);
                    }
                }
            }, "正在批量导入用户...", "请稍候");

            _logger.Success($"[用户管理] 批量导入完成：成功 {successCount} 条，失败 {saveFailedNames.Count} 条。");

            var resultMessage = $"批量导入完成：成功 {successCount} 条。" +
                (saveFailedNames.Count > 0 ? $"\n以下 {saveFailedNames.Count} 个账号写入失败：{string.Join("、", saveFailedNames)}" : string.Empty);
            _messageService.ShowMessage(
                resultMessage,
                "导入结果",
                MessageBoxButton.OK,
                saveFailedNames.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            LoadUsersCommand.Execute();
        }
    }
}
