using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using PF.UI.Shared.Tools.Interop;

namespace PF.UI.Shared.Data;

public class GlowBitmap : DisposableObject
{
    public const int GlowBitmapPartCount = 16;

    private const int BytesPerPixelBgra32 = 4;

    private static readonly CachedBitmapInfo[] _transparencyMasks = new CachedBitmapInfo[GlowBitmapPartCount];

    private readonly InteropValues.BITMAPINFO _bitmapInfo;

    private readonly IntPtr _pbits;

    public GlowBitmap(IntPtr hdcScreen, int width, int height)
    {
        _bitmapInfo.biSize = Marshal.SizeOf(typeof(InteropValues.BITMAPINFOHEADER));
        _bitmapInfo.biPlanes = 1;
        _bitmapInfo.biBitCount = 32;
        _bitmapInfo.biCompression = 0;
        _bitmapInfo.biXPelsPerMeter = 0;
        _bitmapInfo.biYPelsPerMeter = 0;
        _bitmapInfo.biWidth = width;
        _bitmapInfo.biHeight = -height;

        Handle = InteropMethods.CreateDIBSection(
            hdcScreen,
            ref _bitmapInfo,
            0u,
            out _pbits,
            IntPtr.Zero,
            0u);
    }

    public IntPtr Handle { get; }

    public IntPtr DIBits => _pbits;

    public int Width => _bitmapInfo.biWidth;

    public int Height => -_bitmapInfo.biHeight;

    protected override void DisposeNativeResources() => InteropMethods.DeleteObject(Handle);

    private static byte PremultiplyAlpha(byte channel, byte alpha) => (byte) (channel * alpha / 255.0);

    public static GlowBitmap Create(GlowDrawingContext drawingContext, GlowBitmapPart bitmapPart, Color color)
    {
        var orCreateAlphaMask =
            GetOrCreateAlphaMask(bitmapPart);

        var glowBitmap =
            new GlowBitmap(
                drawingContext.ScreenDC,
                orCreateAlphaMask.Width,
                orCreateAlphaMask.Height);

        for (var i = 0; i < orCreateAlphaMask.DIBits.Length; i += BytesPerPixelBgra32)
        {
            var b = orCreateAlphaMask.DIBits[i + 3];
            var val = PremultiplyAlpha(color.R, b);
            var val2 = PremultiplyAlpha(color.G, b);
            var val3 = PremultiplyAlpha(color.B, b);
            Marshal.WriteByte(glowBitmap.DIBits, i, val3);
            Marshal.WriteByte(glowBitmap.DIBits, i + 1, val2);
            Marshal.WriteByte(glowBitmap.DIBits, i + 2, val);
            Marshal.WriteByte(glowBitmap.DIBits, i + 3, b);
        }

        return glowBitmap;
    }

    private static CachedBitmapInfo GetOrCreateAlphaMask(GlowBitmapPart bitmapPart)
    {
        if (_transparencyMasks[(int) bitmapPart] == null)
        {
            var bitmapImage = new BitmapImage(new Uri($"pack://application:,,,/HandyControl;Component/Resources/Images/GlowWindow/{bitmapPart}.png"));

            var array = new byte[BytesPerPixelBgra32 * bitmapImage.PixelWidth * bitmapImage.PixelHeight];
            var stride = BytesPerPixelBgra32 * bitmapImage.PixelWidth;
            bitmapImage.CopyPixels(array, stride, 0);
            bitmapImage.Freeze();

            _transparencyMasks[(int) bitmapPart] =
                new CachedBitmapInfo(
                    array,
                    bitmapImage.PixelWidth,
                    bitmapImage.PixelHeight);
        }

        return _transparencyMasks[(int) bitmapPart];
    }

    private sealed class CachedBitmapInfo
    {
        public readonly byte[] DIBits;
        public readonly int Height;
        public readonly int Width;

        public CachedBitmapInfo(byte[] diBits, int width, int height)
        {
            Width = width;
            Height = height;
            DIBits = diBits;
        }
    }
}
