using Prism.Dialogs;
using System;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace PF.Modules.Alarm.Dialogs
{
    /// <summary>
    /// PFAlarmBaseWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PFAlarmBaseWindow : PF.UI.Controls.Window, IDialogWindow
    {
        public PFAlarmBaseWindow()
        {
            InitializeComponent();

            // 绑定加载事件以触发弹出动画
            this.Loaded += NotificationWindow_Loaded;

            // 绑定关闭命令以触发缩回动画
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, CloseEvent));
        }

        // Prism IDialogWindow 接口所需属性
        public IDialogResult Result { get; set; }

        /// <summary>
        /// 窗体加载时的弹出动画 (从屏幕右侧滑入)
        /// </summary>
        private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.UpdateLayout(); // 确保ActualWidth和ActualHeight已计算

            SystemSounds.Asterisk.Play(); // 播放提示声

            double right = System.Windows.SystemParameters.WorkArea.Right; // 工作区最右边的值
            double topFrom = System.Windows.SystemParameters.WorkArea.Bottom - 5;

            this.Top = topFrom - this.ActualHeight;

            DoubleAnimation animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromMilliseconds(250)); // 动画持续时间
            animation.From = right;
            animation.To = right - this.ActualWidth; // 设定通知从右往左弹出

            this.BeginAnimation(Window.LeftProperty, animation);
        }

        /// <summary>
        /// 窗体关闭时的缩回动画 (向屏幕右侧滑出)
        /// </summary>
        private void CloseEvent(object sender, ExecutedRoutedEventArgs e)
        {
            double right = System.Windows.SystemParameters.WorkArea.Right;

            DoubleAnimation animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromMilliseconds(250));

            // 动画完成后实际关闭窗体
            animation.Completed += (s, a) => { this.Close(); };

            animation.From = right - this.ActualWidth;
            animation.To = right;

            this.BeginAnimation(Window.LeftProperty, animation);
        }

        /// <summary>
        /// 允许通过拖拽移动窗口
        /// </summary>
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}