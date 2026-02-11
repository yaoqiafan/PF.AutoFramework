using PF.UI.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PF.UI.Controls
{
    [TemplatePart(Name = ElementTextBlockStart, Type = typeof(TextBlock))]
    [TemplatePart(Name = ElementTextBlockEnd, Type = typeof(TextBlock))]
    [TemplatePart(Name = ElementPopStart, Type = typeof(Popup))]
    [TemplatePart(Name = ElementPopEnd, Type = typeof(Popup))]
    public class DateTimeSelector : Control
    {
        public event EventHandler<TimeRangeChangedEventArgs> TimeRangeChanged;
        static DateTimeSelector()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DateTimeSelector), new FrameworkPropertyMetadata(typeof(DateTimeSelector)));
        }

        #region Constants

        private const string ElementTextBlockStart = "PART_TextBlockStart";

        private const string ElementTextBlockEnd = "PART_TextBlockEnd";

        private const string ElementPopStart = "PART_PopStart";

        private const string ElementPopEnd = "PART_PopEnd";


        protected virtual void OnTimeRangeChanged(TimeRangeChangedEventArgs e)
        {
            e.StartTime = StartTime;
            e.EndTime = EndTime;
            TimeRangeChanged?.Invoke(this, e);
        }

        #endregion Constants


        #region Data

        private TextBlock _TextBlockStart;

        private TextBlock _TextBlockEnd;

        private Popup _PopStart;

        private Popup _PopEnd;

        private CalendarWithListClock _StartCalendar;

        private CalendarWithListClock _EndCalendar;

        private RadioButton _RadioButton1;
        private RadioButton _RadioButton2;
        private RadioButton _RadioButton3;

        #endregion Data

        public DateTime StartTime
        {
            get => (DateTime)GetValue(StartTimeProperty);
            set => SetValue(StartTimeProperty, value);
        }

        public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register("StartTime", typeof(DateTime), typeof(DateTimeSelector), new PropertyMetadata(DateTime.Now, OnTimeRangePropertyChanged));


        public DateTime EndTime
        {
            get => (DateTime)GetValue(EndTimeProperty);
            set => SetValue(EndTimeProperty, value);
        }

        public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register("EndTime", typeof(DateTime), typeof(DateTimeSelector), new PropertyMetadata(DateTime.Now, OnTimeRangePropertyChanged));

       
        private static void OnTimeRangePropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as DateTimeSelector;
            if (control == null) return;

            TimeRangeChangedEventArgs res = new TimeRangeChangedEventArgs();
            control.OnTimeRangeChanged(res);
        }

        public double CheckBoxHeight
        {
            get => (double)GetValue(CheckBoxHeightProperty);
            set => SetValue(CheckBoxHeightProperty, value);
        }

        public static readonly DependencyProperty CheckBoxHeightProperty = DependencyProperty.Register("CheckBoxHeight", typeof(double), typeof(DateTimeSelector), new PropertyMetadata(50.0));


        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set
            {
                SetValue(IsCheckedProperty, value);
            }
        }
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(DateTimeSelector), new PropertyMetadata(true));


        public bool ShowCheckBox
        {
            get { return (bool)GetValue(ShowCheckBoxProperty); }
            set
            {
                SetValue(ShowCheckBoxProperty, value);
            }
        }
        public static readonly DependencyProperty ShowCheckBoxProperty = DependencyProperty.Register("ShowCheckBox", typeof(bool), typeof(DateTimeSelector), new PropertyMetadata(true));


        public override void OnApplyTemplate()
        {
            if (_TextBlockStart != null)
            {
                _TextBlockStart.PreviewMouseLeftButtonUp -= PopStartOpen_PreviewMouseLeftButtonUp;
            }
            if (_TextBlockEnd != null)
            {
                _TextBlockEnd.PreviewMouseLeftButtonUp -= PopEndOpen_PreviewMouseLeftButtonUp;
            }
            if (_StartCalendar != null)
            {
                _StartCalendar.DisplayDateTimeChanged -= _StartCalendar_DisplayDateTimeChanged;
            }
            if (_EndCalendar != null)
            {
                _EndCalendar.DisplayDateTimeChanged -= _EndCalendar_DisplayDateTimeChanged;
            }
            if (_RadioButton1 != null)
            {
                _RadioButton1.Checked -= _RadioButton_Checked;
            }
            if (_RadioButton2 != null)
            {
                _RadioButton2.Checked -= _RadioButton_Checked;
            }
            if (_RadioButton3 != null)
            {
                _RadioButton3.Checked -= _RadioButton_Checked;
            }


            base.OnApplyTemplate();

            _TextBlockStart = GetTemplateChild(ElementTextBlockStart) as TextBlock;
            if (_TextBlockStart != null)
            {
                _TextBlockStart.PreviewMouseLeftButtonUp += PopStartOpen_PreviewMouseLeftButtonUp;
            }
            _TextBlockEnd = GetTemplateChild(ElementTextBlockEnd) as TextBlock;
            if (_TextBlockEnd != null)
            {
                _TextBlockEnd.PreviewMouseLeftButtonUp += PopEndOpen_PreviewMouseLeftButtonUp;
            }

            _PopStart = GetTemplateChild(ElementPopStart) as Popup;
            if (_PopStart != null)
            {
                _PopStart.PreviewKeyDown += _PopStart_PreviewKeyDown; ;
            }

            _PopEnd = GetTemplateChild(ElementPopEnd) as Popup;
            if (_PopEnd != null)
            {
                _PopEnd.PreviewKeyDown += _PopStart_PreviewKeyDown; ;
            }

            _StartCalendar = GetTemplateChild("StartCalendar") as CalendarWithListClock;
            if (_StartCalendar != null)
            {
                _StartCalendar.DisplayDateTimeChanged += _StartCalendar_DisplayDateTimeChanged;
            }

            _EndCalendar = GetTemplateChild("EndCalendar") as CalendarWithListClock;
            if (_EndCalendar != null)
            {
                _EndCalendar.DisplayDateTimeChanged += _EndCalendar_DisplayDateTimeChanged;
            }


            _RadioButton1 = GetTemplateChild("RadioButton1") as RadioButton;
            if (_RadioButton1 != null)
            {
                _RadioButton1.Checked += _RadioButton_Checked;
            }

            _RadioButton2 = GetTemplateChild("RadioButton2") as RadioButton;
            if (_RadioButton2 != null)
            {
                _RadioButton2.Checked += _RadioButton_Checked;
            }

            _RadioButton3 = GetTemplateChild("RadioButton3") as RadioButton;
            if (_RadioButton3 != null)
            {
                _RadioButton3.Checked += _RadioButton_Checked;
            }

        }

        private void _PopStart_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _PopStart.IsOpen = false;
                _PopEnd.IsOpen = false;
            }
        }

        private void _RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if (radioButton != null)
            {
                if (radioButton.Tag == null) { return; }
                switch (radioButton.Tag.ToString())
                {
                    case "DAY":
                        StartTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 00:00:00"));
                        EndTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 23:59:59"));
                        break;
                    case "WEEK":
                        EndTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 23:59:59"));
                        StartTime = EndTime.AddDays(-7).AddSeconds(1);
                        break;
                    case "MONTH":
                        EndTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 23:59:59"));
                        StartTime = EndTime.AddDays(-30).AddSeconds(1);
                        break;
                    default:
                        StartTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 00:00:00"));
                        EndTime = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd 23:59:59"));
                        break;
                }

                _StartCalendar.UPadtaSelectedDate(StartTime);
                _EndCalendar.UPadtaSelectedDate(EndTime);

            }
        }

        private void _EndCalendar_DisplayDateTimeChanged(object sender, FunctionEventArgs<DateTime> e)
        {
            EndTime = _EndCalendar.DisplayDateTime;
        }

        private void _StartCalendar_DisplayDateTimeChanged(object sender, FunctionEventArgs<DateTime> e)
        {
            StartTime = _StartCalendar.DisplayDateTime;
        }

        private void PopStartOpen_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_PopStart != null && _PopEnd != null)
            {
                _PopStart.IsOpen = true;
                _PopEnd.IsOpen = false;
            }
        }


        private void PopEndOpen_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_PopStart != null && _PopEnd != null)
            {
                _PopStart.IsOpen = false;
                _PopEnd.IsOpen = true;
            }
        }
    }


    public class TimeRangeChangedEventArgs : EventArgs
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }
    }
      
}
