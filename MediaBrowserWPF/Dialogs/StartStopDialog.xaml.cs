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

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für StartStopDialog.xaml
    /// </summary>
    public partial class StartStopDialog : Window
    {
        public StartStopDialog()
        {
            InitializeComponent();   
        }

        public DateTime? StartDate
        {
            get; set;
        }

        public DateTime? StopDate
        {
            get; set;
        }

        public StartStopDialog(DateTime start, DateTime stop)
        {
            InitializeComponent();

            if (start >= stop)
            {
                start = start.Date;
                stop = start.AddDays(1);
            }

            this.DtPickerStart.SelectedDate = start;
            this.DtPickerStop.SelectedDate = stop;

            this.TimeControlStart.Hours = start.Hour;
            this.TimeControlStart.Minutes = start.Minute;
            this.TimeControlStart.Seconds = start.Second;

            this.TimeControlStop.Hours = stop.Hour;
            this.TimeControlStop.Minutes = stop.Minute;
            this.TimeControlStop.Seconds = stop.Second;
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.StartDate = this.DtPickerStart.SelectedDate.Value.Date
                .AddHours(this.TimeControlStart.Hours)
                .AddMinutes(this.TimeControlStart.Minutes)
                .AddSeconds(this.TimeControlStart.Seconds);

            this.StopDate = this.DtPickerStop.SelectedDate.Value.Date
                .AddHours(this.TimeControlStop.Hours)
                .AddMinutes(this.TimeControlStop.Minutes)
                .AddSeconds(this.TimeControlStop.Seconds);

            this.DialogResult = true;
            this.Close();
        }
    }
}
