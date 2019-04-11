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
using WPFMediaKit.DirectShow.Controls;

namespace MediaBrowserWPF.UserControls.Video
{
    /// <summary>
    /// Interaktionslogik für Multiviewer.xaml
    /// </summary>
    public partial class Multiviewer : UserControl, IVideoControl
    {
        DispatcherTimer PositionChangedTimer;
        bool isPlaying;
        bool init1Set, init2Set, init3Set, init4Set;
        long initPos;

        public event EventHandler EndReached;
        public event EventHandler PositionChanged;
        private MediaUriElement VideoPlayer;
        private int selectedVideoPlayer = 0;

        public Multiviewer()
        {
            InitializeComponent();

            this.VideoPlayer = this.VideoPlayer1;

            this.VideoPlayer1.MediaEnded += new RoutedEventHandler(VideoPlayer_MediaEnded);
            this.VideoPlayer2.MediaEnded += new RoutedEventHandler(VideoPlayer_MediaEnded);
            this.VideoPlayer3.MediaEnded += new RoutedEventHandler(VideoPlayer_MediaEnded);
            this.VideoPlayer4.MediaEnded += new RoutedEventHandler(VideoPlayer_MediaEnded);

            this.VideoPlayer1.MediaOpened += new RoutedEventHandler(VideoPlayer_MediaOpened);
            this.VideoPlayer2.MediaOpened += new RoutedEventHandler(VideoPlayer_MediaOpened);
            this.VideoPlayer3.MediaOpened += new RoutedEventHandler(VideoPlayer_MediaOpened);
            this.VideoPlayer4.MediaOpened += new RoutedEventHandler(VideoPlayer_MediaOpened);

            this.PositionChangedTimer = new DispatcherTimer();
            this.PositionChangedTimer.IsEnabled = true;
            this.PositionChangedTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            this.PositionChangedTimer.Tick += new EventHandler(PositionChangedTimer_Tick);
            this.PositionChangedTimer.Start();
        }

        void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.Reset();
        }

