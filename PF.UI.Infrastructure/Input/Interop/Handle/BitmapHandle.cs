using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace PF.UI.Shared.Tools.Interop;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public sealed class BitmapHandle : WpfSafeHandle
{
    [SecurityCritical]
    private BitmapHandle() : this(true)
    {
        //请不要删除此构造函数，否则当使用自定义ico文件时会报错
    }

    [SecurityCritical]
    private BitmapHandle(bool ownsHandle) : base(ownsHandle, CommonHandles.GDI)
    {
    }

    [SecurityCritical]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    protected override bool ReleaseHandle()
    {
        return InteropMethods.DeleteObject(handle);
    }

    [SecurityCritical]
    public HandleRef MakeHandleRef(object obj)
    {
        return new(obj, handle);
    }

    [SecurityCritical]
    public static BitmapHandle CreateFromHandle(IntPtr hbitmap, bool ownsHandle = true)
    {
        return new(ownsHandle)
        {
            handle = hbitmap,
        };
    }
}
