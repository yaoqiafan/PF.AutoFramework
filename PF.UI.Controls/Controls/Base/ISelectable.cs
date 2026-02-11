using System.Windows;

namespace PF.UI.Controls;

public interface ISelectable
{
    event RoutedEventHandler Selected;

    bool IsSelected { get; set; }
}
