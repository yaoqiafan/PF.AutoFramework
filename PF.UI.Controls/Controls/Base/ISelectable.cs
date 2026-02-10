using System.Windows;

namespace PF.Controls;

public interface ISelectable
{
    event RoutedEventHandler Selected;

    bool IsSelected { get; set; }
}
