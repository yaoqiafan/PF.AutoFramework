using PF.UI.Controls.Data;
using System.Windows;
using System.Windows.Media;

namespace PF.UI.Resources
{
    /// <summary>
    /// Splash.xaml 的交互逻辑
    /// </summary>
    public partial class Splash : PF.UI.Controls.PFWindow
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



        public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
          nameof(TextBrush), typeof(Brush), typeof(Splash), new PropertyMetadata(Brushes.White));
        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }



        public static readonly DependencyProperty MessageinfoProperty = DependencyProperty.Register(
         nameof(Messageinfo), typeof(string), typeof(Splash), new PropertyMetadata("Loading..."));
        public string Messageinfo
        {
            get => (string)GetValue(MessageinfoProperty);
            set => SetValue(MessageinfoProperty, value);
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
        }
    }
}
