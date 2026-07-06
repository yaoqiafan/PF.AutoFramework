using PF.Application.Base.Configuration;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Windows.Input;

namespace PF.Application.Base.ViewModels
{
    /// <summary>
    /// 系统公共参数页 ViewModel
    /// </summary>
    public class BaseParamsViewModel : RegionViewModelBase
    {
        private readonly CommonSettings _commonSettings;

        /// <summary>注入系统公共配置并初始化保存命令。</summary>
        public BaseParamsViewModel(CommonSettings commonSettings)
        {
            _commonSettings = commonSettings;
            SaveCommmand = new DelegateCommand(() => 
            { 
                _commonSettings.Save();
                MessageService.ShowMessage("参数保存成功！");
            });
        }

        /// <summary>当前系统公共参数对象（绑定到 PropertyGrid）。</summary>
        public CommonSettings Params => _commonSettings;

        /// <summary>保存当前公共参数到 user.config 文件。</summary>
        public ICommand SaveCommmand { get; private set; }
    }
}
