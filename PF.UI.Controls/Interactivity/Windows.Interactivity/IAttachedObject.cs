using System.Windows;

namespace PF.UI.Controls;

public interface IAttachedObject
{
    void Attach(DependencyObject dependencyObject);
    void Detach();

    DependencyObject AssociatedObject { get; }
}
