using PF.Core.Interfaces.Identity;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Modules.Identity.ViewModels
{
    // 继承自已有的 PFDialogViewModelBase
    public class LoginViewModel : PFDialogViewModelBase
    {
        private readonly IUserService _userService;
        private string _userName;
        private string _password;
        private string _errorMessage;
        private bool _isLoggingIn;

        public LoginViewModel(IUserService userService)
        {
            _userService = userService;
            Title = "系统登录"; // 设置弹窗标题

            LoginCommand = new DelegateCommand(ExecuteLogin, CanExecuteLogin)
                .ObservesProperty(() => UserName)
                .ObservesProperty(() => Password)
                .ObservesProperty(() => IsLoggingIn);

            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => SetProperty(ref _isLoggingIn, value);
        }

        public DelegateCommand LoginCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private bool CanExecuteLogin()
        {
            return !string.IsNullOrWhiteSpace(UserName) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !IsLoggingIn;
        }

        private async void ExecuteLogin()
        {
            IsLoggingIn = true;
            ErrorMessage = string.Empty;

            try
            {
                var result = await _userService.LoginAsync(UserName, Password);
                if (result)
                {
                    // 登录成功，关闭弹窗，返回 OK
                    var dialogResult = new DialogResult(ButtonResult.OK);
                    // 假设父类暴露了 RequestClose 属性或方法
                    RequestClose.Invoke(dialogResult);
                }
                else
                {
                    ErrorMessage = "用户名或密码错误";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"登录异常: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}