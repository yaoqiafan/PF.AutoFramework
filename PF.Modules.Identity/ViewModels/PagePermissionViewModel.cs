using PF.Core.Constants;
using PF.Core.Entities.Identity; //
using PF.Core.Enums; //
using PF.Core.Interfaces.Identity; //
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PF.Modules.Identity.ViewModels
{
    public class PagePermissionViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private ObservableCollection<PermissionCheckItem> _permissionList;
        private ObservableCollection<UserInfo> _users;
        private UserInfo _selectedUser;

        // 模拟数据库：存储用户的个性化权限配置 (Key: UserId, Value: List of Allowed ViewNames)
        // 实际开发中，这应该是一个 PermissionService
        private static Dictionary<string, List<string>> _userCustomPermissions = new Dictionary<string, List<string>>();

        // 所有系统可用页面定义
        private readonly List<PermissionCheckItem> _allSystemViews;

        public PagePermissionViewModel(IUserService userService)
        {
            _userService = userService;

            // 1. 初始化所有可用页面
            _allSystemViews = new List<PermissionCheckItem>
            {
                new PermissionCheckItem { ViewName = NavigationConstants.Views.UserManagementView, Description = "用户管理" },
                new PermissionCheckItem { ViewName = NavigationConstants.Views.ParameterView_SystemConfigParam, Description = "参数设置(系统)" },
                new PermissionCheckItem { ViewName = NavigationConstants.Views.LoggingListView, Description = "日志查询" },
                new PermissionCheckItem { ViewName = "ProductionView", Description = "生产监控" },
                new PermissionCheckItem { ViewName = NavigationConstants.Views.ParameterView_CommonParam, Description = "通用参数" },
                new PermissionCheckItem { ViewName = NavigationConstants.Views.ParameterView_UserLoginParam, Description = "登录设置" }
            };

            SaveCommand = new DelegateCommand(SaveConfig);
            RefreshUsersCommand = new DelegateCommand(LoadUsers);

            // 初始加载用户列表
            LoadUsers();
        }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RefreshUsersCommand { get; }

        /// <summary>
        /// 用户列表
        /// </summary>
        public ObservableCollection<UserInfo> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        /// <summary>
        /// 当前选中的用户
        /// </summary>
        public UserInfo SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    if (value != null)
                    {
                        LoadPermissionsForUser(value);
                    }
                    else
                    {
                        PermissionList = null;
                    }
                }
            }
        }

        /// <summary>
        /// 当前用户的页面权限列表
        /// </summary>
        public ObservableCollection<PermissionCheckItem> PermissionList
        {
            get => _permissionList;
            set => SetProperty(ref _permissionList, value);
        }

        private async void LoadUsers()
        {
            // 调用 UserService 获取真实用户列表
            var users = await _userService.GetUserListAsync();
            Users = users;

            // 默认选中第一个用户方便操作
            if (Users != null && Users.Any())
            {
                SelectedUser = Users.First();
            }
        }

        /// <summary>
        /// 加载特定用户的权限
        /// </summary>
        private void LoadPermissionsForUser(UserInfo user)
        {
            // 1. 创建显示列表（从所有页面模板复制）
            var displayList = _allSystemViews.Select(v => new PermissionCheckItem
            {
                ViewName = v.ViewName,
                Description = v.Description,
                IsAuthorized = false
            }).ToList();

            List<string> allowedViews;

            // 2. 判断逻辑：
            // A. 如果该用户之前已经单独保存过配置（个性化），则加载个性化配置
            // B. 如果没有，则根据其 UserLevel 加载默认的角色模板
            if (user.UserId != null && _userCustomPermissions.ContainsKey(user.UserId))
            {
                allowedViews = _userCustomPermissions[user.UserId];
            }
            else
            {
                allowedViews = GetDefaultAllowedViewsByLevel(user.Root);
            }

            // 3. 将允许的页面打勾
            foreach (var item in displayList)
            {
                if (allowedViews.Contains(item.ViewName))
                {
                    item.IsAuthorized = true;
                }
            }

            PermissionList = new ObservableCollection<PermissionCheckItem>(displayList);
        }

        /// <summary>
        /// 获取某等级的默认页面列表 (模板逻辑)
        /// </summary>
        private List<string> GetDefaultAllowedViewsByLevel(UserLevel level)
        {
            var defaults = new List<string>();

            // 所有人都默认有的页面
            defaults.Add(NavigationConstants.Views.LoggingListView); // 日志

            // 根据等级追加
            switch (level)
            {
                case UserLevel.SuperUser:
                case UserLevel.Administrator:
                    // 管理员默认拥有所有页面
                    return _allSystemViews.Select(v => v.ViewName).ToList();

                case UserLevel.Engineer:
                    defaults.Add("ProductionView");
                    defaults.Add(NavigationConstants.Views.ParameterView_CommonParam);
                    defaults.Add(NavigationConstants.Views.ParameterView_SystemConfigParam);
                    defaults.Add(NavigationConstants.Views.ParameterView_UserLoginParam);
                    // 工程师默认没有 UserManagementView
                    break;

                case UserLevel.Operator:
                    defaults.Add("ProductionView");
                    defaults.Add(NavigationConstants.Views.ParameterView_CommonParam);
                    // 操作员只有生产和通用参数
                    break;
            }

            return defaults;
        }

        private void SaveConfig()
        {
            if (SelectedUser == null || SelectedUser.UserId == null) return;

            // 获取当前界面上勾选的页面
            var currentAllowed = PermissionList.Where(p => p.IsAuthorized)
                                             .Select(p => p.ViewName)
                                             .ToList();

            // 保存到模拟数据库（覆盖旧配置）
            if (_userCustomPermissions.ContainsKey(SelectedUser.UserId))
            {
                _userCustomPermissions[SelectedUser.UserId] = currentAllowed;
            }
            else
            {
                _userCustomPermissions.Add(SelectedUser.UserId, currentAllowed);
            }

            System.Windows.MessageBox.Show($"用户 [{SelectedUser.UserName}] (等级:{SelectedUser.Root}) 的个性化权限已保存。\n" +
                                           $"包含 {currentAllowed.Count} 个页面。");
        }
    }

    // 权限配置项模型 (保持不变)
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