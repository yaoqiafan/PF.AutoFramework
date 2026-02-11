using System.Windows;


namespace PF.UI.Shared.Data;

public class FunctionEventArgs<T> : RoutedEventArgs
{
    public FunctionEventArgs(T info)
    {
        Info = info;
    }

    public FunctionEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source)
    {
    }

    public T Info { get; set; }
}
