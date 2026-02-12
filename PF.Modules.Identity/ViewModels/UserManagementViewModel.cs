using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.Identity.ViewModels
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private ObservableCollection<UserInfo> _users;
        private UserInfo _selectedUser;
        private bool _isBusy;

        public UserManagementViewModel(IUserService userService)
        {
            _userService = userService;

            Users = new ObservableCollection<UserInfo>();
            UserLevels = new ObservableCollection<UserLevel>(Enum.GetValues(typeof(UserLevel)).Cast<UserLevel>());

            RefreshCommand = new DelegateCommand(async () => await LoadData());
            AddUserCommand = new DelegateCommand(ExecuteAddUser);
            SaveUserCommand = new DelegateCommand(async () => await ExecuteSaveUser(), () => SelectedUser != null)
                .ObservesProperty(() => SelectedUser);
            DeleteUserCommand = new DelegateCommand(async () => await ExecuteDeleteUser(), () => SelectedUser != null)
                .ObservesProperty(() => SelectedUser);

            // 初始化加载
            RefreshCommand.Execute();
        }

        public ObservableCollection<UserInfo> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public ObservableCollection<UserLevel> UserLevels { get; }

        public UserInfo SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AddUserCommand { get; }
        public DelegateCommand SaveUserCommand { get; }
        public DelegateCommand DeleteUserCommand { get; }

        private async Task LoadData()
        {
            IsBusy = true;
            var list = await _userService.GetUserListAsync();
            Users = new ObservableCollection<UserInfo>(list);
            IsBusy = false;
        }

        private void ExecuteAddUser()
        {
            var newUser = new UserInfo
            {
                UserId = Guid.NewGuid().ToString("N"),
                UserName = "NewUser",
                Root = UserLevel.Operator,
                Password = "123"
            };
            Users.Add(newUser);
            SelectedUser = newUser;
        }

        private async Task ExecuteSaveUser()
        {
            if (SelectedUser == null) return;

            var result = await _userService.SaveUserAsync(SelectedUser);
            if (result)
            {
                MessageBox.Show("保存成功");
            }
            else
            {
                MessageBox.Show("保存失败");
            }
        }

        private async Task ExecuteDeleteUser()
        {
            if (SelectedUser == null) return;

            if (MessageBox.Show($"确定要删除用户 {SelectedUser.UserName} 吗？", "警告", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var result = await _userService.DeleteUserAsync(SelectedUser);
                if (result)
                {
                    Users.Remove(SelectedUser);
                    SelectedUser = null;
                }
            }
        }
    }
}