        public void Reset()
        {
            this.init1Set = false;
            this.init2Set = false;
            this.init3Set = false;
            this.init4Set = false;

            this.VideoPlayer1.Visibility = System.Windows.Visibility.Hidden;
            this.VideoPlayer2.Visibility = System.Windows.Visibility.Hidden;
            this.VideoPlayer3.Visibility = System.Windows.Visibility.Hidden;
            this.VideoPlayer4.Visibility = System.Windows.Visibility.Hidden;
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
                this.VideoPlayer1.Loop = value;
                this.VideoPlayer2.Loop = value;
                this.VideoPlayer3.Loop = value;
                this.VideoPlayer4.Loop = value;
            }
        }

        public List<TrackInfo> TrackInfo
        {
            get
            {
                List<TrackInfo> trackInfoList = new List<TrackInfo>();
               
                return trackInfoList;
            }
        }

        public void SetVideoTrack(TrackInfo trackInfo)
        {

        }


        public Tuple<double, double> PlayInterval
        {
            get;
            set;
        }

        public long Interval
        {
            get
            {
                if (this.VideoPlayer == null)
                    return 0;

                if (this.PlayInterval == null)
                    return this.VideoPlayer.MediaDuration;

                if (this.PlayInterval.Item1 > 0 && this.PlayInterval.Item2 > this.PlayInterval.Item1)
                {
                    return 10000 * ((long)(this.PlayInterval.Item2 - this.PlayInterval.Item1) * 1000);
                }
                else if (this.PlayInterval.Item1 > 0)
                {
                    return this.VideoPlayer.MediaDuration - 10000 * ((long)(this.PlayInterval.Item1) * 1000);
                }
                else if (this.PlayInterval.Item2 > 0)
                {
                    return 10000 * ((long)(this.PlayInterval.Item2) * 1000);
                }
                else
                {
                    return this.VideoPlayer.MediaDuration;
                }
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
                if (Keyboard.Modifiers != ModifierKeys.Control)
                {
                    this.initPos = (long)(this.VideoPlayer.MediaDuration * value) - (long)(10000000 * this.PlayInterval.Item1);
                    this.init1Set = false;
                    this.init2Set = false;
                    this.init3Set = false;
                    this.init4Set = false;
                }
                else
                {
                    this.VideoPlayer.MediaPosition = (long)(this.VideoPlayer.MediaDuration * value);
                }
            }
        }

        public string Source
        {
            set
            {
                if (value == null)
                {
                    this.VideoPlayer1.Stop();
                    this.VideoPlayer2.Stop();
                    this.VideoPlayer3.Stop();
                    this.VideoPlayer4.Stop();
                    return;
                }

                this.initPos = 0;
                this.VideoPlayer1.Volume = this.VideoPlayer.Volume;
                this.VideoPlayer = this.VideoPlayer1;
                this.VideoPlayer2.Volume = 0;
                this.VideoPlayer3.Volume = 0;
                this.VideoPlayer4.Volume = 0;

                this.VideoPlayer1.Source = new Uri(value);
                this.VideoPlayer2.Source = new Uri(value);
                this.VideoPlayer3.Source = new Uri(value);
                this.VideoPlayer4.Source = new Uri(value);
                this.Play();
            }
        }

        public System.Drawing.Bitmap TakeSnapshot()
        {
            System.Drawing.Bitmap bmp = null;

            try
            {
                RenderTargetBitmap renderTargetBitmap =
                  new RenderTargetBitmap((int)this.PlayerGrid.ActualWidth, (int)this.PlayerGrid.ActualHeight, 96, 96, PixelFormats.Pbgra32);

                VisualBrush sourceBrush = new VisualBrush(this.PlayerGrid);
                DrawingVisual drawingVisual = new DrawingVisual();
                DrawingContext drawingContext = drawingVisual.RenderOpen();

                using (drawingContext)
                {
                    drawingContext.DrawRectangle(sourceBrush, null, new Rect(new Point(0, 0),
                        new Point((int)this.PlayerGrid.ActualWidth, (int)this.PlayerGrid.ActualHeight)));
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
            if (this.isPlaying && this.VideoPlayer.MediaDuration > 0)
            {
                this.init1Set = this.initPos == 0;
                this.VideoPlayer1.Visibility = System.Windows.Visibility.Visible;

                if (this.VideoPlayer == this.VideoPlayer1)
                {
                    this.SetPlayerPos(ref this.init1Set, this.VideoPlayer1, this.initPos + 0 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init2Set, this.VideoPlayer2, this.initPos + 1 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init3Set, this.VideoPlayer3, this.initPos + 2 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init4Set, this.VideoPlayer4, this.initPos + 3 * this.Interval / 4);
                }
                else if (this.VideoPlayer == this.VideoPlayer2)
                {
                    this.SetPlayerPos(ref this.init1Set, this.VideoPlayer2, this.initPos + 0 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init2Set, this.VideoPlayer3, this.initPos + 1 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init3Set, this.VideoPlayer4, this.initPos + 2 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init4Set, this.VideoPlayer1, this.initPos + 3 * this.Interval / 4);
                }
                else if (this.VideoPlayer == this.VideoPlayer3)
                {
                    this.SetPlayerPos(ref this.init1Set, this.VideoPlayer3, this.initPos + 0 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init2Set, this.VideoPlayer4, this.initPos + 1 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init3Set, this.VideoPlayer1, this.initPos + 2 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init4Set, this.VideoPlayer2, this.initPos + 3 * this.Interval / 4);
                }
                else if (this.VideoPlayer == this.VideoPlayer4)
                {
                    this.SetPlayerPos(ref this.init1Set, this.VideoPlayer4, this.initPos + 0 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init2Set, this.VideoPlayer3, this.initPos + 1 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init3Set, this.VideoPlayer2, this.initPos + 2 * this.Interval / 4);
                    this.SetPlayerPos(ref this.init4Set, this.VideoPlayer1, this.initPos + 3 * this.Interval / 4);
                }
                this.initPos = 0;

                if (this.PositionChanged != null)
                    this.PositionChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetPlayerPos(ref bool initSet, MediaUriElement player, long pos)
        {
            if (!initSet && player.MediaPosition != 0)
            {
                pos = pos % this.Interval;
                initSet = true;
                player.Visibility = System.Windows.Visibility.Visible;
                player.MediaPosition = (this.PlayInterval == null ? 0 : (10000 * (long)(this.PlayInterval.Item1 * 1000))) + pos;
            }

            double currentPos = (double)(player.MediaPosition / 10000) / 1000;

            if ((this.PlayInterval.Item1 > 0 && currentPos < this.PlayInterval.Item1)
                || (this.PlayInterval.Item2 > 0 && this.PlayInterval.Item2 > this.PlayInterval.Item1
                && currentPos >= this.PlayInterval.Item2))
            {
                if (currentPos >= this.PlayInterval.Item2 && player == this.VideoPlayer && this.EndReached != null && !this.IsLoop)
                {
                    this.EndReached.Invoke(this, EventArgs.Empty);
                }

                player.MediaPosition = (long)(this.PlayInterval.Item1 * 10000000);
            }
        }

        public void Dispose()
        {
            this.VideoPlayer1.Stop();
            this.VideoPlayer2.Stop();
            this.VideoPlayer3.Stop();
            this.VideoPlayer4.Stop();
        }

        public void KeyTabDown()
        {
            switch (this.selectedVideoPlayer)
            {
                case 0:
                    SetPlayer2();
                    break;

                case 1:
                    SetPlayer3();
                    break;

                case 2:
                    SetPlayer4();
                    break;

                case 3:
                    SetPlayer1();
                    break;
            }
        }

        private void VideoPlayer1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetPlayer1();
        }

        private void SetPlayer1()
        {
            MainWindow.GiveShortFeedback();
            double snd = this.VideoPlayer.Volume;
            this.VideoPlayer1.Volume = snd;
            this.VideoPlayer2.Volume = 0;
            this.VideoPlayer3.Volume = 0;
            this.VideoPlayer4.Volume = 0;
            this.VideoPlayer = this.VideoPlayer1;
            this.selectedVideoPlayer = 0;

            this.SetMultiPosition();
        }

        private void VideoPlayer2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetPlayer2();
        }

        private void SetPlayer2()
        {
            MainWindow.GiveShortFeedback();
            double snd = this.VideoPlayer.Volume;
            this.VideoPlayer1.Volume = 0;
            this.VideoPlayer2.Volume = snd;
            this.VideoPlayer3.Volume = 0;
            this.VideoPlayer4.Volume = 0;
            this.VideoPlayer = this.VideoPlayer2;
            this.selectedVideoPlayer = 1;

            this.SetMultiPosition();
        }

        private void VideoPlayer3_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetPlayer3();
        }

        private void SetPlayer3()
        {
            MainWindow.GiveShortFeedback();
            double snd = this.VideoPlayer.Volume;
            this.VideoPlayer1.Volume = 0;
            this.VideoPlayer2.Volume = 0;
            this.VideoPlayer3.Volume = snd;
            this.VideoPlayer4.Volume = 0;
            this.VideoPlayer = this.VideoPlayer3;
            this.selectedVideoPlayer = 2;

            this.SetMultiPosition();
        }

        private void VideoPlayer4_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetPlayer4();
        }

        private void SetPlayer4()
        {
            MainWindow.GiveShortFeedback();
            double snd = this.VideoPlayer.Volume;
            this.VideoPlayer1.Volume = 0;
            this.VideoPlayer2.Volume = 0;
            this.VideoPlayer3.Volume = 0;
            this.VideoPlayer4.Volume = snd;
            this.VideoPlayer = this.VideoPlayer4;
            this.selectedVideoPlayer = 3;

            this.SetMultiPosition();
        }

        private void SetMultiPosition()
        {
            long diff = 0;
            long pos = this.VideoPlayer.MediaPosition;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                diff = 0;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                diff = 10000000;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                diff = 100000000;
            }
            else
            {
                return;
            }

            this.VideoPlayer1.MediaPosition = pos;
            this.VideoPlayer2.MediaPosition = pos + diff;
            this.VideoPlayer3.MediaPosition = pos + diff * 2;
            this.VideoPlayer4.MediaPosition = pos + diff * 3;
        }        
    }
}
