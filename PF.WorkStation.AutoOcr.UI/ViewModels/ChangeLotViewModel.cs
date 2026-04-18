using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// ChangeLotViewModel
    /// </summary>
    public class ChangeLotViewModel : PFDialogViewModelBase
    {


        #region 参数

        private string _userid = "";
        /// <summary>
        /// 成员
        /// </summary>

        public string UserId
        {
            get => _userid;
            set => SetProperty(ref _userid, value);
        }

        private string _lotid = "";
        /// <summary>
        /// 成员
        /// </summary>

        public string LotId
        {
            get => _lotid;
            set => SetProperty(ref _lotid, value);
        }



                        
       





        private bool _isOk = false;
        /// <summary>
        /// ChangeLotViewModel 构造函数
        /// </summary>

        public ChangeLotViewModel()
        {
            Title = "输入工单工号";
            ConfirmCommand = new DelegateCommand (OK);
            CancelCommand = new DelegateCommand (NG);
            
        }

        #endregion 

        #region Dialog 生命周期
        /// <summary>
        /// OnDialogOpened
        /// </summary>

        public override void OnDialogOpened(IDialogParameters parameters)
        {

        }
        /// <summary>
        /// OnDialogClosed
        /// </summary>

        public override void OnDialogClosed()
        {
           

        }

        #endregion


        private  void OK()
        {
            var param = new DialogParameters { { "Userid", this.UserId }, { "Lotid", this.LotId } };
            RequestClose.Invoke(param, ButtonResult.OK);
        }

        private void NG()
        {
          
            RequestClose.Invoke( ButtonResult.Cancel );

        }

    }
}
