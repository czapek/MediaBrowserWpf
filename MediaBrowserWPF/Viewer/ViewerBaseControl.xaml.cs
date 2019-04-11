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
using MediaBrowser4.Objects;
using System.IO;
using MediaBrowser4;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using MediaBrowser4.Utilities;
using System.Windows.Media.Effects;
using System.Windows.Controls.Primitives;

namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für ViewerBaseControl.xaml
    /// </summary>
    public partial class ViewerBaseControl : UserControl
    {
        UserControls.Video.VideoPlayer.Player selectedPlayer;
        DispatcherTimer mediaTimer;
        private List<MediaItem> history = new List<MediaItem>();
        private int historyPointer = 0;
        private int soundVolume;
        public event EventHandler EndReached;
        public event EventHandler MediaLoaded;
        public event EventHandler Activated;

        public ViewerBaseControl()
        {
            InitializeComponent();

            this.mediaTimer = new DispatcherTimer();
            this.mediaTimer.IsEnabled = false;
            this.mediaTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            this.mediaTimer.Tick += new EventHandler(mediaTimer_Tick);

            this.VideoPlayer.VideoPlayerIntern.EndReached += new EventHandler(VideoPlayerIntern_EndReached);
            this.VideoPlayer.VideoPlayerIntern.PositionChanged += new EventHandler(VideoPlayerIntern_PositionChanged);

            if (!Enum.TryParse(MediaBrowserContext.SelectedVideoPlayer, out this.selectedPlayer))
            {
                this.selectedPlayer = UserControls.Video.VideoPlayer.Player.WpfMediaKit;
            }
        }

        bool fadeIn = false;
        private void Storyboard_Completed(object sender, EventArgs e)
        {
            this.fadeIn = false;
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

        void VideoPlayerIntern_EndReached(object sender, EventArgs e)
        {
            if (this.EndReached != null)
                this.EndReached.Invoke(this, EventArgs.Empty);
        }

        void VideoPlayerIntern_PositionChanged(object sender, EventArgs e)
        {
            if (this.fadeIn && this.Dispatcher.CheckAccess())
            {
                this.fadeIn = false;
                Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
                storyBoardPanorama.Begin();
            }
        }

        private void ImagePlayer_ImageLoaded(object sender, EventArgs e)
        {
            if (this.fadeIn && this.Dispatcher.CheckAccess())
            {
                this.fadeIn = false;
                Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
                storyBoardPanorama.Begin();
            }

            if (this.MediaLoaded != null)
                this.MediaLoaded.Invoke(this, EventArgs.Empty);
        }

        private void VideoPlayer_VideoLoaded(object sender, EventArgs e)
        {
            this.VideoPlayer.VideoPlayerIntern.Volume = this.soundVolume;

            if (this.MediaLoaded != null)
                this.MediaLoaded.Invoke(this, EventArgs.Empty);

        }

        void mediaTimer_Tick(object sender, EventArgs e)
        {
            this.WatchDirectShow();
        }

        private bool setFromHistory = false;
        public bool SetPreviousItemFromHistory()
        {
            if (this.history.Count > 0 && this.historyPointer > -1)
            {
                this.setFromHistory = true;

                if (this.Source == (this.history.Count > 0 ? this.history[Math.Min(this.historyPointer, this.history.Count - 1)] : null))
                    this.historyPointer--;

                this.Source = this.history.Count > 0 ? this.history[Math.Max(this.historyPointer, 0)] : null;

                this.historyPointer--;

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool SetNextItemFromHistory()
        {
            if (this.historyPointer < this.history.Count - 1 && this.historyPointer >= -1 && this.setFromHistory)
            {
                this.historyPointer++;
                this.setFromHistory = true;

                if (this.Source == (this.history.Count > 0 ? this.history[Math.Min(this.historyPointer, this.history.Count - 1)] : null))
                    this.historyPointer++;

                this.Source = this.history.Count > 0 ? this.history[Math.Min(this.historyPointer, this.history.Count - 1)] : null;

                if (this.historyPointer == this.history.Count - 1)
                    this.setFromHistory = false;

                return true;
            }
            else
            {
                return false;
            }

        }

        private MediaItem source;
        public MediaItem Source
        {
            set
            {
                this.fadeIn = false;
                if (value == null || value.IsDeleted)
                {
                    value = null;
                }

                if (value != this.Source)
                {
                    this.fadeIn = true;
                    Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeOut"];
                    storyBoardPanorama.Begin();
                }

                this.VideoElement.Visibility = System.Windows.Visibility.Collapsed;
                this.ImageElement.Visibility = System.Windows.Visibility.Collapsed;

                if (value is MediaItemBitmap)
                    this.ViewMediaItemRgb(value as MediaItemBitmap);
                else if (value is MediaItemVideo)
                    this.ViewMediaItemDirectShow(value as MediaItemVideo);

                if (value != null && value != this.Source && !this.setFromHistory)
                {
                    this.history.Add(value);
                    this.historyPointer = this.history.Count - 2;
                }

                if (this.history.Any(x => x.IsDeleted))
                {
                    this.history = this.history.Where(x => !x.IsDeleted).ToList();
                    this.historyPointer = this.history.Count - 1;
                }

                this.source = value;
            }

            get
            {
                return this.source;
            }
        }

        public void Pause()
        {
            if (this.VideoPlayer.VideoPlayerIntern != null)
                this.VideoPlayer.VideoPlayerIntern.Pause();

        }

        public void Play()
        {
            if (this.VideoPlayer.VideoPlayerIntern != null)
                this.VideoPlayer.VideoPlayerIntern.Play();

        }

        public bool IsSliderBottom
        {
            set
            {
                this.VideoPlayer.IsSliderBottom = value;
            }

            get
            {
                return this.VideoPlayer.IsSliderBottom;
            }
        }

        bool isActive = false;
        public void SetActive()
        {
            if (this.Activated != null)
                this.Activated.Invoke(this, EventArgs.Empty);

            DropShadowEffect dropShadowEffect = this.Effect as DropShadowEffect;
            isActive = dropShadowEffect != null && dropShadowEffect.Color != Colors.Black;

            Storyboard storyBoard = (Storyboard)Resources["StoryBoardSetActive"];
            storyBoard.Begin();
        }

        public int Volume
        {
            set
            {
                this.soundVolume = value;
                if (this.soundVolume > 100)
                    this.soundVolume = 100;

                if (this.soundVolume < 0)
                    this.soundVolume = 0;

                this.VideoPlayer.VideoPlayerIntern.Volume = this.soundVolume;
            }

            get
            {
                return this.soundVolume;
            }
        }

        private void ViewMediaItemRgb(MediaItemBitmap mItem)
        {
            this.VideoPlayer.VideoPlayerIntern.MediaItemSource = null;
            this.mediaTimer.IsEnabled = false;

            if (!File.Exists(mItem.FullName))
                return;

            try
            {
                this.ImagePlayer.MediaItemSource = mItem;
                this.ImageElement.Visibility = System.Windows.Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Mouse.OverrideCursor = Cursors.None;
            }
        }

        public long TimeMilliseconds
        {
            get
            {
                return this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds;
            }
        }

        private void ViewMediaItemDirectShow(MediaItemVideo mItem)
        {
            try
            {
                this.mediaTimer.IsEnabled = true;
                this.ImagePlayer.MediaItemSource = null;
                this.VideoPlayer.SelectedPlayer = this.selectedPlayer;
                this.VideoPlayer.VideoPlayerIntern.Volume = this.soundVolume;
                this.VideoPlayer.VideoPlayerIntern.MediaItemSource = mItem;
                this.VideoPlayer.VideoPlayerIntern.PlayInterval = Tuple.Create<double, double>(mItem.DirectShowInfo.StartPosition, mItem.DirectShowInfo.StopPosition);

                if (!File.Exists(mItem.FullName))
                    return;

                if (mItem.DirectShowInfo.StartPosition > 0)
                {
                    if (this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
                        this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds = mItem.DirectShowInfo.StartPositionMilliseconds;
                }

                this.VideoElement.Visibility = System.Windows.Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Mouse.OverrideCursor = Cursors.None;
            }
        }

        private void WatchDirectShow()
        {
            if (this.Source is MediaItemVideo)
            {
                MediaItemVideo mItem = this.Source as MediaItemVideo;
                bool setPosition = false;
                if (mItem.DirectShowInfo.StartPosition > 0)
                {
                    if (this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds < mItem.DirectShowInfo.StartPositionMilliseconds)
                        setPosition = true;
                }

                if (mItem.DirectShowInfo.StopPosition > 0)
                {
                    if (this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds > mItem.DirectShowInfo.StopPositionMilliseconds)
                    {
                        if (!this.IsLoop)
                        {
                            this.VideoPlayer.VideoPlayerIntern.Pause();
                            if (this.EndReached != null)
                                this.EndReached.Invoke(this, EventArgs.Empty);
                        }
                        else
                            setPosition = true;
                    }
                }

                if (setPosition)
                    this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds = mItem.DirectShowInfo.StartPositionMilliseconds;
            }
        }

        public void Dispose()
        {
            this.mediaTimer.IsEnabled = false;
            this.ImagePlayer.Dispose();
            this.VideoPlayer.VideoPlayerIntern.Dispose();
        }

        Point lastMousePosition;
        private void MainMediaContainer_MouseMove(object sender, MouseEventArgs e)
        {
            Point nowPosition = e.GetPosition(this);

            double moveX = lastMousePosition.X - nowPosition.X;
            double moveY = lastMousePosition.Y - nowPosition.Y;

            if (Math.Abs(lastMousePosition.X - nowPosition.X) > 2
                || Math.Abs(lastMousePosition.Y - nowPosition.Y) > 2)
            {
                Mouse.OverrideCursor = null;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Mouse.OverrideCursor = Cursors.Hand;

                if (this.Source is MediaItemBitmap)
                {
                    this.ImagePlayer.TranslateX -= moveX;
                    this.ImagePlayer.TranslateY -= moveY;
                }
                else if (this.Source is MediaItemVideo)
                {
                    this.VideoPlayer.VideoPlayerIntern.TranslateX -= moveX;
                    this.VideoPlayer.VideoPlayerIntern.TranslateY -= moveY;
                }
            }

            lastMousePosition = e.GetPosition(this);
        }     

        public void ResetTransform()
        {
            if (this.Source is MediaItemBitmap)
            {
                this.ImagePlayer.ScaleFactor = 1;
                this.ImagePlayer.TranslateX = 0;
                this.ImagePlayer.TranslateY = 0;
            }
            else if (this.Source is MediaItemVideo)
            {
                this.VideoPlayer.VideoPlayerIntern.ScaleFactor = 1;
                this.VideoPlayer.VideoPlayerIntern.TranslateX = 0;
                this.VideoPlayer.VideoPlayerIntern.TranslateY = 0;
            }
        }

        private void MainMediaContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //bool keyCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) > 0;

            if (isActive)
            {
                if (e.Delta < 0)
                {
                    if (this.Source is MediaItemBitmap)
                    {
                        this.ImagePlayer.ScaleFactor = this.ImagePlayer.ScaleFactor > 4 ? this.ImagePlayer.ScaleFactor : this.ImagePlayer.ScaleFactor * 1.1;
                    }
                    else if (this.Source is MediaItemVideo)
                    {
                        this.VideoPlayer.VideoPlayerIntern.ScaleFactor = this.VideoPlayer.VideoPlayerIntern.ScaleFactor > 4 ? this.VideoPlayer.VideoPlayerIntern.ScaleFactor : this.VideoPlayer.VideoPlayerIntern.ScaleFactor * 1.1;
                    }
                }
                else
                {
                    if (this.Source is MediaItemBitmap)
                    {
                        this.ImagePlayer.ScaleFactor = this.ImagePlayer.ScaleFactor < .5 ? this.ImagePlayer.ScaleFactor : this.ImagePlayer.ScaleFactor * .9;
                    }
                    else if (this.Source is MediaItemVideo)
                    {
                        this.VideoPlayer.VideoPlayerIntern.ScaleFactor = this.VideoPlayer.VideoPlayerIntern.ScaleFactor < .5 ? this.VideoPlayer.VideoPlayerIntern.ScaleFactor : this.VideoPlayer.VideoPlayerIntern.ScaleFactor * .9;
                    }
                }

                e.Handled = true;
            }
        }
    }
}
