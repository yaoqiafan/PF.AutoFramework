using System;
using System.Windows;
using System.Windows.Controls;
using PF.UI.Shared.Tools;
using PF.UI.Shared.Tools.Interop;

namespace PF.UI.Controls;

public sealed class GrowlWindow : Window
{
    public Panel GrowlPanel { get; set; }

    public GrowlWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;

        GrowlPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top
        };

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            IsInertiaEnabled = true,
            Content = GrowlPanel
        };
    }

    public void Init()
    {
        var desktopWorkingArea = SystemParameters.WorkArea;
        Height = desktopWorkingArea.Height;
        Left = desktopWorkingArea.Right - Width;
        Top = 0;
    }

    protected override void OnSourceInitialized(EventArgs e)
        => InteropMethods.IntDestroyMenu(this.GetHwndSource().CreateHandleRef());
}
