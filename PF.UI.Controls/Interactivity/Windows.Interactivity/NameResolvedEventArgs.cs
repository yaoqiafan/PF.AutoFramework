using System;

namespace PF.UI.Controls;

internal sealed class NameResolvedEventArgs : EventArgs
{
    public NameResolvedEventArgs(object oldObject, object newObject)
    {
        OldObject = oldObject;
        NewObject = newObject;
    }

    public object NewObject { get; }

    public object OldObject { get; }
}
