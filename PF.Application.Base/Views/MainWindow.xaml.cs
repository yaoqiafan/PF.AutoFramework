using PF.Application.Base.Configuration;
using PF.Application.Base.ViewModels;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Production;
using Prism;
using PF.UI.Controls;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SwWindow = System.Windows.Window;

namespace PF.Application.Base.Views
{
    /// <summary>框架主窗口：数据驱动状态栏 + 侧边菜单 + 主题切换 + 登录动画。</summary>
    public partial class MainWindow : PF.UI.Controls.Window
    {
        private readonly IMessageService _messageService;
        private readonly CommonSettings _commonSettings;
        private readonly IEventAggregator _eventAggregator;
        private readonly IEnumerable<IMechanism> _mechanismsList;
        private readonly IContainerProvider _containerProvider;
        private bool _isAnimating;
        private bool _isClosing; // Window_Closing 重入门：防确认对话框期间用户再次点关闭弹出第二个对话框

        /// <summary>注入依赖并完成窗口初始化，订阅用户变更事件与 Loaded 事件。</summary>
        public MainWindow(
            MainWindowViewModelBase viewModel,
            IMessageService messageService,
            CommonSettings commonSettings,
            IEnumerable<IMechanism> mechanisms,
            IEventAggregator eventAggregator,
            IContainerProvider containerProvider)
        {
            InitializeComponent();
            DataContext = viewModel;

            _messageService = messageService;
            _commonSettings = commonSettings;
            _mechanismsList = mechanisms;
            _eventAggregator = eventAggregator;
            _containerProvider = containerProvider;

            _eventAggregator.GetEvent<UserChangedEvent>().Subscribe(OnUserLogined);
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModelBase vm)
            {
                RenderSideMenu(vm.MenuItems);
                vm.MenuItems.CollectionChanged += (s, args) =>
                    Dispatcher.Invoke(() => RenderSideMenu(vm.MenuItems));
            }

            ThemeIcon.Kind = _commonSettings.Skin == SkinType.Dark ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
        }

