using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.ViewModels;
using PF.Core.Entities.Identity;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Controls;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PF.Application.Shell.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : PF.UI.Controls.Window
    {
        private readonly IMessageService _messageService;
        private readonly CommonSettings _commonSettings;
        private readonly IEventAggregator _eventAggregator;
        private readonly IEnumerable<IMechanism> _mechanismslist;

        /// <summary>
        /// 初始化实例
        /// </summary>
        public MainWindow(IMessageService messageService, CommonSettings commonSettings, IEnumerable<IMechanism> mechanisms, IEventAggregator eventAggregator)
        {
            InitializeComponent();
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<UserChangedEvent>().Subscribe(OnUserLogined);
            this.Loaded += MainWindow_Loaded; // 订阅 Loaded 事件
            _messageService = messageService;
            _commonSettings = commonSettings;
            _mechanismslist = mechanisms;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                RenderSideMenu(vm.MenuItems);

                vm.MenuItems.CollectionChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() => RenderSideMenu(vm.MenuItems));
                };
            }
        }

        private async void OnUserLogined(UserInfo? info)
        {
            // 防止 info 为 null 导致异常
            if (info == null)
            {
                return;
            }

            // 使用 UI 线程调度，并加入延迟等待主界面渲染完毕
            await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                // 延迟 500 毫秒，确保页面切换完成，避免瞬间动画丢失
                await Task.Delay(500);

                switch (info.Root)
                {
                    case Core.Enums.UserLevel.Null:
                        TriggerConfetti(ConfettiEffectType.Snow);
                        break;

                    case Core.Enums.UserLevel.Operator:
                        if (_commonSettings.EnableOperatorAnimation)
                            TriggerConfetti(_commonSettings.OperatorAnimationType);
                        break;

                    case Core.Enums.UserLevel.Engineer:
                        if (_commonSettings.EnableEngineerAnimation)
                            TriggerConfetti(_commonSettings.EngineerAnimationType);
                        break;

                    case Core.Enums.UserLevel.Administrator:
                        if (_commonSettings.EnableAdministratorAnimation)
                            TriggerConfetti(_commonSettings.AdministratorAnimationType);
                        break;

                    case Core.Enums.UserLevel.SuperUser:
                        if (_commonSettings.EnableSuperuserAnimation)
                            TriggerConfetti(_commonSettings.SuperuserAnimationType);
                        break;

                    default:
                        break;
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
                    Background = (Brush)FindResource("LightPrimaryBrush"), // 还原原有的背景色
                    Margin = new Thickness(0, 0, 0, 10),
                    Icon = CreateIconElement(group.Icon), // 渲染组图标
                    IsExpanded = false// 渲染子节点图标
                };

                foreach (var child in group.Children)
                {
                    var childItem = new SideMenuItem
                    {
                        Header = $" {child.Title}",
                        Tag = child,
                        DataContext = child,
                        Icon = CreateIconElement(child.Icon),
                        IsExpanded = false// 渲染子节点图标
                    };

                    groupItem.Items.Add(childItem);
                }

                MainSideMenu.Items.Add(groupItem);
            }
        }

        /// <summary>
        /// 智能解析图标：包含 .png 则生成 Image，否则视为 StaticResource 的 Geometry Path
        /// </summary>
        private object CreateIconElement(string iconStr)
        {
            if (string.IsNullOrEmpty(iconStr)) return null;

            if (iconStr.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var image = new Image();
                // 补全 Pack URI 协议，防止找不到图片
                string packUri = iconStr.StartsWith("/") ? $"pack://application:,,,{iconStr}" : iconStr;
                image.Source = new BitmapImage(new Uri(packUri, UriKind.Absolute));
                image.SetResourceReference(FrameworkElement.StyleProperty, "IconStyle");
                return image;
            }
            else
            {
                var path = new Path
                {
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Fill
                };

                // 绑定 Geometry 资源 (如 SettingIcon, AudioGeometry)
                path.SetResourceReference(Path.DataProperty, iconStr);
                // 绑定颜色资源
                path.SetResourceReference(Path.FillProperty, "TextIconBrush");

                return path;
            }
        }

        private void ButtonSkins_OnClick(object sender, RoutedEventArgs e)
        {
            Button button = e.OriginalSource as Button;
            if (e.OriginalSource is Button)
            {
                PopupConfig.IsOpen = false;
                if (button.Tag.Equals(_commonSettings.Skin.ToString()))
                {
                    return;
                }

                _commonSettings.Skin = (SkinType)Enum.Parse(typeof(SkinType), button.Tag.ToString());
                ((App)System.Windows.Application.Current).UpdateSkin(button.Tag.ToString());
                _commonSettings.Save();
            }
        }

        private void ButtonConfig_OnClick(object sender, RoutedEventArgs e) => PopupConfig.IsOpen = true;

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var result = await _messageService.ShowMessageAsync("确定要退出系统吗？",
                "退出提示",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == ButtonResult.Yes)
            {
                try
                {
                    foreach (var item in _mechanismslist)
                    {
                        item.Dispose();
                    }
                    // [可选] 在这里执行资源清理，例如记录退出日志
                    // var logService = Prism.Ioc.ContainerLocator.Container.Resolve<ILogService>();
                    // logService?.Info("用户关闭了主程序，系统退出。", "System");
                }
                catch
                {
                    // 忽略清理时的异常，确保程序能顺利关闭
                }

                // 3. 彻底结束当前进程
                // 使用 Environment.Exit(0) 而不是 Application.Current.Shutdown() 的好处是：
                // 它可以强行终止所有后台线程（比如您程序里运行的 TCP 监听或 Log4Net 队列），防止出现界面关闭但后台进程卡死在任务管理器里的“僵尸进程”现象。
                Environment.Exit(0);
            }
            else
            {
                // 用户点击了“否”，取消关闭操作，程序继续运行
                e.Cancel = true;
            }
        }

        #region 登录动画 (视觉优化版)

        // 定义一组更现代、更鲜艳的“多巴胺”配色
        private static readonly string[] VibrantColors = [
            "#26ccff", "#a25afd", "#ff5e7e", "#88ff5a", "#fcff42", "#ffa62d", "#ff36ff"
        ];

        // 雪花专用的冰雪蓝白配色，比单调的纯白更有空间感
        private static readonly string[] SnowColors = ["#ffffff", "#e0f7fa", "#b2ebf2"];

        private void TriggerConfetti(ConfettiEffectType effectType)
        {
            switch (effectType)
            {
                case ConfettiEffectType.BasicCannon:
                    {
                        ConfettiCannon.Fire(new ConfettiCannon.Options
                        {
                            ParticleCount = 150,       // 增加粒子数量，更饱满
                            Spread = 90,               // 扩大散开角度
                            StartVelocity = 45,        // 提高初始速度，更有爆发力
                            Origin = new Point(0.5, 0.6),
                            Colors = VibrantColors.ToList()     // 应用多彩配色
                        });
                        break;
                    }

                case ConfettiEffectType.RandomDirection:
                    {
                        var random = new Random();
                        ConfettiCannon.Fire(new ConfettiCannon.Options
                        {
                            Angle = random.Next(55, 125),
                            Spread = random.Next(60, 90),
                            ParticleCount = random.Next(100, 160),
                            Origin = new Point(0.5, 0.6),
                            Colors = VibrantColors.ToList(),
                            Shapes = ["square", "circle"] // 圆形和方形混合，更加灵动
                        });
                        break;
                    }

                case ConfettiEffectType.RealisticLook:
                    {
                        const int count = 250; // 提升总基数，让满屏飘落感更强

                        void Fire(double particleRatio, ConfettiCannon.Options options)
                        {
                            options.ParticleCount = (int)Math.Floor(count * particleRatio);
                            options.Colors = VibrantColors.ToList(); // 统一覆盖鲜艳配色
                            ConfettiCannon.Fire(options);
                        }

                        // 模拟真实纸片的不同空气阻力和重量感
                        Fire(0.25, new ConfettiCannon.Options { Spread = 26, StartVelocity = 55 });
                        Fire(0.2, new ConfettiCannon.Options { Spread = 60 });
                        Fire(0.35, new ConfettiCannon.Options { Spread = 100, Decay = 0.91, Scalar = 0.8 });
                        Fire(0.1, new ConfettiCannon.Options { Spread = 120, StartVelocity = 25, Decay = 0.92, Scalar = 1.2 });
                        Fire(0.1, new ConfettiCannon.Options { Spread = 120, StartVelocity = 45 });
                        break;
                    }

                case ConfettiEffectType.Fireworks:
                    {
                        const int duration = 15 * 1000;
                        var random = new Random();
                        DateTime animationEnd = DateTime.Now + TimeSpan.FromMilliseconds(duration);

                        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
                        {
                            Interval = TimeSpan.FromMilliseconds(250)
                        };

                        void OnTimerOnTick(object o, EventArgs _)
                        {
                            double timeLeft = (animationEnd - DateTime.Now).TotalMilliseconds;

                            if (timeLeft <= 0)
                            {
                                timer.Tick -= OnTimerOnTick;
                                timer.Stop();
                            }

                            var particleCount = (int)(50 * (timeLeft / duration));

                            // 每次爆炸随机抽取两个鲜艳的颜色作为主色调，模拟真实烟花的纯色叠加
                            string[] explosionColors = [VibrantColors[random.Next(VibrantColors.Length)], VibrantColors[random.Next(VibrantColors.Length)]];

                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                StartVelocity = 30,
                                Spread = 360,
                                Ticks = 60,
                                ParticleCount = particleCount,
                                Colors = explosionColors.ToList(),
                                Origin = new Point
                                {
                                    X = RandomInRange(0.1, 0.3),
                                    Y = random.NextDouble() - 0.2
                                }
                            });
                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                StartVelocity = 30,
                                Spread = 360,
                                Ticks = 60,
                                ParticleCount = particleCount,
                                Colors = explosionColors.ToList(),
                                Origin = new Point
                                {
                                    X = RandomInRange(0.7, 0.9),
                                    Y = random.NextDouble() - 0.2
                                }
                            });
                        }

                        timer.Tick += OnTimerOnTick;
                        timer.Start();
                        break;
                    }

                case ConfettiEffectType.Stars:
                    {
                        void Shoot()
                        {
                            // 主体：大星星（加入微弱重力，有坠落感）
                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                Spread = 360,
                                Ticks = 60,
                                Gravity = 0.1, // 原为0，加一点重力更自然
                                Decay = 0.96,
                                StartVelocity = 35,
                                Colors = ["#FFE400", "#FFBD00", "#E89400", "#FFCA6C", "#FDFFB8"],
                                ParticleCount = 40,
                                Scalar = 1.2,
                                Shapes = ["star"]
                            });
                            // 伴随：细小的碎星光（小圆点）
                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                Spread = 360,
                                Ticks = 60,
                                Gravity = 0.1,
                                Decay = 0.96,
                                StartVelocity = 35,
                                Colors = ["#ffffff", "#FDFFB8"],
                                ParticleCount = 15,
                                Scalar = 0.6, // 尺寸缩小
                                Shapes = ["circle"]
                            });
                        }

                        void OnTimerOnTick(object o, EventArgs _)
                        {
                            Shoot();

                            if (o is not DispatcherTimer timer) return;

                            timer.Tick -= OnTimerOnTick;
                            timer.Stop();
                        }

                        void SetTimeout(Action action, int delayMilliseconds)
                        {
                            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMilliseconds) };
                            timer.Tick += OnTimerOnTick;
                            timer.Start();
                        }

                        // 稍微拉开时间差，爆发的层次感更好
                        SetTimeout(Shoot, 0);
                        SetTimeout(Shoot, 150);
                        SetTimeout(Shoot, 300);
                        break;
                    }

                case ConfettiEffectType.Snow:
                    {
                        const int duration = 15 * 1000;
                        var random = new Random();
                        DateTime animationEnd = DateTime.Now + TimeSpan.FromMilliseconds(duration);
                        double skew = 1;

                        new AnimationFrame().Start(_ =>
                        {
                            double timeLeft = (animationEnd - DateTime.Now).TotalMilliseconds;

                            if (timeLeft <= 0) return true;

                            int ticks = (int)Math.Max(200, 500 * (timeLeft / duration));
                            skew = Math.Max(0.8, skew - 0.001);

                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                ParticleCount = 1,
                                StartVelocity = 0,
                                Ticks = ticks,
                                Origin = new Point
                                {
                                    X = random.NextDouble(),
                                    Y = random.NextDouble() * skew - 0.2
                                },
                                Colors = SnowColors.ToList(), // 使用更高级的蓝白冰雪配色
                                Shapes = ["circle"],
                                Gravity = RandomInRange(0.3, 0.5), // 稍微降低重力，让雪飘得更慢
                                Scalar = RandomInRange(0.3, 0.9),  // 大小错落有致
                                Drift = RandomInRange(-0.6, 0.6),  // 增加横向被风吹拂的摆动感
                            });

                            return false;
                        });
                        break;
                    }

                case ConfettiEffectType.SchoolPride:
                    {
                        const int duration = 15 * 1000;
                        DateTime animationEnd = DateTime.Now + TimeSpan.FromMilliseconds(duration);

                        new AnimationFrame().Start(_ =>
                        {
                            double timeLeft = (animationEnd - DateTime.Now).TotalMilliseconds;
                            if (timeLeft <= 0) return true;

                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                ParticleCount = 3, // 稍微增加喷射密度
                                Angle = 60,
                                Spread = 55,
                                Origin = new Point(0.05, 0.5),
                                Colors = VibrantColors.ToList(), // 换成多彩色，渲染欢庆气氛
                                Shapes = ["square", "circle"] // 增加形状丰富度
                            });
                            ConfettiCannon.Fire(new ConfettiCannon.Options
                            {
                                ParticleCount = 3,
                                Angle = 120,
                                Spread = 55,
                                Origin = new Point(0.95, 0.5),
                                Colors = VibrantColors.ToList(),
                                Shapes = ["square", "circle"]
                            });

                            return false;
                        });
                        break;
                    }

                
            }
        }

        private static double RandomInRange(double min, double max)
        {
            var random = new Random();
            return random.NextDouble() * (max - min) + min;
        }

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
                long nowTicks = DateTime.Now.Ticks;
                double deltaTime = (nowTicks - _lastTicks) / 10000000.0;
                _lastTicks = nowTicks;

                if (_callback?.Invoke(deltaTime) == true)
                {
                    CompositionTarget.Rendering -= OnRendering;
                }
            }
        }
        #endregion

    }
}