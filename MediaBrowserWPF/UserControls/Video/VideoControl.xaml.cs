using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls.Video
{
    /// <summary>
    /// Interaktionslogik für VideoControl.xaml
    /// </summary>
    public partial class VideoControl : UserControl
    {
        public event EventHandler VideoLoaded;

        public class StatusbarVisibleArgs : EventArgs
        {
            public int MarginBottom { get; set; }
        }

        public event EventHandler<StatusbarVisibleArgs> StatusbarVisible;
        DispatcherTimer sliderTimer;

        public VideoControl()
        {
            InitializeComponent();

            this.sliderTimer = new DispatcherTimer();
            this.sliderTimer.IsEnabled = false;
            this.sliderTimer.Interval = new TimeSpan(0, 0, 0, 0, 3000);
            this.sliderTimer.Tick += new EventHandler(sliderTimer_Tick);
        }

        public MediaBrowserWPF.UserControls.Video.VideoPlayer.Player SelectedPlayer
        {
            set
            {
                this.VideoPlayer.SelectedPlayer = value;
            }

            get
            {
                return this.VideoPlayer.SelectedPlayer;
            }
        }

        public bool IsSliderBottom
        {
            set
            {
                this.SliderVideo.VerticalAlignment = value ? System.Windows.VerticalAlignment.Bottom : System.Windows.VerticalAlignment.Top;
            }

            get
            {
                return this.SliderVideo.VerticalAlignment == System.Windows.VerticalAlignment.Bottom;
            }

        }

        public VideoPlayer VideoPlayerIntern
        {
            get
            {
                return this.VideoPlayer;
            }
        }

        public bool IsLoop
        {
            get
            {
                return this.VideoPlayer.IsLoop;
            }

            set
            {
                this.VideoPlayer.IsLoop = value;
            }
        }

        public bool ShowPlayTime
        {
            set
            {
                if (value)
                {
                    this.InfoTextBlockTime.Visibility = System.Windows.Visibility.Visible;
                    this.InfoTextBlockTimeBlur.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    this.InfoTextBlockTime.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoTextBlockTimeBlur.Visibility = System.Windows.Visibility.Collapsed;
                }
            }

            get
            {
                return this.InfoTextBlockTime.Visibility == System.Windows.Visibility.Visible;
            }
        }

        private void SliderVideo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            double rel = e.GetPosition(this.SliderVideo).X / this.SliderVideo.ActualWidth;

            if (rel >= 0 && rel <= 1)
            {
                this.VideoPlayer.Position = (float)rel;
            }
        }

        private void SliderVideo_MouseMove(object sender, MouseEventArgs e)
        {
            double rel = e.GetPosition(this.SliderVideo).X / this.SliderVideo.ActualWidth;
            this.SliderVideo.ToolTip = MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(Math.Round(this.Duration * this.SliderVideo.Value), 0)
                + " / " + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(Math.Round(this.Duration), 0)
                + " (" + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(Math.Round(this.Duration * rel), 0) + ")";
        }

        void sliderTimer_Tick(object sender, EventArgs e)
        {
            this.SliderVideo.Visibility = System.Windows.Visibility.Collapsed;

            if (this.StatusbarVisible != null)
                this.StatusbarVisible.Invoke(this, new StatusbarVisibleArgs() { MarginBottom = 0 });

            this.sliderTimer.Stop();
        }

        private void VideoPlayer_PositionChanged(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (this.InfoTextBlockTime.Visibility == System.Windows.Visibility.Visible)
                {      
                    int perc = (int)(this.VideoPlayer.Position * 100);
                    this.InfoTextBlockTime.Text = (this.VideoPlayer.Speedratio != 1 ? String.Format("{0:n2}x ", this.VideoPlayer.Speedratio) : String.Empty)
                        + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(this.VideoPlayer.TimeMilliseconds * 10000)
                        + " (" + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(Math.Round(this.Duration), 0) + ")"; //+ (perc > 100 ? 100 : (perc < 0 ? 0 : perc)) 
                    this.InfoTextBlockTimeBlur.Text = this.InfoTextBlockTime.Text;
                }

                this.SliderVideo.Value = (double)this.VideoPlayer.Position;
            }));
        }

        private double Duration
        {
            get
            {
                if (this.VideoPlayer.Position > 0)
                    return this.VideoPlayer.TimeSeconds * (1 / (double)this.VideoPlayer.Position);
                else
                    return 0;
            }
        }

        private void UserControl_LayoutUpdated(object sender, EventArgs e)
        {
            if (this.RenderSize.Width > 0)
            {
                this.InfoTextBlockTime.FontSize = (this.RenderSize.Width + this.RenderSize.Height + 30) / 60;
                this.InfoTextBlockTimeBlur.FontSize = this.InfoTextBlockTime.FontSize;
            }
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            if ((this.SliderVideo.VerticalAlignment == VerticalAlignment.Top && e.GetPosition(this).Y < 50) ||
                (this.SliderVideo.VerticalAlignment == VerticalAlignment.Bottom && e.GetPosition(this).Y > this.ActualHeight - 50))
            {
                this.SliderVideo.Visibility = System.Windows.Visibility.Visible;

                if (this.StatusbarVisible != null)
                    this.StatusbarVisible.Invoke(this, new StatusbarVisibleArgs() { MarginBottom = 20 });
            }
            else if (this.SliderVideo.Visibility == System.Windows.Visibility.Visible)
            {
                this.sliderTimer.Start();
            }
        }

        private void VideoPlayer_VideoLoaded(object sender, EventArgs e)
        {
            if (this.VideoLoaded != null)
            {
                this.VideoLoaded.Invoke(sender, e);
            }
        }
    }
}
