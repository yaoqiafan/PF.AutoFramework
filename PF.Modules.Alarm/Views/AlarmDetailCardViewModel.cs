using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Modules.Alarm.Views
{
    public class AlarmDetailCardViewModel : PFDialogViewModelBase
    {
        public AlarmDetailCardViewModel()
        {
            Title = "异常详情";


            ConfirmCommand = new DelegateCommand(() => { EventAggregator.GetEvent<SystemResetRequestedEvent>().Publish(); RequestClose.Invoke(new DialogResult()
            {
                Result = ButtonResult.OK,
            });
            });

            CancelCommand = new DelegateCommand(() =>
            {
                RequestClose.Invoke(new DialogResult()
                {
                    Result = ButtonResult.Cancel,
                });
            });
        }

        private AlarmRecord _AlarmRecord;
        public AlarmRecord Alarm
        {
            get { return _AlarmRecord; }
            set { SetProperty(ref _AlarmRecord, value); }
        }

       
        #region 接口实现
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            if (parameters.ContainsKey("Data"))
            {
                var paramItem = parameters.GetValue<AlarmRecord>("Data");
                Alarm = paramItem;
                Title = Alarm.Message;
            }
        }


        #endregion


    }
}
