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
using System.Windows.Threading;
using MediaBrowserWPF.Viewer;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using MediaBrowser4;

namespace MediaBrowserWPF.UserControls.Video
{
    /// <summary>
    /// Interaktionslogik für VideoPlayer.xaml
    /// </summary>
    public partial class VideoPlayer : UserControl, IMediaControl
    {
        public event EventHandler EndReached;
        public event EventHandler PositionChanged;
        private IVideoControl videoPlayer;
        private MediaItemVideo visibleMediaItem;
        public event EventHandler VideoLoaded;
        DispatcherTimer windTimer;
        double windSeconds;
        double windForward;
        private bool usePreviewDb = false;
        public bool UsePreviewDb { get; set; }

        public enum Player { None, VideoLan, MediaElement, WpfMediaKit, nVlc, Multiplayer };

        private Player initalizedPlayer;
        public Player SelectedPlayer { get; set; }
        public bool IsVideoLoading { get; set; }

        public VideoPlayer()
        {
            InitializeComponent();

            this.windTimer = new DispatcherTimer();
            this.windTimer.IsEnabled = false;
            this.windTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            this.windTimer.Tick += new EventHandler(windTimer_Tick);
        }

        public ViewerState ViewerState { get; set; }

        public Tuple<double, double> PlayInterval
        {
            set
            {
                if (this.initalizedPlayer == Player.Multiplayer)
                {
                    ((Multiviewer)this.videoPlayer).PlayInterval = value;
                }
            }
        }

        public void SetVideoTrack(TrackInfo trackInfo)
        {
            this.videoPlayer.SetVideoTrack(trackInfo);
        }

        public List<TrackInfo> TrackInfo
        {
            get
            {
                if (this.videoPlayer == null)
                    return new List<TrackInfo>();
                else
                    return this.videoPlayer.TrackInfo;
            }
        }

        #region Crop
        private double cropTopRel;
        public double CropTopRel
        {
            get
            {
                return this.cropTopRel;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                if (value + this.cropBottomRel > 95)
                {
                    value = 95 - this.cropBottomRel;
                }
                else if (value < 0)
                    value = 0;

                this.cropTopRel = value;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropTop = this.VideoPlayerContainer.ActualHeight * value / 100;
                else
                    this.CropTop = this.VideoPlayerContainer.ActualWidth * value / 100;

            }
        }

