using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Identity.ViewModels
{
    public class UserManagementViewModel : RegionViewModelBase
    {
        private readonly IUserService    _userService;
        private readonly IMessageService _messageService;
        private readonly ILogService     _logger;

        // ── 绑定属性 ──────────────────────────────────────────────────────────

        private ObservableCollection<UserInfo> _users = new();
        /// <summary>用户列表（DataGrid 数据源）</summary>
        public ObservableCollection<UserInfo> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        private UserInfo? _selectedUser;
        /// <summary>当前选中的用户（右侧编辑面板数据上下文）</summary>
        public UserInfo? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                    SaveCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>权限等级枚举列表（ComboBox 数据源，排除 Null）</summary>
        public IEnumerable<UserLevel> UserLevels { get; } =
            Enum.GetValues(typeof(UserLevel))
                .Cast<UserLevel>()
                .Where(l => l != UserLevel.Null)
                .ToList();

        // ── 命令 ──────────────────────────────────────────────────────────────

        /// <summary>刷新用户列表</summary>
        public DelegateCommand LoadUsersCommand { get; }

        /// <summary>新增用户草稿（不立即持久化，需点击保存）</summary>
        public DelegateCommand AddCommand { get; }

        /// <summary>保存选中用户的修改（新增或编辑均通过此命令落盘）</summary>
        public DelegateCommand SaveCommand { get; }

        /// <summary>删除指定用户（带二次确认弹窗）</summary>
        public DelegateCommand<UserInfo> DeleteCommand { get; }

        // ── 构造函数 ──────────────────────────────────────────────────────────

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

        private async Task LoadUsersAsync()
        {
            try
            {
                var list = await _userService.GetUserListAsync();
                Users = list ?? new ObservableCollection<UserInfo>();
                _logger.Info($"[用户管理] 用户列表加载完成，共 {Users.Count} 条。");
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

        private void ExecuteAdd()
        {
            var level   = UserLevel.Operator;
            var newUser = new UserInfo
            {
                UserId          = Guid.NewGuid().ToString("N")[..8],
                UserName        = "新用户",
                Password        = "PF111",
                Root            = level,
                AccessibleViews = GetDefaultAccessibleViews(level),
            };

            Users.Add(newUser);
            SelectedUser = newUser;
            _logger.Info($"[用户管理] 已创建新用户草稿 UserId={newUser.UserId}，默认权限 {level}，请填写后点击"保存更改"落盘。");
        }

        /// <summary>
        /// 根据权限等级返回该角色应具备的默认可访问页面列表。
        /// 高等级权限向下包含低等级权限（累积模型）。
        /// </summary>
        private static List<string> GetDefaultAccessibleViews(UserLevel level)
        {
            // Operator：日志查看 + 基础参数
            var views = new List<string>
            {
                NavigationConstants.Views.LoggingListView,
                NavigationConstants.Views.ParameterView_CommonParam,
            };

            if (level < UserLevel.Engineer)
                return views;

            // Engineer：新增硬件调试 + 系统参数 + 机构/工站调试
            views.AddRange(new[]
            {
                NavigationConstants.Views.ParameterView_SystemConfigParam,
                NavigationConstants.Views.HardwareDebugView,
                NavigationConstants.Views.MechanismDebugView,
                NavigationConstants.Views.StationDebugView,
            });

            if (level < UserLevel.Administrator)
                return views;

            // Administrator：新增日志管理 + 硬件参数 + 权限查看
            views.AddRange(new[]
            {
                NavigationConstants.Views.LogManagementView,
                NavigationConstants.Views.ParameterView_HardwareParam,
                NavigationConstants.Views.PagePermissionView,
            });

            if (level < UserLevel.SuperUser)
                return views;

            // SuperUser：完整权限（追加用户管理 + 用户参数）
            views.AddRange(new[]
            {
                NavigationConstants.Views.UserManagementView,
                NavigationConstants.Views.ParameterView_UserLoginParam,
            });

            return views;
        }

        private async Task SaveAsync()
        {
            if (SelectedUser == null) return;

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
                    Users.Remove(user);
                    if (SelectedUser == user) SelectedUser = null;
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
    }
}
