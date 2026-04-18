using PF.Application.Shell.CustomConfiguration.Param;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PF.Application.Shell.ViewModels
{
    /// <summary>
    /// ViewModelBase 视图模型
    /// </summary>
    public class BaseParamsViewModel : ViewModelBase
    {
        private readonly CommonSettings _commonSettings;
        /// <summary>
        /// BaseParamsViewModel 视图模型
        /// </summary>
        public BaseParamsViewModel(CommonSettings commonSettings )
        {
            _commonSettings = commonSettings;
            SaveCommmand = new DelegateCommand(() => { _commonSettings.Save(); });
        }



        /// <summary>
        /// Params
        /// </summary>
        public CommonSettings Params => _commonSettings;

        /// <summary>
        /// SaveCommmand
        /// </summary>
        public ICommand SaveCommmand { get; private set; }
    }
}
