using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PF.UI.Infrastructure.Behaviors
{
    /// <summary>
    /// EnterKeyTraversalBehavior 行为
    /// </summary>
    public  class EnterKeyTraversalBehavior
    {
        /// <summary>
        /// IsEnabledProperty
        /// </summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(EnterKeyTraversalBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>
        /// 设置IsEnabled
        /// </summary>
        public static void SetIsEnabled(DependencyObject element, bool value) =>
            element.SetValue(IsEnabledProperty, value);

        /// <summary>
        /// 获取IsEnabled
        /// </summary>
        public static bool GetIsEnabled(DependencyObject element) =>
            (bool)element.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement ui)
            {
                if ((bool)e.NewValue)
                    ui.PreviewKeyDown += Ui_PreviewKeyDown;
                else
                    ui.PreviewKeyDown -= Ui_PreviewKeyDown;
            }
        }

        private static void Ui_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (sender is UIElement element)
                {
                    // 尝试把焦点往下一个可聚焦元素移动
                    element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }
    }
}
