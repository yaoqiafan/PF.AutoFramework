using System;

namespace PF.Controls;

public class PreviewInvokeEventArgs : EventArgs
{
    public bool Cancelling { get; set; }
}
