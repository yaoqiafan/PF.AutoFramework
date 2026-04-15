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
       
        public Splash()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty WelcomeTextProperty = DependencyProperty.Register(
           nameof(WelcomeText), typeof(string), typeof(Splash), new PropertyMetadata(String.Empty));

        public string WelcomeText
        {
            get => (string)GetValue(WelcomeTextProperty);
            set => SetValue(WelcomeTextProperty, value);
        }

        public static readonly DependencyProperty VersionNumberProperty = DependencyProperty.Register(
           nameof(VersionNumber), typeof(string), typeof(Splash), new PropertyMetadata(String.Empty));

        public string VersionNumber
        {
            get => (string)GetValue(VersionNumberProperty);
            set => SetValue(VersionNumberProperty, value);
        }

        public static readonly DependencyProperty WelcomeText_smallProperty = DependencyProperty.Register(
          nameof(WelcomeText_small), typeof(string), typeof(Splash), new PropertyMetadata(String.Empty));
        public string WelcomeText_small
        {
            get => (string)GetValue(WelcomeText_smallProperty);
            set => SetValue(WelcomeText_smallProperty, value);
        }


        public static readonly DependencyProperty MessageinfoProperty = DependencyProperty.Register(
         nameof(Messageinfo), typeof(string), typeof(Splash), new PropertyMetadata("Loading..."));
        public string Messageinfo
        {
            get => (string)GetValue(MessageinfoProperty);
            set => SetValue(MessageinfoProperty, value);
        }


      
        public static readonly DependencyProperty MessageTypeProperty = DependencyProperty.Register(
            nameof(MessageType), typeof(MsgType), typeof(Splash), new PropertyMetadata(default));

        public MsgType MessageType
        {
            get => (MsgType)GetValue(MessageTypeProperty);
            set => SetValue(MessageTypeProperty, value);
        }



        public Func<Task<bool>> LoadingAction { get; set; } = () => Task.FromResult(true);

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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
