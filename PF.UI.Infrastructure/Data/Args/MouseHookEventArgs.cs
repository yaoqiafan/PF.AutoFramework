using System;
using PF.UI.Infrastructure.Tools.Interop;

namespace PF.UI.Infrastructure.Data;

public class MouseHookEventArgs : EventArgs
{
    public MouseHookMessageType MessageType { get; set; }

    public InteropValues.POINT Point { get; set; }
}
