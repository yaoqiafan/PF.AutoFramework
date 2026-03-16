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
    public class BaseParamsViewModel : ViewModelBase
    {
        private readonly CommonSettings _commonSettings;
        public BaseParamsViewModel(CommonSettings commonSettings )
        {
            _commonSettings = commonSettings;
            SaveCommmand = new DelegateCommand(() => { _commonSettings.Save(); });
        }



        public CommonSettings Params => _commonSettings;

        public ICommand SaveCommmand { get; private set; }
    }
}
