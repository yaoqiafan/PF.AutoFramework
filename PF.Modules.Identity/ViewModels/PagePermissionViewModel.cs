using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.Identity.ViewModels
{
    public class PagePermissionViewModel : RegionViewModelBase
    {
        private readonly IUserService _userService;
        private readonly ILogService _logService;
        private readonly INavigationMenuService _navMenuService;

        private ObservableCollection<PermissionCheckItem> _permissionList;
        private ObservableCollection<UserInfo> _users;
        private UserInfo _selectedUser;
        private bool _isLoading;

        private readonly List<PermissionCheckItem> _allSystemViews;

        public PagePermissionViewModel(
            IUserService userService,
            ILogService logService,
            INavigationMenuService navMenuService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
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

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RefreshUsersCommand { get; }
        public DelegateCommand ApplyDefaultPermissionsCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<UserInfo> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public UserInfo SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    if (value != null) LoadPermissionsForUser(value);
                    else PermissionList = null;
                }
            }
        }

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
                _logService.Error($"动态解析菜单树异常: {ex.Message}", "PagePermission", ex);
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
                Users = new ObservableCollection<UserInfo>(users);

                if (Users != null && Users.Any()) SelectedUser = Users.First();
            }
            catch (Exception ex)
            {
                _logService.Error($"加载用户列表失败: {ex.Message}", "PagePermission");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadPermissionsForUser(UserInfo user)
        {
            if (user == null) return;

            var displayList = _allSystemViews.Select(v => new PermissionCheckItem
            {
                ViewName = v.ViewName,
                Description = v.Description,
                IsAuthorized = false
            }).ToList();

            List<string> allowedViews = (user.AccessibleViews != null && user.AccessibleViews.Any())
                                        ? user.AccessibleViews
                                        : GetDefaultAllowedViewsByLevel(user.Root);

            foreach (var item in displayList)
            {
                if (allowedViews.Contains(item.ViewName))
                {
                    item.IsAuthorized = true;
                }
            }

            PermissionList = new ObservableCollection<PermissionCheckItem>(displayList);
        }

        private List<string> GetDefaultAllowedViewsByLevel(UserLevel level)
        {
            var defaults = new List<string> { NavigationConstants.Views.LoggingListView };

            switch (level)
            {
                case UserLevel.SuperUser:
                case UserLevel.Administrator:
                    return _allSystemViews.Select(v => v.ViewName).ToList();

                case UserLevel.Engineer:
                    defaults.Add(NavigationConstants.Views.ParameterView_CommonParam);
                    defaults.Add(NavigationConstants.Views.ParameterView_SystemConfigParam);
                    defaults.Add(NavigationConstants.Views.ParameterView_UserLoginParam);
                    break;

                case UserLevel.Operator:
                    defaults.Add(NavigationConstants.Views.ParameterView_CommonParam);
                    break;
            }

            return defaults;
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

            _logService.Info($"已将用户 [{SelectedUser.UserName}] 的权限勾选状态重置为 {SelectedUser.Root} 等级的默认状态", "PagePermission");
        }

        private async Task SaveConfigAsync()
        {
            if (SelectedUser == null) return;

            IsLoading = true;
            try
            {
                var currentAllowed = PermissionList.Where(p => p.IsAuthorized).Select(p => p.ViewName).ToList();
                SelectedUser.AccessibleViews = currentAllowed;
                bool isSuccess = await _userService.SaveUserAsync(SelectedUser);

                if (isSuccess)
                {
                    _logService.Info($"用户 [{SelectedUser.UserName}] 权限配置已存入数据库", "PagePermission");
                    MessageService.ShowMessage ($"用户 [{SelectedUser.UserName}] 权限配置已保存！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                     MessageService.ShowMessage ("保存失败，请检查数据库连接。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logService.Error($"保存用户权限时异常: {ex.Message}", "PagePermission", ex);
                 MessageService.ShowMessage ($"保存出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class PermissionCheckItem : BindableBase
    {
        private bool _isAuthorized;
        public string ViewName { get; set; }
        public string Description { get; set; }
        public bool IsAuthorized
        {
            get => _isAuthorized;
            set => SetProperty(ref _isAuthorized, value);
        }
    }
}