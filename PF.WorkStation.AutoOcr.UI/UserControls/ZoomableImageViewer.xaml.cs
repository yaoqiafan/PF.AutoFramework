using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    public partial class ZoomableImageViewer : UserControl
    {
        // ══════════════════════════════════════════════════════════
        //  静态缓存系统默认图片 (纯 WPF 矢量绘制方案，零外部依赖)
        // ══════════════════════════════════════════════════════════
        private static readonly Geometry DefaultInfoGeometry = ExtractDefaultInfoGeometry();

        private static Geometry ExtractDefaultInfoGeometry()
        {
            // 蓝色扁平化“信息(i)”矢量路径数据
            var d = "M16,2 C8.271,2 2,8.271 2,16 C2,23.729 8.271,30 16,30 C23.729,30 30,23.729 30,16 C30,8.271 23.729,2 16,2 z M17,25 L15,25 L15,14 L17,14 L17,25 z M17,11 L15,11 L15,9 L17,9 L17,11 z";
            return Geometry.Parse(d);
        }

        // ══════════════════════════════════════════════════════════
        //  依赖属性定义
        // ══════════════════════════════════════════════════════════

        // 1. 主图片路径 (string)
        /// <summary>
        /// SourceProperty
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(string), typeof(ZoomableImageViewer),
                new PropertyMetadata(null, OnSourceChanged));
        /// <summary>
        /// 成员
        /// </summary>

        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZoomableImageViewer viewer)
            {
                viewer.LoadImageSafe(e.NewValue as string);
            }
        }

        // 2. 错误占位图 (ImageSource 类型，常用于绑定内置资源)
        /// <summary>
        /// ErrorSourceProperty
        /// </summary>
        public static readonly DependencyProperty ErrorSourceProperty =
            DependencyProperty.Register("ErrorSource", typeof(ImageSource), typeof(ZoomableImageViewer),
                new PropertyMetadata(null, OnErrorSourceChanged));
        /// <summary>
        /// 成员
        /// </summary>

        public ImageSource ErrorSource
        {
            get => (ImageSource)GetValue(ErrorSourceProperty);
            set => SetValue(ErrorSourceProperty, value);
        }

        private static void OnErrorSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZoomableImageViewer viewer)
            {
                // 当错误备用图改变时，重新评估当前加载状态
                viewer.LoadImageSafe(viewer.Source);
            }
        }

        // 3. 自定义 OSD 标注内容
        /// <summary>
        /// AnnotationContentProperty
        /// </summary>
        public static readonly DependencyProperty AnnotationContentProperty =
            DependencyProperty.Register("AnnotationContent", typeof(object), typeof(ZoomableImageViewer), new PropertyMetadata(null));
        /// <summary>
        /// 成员
        /// </summary>

        public object AnnotationContent
        {
            get => GetValue(AnnotationContentProperty);
            set => SetValue(AnnotationContentProperty, value);
        }

        // ══════════════════════════════════════════════════════════
        //  内部状态与加载分发机制
        // ══════════════════════════════════════════════════════════
        private Point _dragStartPoint;
        private Point _translateOrigin;
        private bool _isDragging = false;
        private readonly double _zoomFactor = 1.15;

        // 记录当前实际显示的物理文件路径（仅物理文件允许执行“另存为”）
        private string _currentDisplayedPath = null;
        /// <summary>
        /// ZoomableImageViewer 构造函数
        /// </summary>

        public ZoomableImageViewer()
        {
            InitializeComponent();

            // 控件初始化时，加载并显示高清矢量默认图标，防止黑屏
            ShowDefaultInfo();
        }

        /// <summary>
        /// 显示保底的矢量默认图标（无极缩放永不糊）
        /// </summary>
        private void ShowDefaultInfo()
        {
            var drawing = new GeometryDrawing
            {
                Geometry = DefaultInfoGeometry,
                Brush = new SolidColorBrush(Color.FromArgb(255, 30, 144, 255))
            };

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze(); // 冻结提升渲染性能

            MainImage.Source = drawingImage;
            _currentDisplayedPath = null; // 矢量图无实体路径
        }

        /// <summary>
        /// 尝试安全地将本地文件加载为内存 BitmapImage（自动解除文件占用）
        /// </summary>
        private bool TryLoadBitmap(string path, out BitmapImage bitmap)
        {
            bitmap = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // 核心：释放文件句柄
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                bitmap = bmp;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"尝试加载图片失败 [{path}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 核心加载分发逻辑：主图(物理路径) -> 备用错误图(内置资源) -> 系统矢量图
        /// </summary>
        private void LoadImageSafe(string imagePath)
        {
            // 优先级 1：尝试加载物理主图
            if (TryLoadBitmap(imagePath, out var mainBitmap))
            {
                MainImage.Source = mainBitmap;
                _currentDisplayedPath = imagePath;
            }
            // 优先级 2：主图失败，降级加载 ErrorSource 资源图
            else if (ErrorSource != null)
            {
                MainImage.Source = ErrorSource;
                _currentDisplayedPath = null;
            }
            // 优先级 3：彻底兜底，退回矢量图标
            else
            {
                ShowDefaultInfo();
            }

            // 无论切了什么图，都将其缩放和平移状态复原居中
            ResetTransform();
        }

        private void ResetTransform()
        {
            ImageScaleTransform.ScaleX = 1.0;
            ImageScaleTransform.ScaleY = 1.0;
            ImageTranslateTransform.X = 0;
            ImageTranslateTransform.Y = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  交互逻辑 (滚轮缩放、鼠标拖拽平移)
        // ══════════════════════════════════════════════════════════

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MainImage.Source == null) return;

            // 以鼠标当前位置为锚点进行缩放
            Point mousePos = e.GetPosition(TransformRoot);
            double oldScale = ImageScaleTransform.ScaleX;
            double newScale = e.Delta > 0 ? oldScale * _zoomFactor : oldScale / _zoomFactor;

            // 设置缩放的极值上下限 (0.1倍 ~ 50倍)
            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 50.0) newScale = 50.0;

            // 补偿位移，保持鼠标指向的像素点不动
            ImageTranslateTransform.X -= mousePos.X * (newScale - oldScale);
            ImageTranslateTransform.Y -= mousePos.Y * (newScale - oldScale);
            ImageScaleTransform.ScaleX = newScale;
            ImageScaleTransform.ScaleY = newScale;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MainImage.Source == null) return;

            _dragStartPoint = e.GetPosition(ViewportGrid);
            _translateOrigin = new Point(ImageTranslateTransform.X, ImageTranslateTransform.Y);

            ViewportGrid.CaptureMouse();
            _isDragging = true;
            ViewportGrid.Cursor = Cursors.SizeAll;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetPosition(ViewportGrid);
            double offsetX = currentPoint.X - _dragStartPoint.X;
            double offsetY = currentPoint.Y - _dragStartPoint.Y;

            ImageTranslateTransform.X = _translateOrigin.X + offsetX;
            ImageTranslateTransform.Y = _translateOrigin.Y + offsetY;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            ViewportGrid.ReleaseMouseCapture();
            _isDragging = false;
            ViewportGrid.Cursor = Cursors.Arrow;
        }

        // ══════════════════════════════════════════════════════════
        //  右键菜单逻辑
        // ══════════════════════════════════════════════════════════

        private void MenuItem_Reset_Click(object sender, RoutedEventArgs e)
        {
            ResetTransform();
        }

        private void MenuItem_SaveAs_Click(object sender, RoutedEventArgs e)
        {
            // 只有内部记录的 _currentDisplayedPath 存在实体文件时，才允许右键另存为
            if (string.IsNullOrWhiteSpace(_currentDisplayedPath) || !File.Exists(_currentDisplayedPath))
            {
                MessageBox.Show("当前没有可导出的物理图像源文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ext = Path.GetExtension(_currentDisplayedPath);
            string fileName = Path.GetFileName(_currentDisplayedPath);

            var saveFileDialog = new SaveFileDialog
            {
                Title = "另存为图片",
                Filter = $"图像文件 (*{ext})|*{ext}|所有文件 (*.*)|*.*",
                FileName = $"Export_{fileName}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 直接拷贝原文件，实现最快且100%无损导出
                    File.Copy(_currentDisplayedPath, saveFileDialog.FileName, overwrite: true);
                    MessageBox.Show("图片无损导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}