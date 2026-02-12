using System.Windows;
using PF.UI.Shared.Data;

namespace PF.UI.Controls;

public class PasswordBoxAttach
{
    /// <summary>
    ///     密码长度
    /// </summary>
    public static readonly DependencyProperty PasswordLengthProperty = DependencyProperty.RegisterAttached(
        "PasswordLength", typeof(int), typeof(PasswordBoxAttach), new PropertyMetadata(ValueBoxes.Int0Box));

    public static void SetPasswordLength(DependencyObject element, int value) => element.SetValue(PasswordLengthProperty, value);

    public static int GetPasswordLength(DependencyObject element) => (int) element.GetValue(PasswordLengthProperty);

    /// <summary>
    ///     是否监测
    /// </summary>
    public static readonly DependencyProperty IsMonitoringProperty = DependencyProperty.RegisterAttached(
        "IsMonitoring", typeof(bool), typeof(PasswordBoxAttach), new FrameworkPropertyMetadata(ValueBoxes.FalseBox, FrameworkPropertyMetadataOptions.Inherits, OnIsMonitoringChanged));

    private static void OnIsMonitoringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is System.Windows.Controls.PasswordBox passwordBox)
        {
            if (e.NewValue is bool boolValue)
            {
                if (boolValue)
                {
                    passwordBox.PasswordChanged += OnPasswordChanged;
                }
                else
                {
                    passwordBox.PasswordChanged -= OnPasswordChanged;
                }
            }
        }
    }

    public static void SetIsMonitoring(DependencyObject element, bool value) => element.SetValue(IsMonitoringProperty, ValueBoxes.BooleanBox(value));

    public static bool GetIsMonitoring(DependencyObject element) => (bool) element.GetValue(IsMonitoringProperty);

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            SetPasswordLength(passwordBox, passwordBox.Password.Length);
        }
    }



    /// <summary>
    /// 注册附加依赖属性"Password"，类型为string，当其值改变时触发OnPasswordPropertyChanged方法
    /// </summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
    "Password",
    typeof(string),
    typeof(PasswordBoxAttach),
    new FrameworkPropertyMetadata(string.Empty, OnPasswordPropertyChanged));

    /// <summary>
    /// 注册附加依赖属性"Attach"，表示是否需要启用密码绑定功能，当其值改变时触发Attach方法
    /// </summary>
    public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached(
    "Attach",
    typeof(bool),
    typeof(PasswordBoxAttach),
    new PropertyMetadata(false, Attach));

    /// <summary>
    /// 私有静态依赖属性"IsUpdating"，用于跟踪PasswordBox密码更新状态
    /// </summary>
    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
    "IsUpdating",
    typeof(bool), typeof(PasswordBoxAttach));

    /// <summary>
    /// 设置附加属性"Attach"的值
    /// </summary>
    public static void SetAttach(DependencyObject dp, bool value)
    {
        dp.SetValue(AttachProperty, value);
    }

    /// <summary>
    /// 取附加属性"Attach"的值
    /// </summary>
    public static bool GetAttach(DependencyObject dp)
    {
        return (bool)dp.GetValue(AttachProperty);
    }

    /// <summary>
    /// 获取附加属性"Password"的值
    /// </summary>
    public static string GetPassword(DependencyObject dp)
    {
        return (string)dp.GetValue(PasswordProperty);
    }

    /// <summary>
    /// 置附加属性"Password"的值
    /// </summary>
    public static void SetPassword(DependencyObject dp, string value)
    {
        dp.SetValue(PasswordProperty, value);
    }

    /// <summary>
    /// 获取附加属性"IsUpdating"的值
    /// </summary>
    private static bool GetIsUpdating(DependencyObject dp)
    {
        return (bool)dp.GetValue(IsUpdatingProperty);
    }

    /// <summary>
    /// 设置附加属性"IsUpdating"的值
    /// </summary>
    private static void SetIsUpdating(DependencyObject dp, bool value)
    {
        dp.SetValue(IsUpdatingProperty, value);
    }

    /// <summary>
    /// 当"Password"属性发生变化时调用此方法，同步PasswordBox的实际密码与绑定值
    /// </summary>
    private static void OnPasswordPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            passwordBox.PasswordChanged -= PasswordChanged;

            // 防止在更新过程中引发无限循环
            if (!(bool)GetIsUpdating(passwordBox))
            {
                passwordBox.Password = (string)e.NewValue;
            }
            passwordBox.PasswordChanged += PasswordChanged;
        }
    }

    /// <summary>
    /// 当"Attach"属性值变化时（即绑定或解绑事件处理程序）调用此方法
    /// </summary>
    private static void Attach(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(sender is System.Windows.Controls.PasswordBox passwordBox))
            return;
        if ((bool)e.OldValue)
        {
            passwordBox.PasswordChanged -= PasswordChanged;
        }
        if ((bool)e.NewValue)
        {
            passwordBox.PasswordChanged += PasswordChanged;
        }
    }

    /// <summary>
    /// 密码更改事件处理程序，当PasswordBox的密码发生更改时，更新绑定到Password属性的值
    /// </summary>
    private static void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            SetIsUpdating(passwordBox, true);
            SetPassword(passwordBox, passwordBox.Password);
            SetIsUpdating(passwordBox, false);
        }
    }
}
