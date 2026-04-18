using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
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

namespace PF.UI.Resources
{
    /// <summary>
    /// Splash.xaml 的交互逻辑
    /// </summary>
    public partial class Splash : PF.UI.Controls.Window
    {
       
        /// <summary>
        /// 初始化实例
        /// </summary>
        public Splash()
        {
            InitializeComponent();
        }

        /// <summary>
        /// WelcomeTextProperty
        /// </summary>
        public static readonly DependencyProperty WelcomeTextProperty = DependencyProperty.Register(
           nameof(WelcomeText), typeof(string), typeof(Splash), new PropertyMetadata(String.Empty));

        /// <summary>
        /// WelcomeText
        /// </summary>
        public string WelcomeText
        {
            get => (string)GetValue(WelcomeTextProperty);
            set => SetValue(WelcomeTextProperty, value);
        }

        /// <summary>
        /// VersionNumberProperty
        /// </summary>
        public static readonly DependencyProperty VersionNumberProperty = DependencyProperty.Register(
           nameof(VersionNumber), typeof(string), typeof(Splash), new PropertyMetadata(String.Empty));

        /// <summary>
        /// VersionNumber
        /// </summary>
        public string VersionNumber
        {
            get => (string)GetValue(VersionNumberProperty);
            set => SetValue(VersionNumberProperty, value);
        }

        /// <summary>
        /// WelcomeText_smallProperty
        /// </summary>
        public static readonly DependencyProperty WelcomeText_smallProperty = DependencyProperty.Register(
          nameof(WelcomeText_small), typeof(string), typeof(Splash), new PropertyMetadata(String.Empty));
        /// <summary>
        /// WelcomeText_small
        /// </summary>
        public string WelcomeText_small
        {
            get => (string)GetValue(WelcomeText_smallProperty);
            set => SetValue(WelcomeText_smallProperty, value);
        }


        /// <summary>
        /// MessageinfoProperty
        /// </summary>
        public static readonly DependencyProperty MessageinfoProperty = DependencyProperty.Register(
         nameof(Messageinfo), typeof(string), typeof(Splash), new PropertyMetadata("Loading..."));
        /// <summary>
        /// Messageinfo
        /// </summary>
        public string Messageinfo
        {
            get => (string)GetValue(MessageinfoProperty);
            set => SetValue(MessageinfoProperty, value);
        }


      
        /// <summary>
        /// MessageTypeProperty
        /// </summary>
        public static readonly DependencyProperty MessageTypeProperty = DependencyProperty.Register(
            nameof(MessageType), typeof(MsgType), typeof(Splash), new PropertyMetadata(default));

        /// <summary>
        /// MessageType
        /// </summary>
        public MsgType MessageType
        {
            get => (MsgType)GetValue(MessageTypeProperty);
            set => SetValue(MessageTypeProperty, value);
        }



        /// <summary>
        /// 加载ingAction
        /// </summary>
        public Func<Task<bool>> LoadingAction { get; set; } = () => Task.FromResult(true);

        /// <summary>
        /// 初始化实例
        /// </summary>
        public async void SplashLoaded()
        {
            bool res = true;

            if (LoadingAction != null)
            {
                res = await LoadingAction();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.DialogResult = res;
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SplashLoaded();
        }



        /// <summary>
        /// 更新Message
        /// </summary>
        public void UpdateMessage(string status, MsgType msgType = MsgType.Info)
        {
            Messageinfo = status;
            MessageType = msgType;
        }
    }


    /// <summary>
    /// 日志级别到颜色转换器
    /// </summary>
    public class MessageTypeToBrushConverter : IValueConverter
    {
        /// <summary>
        /// 转换值
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MsgType level)
            {
                return level switch
                {
                    MsgType.Info => new SolidColorBrush(Colors.White),
                    MsgType.Success => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    MsgType.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    MsgType.Fatal => new SolidColorBrush(Color.FromRgb(183, 28, 28)),
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        /// <summary>
        /// 转换值
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
