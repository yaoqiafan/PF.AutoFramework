using System;

namespace PF.UI.Shared.Media;

public class DrawingPropertyChangedEventArgs : EventArgs
{
    public bool IsAnimated { get; set; }

    public DrawingPropertyMetadata Metadata { get; set; }
}
