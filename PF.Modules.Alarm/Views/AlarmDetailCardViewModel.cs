using System.Windows;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Modules.Alarm.Views
{
    /// <summary>报警详情卡片 ViewModel</summary>
    public class AlarmDetailCardViewModel : PFDialogViewModelBase
    {
        private SubscriptionToken _resetToken;

        /// <summary>控制"异常复位"按钮的可见性</summary>
        public Visibility ResetButtonVisibility { get; private set; } = Visibility.Visible;

        /// <summary>初始化报警详情卡片 ViewModel</summary>
        public AlarmDetailCardViewModel()
        {
            Title = "异常详情";

            ConfirmCommand = new DelegateCommand(() =>
            {
                _resetToken?.Dispose();
                _resetToken = null;
                EventAggregator.GetEvent<SystemResetRequestedEvent>().Publish();
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.OK });
            });

            CancelCommand = new DelegateCommand(() =>
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel }));
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
                Alarm = parameters.GetValue<AlarmRecord>("Data");
                Title = Alarm.Message;
            }

            bool showReset = !parameters.ContainsKey("ShowResetButton")
                || parameters.GetValue<bool>("ShowResetButton");
            ResetButtonVisibility = showReset ? Visibility.Visible : Visibility.Collapsed;

            // 订阅外部复位事件，触发时关闭弹窗
            _resetToken = EventAggregator.GetEvent<SystemResetRequestedEvent>()
                .Subscribe(
                    () => RequestClose.Invoke(new DialogResult { Result = ButtonResult.None }),
                    ThreadOption.UIThread,
                    keepSubscriberReferenceAlive: false);
        }
        /// <summary>
        /// 窗体关闭时触发
        /// </summary>
        public override void OnDialogClosed()
        {
            base.OnDialogClosed();
            _resetToken?.Dispose();
            _resetToken = null;
        }
        #endregion
    }
}
