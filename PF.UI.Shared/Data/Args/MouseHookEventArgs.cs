using System;
using PF.UI.Shared.Tools.Interop;

namespace PF.UI.Shared.Data;

public class MouseHookEventArgs : EventArgs
{
    public MouseHookMessageType MessageType { get; set; }

    public InteropValues.POINT Point { get; set; }
}
