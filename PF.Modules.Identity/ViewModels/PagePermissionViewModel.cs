using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Infrastructure.Logging;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Identity.ViewModels
{
    /// <summary>页面权限管理 ViewModel</summary>
    public class PagePermissionViewModel : RegionViewModelBase
    {
        private readonly IUserService _userService;
        private readonly CategoryLogger _uiLogger;
        private readonly INavigationMenuService _navMenuService;

        private ObservableCollection<PermissionCheckItem> _permissionList;
        private ObservableCollection<UserInfo> _users;
        private UserInfo _selectedUser;
        private bool _isLoading;
        private bool _hasUnsavedDefaults;

        private readonly List<PermissionCheckItem> _allSystemViews;

        /// <summary>初始化页面权限管理 ViewModel</summary>
        public PagePermissionViewModel(
            IUserService userService,
            ILogService logService,
            INavigationMenuService navMenuService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _uiLogger = CategoryLoggerFactory.UI(logService ?? throw new ArgumentNullException(nameof(logService)));
            _navMenuService = navMenuService ?? throw new ArgumentNullException(nameof(navMenuService));

            _allSystemViews = new List<PermissionCheckItem>();

            SaveCommand = new DelegateCommand(async () => await SaveConfigAsync(), CanSaveConfig)
                          .ObservesProperty(() => SelectedUser)
                          .ObservesProperty(() => IsLoading);

            ApplyDefaultPermissionsCommand = new DelegateCommand(ApplyDefaultPermissions, CanSaveConfig)
                          .ObservesProperty(() => SelectedUser)
                          .ObservesProperty(() => IsLoading);

            RefreshUsersCommand = new DelegateCommand(async () => await LoadUsersAsync());

            LoadSystemViewsFromNavigation();
            _ = LoadUsersAsync();
        }

        /// <summary>保存权限配置命令</summary>
        public DelegateCommand SaveCommand { get; }
        /// <summary>刷新用户列表命令</summary>
        public DelegateCommand RefreshUsersCommand { get; }
        /// <summary>应用默认权限命令</summary>
        public DelegateCommand ApplyDefaultPermissionsCommand { get; }

        /// <summary>获取或设置是否正在加载</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// 当当前选中的用户尚无持久化的 AccessibleViews 时为 true，
        /// 提示操作员当前显示的是等级默认方案，需要手动保存以生效。
        /// </summary>
        public bool HasUnsavedDefaults
        {
            get => _hasUnsavedDefaults;
            set => SetProperty(ref _hasUnsavedDefaults, value);
        }

        /// <summary>获取或设置用户列表</summary>
        public ObservableCollection<UserInfo> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        /// <summary>获取或设置选中的用户</summary>
        public UserInfo SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    if (value != null) LoadPermissionsForUser(value);
                    else { PermissionList = null; HasUnsavedDefaults = false; }
                }
            }
        }

        /// <summary>获取或设置权限列表</summary>
        public ObservableCollection<PermissionCheckItem> PermissionList
        {
            get => _permissionList;
            set => SetProperty(ref _permissionList, value);
        }

        private void LoadSystemViewsFromNavigation()
        {
            try
            {
                var menuItems = _navMenuService.MenuItems;
                ExtractMenuItems(menuItems);
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"动态解析菜单树异常: {ex.Message}", ex);
            }
        }

        private void ExtractMenuItems(IEnumerable<NavigationItem> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null) continue;

                string viewName = item.ViewName;
                string description = item.Title;

                if (!string.IsNullOrEmpty(viewName) && !_allSystemViews.Any(v => v.ViewName == viewName))
                {
                    _allSystemViews.Add(new PermissionCheckItem
                    {
                        ViewName = viewName,
                        Description = !string.IsNullOrEmpty(description) ? description : viewName
                    });
                }

                if (item.Children != null && item.Children.Any())
                {
                    ExtractMenuItems(item.Children);
                }
            }
        }

        private async Task LoadUsersAsync()
        {
            IsLoading = true;
            try
            {
                var users = await _userService.GetUserListAsync();

                // 只列出等级低于自己的账号（超级用户全部可见），与「用户管理」页同一门槛。
                // 此前列出全部账号：能进本页的人可以给自己或更高等级的账号勾选任意页面，
                // 绕过等级体系拿到本不该有的页面权限
                var current = _userService.CurrentUser;
                var currentLevel = current?.Root ?? UserLevel.Null;
                var visible = users.Where(u => currentLevel == UserLevel.SuperUser || u.Root < currentLevel);
                Users = new ObservableCollection<UserInfo>(visible);

                if (Users != null && Users.Any()) SelectedUser = Users.First();
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"加载用户列表失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadPermissionsForUser(UserInfo user)
        {
            if (user == null) return;

            // 检测该用户是否已有持久化的权限配置
            bool hasPersistedPermissions = user.AccessibleViews != null && user.AccessibleViews.Any();
            HasUnsavedDefaults = !hasPersistedPermissions;

            // 若无持久化配置则回显等级默认方案（但不自动写库，需用户点击保存）
            List<string> allowedViews = hasPersistedPermissions
                ? user.AccessibleViews
                : GetDefaultAllowedViewsByLevel(user.Root);

            var displayList = _allSystemViews.Select(v => new PermissionCheckItem
            {
                ViewName = v.ViewName,
                Description = v.Description,
                IsAuthorized = allowedViews.Contains(v.ViewName)
            }).ToList();

            PermissionList = new ObservableCollection<PermissionCheckItem>(displayList);
        }

        private List<string> GetDefaultAllowedViewsByLevel(UserLevel level)
        {
            return PermissionHelper.GetDefaultAccessibleViews(level);
        }

        private bool CanSaveConfig()
        {
            return SelectedUser != null && !IsLoading;
        }

        private void ApplyDefaultPermissions()
        {
            if (SelectedUser == null || PermissionList == null) return;

            var defaultViews = GetDefaultAllowedViewsByLevel(SelectedUser.Root);

            foreach (var item in PermissionList)
            {
                item.IsAuthorized = defaultViews.Contains(item.ViewName);
            }

            HasUnsavedDefaults = true;
            _uiLogger.Info($"已将用户 [{SelectedUser.UserName}] 的权限勾选状态重置为 {SelectedUser.Root} 等级的默认状态（尚未保存）");
        }

        private async Task SaveConfigAsync()
        {
            if (SelectedUser == null) return;

            IsLoading = true;
            try
            {
                // 自己没有的页面不能授予别人，也不能替别人取消：这类页面保持该账号原来的状态
                var original = SelectedUser.AccessibleViews ?? new List<string>();
                int untouched = 0;
                var currentAllowed = new List<string>();
                foreach (var p in PermissionList)
                {
                    bool granted;
                    if (_userService.HasPagePermission(p.ViewName))
                    {
                        granted = p.IsAuthorized;
                    }
                    else
                    {
                        granted = original.Contains(p.ViewName);
                        if (granted != p.IsAuthorized) untouched++;
                    }
                    if (granted) currentAllowed.Add(p.ViewName);
                }
                SelectedUser.AccessibleViews = currentAllowed;
                bool isSuccess = await _userService.SaveUserAsync(SelectedUser);

                if (isSuccess)
                {
                    HasUnsavedDefaults = false;
                    _uiLogger.Info($"用户 [{CurrentUserName}] 修改了 [{SelectedUser.UserName}] 的页面权限（{currentAllowed.Count} 个页面）并持久化到数据库");
                    LoadPermissionsForUser(SelectedUser); // 回显实际保存的结果（未生效的勾选会被还原）
                    MessageService.ShowMessage(
                        $"用户 [{SelectedUser.UserName}] 权限配置已保存！" +
                        (untouched > 0 ? $"\n\n其中 {untouched} 个页面当前账号自己没有权限，不能授予或取消，已保持原状。" : string.Empty),
                        "保存成功", MessageBoxButton.OK, untouched > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                }
                else
                {
                    MessageService.ShowMessage("保存失败，请检查数据库连接。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"保存用户权限时异常: {ex.Message}", ex);
                MessageService.ShowMessage($"保存出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>权限检查项</summary>
    public class PermissionCheckItem : BindableBase
    {
        private bool _isAuthorized;
        /// <summary>获取或设置视图名称</summary>
        public string ViewName { get; set; }
        /// <summary>获取或设置描述</summary>
        public string Description { get; set; }
        /// <summary>获取或设置是否已授权</summary>
        public bool IsAuthorized
        {
            get => _isAuthorized;
            set => SetProperty(ref _isAuthorized, value);
        }
    }
}
