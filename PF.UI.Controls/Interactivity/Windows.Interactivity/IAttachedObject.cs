using System.Windows;

namespace PF.Controls;

public interface IAttachedObject
{
    void Attach(DependencyObject dependencyObject);
    void Detach();

    DependencyObject AssociatedObject { get; }
}
