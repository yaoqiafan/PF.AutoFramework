using PF.UI.Shared.Data;
using PF.UI.Shared.Tools;
using PF.UI.Shared.Tools.Extension;
using PF.UI.Shared.Tools.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation; // 新增：用于弹出动画
using System.Windows.Shell;

namespace PF.UI.Controls
{
    [TemplatePart(Name = ElementNonClientArea, Type = typeof(UIElement))]
    public class Window : System.Windows.Window
    {
        #region fields

        private const string ElementNonClientArea = "PART_NonClientArea";

        private bool _isFullScreen;

        private Thickness _actualBorderThickness;

        private readonly Thickness _commonPadding;

        private bool _showNonClientArea = true;

        private double _tempNonClientAreaHeight;

        private WindowState _tempWindowState;

        private WindowStyle _tempWindowStyle;

        private ResizeMode _tempResizeMode;

        private UIElement _nonClientArea;

        #endregion

        #region ctor

        static Window()
        {
            StyleProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata(ResourceHelper.GetResourceInternal<Style>(ResourceToken.WindowWin10)));
        }

        public Window()
        {
            Padding = new Thickness(5);

            var chrome = new WindowChrome
            {
                CornerRadius = new CornerRadius(),
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                UseAeroCaptionButtons = false
            };

            BindingOperations.SetBinding(chrome, WindowChrome.CaptionHeightProperty,
                new Binding(NonClientAreaHeightProperty.Name) { Source = this });
            WindowChrome.SetWindowChrome(this, chrome);
            _commonPadding = Padding;

            // 优化：将命令绑定移到构造函数，避免 Loaded 多次触发时重复绑定
            CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand,
                (s, e) => WindowState = WindowState.Minimized));
            CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand,
                (s, e) => WindowState = WindowState.Maximized));
            CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand,
                (s, e) => WindowState = WindowState.Normal));
            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (s, e) => Close()));
            CommandBindings.Add(new CommandBinding(SystemCommands.ShowSystemMenuCommand, ShowSystemMenu));

            // 优化：使用标准方法订阅 Loaded 事件
            Loaded += OnWindowLoaded;
        }

        #endregion 

        #region prop

        public static readonly DependencyProperty NonClientAreaContentProperty = DependencyProperty.Register(
            nameof(NonClientAreaContent), typeof(object), typeof(Window), new PropertyMetadata(default(object)));

        public object NonClientAreaContent
        {
            get => GetValue(NonClientAreaContentProperty);
            set => SetValue(NonClientAreaContentProperty, value);
        }

        public static readonly DependencyProperty CloseButtonHoverBackgroundProperty = DependencyProperty.Register(
            nameof(CloseButtonHoverBackground), typeof(Brush), typeof(Window),
            new PropertyMetadata(default(Brush)));

        public Brush CloseButtonHoverBackground
        {
            get => (Brush)GetValue(CloseButtonHoverBackgroundProperty);
            set => SetValue(CloseButtonHoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty CloseButtonHoverForegroundProperty =
            DependencyProperty.Register(
                nameof(CloseButtonHoverForeground), typeof(Brush), typeof(Window),
                new PropertyMetadata(default(Brush)));

        public Brush CloseButtonHoverForeground
        {
            get => (Brush)GetValue(CloseButtonHoverForegroundProperty);
            set => SetValue(CloseButtonHoverForegroundProperty, value);
        }

        public static readonly DependencyProperty CloseButtonBackgroundProperty = DependencyProperty.Register(
            nameof(CloseButtonBackground), typeof(Brush), typeof(Window), new PropertyMetadata(Brushes.Transparent));

        public Brush CloseButtonBackground
        {
            get => (Brush)GetValue(CloseButtonBackgroundProperty);
            set => SetValue(CloseButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty CloseButtonForegroundProperty = DependencyProperty.Register(
            nameof(CloseButtonForeground), typeof(Brush), typeof(Window),
            new PropertyMetadata(Brushes.White));

        public Brush CloseButtonForeground
        {
            get => (Brush)GetValue(CloseButtonForegroundProperty);
            set => SetValue(CloseButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty OtherButtonBackgroundProperty = DependencyProperty.Register(
            nameof(OtherButtonBackground), typeof(Brush), typeof(Window), new PropertyMetadata(Brushes.Transparent));

        public Brush OtherButtonBackground
        {
            get => (Brush)GetValue(OtherButtonBackgroundProperty);
            set => SetValue(OtherButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty OtherButtonForegroundProperty = DependencyProperty.Register(
            nameof(OtherButtonForeground), typeof(Brush), typeof(Window),
            new PropertyMetadata(Brushes.White));

        public Brush OtherButtonForeground
        {
            get => (Brush)GetValue(OtherButtonForegroundProperty);
            set => SetValue(OtherButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty OtherButtonHoverBackgroundProperty = DependencyProperty.Register(
            nameof(OtherButtonHoverBackground), typeof(Brush), typeof(Window),
            new PropertyMetadata(default(Brush)));

        public Brush OtherButtonHoverBackground
        {
            get => (Brush)GetValue(OtherButtonHoverBackgroundProperty);
            set => SetValue(OtherButtonHoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty OtherButtonHoverForegroundProperty =
            DependencyProperty.Register(
                nameof(OtherButtonHoverForeground), typeof(Brush), typeof(Window),
                new PropertyMetadata(default(Brush)));

        public Brush OtherButtonHoverForeground
        {
            get => (Brush)GetValue(OtherButtonHoverForegroundProperty);
            set => SetValue(OtherButtonHoverForegroundProperty, value);
        }

        public static readonly DependencyProperty NonClientAreaBackgroundProperty = DependencyProperty.Register(
            nameof(NonClientAreaBackground), typeof(Brush), typeof(Window),
            new FrameworkPropertyMetadata(default(Brush), FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush NonClientAreaBackground
        {
            get => (Brush)GetValue(NonClientAreaBackgroundProperty);
            set => SetValue(NonClientAreaBackgroundProperty, value);
        }

        public static readonly DependencyProperty NonClientAreaForegroundProperty = DependencyProperty.Register(
            nameof(NonClientAreaForeground), typeof(Brush), typeof(Window),
            new FrameworkPropertyMetadata(default(Brush), FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush NonClientAreaForeground
        {
            get => (Brush)GetValue(NonClientAreaForegroundProperty);
            set => SetValue(NonClientAreaForegroundProperty, value);
        }

        public static readonly DependencyProperty NonClientAreaHeightProperty = DependencyProperty.Register(
            nameof(NonClientAreaHeight), typeof(double), typeof(Window),
            new FrameworkPropertyMetadata(22.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public double NonClientAreaHeight
        {
            get => (double)GetValue(NonClientAreaHeightProperty);
            set => SetValue(NonClientAreaHeightProperty, value);
        }

        public static readonly DependencyProperty ShowNonClientAreaProperty = DependencyProperty.Register(
            nameof(ShowNonClientArea), typeof(bool), typeof(Window),
            new PropertyMetadata(ValueBoxes.TrueBox, OnShowNonClientAreaChanged));

        public bool ShowNonClientArea
        {
            get => (bool)GetValue(ShowNonClientAreaProperty);
            set => SetValue(ShowNonClientAreaProperty, ValueBoxes.BooleanBox(value));
        }

        public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(
            nameof(ShowTitle), typeof(bool), typeof(Window),
            new FrameworkPropertyMetadata(ValueBoxes.TrueBox, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowTitle
        {
            get => (bool)GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, ValueBoxes.BooleanBox(value));
        }

        public static readonly DependencyProperty IsFullScreenProperty = DependencyProperty.Register(
            nameof(IsFullScreen), typeof(bool), typeof(Window),
            new PropertyMetadata(ValueBoxes.FalseBox, OnIsFullScreenChanged));

        public bool IsFullScreen
        {
            get => (bool)GetValue(IsFullScreenProperty);
            set => SetValue(IsFullScreenProperty, ValueBoxes.BooleanBox(value));
        }

        public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(
            nameof(ShowIcon), typeof(bool), typeof(Window),
            new FrameworkPropertyMetadata(ValueBoxes.TrueBox, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        // 独立的界面图标依赖属性，与原生Icon区分开
        public static readonly DependencyProperty UIIconProperty = DependencyProperty.Register(
            nameof(UIIcon), typeof(ImageSource), typeof(Window),
            new FrameworkPropertyMetadata(default(ImageSource), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public ImageSource UIIcon
        {
            get => (ImageSource)GetValue(UIIconProperty);
            set => SetValue(UIIconProperty, value);
        }

        #endregion

        #region methods

        #region public

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Debug.WriteLine($"OnApplyTemplate - Padding value: {Padding}");
            _nonClientArea = GetTemplateChild(ElementNonClientArea) as UIElement;
        }

        #endregion

        #region protected

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.GetHwndSource()?.AddHook(HwndSourceHook);
        }

        // 优化：重写 OnClosing 并在其中移除 HwndSourceHook，避免在关闭后获取 Handle 报错
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.RemoveHook(HwndSourceHook);
            }
            base.OnClosing(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                _tempNonClientAreaHeight = NonClientAreaHeight;
                NonClientAreaHeight += 0;
            }
            else
            {
                BorderThickness = _actualBorderThickness;
                NonClientAreaHeight = _tempNonClientAreaHeight;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnWindowLoaded; // 解绑，确保初始化逻辑只走一次

            // 触发弹出动画
            PlayPopupAnimation();

            _actualBorderThickness = BorderThickness;
            _tempNonClientAreaHeight = NonClientAreaHeight;

            if (WindowState == WindowState.Maximized)
            {
                _tempNonClientAreaHeight += 0;
            }

            _tempWindowState = WindowState;
            _tempWindowStyle = WindowStyle;
            _tempResizeMode = ResizeMode;

            SwitchIsFullScreen(_isFullScreen);
            SwitchShowNonClientArea(_showNonClientArea);

            if (WindowState == WindowState.Maximized)
            {
                _tempNonClientAreaHeight -= 0;
            }

            if (SizeToContent != SizeToContent.WidthAndHeight)
                return;

            SizeToContent = SizeToContent.Height;
            Dispatcher.BeginInvoke(new Action(() => { SizeToContent = SizeToContent.WidthAndHeight; }));
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (SizeToContent == SizeToContent.WidthAndHeight)
                InvalidateMeasure();
        }

        #endregion

        #region private

        // 新增：执行弹出动画
        private void PlayPopupAnimation()
        {
            // 1. 窗口本身的透明度可以直接做动画 (Windows 系统底层支持)
            this.Opacity = 0;
            var duration = new Duration(TimeSpan.FromMilliseconds(300));
            var opacityAnim = new DoubleAnimation(0, 1.0, duration);
            this.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

            // 2. 窗口对象不支持 RenderTransform，我们获取它的内部根节点（模板的 Root）进行缩放
            if (VisualTreeHelper.GetChildrenCount(this) > 0)
            {
                if (VisualTreeHelper.GetChild(this, 0) is UIElement rootChild)
                {
                    // 设置缩放中心点为中心
                    rootChild.RenderTransformOrigin = new Point(0.5, 0.5);

                    // 初始化缩放变换
                    var scaleTransform = new ScaleTransform(0.8, 0.8);
                    rootChild.RenderTransform = scaleTransform;

                    var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
                    var scaleAnim = new DoubleAnimation(0.8, 1.0, duration) { EasingFunction = easing };

                    // 执行缩放动画
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            }
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = (InteropValues.MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(InteropValues.MINMAXINFO));
            var monitor = InteropMethods.MonitorFromWindow(hwnd, InteropValues.MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero && mmi != null)
            {
                InteropValues.APPBARDATA appBarData = default;
                var autoHide = InteropMethods.SHAppBarMessage(4, ref appBarData) != 0;
                if (autoHide)
                {
                    var monitorInfo = default(InteropValues.MONITORINFO);
                    monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(InteropValues.MONITORINFO));
                    InteropMethods.GetMonitorInfo(monitor, ref monitorInfo);
                    var rcWorkArea = monitorInfo.rcWork;
                    var rcMonitorArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top - 1);
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            switch (msg)
            {
                case InteropValues.WM_WINDOWPOSCHANGED:
                    Padding = WindowState == WindowState.Maximized ? WindowHelper.WindowMaximizedPadding : _commonPadding;
                    break;
                case InteropValues.WM_GETMINMAXINFO:
                    WmGetMinMaxInfo(hwnd, lparam);
                    Padding = WindowState == WindowState.Maximized ? WindowHelper.WindowMaximizedPadding : _commonPadding;
                    break;
                case InteropValues.WM_NCHITTEST:
                    try
                    {
                        _ = lparam.ToInt32();
                    }
                    catch (OverflowException)
                    {
                        handled = true;
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private static void OnShowNonClientAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && e.NewValue is bool showNonClientArea)
            {
                window.SwitchShowNonClientArea(showNonClientArea);
            }
        }

        private void SwitchShowNonClientArea(bool showNonClientArea)
        {
            if (_nonClientArea == null)
            {
                _showNonClientArea = showNonClientArea;
                return;
            }

            if (showNonClientArea)
            {
                if (IsFullScreen)
                {
                    _nonClientArea.Show(false);
                    _tempNonClientAreaHeight = NonClientAreaHeight;
                    NonClientAreaHeight = 0;
                }
                else
                {
                    _nonClientArea.Show(true);
                    NonClientAreaHeight = _tempNonClientAreaHeight;
                }
            }
            else
            {
                _nonClientArea.Show(false);
                _tempNonClientAreaHeight = NonClientAreaHeight;
                NonClientAreaHeight = 0;
            }
        }

        private static void OnIsFullScreenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && e.NewValue is bool isFullScreen)
            {
                window.SwitchIsFullScreen(isFullScreen);
            }
        }

        private void SwitchIsFullScreen(bool isFullScreen)
        {
            if (_nonClientArea == null)
            {
                _isFullScreen = isFullScreen;
                return;
            }

            if (isFullScreen)
            {
                _nonClientArea.Show(false);
                _tempNonClientAreaHeight = NonClientAreaHeight;
                NonClientAreaHeight = 0;

                _tempWindowState = WindowState;
                _tempWindowStyle = WindowStyle;
                _tempResizeMode = ResizeMode;
                WindowStyle = WindowStyle.None;

                // 下面三行不能改变，为了刷新 WindowChrome 的 Workaround
                WindowState = WindowState.Maximized;
                WindowState = WindowState.Minimized;
                WindowState = WindowState.Maximized;
            }
            else
            {
                if (ShowNonClientArea)
                {
                    _nonClientArea.Show(true);
                    NonClientAreaHeight = _tempNonClientAreaHeight;
                }
                else
                {
                    _nonClientArea.Show(false);
                    _tempNonClientAreaHeight = NonClientAreaHeight;
                    NonClientAreaHeight = 0;
                }

                WindowState = _tempWindowState;
                WindowStyle = _tempWindowStyle;
                ResizeMode = _tempResizeMode;
            }
        }

        private void ShowSystemMenu(object sender, ExecutedRoutedEventArgs e)
        {
            var point = WindowState == WindowState.Maximized
                ? new Point(0, NonClientAreaHeight)
                : new Point(Left, Top + NonClientAreaHeight);
            SystemCommands.ShowSystemMenu(this, point);
        }

        #endregion

        #endregion
    }
}