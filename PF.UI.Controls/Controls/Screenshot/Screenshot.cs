using System;
using System.Windows.Media;
using PF.UI.Shared.Data;

namespace PF.Controls;

public class Screenshot
{
    public static event EventHandler<FunctionEventArgs<ImageSource>> Snapped;

    public void Start() => new ScreenshotWindow(this).Show();

    public void OnSnapped(ImageSource source) => Snapped?.Invoke(this, new FunctionEventArgs<ImageSource>(source));
}
