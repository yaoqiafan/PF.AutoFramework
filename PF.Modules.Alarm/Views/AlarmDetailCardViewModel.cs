using System.Windows;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;

namespace PF.Modules.Alarm.Views
{
    /// <summary>报警详情卡片 ViewModel</summary>
    public class AlarmDetailCardViewModel : PFDialogViewModelBase
    {
        private SubscriptionToken _resetToken;
        private SubscriptionToken _alarmClearedToken;

        /// <summary>控制"异常复位"按钮的可见性</summary>
        public Visibility ResetButtonVisibility { get; private set; } = Visibility.Visible;

        /// <summary>初始化报警详情卡片 ViewModel</summary>
        public AlarmDetailCardViewModel(IAlarmPermissionService alarmPermission)
        {
            Title = "异常详情";

            ConfirmCommand = new DelegateCommand(() =>
            {
                // 复位会清除全部活跃报警：权限不足时提示并保持弹窗，不发布复位事件（否则其它报警弹窗会被连带关闭）
                if (!alarmPermission.CanReset(out var required))
                {
                    LogService.Info(
                        $"[报警处理] 用户[{CurrentUserName}] 尝试复位被拒绝（需要{required.ToDisplayName()}及以上） | " +
                        $"报警：{Alarm?.Message} | 代码：{Alarm?.ErrorCode} | 来源：{Alarm?.Source}",
                        "操作日志");
                    MessageService.ShowMessage(
                        $"复位被拒绝：当前存在需要「{required.ToDisplayName()}」及以上权限才能清除的报警，" +
                        $"当前用户为「{alarmPermission.CurrentUserLevel.ToDisplayName()}」。\n请登录更高权限账号后再复位。",
                        "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LogService.Info(
                    $"[报警处理] 用户[{CurrentUserName}] 触发系统复位 | " +
                    $"报警：{Alarm?.Message} | 代码：{Alarm?.ErrorCode} | 来源：{Alarm?.Source}",
                    "操作日志");
                _resetToken?.Dispose();
                _resetToken = null;
                EventAggregator.GetEvent<SystemResetRequestedEvent>().Publish();
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.OK });
            });

            CancelCommand = new DelegateCommand(() =>
            {
                LogService.Info(
                    $"[报警处理] 用户[{CurrentUserName}] 查看报警详情后关闭（未复位） | 报警：{Alarm?.Message}",
                    "操作日志");
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.Cancel });
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

            // 订阅报警清除事件（如安全门关闭），匹配到当前报警时自动关闭弹窗
            _alarmClearedToken = EventAggregator.GetEvent<AlarmClearedEvent>()
                .Subscribe(record =>
                {
                    if (Alarm != null
                        && record.Source == Alarm.Source
                        && record.ErrorCode == Alarm.ErrorCode)
                    {
                        RequestClose.Invoke(new DialogResult { Result = ButtonResult.None });
                    }
                }, ThreadOption.UIThread, keepSubscriberReferenceAlive: false);
        }
        /// <summary>
        /// 窗体关闭时触发
        /// </summary>
        public override void OnDialogClosed()
        {
            base.OnDialogClosed();
            _resetToken?.Dispose();
            _resetToken = null;
            _alarmClearedToken?.Dispose();
            _alarmClearedToken = null;
        }
        #endregion
    }
}
