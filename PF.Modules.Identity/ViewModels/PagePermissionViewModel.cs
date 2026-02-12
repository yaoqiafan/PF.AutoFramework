using PF.Core.Enums;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PF.Modules.Identity.ViewModels
{
    public class PagePermissionViewModel : ViewModelBase
    {
        private ObservableCollection<PagePermissionItem> _permissions;

        public PagePermissionViewModel()
        {
            // 模拟加载配置
            // 在实际项目中，这应该从 ConfigService 或 数据库读取
            Permissions = new ObservableCollection<PagePermissionItem>
            {
                new PagePermissionItem { ViewName = "UserManagementView", Description = "用户管理", RequiredLevel = UserLevel.Administrator },
                new PagePermissionItem { ViewName = "ParameterView", Description = "参数设置", RequiredLevel = UserLevel.Engineer },
                new PagePermissionItem { ViewName = "LogListView", Description = "日志查询", RequiredLevel = UserLevel.Operator },
                new PagePermissionItem { ViewName = "ProductionView", Description = "生产监控", RequiredLevel = UserLevel.Null }
            };

            UserLevels = new ObservableCollection<UserLevel>(Enum.GetValues(typeof(UserLevel)).Cast<UserLevel>());
            SaveCommand = new DelegateCommand(SaveConfig);
        }

        public ObservableCollection<PagePermissionItem> Permissions
        {
            get => _permissions;
            set => SetProperty(ref _permissions, value);
        }

        public ObservableCollection<UserLevel> UserLevels { get; }

        public DelegateCommand SaveCommand { get; }

        private void SaveConfig()
        {
            // TODO: 调用配置服务保存 _permissions 列表
            System.Windows.MessageBox.Show("页面权限配置已保存（模拟）");
        }
    }

    // 简单的配置模型类
    public class PagePermissionItem : BindableBase
    {
        private string _viewName;
        private string _description;
        private UserLevel _requiredLevel;

        public string ViewName
        {
            get => _viewName;
            set => SetProperty(ref _viewName, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public UserLevel RequiredLevel
        {
            get => _requiredLevel;
            set => SetProperty(ref _requiredLevel, value);
        }
    }
}