using System;
using System.Runtime.ConstrainedExecution;
using System.Security;

namespace PF.UI.Shared.Tools.Interop;

public sealed class IconHandle : WpfSafeHandle
{
    [SecurityCritical]
    private IconHandle() : base(true, CommonHandles.Icon)
    {
    }

    [SecurityCritical]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    protected override bool ReleaseHandle()
    {
        return InteropMethods.DestroyIcon(handle);
    }

    [SecurityCritical, SecuritySafeCritical]
    public static IconHandle GetInvalidIcon()
    {
        return new();
    }

    [SecurityCritical]
    public IntPtr CriticalGetHandle()
    {
        return handle;
    }
}
