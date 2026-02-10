using PF.UI.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PF.Controls
{
    [TemplatePart(Name = ElementClockPresenter, Type = typeof(ContentPresenter))]
    [TemplatePart(Name = ElementCalendarPresenter, Type = typeof(ContentPresenter))]
    public class CalendarWithListClock : Control
    {
        static CalendarWithListClock()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CalendarWithListClock), new FrameworkPropertyMetadata(typeof(CalendarWithListClock)));
        }



        #region Constants


        private const string ElementClockPresenter = "PART_ClockPresenter";

        private const string ElementCalendarPresenter = "PART_CalendarPresenter";

        #endregion Constants

        #region Data

        private ContentPresenter _clockPresenter;

        private ContentPresenter _calendarPresenter;

        private ListClock _clock;

        private Calendar _calendar;

        private bool _isLoaded;

        private IDictionary<DependencyProperty, bool> _isHandlerSuspended;

        #endregion Data

        #region Public Events



        public event EventHandler<FunctionEventArgs<DateTime>> DisplayDateTimeChanged;



        #endregion Public Events

        public CalendarWithListClock()
        {
            InitCalendarAndClock();
            Loaded += (s, e) =>
            {
                if (_isLoaded) return;
                _isLoaded = true;
                DisplayDateTime = DateTime.Now;
            };
        }

        #region Public Properties

        public static readonly DependencyProperty DateTimeFormatProperty = DependencyProperty.Register(
            nameof(DateTimeFormat), typeof(string), typeof(CalendarWithListClock), new PropertyMetadata("yyyy-MM-dd HH:mm:ss"));

        public string DateTimeFormat
        {
            get => (string)GetValue(DateTimeFormatProperty);
            set => SetValue(DateTimeFormatProperty, value);
        }


        public static readonly DependencyProperty DisplayDateTimeProperty = DependencyProperty.Register(
            nameof(DisplayDateTime), typeof(DateTime), typeof(CalendarWithListClock), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDisplayDateTimeChanged));

        private static void OnDisplayDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (CalendarWithListClock)d;
            //if (ctl.IsHandlerSuspended(DisplayDateTimeProperty)) return;
            var v = (DateTime)e.NewValue;
            ctl._clock.SelectedTime = v;
            ctl._calendar.DisplayDate = v;
            ctl.OnDisplayDateTimeChanged(new FunctionEventArgs<DateTime>(v));
        }

        public DateTime DisplayDateTime
        {
            get => (DateTime)GetValue(DisplayDateTimeProperty);
            set => SetValue(DisplayDateTimeProperty, value);
        }

        #endregion

        #region Public Methods

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _clockPresenter = GetTemplateChild(ElementClockPresenter) as ContentPresenter;
            _calendarPresenter = GetTemplateChild(ElementCalendarPresenter) as ContentPresenter;

            CheckNull();

            _clockPresenter.Content = _clock;
            _calendarPresenter.Content = _calendar;

        }

        #endregion

        #region Protected Methods

        protected virtual void OnDisplayDateTimeChanged(FunctionEventArgs<DateTime> e)
        {
            var handler = DisplayDateTimeChanged;
            handler?.Invoke(this, e);
        }

        #endregion Protected Methods

        #region Private Methods

        private void SetIsHandlerSuspended(DependencyProperty property, bool value)
        {
            if (value)
            {
                _isHandlerSuspended ??= new Dictionary<DependencyProperty, bool>(2);
                _isHandlerSuspended[property] = true;
            }
            else
            {
                _isHandlerSuspended?.Remove(property);
            }
        }

        private void SetValueNoCallback(DependencyProperty property, object value)
        {
            SetIsHandlerSuspended(property, true);
            try
            {
                SetCurrentValue(property, value);
            }
            finally
            {
                SetIsHandlerSuspended(property, false);
            }
        }

        private bool IsHandlerSuspended(DependencyProperty property)
        {
            return _isHandlerSuspended != null && _isHandlerSuspended.ContainsKey(property);
        }

        private void CheckNull()
        {
            if (_clockPresenter == null || _calendarPresenter == null) throw new Exception();
        }


        private void InitCalendarAndClock()
        {
            _clock = new ListClock
            {
                BorderThickness = new Thickness(),
                Background = Brushes.Transparent
            };
            TitleElement.SetBackground(_clock, Brushes.Transparent);
            _clock.DisplayTimeChanged += Clock_DisplayTimeChanged;

            _calendar = new Calendar
            {
                BorderThickness = new Thickness(),
                Background = Brushes.Transparent,
                Focusable = false,
                DisplayMode = CalendarMode.Month,
                FirstDayOfWeek = DayOfWeek.Sunday,
                VerticalAlignment = VerticalAlignment.Bottom,
                SelectionMode = CalendarSelectionMode.SingleDate,
                Language = System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN"),
            };
            TitleElement.SetBackground(_calendar, Brushes.Transparent);
            _calendar.SelectedDatesChanged += Calendar_SelectedDatesChanged;
        }

        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            Mouse.Capture(null);
            UpdateDisplayTime();
        }

        private void Clock_DisplayTimeChanged(object sender, FunctionEventArgs<DateTime> e) => UpdateDisplayTime();

        private void UpdateDisplayTime()
        {
            if (_calendar.SelectedDate != null)
            {
                var date = _calendar.SelectedDate.Value;
                var time = _clock.DisplayTime;

                var result = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second);
                SetValueNoCallback(DisplayDateTimeProperty, result);
            }
        }

        #endregion
        public void UPadtaSelectedDate(DateTime dateTime)
        {
            _calendar.SelectedDate = dateTime;
            _clock.SelectedTime = dateTime;
        }

    }
}
