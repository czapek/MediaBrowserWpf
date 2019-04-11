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
    /// Interaktionslogik für WpfMediaKit.xaml
    /// </summary>
    public partial class WpfMediaKit : UserControl, IVideoControl
    {
        DispatcherTimer PositionChangedTimer;
        bool isPlaying;

        public event EventHandler EndReached;
        public event EventHandler PositionChanged;

        public WpfMediaKit()
        {
            InitializeComponent();

            this.VideoPlayer.MediaEnded += new RoutedEventHandler(VideoPlayer_MediaEnded);

            this.PositionChangedTimer = new DispatcherTimer();
            this.PositionChangedTimer.IsEnabled = true;
            this.PositionChangedTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            this.PositionChangedTimer.Tick += new EventHandler(PositionChangedTimer_Tick);
            this.PositionChangedTimer.Start();
        }

        public void Play()
        {
            this.VideoPlayer.Play();
            this.isPlaying = true;
        }

        public bool IsLoop
        {
            get
            {
                return this.VideoPlayer.Loop;
            }
            set
            {
                this.VideoPlayer.Loop = value;
            }
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

        public bool IsPlaying
        {
            get { return this.isPlaying; }
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
            this.VideoPlayer.MediaPosition += 400000;
        }

        public long TimeMilliseconds
        {
            get
            {
                return this.VideoPlayer.MediaPosition / 10000;
            }
            set
            {
                this.VideoPlayer.MediaPosition = value * 10000;
            }
        }

        public float Position
        {
            get
            {
                if (this.VideoPlayer.MediaDuration == 0 || this.VideoPlayer.MediaPosition == 0)
                {
                    return 0;
                }

                return (float)((double)this.VideoPlayer.MediaPosition / (double)this.VideoPlayer.MediaDuration);
            }

            set
            {
                this.VideoPlayer.MediaPosition = (long)(this.VideoPlayer.MediaDuration * value);
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
                return 50 + (int)((this.VideoPlayer.Volume - .5) * 100);
            }

            set
            {
                this.VideoPlayer.Volume = .5 + (double)(value / 2) / 100.0;
            }
        }

        double muteVol = 0;
        public void VolumeMute()
        {            
            if (muteVol == 0)
            {
                muteVol = this.VideoPlayer.Volume;
                this.VideoPlayer.Volume = 0;
            }
            else
            {
                this.VideoPlayer.Volume = muteVol;
                muteVol = 0;
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (this.EndReached != null && !this.IsLoop)
            {
                this.EndReached.Invoke(this, EventArgs.Empty);
            }
        }

        void PositionChangedTimer_Tick(object sender, EventArgs e)
        {
            if (this.isPlaying && this.PositionChanged != null)
                this.PositionChanged.Invoke(this, EventArgs.Empty);
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
