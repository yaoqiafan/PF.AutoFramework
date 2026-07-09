using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PF.Application.Base.ViewModels
{
    /// <summary>
    /// 框架主显界面 ViewModel：展示项目品牌信息（软件名/公司名/版本）与产品图片。
    /// 图片、简介、说明书路径均来自 <see cref="MainWindowViewModelBase"/>，由各消费项目在
    /// Shell 层的具体子类中重写提供，本类只做只读透传与"打开说明书"命令。
    /// </summary>
    public class MainViewModel : RegionViewModelBase
    {
        private readonly MainWindowViewModelBase _mainWindowViewModel;

        /// <summary>注入主窗口 ViewModel（DI 单例）以读取品牌/产品展示信息。</summary>
        public MainViewModel(MainWindowViewModelBase mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
            OpenManualCommand = new DelegateCommand(OpenManual, () => HasUserManual);

            // 以下为占位命令：先把按钮位置和交互都铺好，具体功能后续再接
            CheckUpdateCommand = new DelegateCommand(() => ShowPlaceholder("检查更新"));
            ContactSupportCommand = new DelegateCommand(() => ShowPlaceholder("联系技术支持"));
            AboutCommand = new DelegateCommand(() => ShowPlaceholder("关于软件"));
        }

        /// <summary>软件名称。</summary>
        public string SoftWareName => _mainWindowViewModel.SoftWareName;

        /// <summary>公司名称。</summary>
        public string CoName => _mainWindowViewModel.CoName;

        /// <summary>软件版本号。</summary>
        public string SoftWareVersion => _mainWindowViewModel.SoftWareVersion;

        /// <summary>产品实物图片路径（项目在 Shell 层重写提供，可为空）。</summary>
        public string ProductImagePath => _mainWindowViewModel.ProductImagePath;

        /// <summary>产品简介文字（项目在 Shell 层重写提供，可为空）。</summary>
        public string ProductDescription => _mainWindowViewModel.ProductDescription;

        /// <summary>说明书文件是否存在，用于控制"打开说明书"按钮的可用性。</summary>
        public bool HasUserManual =>
            !string.IsNullOrWhiteSpace(_mainWindowViewModel.UserManualPath) &&
            File.Exists(_mainWindowViewModel.UserManualPath);

        /// <summary>打开说明书命令：用系统默认关联程序打开配置的说明书文件。</summary>
        public ICommand OpenManualCommand { get; }

        /// <summary>检查更新命令（占位，待接入实际更新检测逻辑）。</summary>
        public ICommand CheckUpdateCommand { get; }

        /// <summary>联系技术支持命令（占位，待接入实际联系方式/工单入口）。</summary>
        public ICommand ContactSupportCommand { get; }

        /// <summary>关于软件命令（占位，待接入关于对话框）。</summary>
        public ICommand AboutCommand { get; }

        private void OpenManual()
        {
            try
            {
                Process.Start(new ProcessStartInfo(_mainWindowViewModel.UserManualPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"无法打开说明书：{ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowPlaceholder(string feature) =>
            MessageService.ShowMessage($"「{feature}」功能开发中，敬请期待。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
