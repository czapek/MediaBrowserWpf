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
using System.IO;

namespace MediaBrowserWPF.UserControls.Video
{
    /// <summary>
    /// Interaktionslogik für WpfMediaElement.xaml
    /// </summary>
    public partial class WpfMediaElement : UserControl, IVideoControl, IDisposable
    {
        DispatcherTimer PositionChangedTimer;
        bool isPlaying;

        public WpfMediaElement()
        {
            InitializeComponent();
            this.VideoPlayer.MediaEnded += new RoutedEventHandler(VideoPlayer_MediaEnded);

            this.PositionChangedTimer = new DispatcherTimer();
            this.PositionChangedTimer.IsEnabled = true;
            this.PositionChangedTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            this.PositionChangedTimer.Tick += new EventHandler(PositionChangedTimer_Tick);
            this.PositionChangedTimer.Start();
        }

        void PositionChangedTimer_Tick(object sender, EventArgs e)
        {
            if (this.isPlaying && this.PositionChanged != null)
                this.PositionChanged.Invoke(this, EventArgs.Empty);
        }

        void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (this.IsLoop)
            {
                this.VideoPlayer.Stop();
                this.Play();
            }
            else
            {
                if (this.EndReached != null)
                    this.EndReached.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler EndReached;
        public event EventHandler PositionChanged;

        public double Speedratio
        {
            get
            {
                return this.VideoPlayer.SpeedRatio;
            }

            set
            {
                this.VideoPlayer.SpeedRatio = value;
            }
        }

        public void Play()
        {
            this.VideoPlayer.Play();
            this.isPlaying = true;
        }

        public bool IsPlaying
        {
            get { return this.isPlaying; }
        }

        public bool IsLoop
        {
            get;
            set;
        }

        public void SetVideoTrack(TrackInfo trackInfo)
        {

        }

        public List<TrackInfo> TrackInfo
        {
            get
            {
                List<TrackInfo> trackInfoList = new List<TrackInfo>();

                return trackInfoList;
            }
        }

        public void Pause()
        {
            this.VideoPlayer.Pause();
            this.isPlaying = false;
        }

        public void Stop()
        {
            this.VideoPlayer.Stop();
            this.isPlaying = false;
        }

        public void NextFrame()
        {
            this.VideoPlayer.Position = this.VideoPlayer.Position.Add(new TimeSpan(500000));
        }
        
        public long TimeMilliseconds
        {
            get
            {
                return this.VideoPlayer.Position.Ticks / 10000;
            }

            set
            {
                this.VideoPlayer.Position = new TimeSpan(value * 10000);
            }
        }

        public float Position
        {
            get
            {
                try
                {
                    if (this.VideoPlayer.Position == null || this.VideoPlayer.NaturalDuration == null
                        || this.VideoPlayer.Position.Ticks == 0 || this.VideoPlayer.NaturalDuration.TimeSpan.Ticks == 0)
                    {
                        return 0;
                    }

                    return (float)((double)this.VideoPlayer.Position.Ticks / (double)this.VideoPlayer.NaturalDuration.TimeSpan.Ticks);
                }
                catch
                {
                    return 0;
                }
            }

            set
            {
                if (this.VideoPlayer.NaturalDuration.HasTimeSpan)
                    this.VideoPlayer.Position = new TimeSpan((long)(this.VideoPlayer.NaturalDuration.TimeSpan.Ticks * value));
            }
        }

        public string Source
        {
            set
            {
                if (value == null)
                {
                    this.VideoPlayer.Stop();
                    return;
                }

                this.VideoPlayer.Source = new Uri(value);
                this.Play();
            }
        }

        public System.Drawing.Bitmap TakeSnapshot()
        {
            System.Drawing.Bitmap bmp = null;

            try
            {
                RenderTargetBitmap renderTargetBitmap =
                  new RenderTargetBitmap((int)this.VideoPlayer.NaturalVideoWidth, (int)this.VideoPlayer.NaturalVideoHeight, 96, 96, PixelFormats.Pbgra32);

                VisualBrush sourceBrush = new VisualBrush(this.VideoPlayer);
                DrawingVisual drawingVisual = new DrawingVisual();
                DrawingContext drawingContext = drawingVisual.RenderOpen();

                using (drawingContext)
                {
                    drawingContext.DrawRectangle(sourceBrush, null, new Rect(new Point(0, 0),
                        new Point((int)this.VideoPlayer.NaturalVideoWidth, (int)this.VideoPlayer.NaturalVideoHeight)));
                }

                renderTargetBitmap.Render(drawingVisual);

                PngBitmapEncoder png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                Stream ms = new MemoryStream();
                png.Save(ms);
                ms.Position = 0;
                bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromStream(ms);
            }
            catch
            {
            }

            return bmp;
        }

        public int Volume
        {
            get
            {
                return (int)(this.VideoPlayer.Volume * 100);
            }

            set
            {
                this.VideoPlayer.Volume = (double)value / 100.0;
            }
        }

        public void VolumeMute()
        {
            this.VideoPlayer.IsMuted = !this.VideoPlayer.IsMuted;           
        }

        public void Dispose()
        {
            this.VideoPlayer.Stop();
        }

        public void Reset()
        {
      
        }

        public void KeyTabDown()
        {
   
        }
    }
}
