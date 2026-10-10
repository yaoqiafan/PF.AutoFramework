using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// PFDialogBase.xaml 的交互逻辑
    /// </summary>
    public partial class PFDialogBaseWindow : PF.UI.Controls.Window, IDialogWindow
    {
        /// <summary>
        /// 初始化实例
        /// </summary>
        public PFDialogBaseWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, CloseEvent));
            ContentRendered += OnContentRendered;
        }

        // Prism 通过 IDialogWindow 显示弹窗：在真正显示之前补好所有者
        // （WPF 不允许模态弹窗开始显示后再设 Owner，不能放到 SourceInitialized 里做）
        void IDialogWindow.Show()
        {
            EnsureOwner();
            Show();
        }

        bool? IDialogWindow.ShowDialog()
        {
            EnsureOwner();
            return ShowDialog();
        }
        /// <summary>
        /// Result
        /// </summary>
        public IDialogResult Result { get; set; }

        private void CloseEvent(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 只在顶部标题栏区域（NonClientAreaHeight = 50px）内拖动，
            // 避免抢占内容区域（如 HALCON ROI 绘制）的鼠标事件
            if (e.GetPosition(this).Y <= 50)
                this.DragMove();
        }

        /// <summary>
        /// 弹窗"消失"的兜底：Prism 打开弹窗时只取"当前激活的窗口"当所有者，那一刻程序没有激活窗口
        /// （操作员刚点过别的程序、刚关掉一个提示框）时所有者为空。本窗口是 ToolWindow 样式、不进任务栏，
        /// 模态弹窗又把主窗口禁用了——弹窗落到别的程序后面就再也找不回来，只能去任务管理器"切换到"，
        /// 看起来像卡死。这里补上：所有者为空或不可见时改用可见的主窗口；实在没有可见窗口，就让弹窗进任务栏。
        /// </summary>
        private void EnsureOwner()
        {
            try
            {
                if (Owner is not { IsVisible: true })
                {
                    var app = Application.Current;
                    var candidate = app?.MainWindow is { IsVisible: true } main && !ReferenceEquals(main, this)
                        ? main
                        : app?.Windows.OfType<Window>().FirstOrDefault(w => !ReferenceEquals(w, this) && w.IsVisible && w.ShowInTaskbar);
                    Owner = candidate;
                }
            }
            catch (InvalidOperationException) { /* 候选窗口不能当所有者（如已关闭），保持原值，下面按"无所有者"处理 */ }

            if (Owner != null) return;

            // 没有可见的所有者：进任务栏，免得落到别的程序后面找不回来；
            // 本窗口没设图标，任务栏上会是一个空白的默认图标，借用主窗口的图标（没有就用框架默认图标）
            ShowInTaskbar = true;
            if (Icon == null)
            {
                try
                {
                    Icon = Application.Current?.MainWindow?.Icon
                           ?? BitmapFrame.Create(new Uri("pack://application:,,,/PF.UI.Resources;component/Images/ICO/-pfico.ico"));
                }
                catch { /* 图标加载失败不影响弹窗显示 */ }
            }
        }

        /// <summary>首次显示后主动激活，保证弹窗在最前面。</summary>
        private void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }
    }
}
