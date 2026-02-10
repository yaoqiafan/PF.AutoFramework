using System.Windows;

namespace PF.UI.Infrastructure.Data;

public class CancelRoutedEventArgs : RoutedEventArgs
{
    public CancelRoutedEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }

    public bool Cancel { get; set; }
}
