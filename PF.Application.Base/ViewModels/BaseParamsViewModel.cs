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

        public BaseParamsViewModel(CommonSettings commonSettings)
        {
            _commonSettings = commonSettings;
            SaveCommmand = new DelegateCommand(() => _commonSettings.Save());
        }

        public CommonSettings Params => _commonSettings;

        public ICommand SaveCommmand { get; private set; }
    }
}
