using PF.Core.Interfaces.Identity;
using PF.UI.Infrastructure.PrismBase;
using System;

namespace PF.Modules.Identity.ViewModels
{
    /// <summary>登录对话框 ViewModel</summary>
    public class LoginViewModel : PFDialogViewModelBase
    {
        private readonly IUserService _userService;
        private string _userName;
        private string _password;
        private string _errorMessage;
        private bool _isLoggingIn;

        /// <summary>初始化登录 ViewModel</summary>
        public LoginViewModel(IUserService userService)
        {
            _userService = userService;
            Title = "系统登录";

            LoginCommand = new DelegateCommand(ExecuteLogin, CanExecuteLogin)
                .ObservesProperty(() => UserName)
                .ObservesProperty(() => Password)
                .ObservesProperty(() => IsLoggingIn);

            LogoutCommand = new DelegateCommand(ExecuteLogout, CanExecuteLogout)
                .ObservesProperty(() => IsLoggingIn);

            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        /// <summary>获取或设置用户名</summary>
        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        /// <summary>获取或设置密码</summary>
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>获取或设置错误消息</summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>获取或设置是否正在登录</summary>
        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => SetProperty(ref _isLoggingIn, value);
        }

        /// <summary>登录命令</summary>
        public DelegateCommand LoginCommand { get; }
        /// <summary>取消命令</summary>
        public DelegateCommand CancelCommand { get; }
        /// <summary>注销命令</summary>
        public DelegateCommand LogoutCommand { get; }

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
                    var dialogResult = new DialogResult(ButtonResult.OK);
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

        private bool CanExecuteLogout()
        {
            // 如果服务中有用户处于登录状态，并且当前没有正在执行登录动作，则允许注销
            return _userService.CurrentUser != null && !IsLoggingIn;
        }

        private void ExecuteLogout()
        {
            _userService.Logout();
            ErrorMessage = "当前用户已注销";
            UserName = string.Empty;
            Password = string.Empty;
            // 也可以选择注销后直接关闭窗口: RequestClose.Invoke(new DialogResult(ButtonResult.OK));
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}