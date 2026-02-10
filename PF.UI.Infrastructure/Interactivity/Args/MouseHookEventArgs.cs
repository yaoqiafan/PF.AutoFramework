using System;
using PF.UI.Infrastructure.Enum;
using PF.UI.Shared.Tools.Interop;

namespace PF.UI.Infrastructure.Interactivity.Args;

public class MouseHookEventArgs : EventArgs
{
    public MouseHookMessageType MessageType { get; set; }

    public InteropValues.POINT Point { get; set; }
}