        private async void OnUserLogined(UserInfo? info)
        {
            if (info == null) return;

            await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                await Task.Delay(500);
                switch (info.Root)
                {
                    case UserLevel.Null:
                        TriggerConfetti(ConfettiEffectType.Snow); break;
                    case UserLevel.Operator:
                        if (_commonSettings.EnableOperatorAnimation)
                            TriggerConfetti(_commonSettings.OperatorAnimationType); break;
                    case UserLevel.Engineer:
                        if (_commonSettings.EnableEngineerAnimation)
                            TriggerConfetti(_commonSettings.EngineerAnimationType); break;
                    case UserLevel.Administrator:
                        if (_commonSettings.EnableAdministratorAnimation)
                            TriggerConfetti(_commonSettings.AdministratorAnimationType); break;
                    case UserLevel.SuperUser:
                        if (_commonSettings.EnableSuperuserAnimation)
                            TriggerConfetti(_commonSettings.SuperuserAnimationType); break;
                }
            }));
        }

        private void RenderSideMenu(ObservableCollection<NavigationItem> menuItems)
        {
            MainSideMenu.Items.Clear();
            foreach (var group in menuItems)
            {
                var groupItem = new SideMenuItem
                {
                    FontSize = 15,
                    Header = group.Title,
                    DataContext = group,
                    Background = (Brush)FindResource("LightPrimaryBrush"),
                    Margin = new Thickness(0, 0, 0, 10),
                    Icon = CreateIconElement(group.Icon),
                    IsExpanded = false
                };
                foreach (var child in group.Children)
                {
                    var childItem = new SideMenuItem
                    {
                        Header = $" {child.Title}",
                        Tag = child,
                        DataContext = child,
                        IsExpanded = false
                    };
                    AttachChildIcon(childItem, child.Icon);
                    groupItem.Items.Add(childItem);
                }
                MainSideMenu.Items.Add(groupItem);
            }
        }

        private object CreateIconElement(string iconStr)
        {
            if (string.IsNullOrEmpty(iconStr)) return null;
            if (iconStr.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var image = new Image();
                string packUri = iconStr.StartsWith("/") ? $"pack://application:,,,{iconStr}" : iconStr;
                image.Source = new BitmapImage(new Uri(packUri, UriKind.Absolute));
                image.SetResourceReference(FrameworkElement.StyleProperty, "IconStyle");
                return image;
            }
            else
            {
                var path = new Path { Width = 20, Height = 20, Stretch = Stretch.Fill, Data = ResolveIconGeometry(iconStr) };
                path.SetResourceReference(Path.FillProperty, "TextIconBrush");
                return path;
            }
        }

        // 悬停动画节奏：先隐藏填充、播放一次描边动画，描边完成后填充色再显示，伴随轻微放大
        private static readonly TimeSpan ChildIconStrokeDuration = TimeSpan.FromSeconds(0.4);
        private static readonly TimeSpan ChildIconFillDuration = TimeSpan.FromSeconds(0.2);
        private static readonly TimeSpan ChildIconGrowDuration = TimeSpan.FromSeconds(0.18);
        private static readonly TimeSpan ChildIconShrinkDuration = TimeSpan.FromSeconds(0.15);
        private const double ChildIconHoverScale = 1.18;
        private const double ChildIconBoxSize = 20;

        /// <summary>
        /// 侧边栏子菜单项图标：静止状态正常显示完整填充图标；鼠标进入子项的瞬间，
        /// 填充变为透明、只保留描边动画播放一次，描边画完后填充色再淡入复原；
        /// 鼠标离开时立即恢复正常填充显示。描边色与填充色统一使用 TextIconBrush。
        /// 非 PNG 图标叠加"填充层(Path) + 描边层(pf:AnimationPath)"两层实现。
        /// </summary>
        private void AttachChildIcon(SideMenuItem item, string iconStr)
        {
            if (string.IsNullOrEmpty(iconStr)) return;
            if (iconStr.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                item.Icon = CreateIconElement(iconStr);
                return;
            }

            var geometry = ResolveIconGeometry(iconStr);
            if (geometry == null) return;

            var fillLayer = new Path
            {
                Width = ChildIconBoxSize,
                Height = ChildIconBoxSize,
                Stretch = Stretch.Uniform,
                Data = geometry
            };
            fillLayer.SetResourceReference(Path.FillProperty, "TextIconBrush");

            var strokeLayer = new AnimationPath
            {
                Width = ChildIconBoxSize,
                Height = ChildIconBoxSize,
                Data = geometry,
                // 控件库自带的长度估算只在路径起点附近采样再线性外推，遇到多子路径/转角
                // 较多的图标会严重失真，导致不同图标描边速度忽快忽慢；这里改为按展平后
                // 折线精确求和，得到与 Stretch=Uniform 渲染结果一致的真实描边长度。
                PathLength = ComputeStrokePathLength(geometry, ChildIconBoxSize),
                StrokeThickness = 0,
                RepeatBehavior = new RepeatBehavior(1),
                FillBehavior = FillBehavior.HoldEnd,
                Duration = new Duration(ChildIconStrokeDuration)
            };
            strokeLayer.SetResourceReference(AnimationPath.StrokeProperty, "TextIconBrush");

            var scale = new ScaleTransform(1, 1);
            var host = new Grid
            {
                Width = ChildIconBoxSize,
                Height = ChildIconBoxSize,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale
            };
            host.Children.Add(fillLayer);
            host.Children.Add(strokeLayer);
            item.Icon = host;

            item.MouseEnter += (s, e) =>
            {
                var grow = new DoubleAnimation(ChildIconHoverScale, ChildIconGrowDuration)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

                strokeLayer.StrokeThickness = 1;
                strokeLayer.IsPlaying = false;
                strokeLayer.IsPlaying = true;

                fillLayer.BeginAnimation(UIElement.OpacityProperty, null);
                fillLayer.Opacity = 0;
                var fillIn = new DoubleAnimation(0, 1, ChildIconFillDuration) { BeginTime = ChildIconStrokeDuration };
                fillLayer.BeginAnimation(UIElement.OpacityProperty, fillIn);
            };
            item.MouseLeave += (s, e) =>
            {
                var shrink = new DoubleAnimation(1, ChildIconShrinkDuration);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);

                strokeLayer.IsPlaying = false;
                strokeLayer.StrokeThickness = 0;

                fillLayer.BeginAnimation(UIElement.OpacityProperty, null);
                fillLayer.Opacity = 1;
            };
        }

        /// <summary>
        /// 精确计算 Geometry 在 Stretch=Uniform、边长为 boxSize 的正方形框内渲染后的描边总长度：
        /// 先展平为折线并逐段求和得到原始长度，再按 Uniform 等比缩放系数换算到目标框尺寸。
        /// 用于替代 pf:AnimationPath 内置的估算（对多子路径/多转角图标误差很大，是描边速度
        /// 忽快忽慢的根因），使不同图标的描边动画播放速度保持一致。
        /// </summary>
        private static double ComputeStrokePathLength(Geometry geometry, double boxSize)
        {
            if (geometry == null || boxSize <= 0) return 0;

            var bounds = geometry.Bounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return 0;

            double rawLength = 0;
            var flattened = geometry.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
            foreach (var figure in flattened.Figures)
            {
                var previous = figure.StartPoint;
                foreach (var segment in figure.Segments)
                {
                    switch (segment)
                    {
                        case PolyLineSegment poly:
                            foreach (var point in poly.Points)
                            {
                                rawLength += (point - previous).Length;
                                previous = point;
                            }
                            break;
                        case LineSegment line:
                            rawLength += (line.Point - previous).Length;
                            previous = line.Point;
                            break;
                    }
                }
                if (figure.IsClosed)
                    rawLength += (previous - figure.StartPoint).Length;
            }

            var scale = Math.Min(boxSize / bounds.Width, boxSize / bounds.Height);
            return rawLength * scale;
        }

        /// <summary>
        /// 解析图标字符串对应的 Geometry：优先按 PackIconKind 枚举名匹配控件库矢量图标库，
        /// 匹配失败则回退为主题资源字典中的 Geometry 资源键（兼容既有 Icon 取值）。
        /// </summary>
        private static Geometry ResolveIconGeometry(string iconStr)
        {
            if (string.IsNullOrEmpty(iconStr)) return null;

            if (Enum.TryParse<PackIconKind>(iconStr, true, out var kind))
            {
                var probe = new PackIcon { Kind = kind };
                if (!string.IsNullOrEmpty(probe.Data))
                    return Geometry.Parse(probe.Data);
            }

            return System.Windows.Application.Current.TryFindResource(iconStr) as Geometry;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 重入门：确认对话框期间用户若再次点关闭，直接忽略第二次触发。
            if (_isClosing) { e.Cancel = true; return; }

            // 必须在首个 await 之前设置 e.Cancel = true，否则事件处理器在首个 await 处即返回，
            // 窗口会先关闭、若 ShutdownMode=OnMainWindowClose 进程开始退出，清理链被截断。
            e.Cancel = true;
            _isClosing = true;

            var result = await _messageService.ShowMessageAsync(
                "确定要退出系统吗？", "退出提示",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != ButtonResult.Yes)
            {
                _isClosing = false; // 用户取消，恢复以允许下次关闭尝试
                return;
            }

            try
            {
                // 机构同步释放
                foreach (var item in _mechanismsList)
                    item.Dispose();

                // 异步释放顺序：AlarmService → ProductionDataService → LogService（最后）。
                // LogService 必须最后——它一旦释放，前面服务在排空/超时时记的 Warn 日志会全部丢失，
                // 而"排空超时"恰恰是最需要留下证据的场景。
                if (_containerProvider.IsRegistered<IAlarmService>())
                    await _containerProvider.Resolve<IAlarmService>().DisposeAsync();
                if (_containerProvider.IsRegistered<IProductionDataService>())
                    await _containerProvider.Resolve<IProductionDataService>().DisposeAsync();
                if (_containerProvider.IsRegistered<ILogService>())
                    await _containerProvider.Resolve<ILogService>().DisposeAsync();
            }
            catch { }
            Environment.Exit(0);
        }

        #region 主题切换

        private void ToggleThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            _isAnimating = true;

            var win = GetWindow(this);
            var w = (int)Math.Max(win.ActualWidth, 1);
            var h = (int)Math.Max(win.ActualHeight, 1);

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(win);

            var btnPos = ThemeBtn.TransformToAncestor(win).Transform(new Point(0, 0));
            var cx = btnPos.X + ThemeBtn.ActualWidth / 2;
            var cy = btnPos.Y + ThemeBtn.ActualHeight / 2;

            var overlayImg = new Image { Source = rtb, Width = w, Height = h, Stretch = Stretch.None };
            var fullRect = new RectangleGeometry(new Rect(0, 0, w, h));
            var hole = new EllipseGeometry { Center = new Point(cx, cy), RadiusX = 0, RadiusY = 0 };
            overlayImg.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, hole);

            var overlayWin = new SwWindow
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize, Width = w, Height = h,
                Left = win.Left, Top = win.Top, Content = overlayImg, Focusable = false
            };
            overlayWin.Show();

            var name = _commonSettings.Skin == SkinType.Dark ? SkinType.Default.ToString() : SkinType.Dark.ToString();
            _commonSettings.Skin = (SkinType)Enum.Parse(typeof(SkinType), name);
            ((PFApplicationBase)System.Windows.Application.Current).UpdateSkin(name);
            _commonSettings.Save();
            ThemeIcon.Kind = _commonSettings.Skin == SkinType.Dark ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;

            var maxR = Math.Sqrt(w * w + h * h) + 20;
            var sw = Stopwatch.StartNew();
            const int durationMs = 600;

            EventHandler renderHandler = null!;
            renderHandler = (_, _) =>
            {
                var t = Math.Min(sw.ElapsedMilliseconds / (double)durationMs, 1.0);
                double eased = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
                hole.RadiusX = maxR * eased;
                hole.RadiusY = maxR * eased;
                if (t >= 1.0)
                {
                    CompositionTarget.Rendering -= renderHandler;
                    overlayWin.Close();
                    _isAnimating = false;
                }
            };
            CompositionTarget.Rendering += renderHandler;
        }

        #endregion

        #region 登录动画

        private static readonly string[] VibrantColors = ["#26ccff", "#a25afd", "#ff5e7e", "#88ff5a", "#fcff42", "#ffa62d", "#ff36ff"];
        private static readonly string[] SnowColors = ["#ffffff", "#e0f7fa", "#b2ebf2"];

        private void TriggerConfetti(ConfettiEffectType effectType)
        {
            switch (effectType)
            {
                case ConfettiEffectType.BasicCannon:
                    ConfettiCannon.Fire(new ConfettiCannon.Options
                    {
                        ParticleCount = 150, Spread = 90, StartVelocity = 45,
                        Origin = new Point(0.5, 0.6), Colors = VibrantColors.ToList()
                    });
                    break;

                case ConfettiEffectType.RandomDirection:
                    var rng = new Random();
                    ConfettiCannon.Fire(new ConfettiCannon.Options
                    {
                        Angle = rng.Next(55, 125), Spread = rng.Next(60, 90),
                        ParticleCount = rng.Next(100, 160), Origin = new Point(0.5, 0.6),
                        Colors = VibrantColors.ToList(), Shapes = ["square", "circle"]
                    });
                    break;

                case ConfettiEffectType.RealisticLook:
                    const int count = 250;
                    void Fire(double ratio, ConfettiCannon.Options opts)
                    {
                        opts.ParticleCount = (int)Math.Floor(count * ratio);
                        opts.Colors = VibrantColors.ToList();
                        ConfettiCannon.Fire(opts);
                    }
                    Fire(0.25, new ConfettiCannon.Options { Spread = 26, StartVelocity = 55 });
                    Fire(0.2,  new ConfettiCannon.Options { Spread = 60 });
                    Fire(0.35, new ConfettiCannon.Options { Spread = 100, Decay = 0.91, Scalar = 0.8 });
                    Fire(0.1,  new ConfettiCannon.Options { Spread = 120, StartVelocity = 25, Decay = 0.92, Scalar = 1.2 });
                    Fire(0.1,  new ConfettiCannon.Options { Spread = 120, StartVelocity = 45 });
                    break;

                case ConfettiEffectType.Fireworks:
                    const int dur = 15000;
                    var rand = new Random();
                    var end = DateTime.Now + TimeSpan.FromMilliseconds(dur);
                    var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(250) };
                    void OnTick(object o, EventArgs _)
                    {
                        double left = (end - DateTime.Now).TotalMilliseconds;
                        if (left <= 0) { timer.Tick -= OnTick; timer.Stop(); return; }
                        int pc = (int)(50 * (left / dur));
                        string[] cols = [VibrantColors[rand.Next(VibrantColors.Length)], VibrantColors[rand.Next(VibrantColors.Length)]];
                        ConfettiCannon.Fire(new ConfettiCannon.Options { StartVelocity = 30, Spread = 360, Ticks = 60, ParticleCount = pc, Colors = cols.ToList(), Origin = new Point(RandomInRange(0.1, 0.3), rand.NextDouble() - 0.2) });
                        ConfettiCannon.Fire(new ConfettiCannon.Options { StartVelocity = 30, Spread = 360, Ticks = 60, ParticleCount = pc, Colors = cols.ToList(), Origin = new Point(RandomInRange(0.7, 0.9), rand.NextDouble() - 0.2) });
                    }
                    timer.Tick += OnTick;
                    timer.Start();
                    break;

                case ConfettiEffectType.Stars:
                    void Shoot()
                    {
                        ConfettiCannon.Fire(new ConfettiCannon.Options { Spread = 360, Ticks = 60, Gravity = 0.1, Decay = 0.96, StartVelocity = 35, Colors = ["#FFE400","#FFBD00","#E89400","#FFCA6C","#FDFFB8"], ParticleCount = 40, Scalar = 1.2, Shapes = ["star"] });
                        ConfettiCannon.Fire(new ConfettiCannon.Options { Spread = 360, Ticks = 60, Gravity = 0.1, Decay = 0.96, StartVelocity = 35, Colors = ["#ffffff","#FDFFB8"], ParticleCount = 15, Scalar = 0.6, Shapes = ["circle"] });
                    }
                    void SetTimeout(int ms) { var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; void h(object o2, EventArgs _2) { Shoot(); t.Tick -= h; t.Stop(); } t.Tick += h; t.Start(); }
                    SetTimeout(0); SetTimeout(150); SetTimeout(300);
                    break;

                case ConfettiEffectType.Snow:
                    const int snowDur = 15000;
                    var snowEnd = DateTime.Now + TimeSpan.FromMilliseconds(snowDur);
                    var snowRng = new Random();
                    double skew = 1;
                    new AnimationFrame().Start(_ =>
                    {
                        double left = (snowEnd - DateTime.Now).TotalMilliseconds;
                        if (left <= 0) return true;
                        int ticks = (int)Math.Max(200, 500 * (left / snowDur));
                        skew = Math.Max(0.8, skew - 0.001);
                        ConfettiCannon.Fire(new ConfettiCannon.Options { ParticleCount = 1, StartVelocity = 0, Ticks = ticks, Origin = new Point(snowRng.NextDouble(), snowRng.NextDouble() * skew - 0.2), Colors = SnowColors.ToList(), Shapes = ["circle"], Gravity = RandomInRange(0.3, 0.5), Scalar = RandomInRange(0.3, 0.9), Drift = RandomInRange(-0.6, 0.6) });
                        return false;
                    });
                    break;

                case ConfettiEffectType.SchoolPride:
                    const int prideDur = 15000;
                    var prideEnd = DateTime.Now + TimeSpan.FromMilliseconds(prideDur);
                    new AnimationFrame().Start(_ =>
                    {
                        if ((prideEnd - DateTime.Now).TotalMilliseconds <= 0) return true;
                        ConfettiCannon.Fire(new ConfettiCannon.Options { ParticleCount = 3, Angle = 60, Spread = 55, Origin = new Point(0.05, 0.5), Colors = VibrantColors.ToList(), Shapes = ["square","circle"] });
                        ConfettiCannon.Fire(new ConfettiCannon.Options { ParticleCount = 3, Angle = 120, Spread = 55, Origin = new Point(0.95, 0.5), Colors = VibrantColors.ToList(), Shapes = ["square","circle"] });
                        return false;
                    });
                    break;
            }
        }

        private static double RandomInRange(double min, double max)
            => new Random().NextDouble() * (max - min) + min;

        private class AnimationFrame
        {
            private Func<double, bool> _callback;
            private long _lastTicks;
            public void Start(Func<double, bool> callback)
            {
                _callback = callback;
                _lastTicks = DateTime.Now.Ticks;
                CompositionTarget.Rendering += OnRendering;
            }
            private void OnRendering(object sender, EventArgs e)
            {
                long now = DateTime.Now.Ticks;
                double dt = (now - _lastTicks) / 10000000.0;
                _lastTicks = now;
                if (_callback?.Invoke(dt) == true)
                    CompositionTarget.Rendering -= OnRendering;
            }
        }

        #endregion
    }
}
