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
using System.Windows.Navigation;
using System.Windows.Shapes;
namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für TimeControl.xaml
    /// </summary>
    public partial class TimeControl : UserControl
    {
        public TimeControl()
        {
            InitializeComponent();
        }

        public TimeSpan Value
        {
            get { return (TimeSpan)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(TimeSpan), typeof(TimeControl),
        new UIPropertyMetadata(DateTime.Now.TimeOfDay, new PropertyChangedCallback(OnValueChanged)));

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeControl control = obj as TimeControl;
            control.Hours = ((TimeSpan)e.NewValue).Hours;
            control.Minutes = ((TimeSpan)e.NewValue).Minutes;
            control.Seconds = ((TimeSpan)e.NewValue).Seconds;
        }

        public int Hours
        {
            get { return (int)GetValue(HoursProperty); }
            set { SetValue(HoursProperty, value); }
        }

        public static readonly DependencyProperty HoursProperty =
        DependencyProperty.Register("Hours", typeof(int), typeof(TimeControl),
        new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));
        public int Minutes
        {
            get { return (int)GetValue(MinutesProperty); }
            set { SetValue(MinutesProperty, value); }
        }

        public static readonly DependencyProperty MinutesProperty =
        DependencyProperty.Register("Minutes", typeof(int), typeof(TimeControl),
        new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        public int Seconds
        {
            get { return (int)GetValue(SecondsProperty); }
            set { SetValue(SecondsProperty, value); }
        }
        public static readonly DependencyProperty SecondsProperty =
        DependencyProperty.Register("Seconds", typeof(int), typeof(TimeControl),
        new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        private static void OnTimeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
           //TimeControl control = obj as TimeControl;
          // control.Value = new TimeSpan(control.Hours, control.Minutes, control.Seconds);           
        }

        private void Down(object sender, KeyEventArgs args)
        {
            switch (((Grid)sender).Name)
            {
                case "sec":
                    if (args.Key == Key.Up)
                        this.Seconds = this.Seconds >= 59 ? 0 : this.Seconds + 1;
                    if (args.Key == Key.Down)
                        this.Seconds = this.Seconds <= 0 ? 59 : this.Seconds - 1;
                    break;
                case "min":
                    if (args.Key == Key.Up)
                        this.Minutes = this.Minutes >= 59 ? 0 : this.Minutes + 1;
                    if (args.Key == Key.Down)
                        this.Minutes = this.Minutes <= 0 ? 59 : this.Minutes - 1;
                    break;
                case "hour":
                    if (args.Key == Key.Up)
                        this.Hours = this.Hours >= 23 ? 0 : this.Hours + 1;
                    if (args.Key == Key.Down)
                        this.Hours = this.Hours <= 0 ? 23 : this.Hours - 1;
                    break;
            }

            if (args.Key == Key.Up
                || args.Key == Key.Down)
            {
                args.Handled = true;
            }
        }

        private void hour_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ((Grid)sender).Focus();
        }

        private void sec_MouseWheel(object sender, MouseWheelEventArgs args)
        {    
            switch (((Grid)sender).Name)
            {
                case "sec":
                    if (args.Delta > 0)
                        this.Seconds = this.Seconds >= 59 ? 0 : this.Seconds + 1;
                    if (args.Delta < 0)
                        this.Seconds = this.Seconds <= 0 ? 59 : this.Seconds - 1;
                    break;
                case "min":
                    if (args.Delta > 0)
                        this.Minutes = this.Minutes >= 59 ? 0 : this.Minutes + 1;
                    if (args.Delta < 0)
                        this.Minutes = this.Minutes <= 0 ? 59 : this.Minutes - 1;
                    break;
                case "hour":
                    if (args.Delta > 0)
                        this.Hours = this.Hours >= 23 ? 0 : this.Hours + 1;
                    if (args.Delta < 0)
                        this.Hours = this.Hours <= 0 ? 23 : this.Hours - 1;
                    break;
            }

            args.Handled = true;
        }
    }
}