        public double CropTop
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Top;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Right;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Left;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Bottom;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.VideoPlayerContainer.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Bottom);
            }
        }

        private double cropBottomRel;
        public double CropBottomRel
        {
            get
            {
                return this.cropBottomRel;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                if (value + this.cropTopRel > 95)
                {
                    value = 95 - this.cropTopRel;
                }
                else if (value < 0)
                    value = 0;

                this.cropBottomRel = value;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropBottom = this.VideoPlayerContainer.ActualHeight * value / 100;
                else
                    this.CropBottom = this.VideoPlayerContainer.ActualWidth * value / 100;
            }
        }

        public double CropBottom
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Bottom;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Left;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Right;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Top;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.VideoPlayerContainer.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Bottom);
            }
        }

        private double cropLeftRel;
        public double CropLeftRel
        {
            get
            {
                return this.cropLeftRel;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                if (value + this.cropRightRel > 95)
                {
                    value = 95 - this.cropRightRel;
                }
                else if (value < 0)
                    value = 0;

                this.cropLeftRel = value;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropLeft = this.VideoPlayerContainer.ActualWidth * value / 100;
                else
                    this.CropLeft = this.VideoPlayerContainer.ActualHeight * value / 100;
            }
        }

        public double CropLeft
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Left;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Top;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Bottom;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Right;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.VideoPlayerContainer.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Bottom);
            }
        }

        private double cropRightRel;
        public double CropRightRel
        {
            get
            {
                return this.cropRightRel;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                if (value + this.cropLeftRel > 95)
                {
                    value = 95 - this.cropLeftRel;
                }
                else if (value < 0)
                    value = 0;

                this.cropRightRel = value;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropRight = this.VideoPlayerContainer.ActualWidth * value / 100;
                else
                    this.CropRight = this.VideoPlayerContainer.ActualHeight * value / 100;
            }
        }

        public double CropRight
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Right;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Bottom;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Top;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.VideoPlayerContainer.Margin.Left;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.VideoPlayerContainer.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.VideoPlayerContainer.Margin.Bottom);
            }
        }

        private void SetCropSize()
        {
            if (this.visibleMediaItem == null || this.MediaRenderSize.Width == 0 || this.MediaRenderSize.Height == 0)
                return;

            double imageRelation = (this.MediaRenderSize.Width)
                / (this.MediaRenderSize.Height);

            double controlRelation = this.RenderSize.Width / this.RenderSize.Height;

            double newWidth, newHeight;

            if (controlRelation > imageRelation)
            {
                newWidth = this.RenderSize.Height * imageRelation;
                newHeight = this.RenderSize.Height;
            }
            else
            {
                newWidth = this.RenderSize.Width;
                newHeight = this.RenderSize.Width / imageRelation;
            }

            if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
            {
                this.CropGrid.Width = newWidth > 0 ? newWidth : 0;
                this.CropGrid.Height = newHeight > 0 ? newHeight : 0;
            }
            else
            {
                this.CropGrid.Height = newWidth > 0 ? newWidth : 0;
                this.CropGrid.Width = newHeight > 0 ? newHeight : 0;
            }
        }

        public void ResetCrop()
        {
            if (this.cropLeftRel < 0) this.cropLeftRel = 0;
            if (this.cropTopRel < 0) this.cropTopRel = 0;
            if (this.cropRightRel < 0) this.cropRightRel = 0;
            if (this.cropBottomRel < 0) this.cropBottomRel = 0;
        }
        #endregion

        #region Clip
        public RectangleGeometry ClipRectangle
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.Clip = value;

                if (value == null)
                {
                    this.clipLeftRel = 0;
                    this.clipBottomRel = 0;
                    this.clipRightRel = 0;
                    this.clipTopRel = 0;
                }
            }

            get
            {
                if (this.Clip == null)
                    this.Clip = new RectangleGeometry(this.MediaRenderSize);

                return (RectangleGeometry)this.Clip;
            }
        }

        private double clipTopRel;
        public double ClipTopRel
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.clipTopRel = value;

                if (this.clipLeftRel == 0 && value <= 0
                   && this.clipRightRel == 0 && this.clipBottomRel == 0)
                {
                    this.ClipRectangle = null;
                    return;
                }

                if (value + this.ClipBottomRel > 95)
                {
                    value = 95 - this.ClipBottomRel;
                    this.clipTopRel = value;
                }
                else if (value < 0)
                    value = 0;

                this.ClipTop = this.MediaRenderSize.Y + (this.MediaRenderSize.Height * value / 100);
            }

            get
            {
                return this.clipTopRel;
            }
        }

        public double ClipTop
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                if (value < 0)
                    value = 0;

                double z = this.ClipRectangle.Rect.Height + (this.ClipRectangle.Rect.Y - value);

                this.ClipRectangle.Rect
                    = new Rect(this.ClipRectangle.Rect.X, value,
                        this.ClipRectangle.Rect.Width,
                        z > 0 ? z : 0);
            }

            get
            {
                return this.ClipRectangle.Rect.Y;
            }
        }

        private double clipLeftRel;
        public double ClipLeftRel
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.clipLeftRel = value;

                if (value <= 0 && clipTopRel == 0
                   && this.clipRightRel == 0 && this.clipBottomRel == 0)
                {
                    this.ClipRectangle = null;
                    return;
                }

                if (value + this.ClipRightRel > 95)
                {
                    value = 95 - this.ClipRightRel;
                    this.clipLeftRel = value;
                }
                else if (value < 0)
                    value = 0;

                this.ClipLeft = this.MediaRenderSize.X + (this.MediaRenderSize.Width * value / 100);
            }

            get
            {
                return this.clipLeftRel;
            }
        }

        public double ClipLeft
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                if (value < 0)
                    value = 0;

                double z = this.ClipRectangle.Rect.Width + (this.ClipRectangle.Rect.X - value);

                this.ClipRectangle.Rect
                    = new Rect(value, this.ClipRectangle.Rect.Y,
                        z > 0 ? z : 0,
                        this.ClipRectangle.Rect.Height);
            }

            get
            {
                return this.ClipRectangle.Rect.X;
            }
        }

        private double clipRightRel;
        public double ClipRightRel
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.clipRightRel = value;

                if (this.clipLeftRel == 0 && clipTopRel == 0
                   && value <= 0 && this.clipBottomRel == 0)
                {
                    this.ClipRectangle = null;
                    return;
                }

                if (value + this.ClipLeftRel > 95)
                {
                    value = 95 - this.ClipLeftRel;
                    this.clipRightRel = value;
                }
                else if (value < 0)
                    value = 0;

                this.ClipRight = this.MediaRenderSize.X + (this.MediaRenderSize.Width * value / 100);
            }

            get
            {
                return this.clipRightRel;
            }
        }

        public double ClipRight
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                if (value < 0)
                    value = 0;

                double z = this.ActualWidth - this.ClipRectangle.Rect.X - value;

                this.ClipRectangle.Rect
                    = new Rect(this.ClipRectangle.Rect.X, this.ClipRectangle.Rect.Y,
                        z > 0 ? z : 0, this.ClipRectangle.Rect.Height);
            }

            get
            {
                return this.ActualWidth - this.ClipRectangle.Rect.X - this.ClipRectangle.Rect.Width;
            }
        }

        private double clipBottomRel;
        public double ClipBottomRel
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.clipBottomRel = value;

                if (this.clipLeftRel == 0 && this.clipTopRel == 0
                   && this.clipRightRel == 0 && value <= 0)
                {
                    this.ClipRectangle = null;
                    return;
                }

                if (value + this.ClipTopRel > 95)
                {
                    value = 95 - this.ClipTopRel;
                    this.clipBottomRel = value;
                }
                else if (value < 0)
                    value = 0;

                this.ClipBottom = this.MediaRenderSize.Y + (this.MediaRenderSize.Height * value / 100);
            }

            get
            {
                return this.clipBottomRel;
            }
        }

        public double ClipBottom
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                double z = this.ActualHeight - this.ClipRectangle.Rect.Y - value;

                this.ClipRectangle.Rect
                    = new Rect(this.ClipRectangle.Rect.X, this.ClipRectangle.Rect.Y,
                        this.ClipRectangle.Rect.Width, z > 0 ? z : 0);
            }

            get
            {
                return this.ActualHeight - this.ClipRectangle.Rect.Y - this.ClipRectangle.Rect.Height;
            }
        }

        public void ResetClip()
        {
            if (this.clipLeftRel < 0) this.clipLeftRel = 0;
            if (this.clipTopRel < 0) this.clipTopRel = 0;
            if (this.clipRightRel < 0) this.clipRightRel = 0;
            if (this.clipBottomRel < 0) this.clipBottomRel = 0;
        }
        #endregion

        #region Rotate
        private MediaItem.MediaOrientation rotate90;
        public MediaItem.MediaOrientation Rotate90(int rotateCounter)
        {
            if (this.usePreviewDb || this.visibleMediaItem == null)
                return MediaItem.MediaOrientation.BOTTOMisBOTTOM;

            if (rotateCounter < 0)
            {
                rotateCounter = 4 - System.Math.Abs(rotateCounter) % 4;
            }

            this.rotate90 += rotateCounter;

            this.rotate90 = (MediaItem.MediaOrientation)((int)this.rotate90 % 4);

            if ((int)this.rotate90 >= 4)
                this.rotate90 = 0;

            this.Orientation = this.rotate90;

            return this.rotate90;
        }

        public MediaItem.MediaOrientation Orientation
        {
            get
            {
                switch ((int)this.RotateTransformLayout.Angle)
                {
                    case 90:
                        return MediaItem.MediaOrientation.RIGHTisBOTTOM;
                    case 180:
                        return MediaItem.MediaOrientation.TOPisBOTTOM;
                    case 270:
                        return MediaItem.MediaOrientation.LEFTisBOTTOM;
                    default:
                        return MediaItem.MediaOrientation.BOTTOMisBOTTOM;
                }
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                int cnt = this.Orientation - value;

                if (cnt > 0)
                {
                    while (cnt > 0)
                    {

                        double x = this.clipLeftRel;
                        this.clipLeftRel = this.clipTopRel;
                        this.clipTopRel = this.clipRightRel;
                        this.clipRightRel = this.clipBottomRel;
                        this.clipBottomRel = x;

                        x = this.cropLeftRel;
                        this.cropLeftRel = this.cropTopRel;
                        this.cropTopRel = this.cropRightRel;
                        this.cropRightRel = this.cropBottomRel;
                        this.cropBottomRel = x;

                        cnt--;
                    }
                }
                else if (cnt < 0)
                {
                    while (cnt < 0)
                    {
                        double y = this.cropLeftRel;
                        this.cropLeftRel = this.cropBottomRel;
                        this.cropBottomRel = this.cropRightRel;
                        this.cropRightRel = this.cropTopRel;
                        this.cropTopRel = y;

                        y = this.clipLeftRel;
                        this.clipLeftRel = this.clipBottomRel;
                        this.clipBottomRel = this.clipRightRel;
                        this.clipRightRel = this.clipTopRel;
                        this.clipTopRel = y;

                        cnt++;
                    }
                }

                this.rotate90 = value;
                switch (this.rotate90)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        this.RotateTransformLayout.Angle = 0;
                        break;

                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        this.RotateTransformLayout.Angle = 90;
                        break;

                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        this.RotateTransformLayout.Angle = 180;
                        break;

                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        this.RotateTransformLayout.Angle = 270;
                        break;
                }
            }
        }

        public double RotateAngle
        {
            get
            {
                return this.RotateTransformRender.Angle;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                value = value % 360;
                if (this.ViewerState == Viewer.ViewerState.CropRotate)
                {
                    if (value > 20)
                        value = 20;
                    else if (value < -20)
                        value = -20;

                    this.SetCropRotate(value, this.VideoPlayerContainer.ActualWidth, this.VideoPlayerContainer.ActualHeight);
                }
                else
                {
                    if (value > 180)
                        value = -180;
                    else if (value < -180)
                        value = 180;
                }

                this.RotateTransformRender.Angle = value;
            }
        }

        private void SetCropRotate(double angle, double width, double height)
        {
            if (this.usePreviewDb)
                return;

            double offset = 80 * Math.Abs(System.Math.Sin(angle * Math.PI / 180));

            this.cropLeftRel = offset;
            this.cropTopRel = offset;
            this.cropRightRel = offset;
            this.cropBottomRel = offset;
        }
        #endregion

        #region Scale and Translate
        private double scaleXDistortFactor;
        public double ScaleXDistortFactor
        {
            get
            {
                return scaleXDistortFactor;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                if (value < 0.1)
                    value = .1;
                else if (value > 1000)
                    value = 1000;

                this.scaleXDistortFactor = value;
                this.ScaleTransformRender.ScaleX = this.ScaleTransformRender.ScaleY * this.scaleXDistortFactor;
            }
        }

        public bool FlipHorizontal
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.ScaleTransformRender.ScaleX = Math.Abs(this.ScaleTransformRender.ScaleX) * (value ? -1 : 1);
            }

            get
            {
                return this.ScaleTransformRender.ScaleX < 0;
            }
        }

        public bool FlipVertical
        {
            set
            {
                if (this.usePreviewDb)
                    return;

                this.ScaleTransformRender.ScaleY = Math.Abs(this.ScaleTransformRender.ScaleY) * (value ? -1 : 1);
            }

            get
            {
                return this.ScaleTransformRender.ScaleY < 0;
            }
        }

        public double ScaleFactor
        {
            get
            {
                return Math.Abs(this.ScaleTransformRender.ScaleY);
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                value = Math.Abs(value);

                if (this.VideoPlayerContainer.ActualHeight * value < 100)
                {
                    this.ScaleTransformRender.ScaleY = 100 / this.VideoPlayerContainer.ActualHeight;
                    this.ScaleTransformRender.ScaleX = 100 / this.VideoPlayerContainer.ActualHeight;
                }
                else if (this.VideoPlayerContainer.ActualWidth * value < 100)
                {
                    this.ScaleTransformRender.ScaleY = 100 / this.VideoPlayerContainer.ActualWidth;
                    this.ScaleTransformRender.ScaleX = 100 / this.VideoPlayerContainer.ActualWidth;
                }
                else
                {
                    this.ScaleTransformRender.ScaleX = value * this.scaleXDistortFactor;
                    this.ScaleTransformRender.ScaleY = value;
                }

                this.VideoPlayerContainer_SizeChanged(null, null);
            }
        }

        public double TranslateX
        {
            get
            {
                return this.TranslateTransformRender.X;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                this.TranslateTransformRender.X = value;
            }
        }

        public double TranslateY
        {
            get
            {
                return this.TranslateTransformRender.Y;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                this.TranslateTransformRender.Y = value;
            }
        }
        #endregion

        public Rect MediaRenderSize
        {
            get
            {
                double rel;
                double relThis = this.ActualWidth / this.ActualHeight;
                double width, height;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                {
                    rel = (double)this.visibleMediaItem.Width / (double)this.visibleMediaItem.Height;

                    if (rel > relThis)
                    {
                        width = this.VideoPlayerContainer.RenderSize.Width;
                        height = this.VideoPlayerContainer.RenderSize.Width / rel;
                    }
                    else
                    {
                        width = this.VideoPlayerContainer.RenderSize.Height * rel;
                        height = this.VideoPlayerContainer.RenderSize.Height;
                    }
                }
                else
                {
                    rel = (double)this.visibleMediaItem.Height / (double)this.visibleMediaItem.Width;

                    if (rel > relThis)
                    {
                        width = this.VideoPlayerContainer.RenderSize.Height;
                        height = this.VideoPlayerContainer.RenderSize.Height / rel;
                    }
                    else
                    {
                        width = this.VideoPlayerContainer.RenderSize.Width * rel;
                        height = this.VideoPlayerContainer.RenderSize.Width;
                    }
                }

                return new Rect(new Point((this.RenderSize.Width - width + this.CropRight + this.CropLeft) / 2, (this.RenderSize.Height - height + this.CropTop + this.CropBottom) / 2),
                    new Size(width - this.CropRight - this.CropLeft, height - this.CropTop - this.CropBottom));
            }
        }

        private bool videoLoaded;
        public MediaItem MediaItemSource
        {
            set
            {
                this.IsVideoLoading = true;
                this.usePreviewDb = this.UsePreviewDb;
                this.VideoPlayerContainer.Visibility = System.Windows.Visibility.Hidden;
                this.videoLoaded = false;

                this.visibleMediaItem = value as MediaItemVideo;

                if (this.visibleMediaItem != null)
                {
                    InitializePlayer();
                }

                this.Orientation = MediaItem.MediaOrientation.BOTTOMisBOTTOM;
                this.Clip = null;
                this.clipLeftRel = 0;
                this.clipBottomRel = 0;
                this.clipRightRel = 0;
                this.clipTopRel = 0;
                this.cropLeftRel = 0;
                this.cropTopRel = 0;
                this.cropRightRel = 0;
                this.cropBottomRel = 0;
                this.RotateTransformRender.Angle = 0;
                this.RotateTransformLayout.Angle = 0;
                this.TranslateTransformRender.X = 0;
                this.TranslateTransformRender.Y = 0;
                this.scaleXDistortFactor = 1.0;
                this.ScaleTransformRender.ScaleX = 1.0;
                this.ScaleTransformRender.ScaleY = 1.0;
                this.VideoPlayerContainer.Margin = new Thickness(0, 0, 0, 0);

                if (this.visibleMediaItem == null)
                {
                    if (this.videoPlayer != null)
                        this.videoPlayer.Source = null;

                    this.IsVideoLoading = false;
                    return;
                }

                if (this.videoPlayer == null
                    || !File.Exists(this.visibleMediaItem.FullName))
                {
                    this.usePreviewDb = MediaBrowserContext.IsImageInPreviewDB(this.visibleMediaItem.VariationId);

                    if (!this.usePreviewDb)
                    {
                        this.visibleMediaItem = null;

                        if (this.videoPlayer != null)
                        {
                            this.videoPlayer.Source = null;
                        }

                        this.IsVideoLoading = false;
                        return;
                    }
                }

                if (this.usePreviewDb)
                {
                    PreviewObject previewObject = MediaBrowser4.MediaBrowserContext.GetImagePreviewDB(this.visibleMediaItem.VariationId);

                    if (previewObject.Extension == "avi")
                    {
                        if (File.Exists(previewObject.TempPath))
                            this.videoPlayer.Source = previewObject.TempPath;
                        else
                        {
                            this.videoPlayer.Source = null;
                            this.IsVideoLoading = false;
                        }
                    }
                    else
                    {
                        this.videoPlayer.Source = null;
                        this.IsVideoLoading = false;
                    }
                }
                else
                {
                    if (MediaBrowserContext.MediaItemsCache != null && this.visibleMediaItem.ImageCachePath == null)
                    {
                        this.visibleMediaItem.ImageCachePath = System.IO.Path.GetTempFileName() + System.IO.Path.GetExtension(this.visibleMediaItem.FileObject.FullName);
                        System.IO.File.Copy(this.visibleMediaItem.FileObject.FullName, this.visibleMediaItem.ImageCachePath);
                        MediaBrowserContext.MediaItemsCache.Add(this.visibleMediaItem.ImageCachePath);
                    }
         
                    this.videoPlayer.Source = this.visibleMediaItem.ImageCachePath != null ? this.visibleMediaItem.ImageCachePath : this.visibleMediaItem.FileObject.FullName;
                    this.ResetDefaults();
                }
            }

            get
            {
                return this.visibleMediaItem;
            }
        }
               
        public void ForceRelation(double relation)
        {
            MediaControlHelper.ForceRelation(relation, this);
        }

        public void ResetDefaults()
        {
            this.visibleMediaItem.ReloadDirectShowInfo();
            this.Orientation = this.visibleMediaItem.Orientation;

            Layer layerRot = this.visibleMediaItem.FindLayer("ROT");
            Layer layerRotCrop = this.visibleMediaItem.FindLayer("ROTC");
            Layer layerCrop = this.visibleMediaItem.FindLayer("CROP");
            Layer layerClip = this.visibleMediaItem.FindLayer("CLIP");
            Layer layerZoom = this.visibleMediaItem.FindLayer("ZOOM");
            Layer layerFlip = this.visibleMediaItem.FindLayer("FLIP");


            this.cropLeftRel = 0;
            this.cropTopRel = 0;
            this.cropRightRel = 0;
            this.cropBottomRel = 0;

            if (layerRotCrop != null)
            {
                //Depreceated (MediaBrowser4 Legacy Code)                
                this.RotateAngle = Convert.ToDouble(layerRotCrop.Action, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);

                if (this.visibleMediaItem.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.visibleMediaItem.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                {
                    this.SetCropRotate(this.RotateAngle, this.visibleMediaItem.Width, this.visibleMediaItem.Height);
                }
                else
                {
                    this.SetCropRotate(this.RotateAngle, this.visibleMediaItem.Height, this.visibleMediaItem.Width);
                }
            }
            else if (layerRot != null)
            {
                this.RotateAngle = Convert.ToDouble(layerRot.Action, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            }

            if (layerCrop != null)
            {
                this.cropLeftRel += Convert.ToDouble(layerCrop.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                this.cropTopRel += Convert.ToDouble(layerCrop.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                this.cropRightRel += Convert.ToDouble(layerCrop.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat);
                this.cropBottomRel += Convert.ToDouble(layerCrop.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat);
            }


            if (layerClip != null)
            {
                this.clipLeftRel = Convert.ToDouble(layerClip.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                this.clipTopRel = Convert.ToDouble(layerClip.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                this.clipRightRel = Convert.ToDouble(layerClip.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat);
                this.clipBottomRel = Convert.ToDouble(layerClip.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat);
            }

            if (layerZoom != null)
            {
                this.TranslateX = Convert.ToDouble(layerZoom.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                this.TranslateY = Convert.ToDouble(layerZoom.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                this.ScaleFactor = Convert.ToDouble(layerZoom.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat);
                this.ScaleXDistortFactor = Convert.ToDouble(layerZoom.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat);
            }

            if (layerFlip != null)
            {
                this.FlipHorizontal = Convert.ToInt32(layerZoom.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat) > 0;
                this.FlipVertical = Convert.ToInt32(layerZoom.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat) > 0;
            }

            // this.updateClipAndCrop = true;
        }

        public System.Drawing.Bitmap TakeSnapshot()
        {
            System.Drawing.Bitmap bmp = (this.initalizedPlayer == Player.nVlc ? this.videoPlayer.TakeSnapshot() : null);

            if (bmp == null)
            {
                BitmapSource renderTargetBitmap = TakeRenderTargetBitmap();

                BmpBitmapEncoder png = new BmpBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                Stream ms = new MemoryStream();
                png.Save(ms);
                ms.Position = 0;
                bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromStream(ms);
            }

            return bmp;
        }

        public BitmapSource TakeRenderTargetBitmap()
        {
            Size dpi = new Size(96, 96);
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Floor(this.ActualWidth), (int)Math.Floor(this.ActualHeight), dpi.Width, dpi.Height, PixelFormats.Pbgra32);

            renderTargetBitmap.Render(this);

            int x = (int)Math.Round(this.MediaRenderSize.X);
            int y = (int)Math.Round(this.MediaRenderSize.Y);
            int width = (int)Math.Round(this.MediaRenderSize.Width);
            int height = (int)Math.Round(this.MediaRenderSize.Height);

            if (x == 1) x = 0;
            if (y == 1) y = 0;

            if (x + width > renderTargetBitmap.Width) width = (int)(renderTargetBitmap.Width - x);
            if (y + height > renderTargetBitmap.Height) height = (int)(renderTargetBitmap.Height - y);

            CroppedBitmap crop = new CroppedBitmap(renderTargetBitmap, new Int32Rect(x, y, width, height));

            return crop;
        }

        public long TimeMilliseconds
        {
            get
            {
                return this.videoPlayer == null ? 0 : this.videoPlayer.TimeMilliseconds;
            }

            set
            {
                if (this.videoPlayer != null)
                    this.videoPlayer.TimeMilliseconds = value;

                if (this.PositionChanged != null)
                    this.PositionChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public double TimeSeconds
        {
            get
            {
                return this.videoPlayer != null ? (double)(this.videoPlayer.TimeMilliseconds / 1000.0) : 0;

            }

            set
            {
                this.videoPlayer.TimeMilliseconds = (long)(value * 1000.0);
            }
        }

        public float Position
        {
            get
            {
                if (this.videoPlayer == null)
                    return 0;
                else
                    return this.videoPlayer.Position;
            }

            set
            {
                if (this.usePreviewDb)
                    return;

                this.videoPlayer.Position = value;
                if (this.PositionChanged != null)
                    this.PositionChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public void NextFrame()
        {
            this.videoPlayer.NextFrame();
            if (this.PositionChanged != null)
                this.PositionChanged.Invoke(this, EventArgs.Empty);
        }

        private bool isLoop;
        public bool IsLoop
        {
            get
            {
                return this.isLoop;
            }
            set
            {
                this.isLoop = value;
                if (this.videoPlayer != null)
                    this.videoPlayer.IsLoop = value;
            }
        }

        public bool IsPlaying
        {
            get { return this.videoPlayer.IsPlaying; }
        }

        public void Pause()
        {
            if (this.videoPlayer != null)
                this.videoPlayer.Pause();
        }

        public void KeyTabDown()
        {
            if (this.videoPlayer != null)
                this.videoPlayer.KeyTabDown();
        }

        public double Speedratio
        {
            get
            {
                return this.videoPlayer.Speedratio;
            }

            set
            {
                if ((this.videoPlayer.Speedratio > 1 && value < 1) || (this.videoPlayer.Speedratio < 1 && value > 1))
                    value = 1;

                this.videoPlayer.Speedratio = (value < 7 ? (value  > .1 ? value : .1) : 7);
            }
        }

        public void Play()
        {
            if (this.videoPlayer != null)
                this.videoPlayer.Play();
        }

        public void Stop()
        {
            if (this.videoPlayer != null)
                this.videoPlayer.Stop();
        }

        int volume = 100;
        public int Volume
        {
            set
            {
                if (this.videoPlayer == null)
                    return;

                this.volume = value;
                if (this.volume > 100)
                    this.volume = 100;

                if (this.volume < 0)
                    this.volume = 0;

                this.videoPlayer.Volume = this.volume;
            }

            get
            {
                return this.volume;
            }
        }

        public void JumpToVideoPosition(double rel)
        {
            if (this.usePreviewDb || this.visibleMediaItem == null)
                return;

            long setPosition = 0;

            if (this.visibleMediaItem.DirectShowInfo.StartPosition == 0 && this.visibleMediaItem.DirectShowInfo.StopPositionTicks == 0)
            {
                setPosition = (long)(this.visibleMediaItem.DurationTicks * rel);
            }
            else
            {
                long interval = this.visibleMediaItem.DurationTicks - this.visibleMediaItem.DirectShowInfo.StartPositionTicks;

                if (this.visibleMediaItem.DirectShowInfo.StartPosition < this.visibleMediaItem.DirectShowInfo.StopPosition)
                {
                    interval = this.visibleMediaItem.DirectShowInfo.StopPositionTicks - this.visibleMediaItem.DirectShowInfo.StartPositionTicks;
                }

                setPosition = this.visibleMediaItem.DirectShowInfo.StartPositionTicks + (long)(interval * rel);
            }

            this.TimeMilliseconds = setPosition / 10000;
        }

        public void JumpToStartPosition()
        {
            if (this.visibleMediaItem == null)
                return;

            this.TimeMilliseconds = this.visibleMediaItem.DirectShowInfo.StartPositionMilliseconds;
        }

        public void JumpToStopPosition()
        {
            if (this.visibleMediaItem == null)
                return;

            if (this.visibleMediaItem.DirectShowInfo.StopPositionTicks - 10000000 * 5 > 0 || (this.visibleMediaItem.DirectShowInfo.StopPosition == 0 && this.visibleMediaItem.Duration > 8))
            {
                if (this.visibleMediaItem.DirectShowInfo.StopPosition == 0)
                    this.TimeMilliseconds = (long)((this.visibleMediaItem.Duration - 5) * 1000);
                else
                    this.TimeMilliseconds = this.visibleMediaItem.DirectShowInfo.StopPositionMilliseconds - 1000 * 5;
            }
            else
            {
                return;
            }
        }

        public void Stopp2Start()
        {
            if (this.visibleMediaItem != null && this.visibleMediaItem.DirectShowInfo.StopPositionMilliseconds > 0)
            {
                long tmp = this.visibleMediaItem.DirectShowInfo.StopPositionMilliseconds;
                this.visibleMediaItem.DirectShowInfo.StopPositionMilliseconds = 0;
                this.visibleMediaItem.DirectShowInfo.StartPositionMilliseconds = tmp;
                this.PlayInterval = Tuple.Create<double, double>(this.visibleMediaItem.DirectShowInfo.StartPosition, this.visibleMediaItem.DirectShowInfo.StopPosition);
            }
        }

        public void SetStartPosition()
        {
            if (!this.usePreviewDb && this.visibleMediaItem != null)
            {
                this.visibleMediaItem.DirectShowInfo.StartPositionMilliseconds = this.TimeMilliseconds;
                this.PlayInterval = Tuple.Create<double, double>(this.visibleMediaItem.DirectShowInfo.StartPosition, this.visibleMediaItem.DirectShowInfo.StopPosition);
            }
        }

        public void SetStopPosition()
        {
            if (!this.usePreviewDb && this.visibleMediaItem != null)
            {
                this.visibleMediaItem.DirectShowInfo.StopPositionMilliseconds = this.TimeMilliseconds;
                this.PlayInterval = Tuple.Create<double, double>(this.visibleMediaItem.DirectShowInfo.StartPosition, this.visibleMediaItem.DirectShowInfo.StopPosition);

                if (this.initalizedPlayer == Player.Multiplayer)
                    this.videoPlayer.Reset();

            }
        }

        public void RemoveStartPosition()
        {
            if (this.usePreviewDb || this.visibleMediaItem == null)
                return;

            this.visibleMediaItem.DirectShowInfo.StartPosition = 0;
            this.PlayInterval = Tuple.Create<double, double>(this.visibleMediaItem.DirectShowInfo.StartPosition, this.visibleMediaItem.DirectShowInfo.StopPosition);
        }

        public void RemoveStopPosition()
        {
            if (this.usePreviewDb || this.visibleMediaItem == null)
                return;

            this.visibleMediaItem.DirectShowInfo.StopPosition = 0;
            this.PlayInterval = Tuple.Create<double, double>(this.visibleMediaItem.DirectShowInfo.StartPosition, this.visibleMediaItem.DirectShowInfo.StopPosition);
        }

        private void InitializePlayer()
        {
            if (this.initalizedPlayer == this.SelectedPlayer)
                return;

            this.initalizedPlayer = this.SelectedPlayer;

            if (this.VideoPlayerContainer.Children.Count > 0)
            {
                ((IVideoControl)this.VideoPlayerContainer.Children[0]).Dispose();
                this.VideoPlayerContainer.Children.Clear();
            }

            switch (this.SelectedPlayer)
            {
                case Player.nVlc:
                    //throw new NotImplementedException("nVlc entfernt");
                    //this.VideoPlayerContainer.Children.Add(new nVlc());
                    //break;

                case Player.MediaElement:
                    this.VideoPlayerContainer.Children.Add(new WpfMediaElement());
                    break;

                //case Player.VideoLan:
                //    throw new NotImplementedException("VideoLan entfernt");
                //    this.VideoPlayerContainer.Children.Add(new VideoLanDotNet());
                //    break;

                case Player.WpfMediaKit:
                    this.VideoPlayerContainer.Children.Add(new WpfMediaKit());
                    break;

                case Player.Multiplayer:
                    this.VideoPlayerContainer.Children.Add(new Multiviewer());
                    break;

                default:
                    throw new Exception("Player nicht gefunden: " + this.SelectedPlayer);

            }

            this.videoPlayer = (IVideoControl)this.VideoPlayerContainer.Children[0];
            this.videoPlayer.IsLoop = this.IsLoop;
            this.videoPlayer.Volume = this.volume;
            this.videoPlayer.PositionChanged += new EventHandler(videoPlayer_PositionChanged);
            this.videoPlayer.EndReached += new EventHandler(videoPlayer_EndReached);
        }

        void videoPlayer_PositionChanged(object sender, EventArgs e)
        {
            if (!this.videoLoaded && this.VideoLoaded != null)
            {
                this.videoLoaded = true;
                this.VideoLoaded.Invoke(this, EventArgs.Empty);

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    this.VideoPlayerContainer.Visibility = System.Windows.Visibility.Visible;
                }));
            }

            if (this.PositionChanged != null)
                this.PositionChanged.Invoke(this, EventArgs.Empty);

        }

        Stopwatch oneEndEvent = new Stopwatch();
        void videoPlayer_EndReached(object sender, EventArgs e)
        {
            if (this.EndReached != null)
            {
                if (this.oneEndEvent.IsRunning)
                {
                    if (this.oneEndEvent.ElapsedMilliseconds > 500)
                        this.oneEndEvent.Reset();
                    else
                        return;
                }

                if (!this.oneEndEvent.IsRunning)
                {
                    this.oneEndEvent.Start();
                    this.EndReached.Invoke(this, EventArgs.Empty);
                }
            }
        }

        void windTimer_Tick(object sender, EventArgs e)
        {
            if (this.windForward > 5 && System.Math.Abs(this.windSeconds) >= 30)
            {
                this.windForward = 30;
            }

            windSeconds += windForward;
        }

        public double WindInit(bool forward, double step)
        {
            if (this.visibleMediaItem is MediaItemVideo)
            {
                if (!this.windTimer.IsEnabled && this.visibleMediaItem != null)
                {
                    this.windForward = forward ? step : -step;
                    this.windSeconds = this.windForward;
                    this.windTimer.IsEnabled = true;
                }

                bool end = false;
                if (this.TimeSeconds + this.windSeconds >= this.visibleMediaItem.Duration - 1)
                {
                    this.windSeconds = this.visibleMediaItem.Duration - this.TimeSeconds - 1;
                    end = true;
                }
                else if ((this.IsPlaying && this.TimeSeconds - 1.5 + this.windSeconds <= 0) || this.TimeSeconds + this.windSeconds <= 0)
                {
                    this.windSeconds = -this.TimeSeconds;
                    end = true;
                }

                if (end)
                    return 0;
                else
                    return this.windSeconds;
            }
            else
            {
                return 0;
            }
        }

        public void WindReset()
        {
            if (this.visibleMediaItem == null)
                return;

            this.windTimer.IsEnabled = false;

            if (this.windSeconds != 0)
            {
                if (this.TimeSeconds + this.windSeconds >= this.visibleMediaItem.Duration)
                {
                    this.TimeSeconds = this.visibleMediaItem.Duration - 1;
                }
                else
                {
                    this.TimeMilliseconds = this.TimeMilliseconds + (long)(1000 * this.windSeconds) < 0 ? 0 : this.TimeMilliseconds + (long)(1000 * this.windSeconds);
                }
            }
        }

        private void VideoPlayerContainer_LayoutUpdated(object sender, EventArgs e)
        {
            this.UpdateClipAndCrop();
            this.SetCropSize();
        }

        private void UpdateClipAndCrop()
        {
            if (!this.usePreviewDb && this.visibleMediaItem != null)
            {
                this.ClipLeftRel = this.clipLeftRel;
                this.ClipTopRel = this.clipTopRel;
                this.ClipBottomRel = this.clipBottomRel;
                this.ClipRightRel = this.clipRightRel;

                this.CropLeftRel = this.cropLeftRel;
                this.CropTopRel = this.cropTopRel;
                this.CropBottomRel = this.cropBottomRel;
                this.CropRightRel = this.cropRightRel;
            }
        }

        public void Dispose()
        {
            if (this.videoPlayer != null)
                this.videoPlayer.Dispose();
        }

        public double ScaleOriginalFactor { get; set; }

        private void VideoPlayerContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.visibleMediaItem != null)
                this.ScaleOriginalFactor = (Math.Max(this.VideoPlayerContainer.RenderSize.Width, this.VideoPlayerContainer.RenderSize.Height) * this.ScaleFactor)
                     / Math.Max(this.visibleMediaItem.Width, this.visibleMediaItem.Height);
        }
    }
}
