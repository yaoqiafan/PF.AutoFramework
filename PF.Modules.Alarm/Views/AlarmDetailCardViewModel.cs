using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Modules.Alarm.Views
{
    /// <summary>报警详情卡片 ViewModel</summary>
    public class AlarmDetailCardViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化报警详情卡片 ViewModel</summary>
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
        /// <summary>获取或设置报警记录</summary>
        public AlarmRecord Alarm
        {
            get { return _AlarmRecord; }
            set { SetProperty(ref _AlarmRecord, value); }
        }

       
        #region 接口实现
        /// <summary>对话框打开时接收报警数据</summary>
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
