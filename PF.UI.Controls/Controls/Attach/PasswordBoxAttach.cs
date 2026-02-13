using System.Windows;
using PF.UI.Shared.Data;

namespace PF.UI.Controls
{
    public class PasswordBoxAttach
    {
        // ---------- 密码长度相关 ----------
        public static readonly DependencyProperty PasswordLengthProperty = DependencyProperty.RegisterAttached(
            "PasswordLength", typeof(int), typeof(PasswordBoxAttach), new PropertyMetadata(ValueBoxes.Int0Box));

        public static void SetPasswordLength(DependencyObject element, int value) => element.SetValue(PasswordLengthProperty, value);
        public static int GetPasswordLength(DependencyObject element) => (int)element.GetValue(PasswordLengthProperty);

        public static readonly DependencyProperty IsMonitoringProperty = DependencyProperty.RegisterAttached(
            "IsMonitoring", typeof(bool), typeof(PasswordBoxAttach),
            new FrameworkPropertyMetadata(ValueBoxes.FalseBox, FrameworkPropertyMetadataOptions.Inherits, OnIsMonitoringChanged));

        public static void SetIsMonitoring(DependencyObject element, bool value) => element.SetValue(IsMonitoringProperty, ValueBoxes.BooleanBox(value));
        public static bool GetIsMonitoring(DependencyObject element) => (bool)element.GetValue(IsMonitoringProperty);

        // ---------- 密码绑定相关 ----------
        public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
            "Password", typeof(string), typeof(PasswordBoxAttach),
            new FrameworkPropertyMetadata(string.Empty, OnPasswordPropertyChanged));

        public static void SetPassword(DependencyObject dp, string value) => dp.SetValue(PasswordProperty, value);
        public static string GetPassword(DependencyObject dp) => (string)dp.GetValue(PasswordProperty);

        public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached(
            "Attach", typeof(bool), typeof(PasswordBoxAttach), new PropertyMetadata(false, OnAttachChanged));

        public static void SetAttach(DependencyObject dp, bool value) => dp.SetValue(AttachProperty, value);
        public static bool GetAttach(DependencyObject dp) => (bool)dp.GetValue(AttachProperty);

        // 内部标志，用于防止 Password 更新时循环触发
        private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
            "IsUpdating", typeof(bool), typeof(PasswordBoxAttach));

        private static bool GetIsUpdating(DependencyObject dp) => (bool)dp.GetValue(IsUpdatingProperty);
        private static void SetIsUpdating(DependencyObject dp, bool value) => dp.SetValue(IsUpdatingProperty, value);

        // ---------- 统一事件订阅管理 ----------
        // 当 IsMonitoring 或 Attach 变化时，调用此方法重新评估是否需要挂载事件
        private static void UpdateEventSubscription(DependencyObject d)
        {
            if (!(d is System.Windows.Controls.PasswordBox passwordBox))
                return;

            bool needEvent = GetIsMonitoring(d) || GetAttach(d);
            bool currentlyHooked = GetIsEventHooked(d);   // 自定义一个内部标志记录当前是否已订阅

            if (needEvent && !currentlyHooked)
            {
                passwordBox.PasswordChanged += OnPasswordChanged;
                SetIsEventHooked(d, true);
            }
            else if (!needEvent && currentlyHooked)
            {
                passwordBox.PasswordChanged -= OnPasswordChanged;
                SetIsEventHooked(d, false);
            }
        }

        // 内部附加属性，用于跟踪当前是否已订阅 PasswordChanged 事件
        private static readonly DependencyProperty IsEventHookedProperty = DependencyProperty.RegisterAttached(
            "IsEventHooked", typeof(bool), typeof(PasswordBoxAttach), new PropertyMetadata(false));

        private static bool GetIsEventHooked(DependencyObject d) => (bool)d.GetValue(IsEventHookedProperty);
        private static void SetIsEventHooked(DependencyObject d, bool value) => d.SetValue(IsEventHookedProperty, value);

        // ---------- 属性变更回调 ----------
        private static void OnIsMonitoringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UpdateEventSubscription(d);
        }

        private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UpdateEventSubscription(d);
        }

        // ---------- 统一事件处理程序 ----------
        private static void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.PasswordBox passwordBox))
                return;

            // 1. 如果启用了密码长度监视，更新 PasswordLength
            if (GetIsMonitoring(passwordBox))
            {
                SetPasswordLength(passwordBox, passwordBox.Password.Length);
            }

            // 2. 如果启用了密码绑定，更新 Password（注意防止循环）
            if (GetAttach(passwordBox))
            {
                // 避免因 Password 属性变化再次触发事件导致无限循环
                if (!GetIsUpdating(passwordBox))
                {
                    SetIsUpdating(passwordBox, true);
                    SetPassword(passwordBox, passwordBox.Password);
                    SetIsUpdating(passwordBox, false);
                }
            }
        }

        // ---------- Password 属性变更回调（用于外部修改时同步到 PasswordBox） ----------
        private static void OnPasswordPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.PasswordBox passwordBox))
                return;

            // 仅在非更新状态下将绑定值赋给 PasswordBox，避免循环
            if (!GetIsUpdating(passwordBox))
            {
                passwordBox.Password = (string)e.NewValue;
            }
        }
    }
}