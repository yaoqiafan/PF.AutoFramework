using System;
using PF.UI.Shared.Tools.Interop;

namespace PF.UI.Shared.Data;

public class GlowDrawingContext : DisposableObject
{
    private readonly GlowBitmap _windowBitmap;

    public InteropValues.BLENDFUNCTION Blend;

    public GlowDrawingContext(int width, int height)
    {
        ScreenDC = InteropMethods.GetDC(IntPtr.Zero);
        if (ScreenDC == IntPtr.Zero) return;
        WindowDC = InteropMethods.CreateCompatibleDC(ScreenDC);
        if (WindowDC == IntPtr.Zero) return;
        BackgroundDC = InteropMethods.CreateCompatibleDC(ScreenDC);
        if (BackgroundDC == IntPtr.Zero) return;
        Blend.BlendOp = 0;
        Blend.BlendFlags = 0;
        Blend.SourceConstantAlpha = 255;
        Blend.AlphaFormat = 1;
        _windowBitmap = new GlowBitmap(ScreenDC, width, height);
        InteropMethods.SelectObject(WindowDC, _windowBitmap.Handle);
    }

    public bool IsInitialized =>
        ScreenDC != IntPtr.Zero && WindowDC != IntPtr.Zero &&
        BackgroundDC != IntPtr.Zero && _windowBitmap != null;

    public IntPtr ScreenDC { get; }

    public IntPtr WindowDC { get; }

    public IntPtr BackgroundDC { get; }

    public int Width => _windowBitmap.Width;

    public int Height => _windowBitmap.Height;

    protected override void DisposeManagedResources() => _windowBitmap.Dispose();

    protected override void DisposeNativeResources()
    {
        if (ScreenDC != IntPtr.Zero) InteropMethods.ReleaseDC(IntPtr.Zero, ScreenDC);
        if (WindowDC != IntPtr.Zero) InteropMethods.DeleteDC(WindowDC);
        if (BackgroundDC != IntPtr.Zero) InteropMethods.DeleteDC(BackgroundDC);
    }
}
