using System;

namespace PF.UI.Controls;

public class PreviewInvokeEventArgs : EventArgs
{
    public bool Cancelling { get; set; }
}
