using System;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PF.UI.Controls;

public class VisualAdornerContainer(UIElement adornedElement) : Adorner(adornedElement)
{
    private Visual? _child;

    public Visual? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value)) return;

            // 1. 【核武器解绑】：暴力拆解任何残留的父级关系！
            if (value != null)
            {
                // 先尝试解绑视觉树 (Visual Tree)
                var parentVisual = VisualTreeHelper.GetParent(value) as Visual;
                if (parentVisual != null && parentVisual != this)
                {
                    if (parentVisual is VisualAdornerContainer oldContainer)
                    {
                        // 如果刚好是我们的同类容器，直接调用受保护方法剥离
                        oldContainer.RemoveVisualChild(value);
                        oldContainer._child = null;
                    }
                    else
                    {
                        // 如果是被其他未知的 WPF 控件锁死了，用反射强行调用底层的 RemoveVisualChild
                        var removeMethod = typeof(Visual).GetMethod("RemoveVisualChild", BindingFlags.Instance | BindingFlags.NonPublic);
                        removeMethod?.Invoke(parentVisual, new object[] { value });
                    }
                }

                // 再尝试解绑逻辑树 (Logical Tree)，防止被死锁
                var logicalParent = LogicalTreeHelper.GetParent(value);
                if (logicalParent != null && logicalParent != this && logicalParent is UIElement parentUI)
                {
                    var removeLogicalMethod = typeof(UIElement).GetMethod("RemoveLogicalChild", BindingFlags.Instance | BindingFlags.NonPublic);
                    removeLogicalMethod?.Invoke(parentUI, new object[] { value });
                }
            }

            // 2. 清理当前新容器自己肚子里可能存在的旧东西
            if (_child != null)
            {
                RemoveVisualChild(_child);
            }

            _child = value;

            // 3. 干净利落地挂载！
            if (_child != null)
            {
                AddVisualChild(_child);

                // 4. 强制立即计算物理布局（保证 Origin 等参数绝对精准）
                if (_child is UIElement uiElement && AdornedElement != null)
                {
                    var size = AdornedElement.RenderSize;
                    if (size.Width > 0 && size.Height > 0)
                    {
                        uiElement.Measure(size);
                        uiElement.Arrange(new Rect(size));
                    }
                }
            }
        }
    }

    protected override int VisualChildrenCount => _child != null ? 1 : 0;

    protected override Visual GetVisualChild(int index)
    {
        if (index == 0 && _child != null) return _child;
        return base.GetVisualChild(index);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (_child is UIElement uiElement)
        {
            uiElement.Measure(constraint);
            return uiElement.DesiredSize;
        }
        return base.MeasureOverride(constraint);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_child is UIElement uiElement)
        {
            uiElement.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}