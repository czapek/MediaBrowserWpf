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
using MediaBrowserWPF.Viewer;
using System.Globalization;
using System.IO;
using MediaBrowserWPF.UserControls.Levels;
using System.Threading.Tasks;
using System.Windows.Threading;
using MediaBrowser4;
using WpfAnimatedGif;
using System.Windows.Media.Animation;
using System.Diagnostics;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.UserControls.RgbImage
{
    /// <summary>
    /// Interaktionslogik für ImageControl2.xaml
    /// </summary>
    public partial class ImageControl : UserControl, IMediaControl, IDisposable
    {
        public event EventHandler ImageLoaded;
        public static BitmapScalingMode DefaultBitmapScalingMode = System.Windows.Media.BitmapScalingMode.LowQuality;
        DispatcherTimer mediaTimer;
        System.Diagnostics.Stopwatch playTime = new System.Diagnostics.Stopwatch();

        public void SetBitmapScalingMode(BitmapScalingMode bitmapScalingMode)
        {
            RenderOptions.SetBitmapScalingMode(this.MainImage, bitmapScalingMode);
        }

        private MediaItemBitmap visibleMediaItem;
        private Dictionary<string, CacheBitmapImage> imageCache = new Dictionary<string, CacheBitmapImage>();
        public HistoRemap HistoRemapRed;
        public HistoRemap HistoRemapGreen;
        public HistoRemap HistoRemapBlue;
        public bool IsAnimatedGif { get; set; }
        public bool NoCache { get; set; }
        private bool usePreviewDb = false;
        public bool UsePreviewDb { get; set; }

        public ImageControl()
        {
            InitializeComponent();
            Dispatcher.CurrentDispatcher.Hooks.DispatcherInactive += new EventHandler(Hooks_DispatcherInactive);
            RenderOptions.SetBitmapScalingMode(this.MainImage, DefaultBitmapScalingMode);

            this.mediaTimer = new DispatcherTimer();
            this.mediaTimer.IsEnabled = false;
            this.mediaTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            this.mediaTimer.Tick += new EventHandler(mediaTimer_Tick);
        }

        void mediaTimer_Tick(object sender, EventArgs e)
        {
            if (this.ShowPlayTime)
            {
                this.InfoTextBlockTime.Text = String.Format("{0}s", this.playTime.ElapsedMilliseconds / 1000);
            }
        }

        public bool ShowPlayTime
        {
            set
            {
                if (value)
                {
                    this.mediaTimer.Start();
                    this.InfoTextBlockTime.Visibility = System.Windows.Visibility.Visible;
                    this.InfoTextBlockTimeBlur.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    this.mediaTimer.Stop();
                    this.InfoTextBlockTime.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoTextBlockTimeBlur.Visibility = System.Windows.Visibility.Collapsed;
                }
            }

            get
            {
                return this.InfoTextBlockTime.Visibility == System.Windows.Visibility.Visible;
            }
        }

        public ViewerState ViewerState { get; set; }

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

                this.cropTopRel = value;
                if (value + this.cropBottomRel > 95)
                {
                    value = 95 - this.cropBottomRel;
                    this.cropTopRel = value;
                }
                else if (value < 0)
                    value = 0;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                   || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropTop = this.MainImage.ActualHeight * value / 100;
                else
                    this.CropTop = this.MainImage.ActualWidth * value / 100;
            }
        }

        public double CropTop
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.MainImage.Margin.Top;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.MainImage.Margin.Right;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.MainImage.Margin.Left;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.MainImage.Margin.Bottom;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.MainImage.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.MainImage.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.MainImage.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.MainImage.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.MainImage.Margin.Bottom);
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

                this.cropBottomRel = value;
                if (value + this.cropTopRel > 95)
                {
                    value = 95 - this.cropTopRel;
                    this.cropBottomRel = value;
                }
                else if (value < 0)
                    value = 0;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                   || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropBottom = this.MainImage.ActualHeight * value / 100;
                else
                    this.CropBottom = this.MainImage.ActualWidth * value / 100;
            }
        }

        public double CropBottom
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.MainImage.Margin.Bottom;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.MainImage.Margin.Left;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.MainImage.Margin.Right;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.MainImage.Margin.Top;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.MainImage.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.MainImage.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.MainImage.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.MainImage.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.MainImage.Margin.Bottom);
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

                this.cropLeftRel = value;
                if (value + this.cropRightRel > 95)
                {
                    value = 95 - this.cropRightRel;
                    this.cropLeftRel = value;
                }
                else if (value < 0)
                    value = 0;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropLeft = this.MainImage.ActualWidth * value / 100;
                else
                    this.CropLeft = this.MainImage.ActualHeight * value / 100;
            }
        }

        public double CropLeft
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.MainImage.Margin.Left;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.MainImage.Margin.Top;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.MainImage.Margin.Bottom;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.MainImage.Margin.Right;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.MainImage.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.MainImage.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.MainImage.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.MainImage.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.MainImage.Margin.Bottom);
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

                this.cropRightRel = value;
                if (value + this.cropLeftRel > 95)
                {
                    value = 95 - this.cropLeftRel;
                    this.cropRightRel = value;
                }
                else if (value < 0)
                    value = 0;

                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    this.CropRight = this.MainImage.ActualWidth * value / 100;
                else
                    this.CropRight = this.MainImage.ActualHeight * value / 100;
            }
        }

        public double CropRight
        {
            get
            {
                switch (this.Orientation)
                {
                    case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                        return -this.MainImage.Margin.Right;
                    case MediaItem.MediaOrientation.LEFTisBOTTOM:
                        return -this.MainImage.Margin.Bottom;
                    case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                        return -this.MainImage.Margin.Top;
                    case MediaItem.MediaOrientation.TOPisBOTTOM:
                        return -this.MainImage.Margin.Left;
                }

                throw new Exception("nicht möglich");
            }
            set
            {
                if (this.usePreviewDb)
                    return;

                this.MainImage.Margin = new Thickness(
                    this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM ? -(value) : this.MainImage.Margin.Left,
                    this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM ? -(value) : this.MainImage.Margin.Top,
                    this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM ? -(value) : this.MainImage.Margin.Right,
                    this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM ? -(value) : this.MainImage.Margin.Bottom);
            }
        }

        private void SetCropSize()
        {
            if (this.visibleMediaItem == null || this.MediaRenderSize.Width == 0 || this.MediaRenderSize.Height == 0)
                return;

            //MediaBrowserWPF.Utilities.ConsoleManager.Show();
            //Console.WriteLine("MediaRenderSize {0:n0}x{1:n0} x:{2:n0} y:{3:n0} / this.Size {4:n0}x{5:n0}  / crop:{6:n0}, {7:n0}, {8:n0}, {9:n0}", 
            //    this.MediaRenderSize.Width, this.MediaRenderSize.Height,
            //    this.MediaRenderSize.X, this.MediaRenderSize.Y,
            //    this.RenderSize.Width, this.RenderSize.Height,
            //    this.CropLeft, this.CropTop, this.CropRight, this.CropBottom);

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
            if (this.usePreviewDb)
                return;

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
            if (this.usePreviewDb)
                return;

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

                    this.SetCropRotate(value, this.MainImage.ActualWidth, this.MainImage.ActualHeight);
                    //   this.updateClipAndCrop = true;
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
        public Rect MediaRenderSize
        {
            get
            {
                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    return new Rect(new Point((this.RenderSize.Width - this.MainImage.RenderSize.Width + this.CropRight + this.CropLeft) / 2, (this.RenderSize.Height - this.MainImage.RenderSize.Height + this.CropTop + this.CropBottom) / 2),
                        new Size(this.MainImage.RenderSize.Width - this.CropRight - this.CropLeft, this.MainImage.RenderSize.Height - this.CropTop - this.CropBottom));
                else
                    return new Rect(new Point((this.RenderSize.Width - this.MainImage.RenderSize.Height + this.CropRight + this.CropLeft) / 2, (this.RenderSize.Height - this.MainImage.RenderSize.Width + this.CropTop + this.CropBottom) / 2),
                    new Size(this.MainImage.RenderSize.Height - this.CropRight - this.CropLeft, this.MainImage.RenderSize.Width - this.CropTop - this.CropBottom));

            }
        }

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

                if (value < 0.1)
                    value = .1;
                else if (value > 1000)
                    value = 1000;

                double clipFactorOld = this.ScaleTransformRender.ScaleY;

                this.ScaleTransformRender.ScaleX = value * this.scaleXDistortFactor;
                this.ScaleTransformRender.ScaleY = value;
                this.MainImage_SizeChanged(null, null);
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

        public ImageSource Source
        {
            get
            {
                return this.MainImage.Source;
            }
        }

        public MediaItem MediaItemSource
        {
            set
            {
                //http://wpfanimatedgif.codeplex.com/documentation
                ImageBehavior.SetAnimatedSource(this.MainImage, null);
                
                this.usePreviewDb = this.UsePreviewDb;
                this.MainImage.Source = null;
                this.visibleMediaItem = value as MediaItemBitmap;

                if (this.visibleMediaItem == null)
                {
                    this.MainImage.Source = null;
                    return;
                }

                if (!File.Exists(this.visibleMediaItem.FullName))
                {
                    this.imageCache.Remove(this.visibleMediaItem.FullName);

                    this.usePreviewDb = MediaBrowserContext.IsImageInPreviewDB(this.visibleMediaItem.VariationId);

                    if (!this.usePreviewDb)
                        return;
                }

                this.ReloadLevels();
                this.SetImage();
                this.ReloadLayers();

                this.playTime.Restart();
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

        private void ReloadLevels()
        {
            if (this.usePreviewDb)
                return;

            Layer layerLevels = this.visibleMediaItem.FindLayer("LEVELS");

            HistoRemap redH = null;
            HistoRemap greenH = null;
            HistoRemap blueH = null;

            if (layerLevels != null)
            {
                string[] level = layerLevels.Action.Split('-');

                redH = new HistoRemap(level[0]);
                greenH = new HistoRemap(level[1]);
                blueH = new HistoRemap(level[2]);
            }
            else
            {
                layerLevels = this.visibleMediaItem.FindLayer("GAMM");

                if (layerLevels != null)
                {
                    double red = Convert.ToDouble(layerLevels.Action.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                    double green = Convert.ToDouble(layerLevels.Action.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                    double blue = Convert.ToDouble(layerLevels.Action.Split(' ')[2], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);

                    redH = new HistoRemap(red);
                    greenH = new HistoRemap(green);
                    blueH = new HistoRemap(blue);
                }
            }

            this.HistoRemapRed = redH;
            this.HistoRemapGreen = greenH;
            this.HistoRemapBlue = blueH;
        }

        public bool IsInvisibleRender = false;
        private bool imageLoaded;
        public void SetImage()
        {
            this.imageLoaded = false;

            if (!this.IsInvisibleRender)
                this.MainImage.Visibility = System.Windows.Visibility.Hidden;

            if (this.visibleMediaItem == null || (!this.visibleMediaItem.FileObject.Exists && !this.usePreviewDb))
            {
                return;
            }

            if (!this.usePreviewDb && this.visibleMediaItem.Filename.ToLower().EndsWith(".gif") && ViewerSource.CountFrames(this.visibleMediaItem) > 1)
            {       
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(this.visibleMediaItem.FileObject.FullName);
                image.EndInit();
                ImageBehavior.SetAnimatedSource(this.MainImage, image);

                this.IsAnimatedGif = true;
            }
            else
            {
                CacheBitmapImage bmp = null;

                if (this.usePreviewDb)
                {
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
                    this.ScaleFactor = 1;
                    this.HistoRemapRed = null;
                    this.HistoRemapGreen = null;
                    this.HistoRemapBlue = null;
                    this.MainImage.Margin = new Thickness(0, 0, 0, 0);

                    PreviewObject previewObject = MediaBrowser4.MediaBrowserContext.GetImagePreviewDB(this.visibleMediaItem.VariationId);

                    if (previewObject.Binary != null)
                    {
                        bmp = new CacheBitmapImage();
                        bmp.BitmapImage = new BitmapImage();
                        bmp.BitmapImage.BeginInit();
                        bmp.BitmapImage.StreamSource = new System.IO.MemoryStream(previewObject.Binary);
                        bmp.BitmapImage.EndInit();

                        if (previewObject.Extension == "agif")
                        {
                            this.IsAnimatedGif = true;
                            ImageBehavior.SetAnimatedSource(this.MainImage, bmp.BitmapImage);
                        }
                        else
                        {
                            this.IsAnimatedGif = false;
                            this.MainImage.Source = bmp.BitmapImage;
                        }
                    }
                }
                else
                {
                    this.IsAnimatedGif = false;
                    bmp = GetChachedImage();

                    if (imageCache.Count > 5)
                    {
                        var a = imageCache.First(x => x.Value.LastUsed == imageCache.Min(y => y.Value.LastUsed));
                        imageCache.Remove(a.Key);
                    }

                    if (this.HistoRemapRed == null
                        || this.HistoRemapGreen == null
                        || this.HistoRemapBlue == null)
                    {
                        this.MainImage.Source = bmp.BitmapImage;
                    }
                    else
                    {
                        WriteableBitmap writeableBitmap = new WriteableBitmap(bmp.BitmapImage);
                        byte[] originalPixels = new byte[writeableBitmap.PixelHeight * writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8];
                        writeableBitmap.CopyPixels(originalPixels, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8, 0);

                        writeableBitmap.Lock();
                        IntPtr buff = writeableBitmap.BackBuffer;
                        int Stride = writeableBitmap.BackBufferStride;

                        List<Tuple<int, int>> startStopList = new List<Tuple<int, int>>();
                        int imageSize = (writeableBitmap.PixelHeight * writeableBitmap.PixelWidth);
                        int steps = 4;
                        int part = imageSize / steps;

                        for (int i = 0; i < steps; i++)
                        {
                            startStopList.Add(new Tuple<int, int>(i * part,
                               (i == steps - 1 ? imageSize : ((i + 1) * part) - 1)));
                        }

                        unsafe
                        {
                            Parallel.ForEach(startStopList, delegate(Tuple<int, int> startStop)
                            {
                                byte* pbuff = (byte*)buff.ToPointer();
                                for (int x = startStop.Item1; x < startStop.Item2; x++)
                                {
                                    int loc = x * 4;
                                    byte tmp = pbuff[loc];
                                    pbuff[loc] = pbuff[loc + 1];
                                    pbuff[loc + 1] = pbuff[loc + 2];
                                    pbuff[loc + 2] = pbuff[loc + 3];
                                    pbuff[loc + 3] = tmp;

                                    pbuff[loc] = (byte)this.HistoRemapBlue[originalPixels[loc]];
                                    pbuff[loc + 1] = (byte)this.HistoRemapGreen[originalPixels[loc + 1]];
                                    pbuff[loc + 2] = (byte)this.HistoRemapRed[originalPixels[loc + 2]];
                                    pbuff[loc + 3] = (byte)originalPixels[loc + 3];
                                }
                            });
                        }

                        writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                        writeableBitmap.Unlock();
                        this.MainImage.Source = writeableBitmap;
                    }
                }
            }
        }

        public void ResetDefaults()
        {
            this.ReloadLayers();
            this.ReloadLevels();

            if (this.HistoRemapRed != null
              || this.HistoRemapGreen != null
              || this.HistoRemapBlue != null)
            {
                this.SetImage();
            }
        }

        private void ReloadLayers()
        {
            if (this.usePreviewDb)
                return;

            this.Orientation = this.visibleMediaItem.Orientation;

            this.ClipRectangle = null;
            this.cropLeftRel = 0;
            this.cropTopRel = 0;
            this.cropRightRel = 0;
            this.cropBottomRel = 0;
            this.RotateAngle = 0;
            this.TranslateX = 0;
            this.TranslateY = 0;
            this.scaleXDistortFactor = 1.0;
            this.ScaleFactor = 1.0;

            Layer layerRot = this.visibleMediaItem.FindLayer("ROT");
            Layer layerRotCrop = this.visibleMediaItem.FindLayer("ROTC");
            Layer layerCrop = this.visibleMediaItem.FindLayer("CROP");
            Layer layerClip = this.visibleMediaItem.FindLayer("CLIP");
            Layer layerZoom = this.visibleMediaItem.FindLayer("ZOOM");
            Layer layerFlip = this.visibleMediaItem.FindLayer("FLIP");

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
                this.FlipHorizontal = Convert.ToInt32(layerFlip.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat) > 0;
                this.FlipVertical = Convert.ToInt32(layerFlip.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat) > 0;
            }
        }

        public String LevelsResultString
        {
            get
            {
                if (this.HistoRemapRed == null
                 || this.HistoRemapGreen == null
                 || this.HistoRemapBlue == null)
                    return null;

                if (this.HistoRemapRed.InputBlack == 0
                    && this.HistoRemapRed.InputGray == 127
                    && this.HistoRemapRed.InputWhite == 255
                    && this.HistoRemapRed.OutputBlack == 0
                    && this.HistoRemapRed.OutputWhite == 255
                    && this.HistoRemapGreen.InputBlack == 0
                    && this.HistoRemapGreen.InputGray == 127
                    && this.HistoRemapGreen.InputWhite == 255
                    && this.HistoRemapGreen.OutputBlack == 0
                    && this.HistoRemapGreen.OutputWhite == 255
                    && this.HistoRemapBlue.InputBlack == 0
                    && this.HistoRemapBlue.InputGray == 127
                    && this.HistoRemapBlue.InputWhite == 255
                    && this.HistoRemapBlue.OutputBlack == 0
                    && this.HistoRemapBlue.OutputWhite == 255
                   ) return null;

                return String.Format("{0} {1} {2} {3} {4} - {5} {6} {7} {8} {9} - {10} {11} {12} {13} {14}"
                    , this.HistoRemapRed.InputBlack
                    , this.HistoRemapRed.InputGray
                    , this.HistoRemapRed.InputWhite
                    , this.HistoRemapRed.OutputBlack
                    , this.HistoRemapRed.OutputWhite
                    , this.HistoRemapGreen.InputBlack
                    , this.HistoRemapGreen.InputGray
                    , this.HistoRemapGreen.InputWhite
                    , this.HistoRemapGreen.OutputBlack
                    , this.HistoRemapGreen.OutputWhite
                    , this.HistoRemapBlue.InputBlack
                    , this.HistoRemapBlue.InputGray
                    , this.HistoRemapBlue.InputWhite
                    , this.HistoRemapBlue.OutputBlack
                    , this.HistoRemapBlue.OutputWhite
                );
            }
        }

        private CacheBitmapImage GetChachedImage()
        {
            CacheBitmapImage bmp;
            if (imageCache.ContainsKey(this.visibleMediaItem.FileObject.FullName))
            {
                bmp = imageCache[this.visibleMediaItem.FileObject.FullName];
            }
            else
            {
                String filePath = this.visibleMediaItem.ImageCachePath != null ? this.visibleMediaItem.ImageCachePath : this.visibleMediaItem.FileObject.FullName;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                if (this.visibleMediaItem.ImageCachePath != null) sw.Stop();

                bmp = new CacheBitmapImage();
                bmp.BitmapImage = this.LoadImage(filePath);

                sw.Stop();

                if (sw.ElapsedMilliseconds > 1000 && this.visibleMediaItem.ImageCachePath == null)
                {
                    Log.Info("Load: " + sw.ElapsedMilliseconds);
                    sw.Reset();
                    sw.Start();
                    this.visibleMediaItem.ImageCachePath = System.IO.Path.GetTempFileName() + System.IO.Path.GetExtension(filePath);
                    System.IO.File.Copy(filePath, this.visibleMediaItem.ImageCachePath);
                    sw.Stop();
                    Log.Info("Cache: " + sw.ElapsedMilliseconds);
                }


                //if (!this.NoCache)
                //    imageCache.Add(this.visibleMediaItem.FileObject.FullName, bmp);
            }

            return bmp;
        }

        private BitmapImage LoadImage(string myImageFile)
        {
            BitmapImage myRetVal = null;

            BitmapImage image = new BitmapImage();
            using (FileStream stream = File.OpenRead(myImageFile))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            myRetVal = image;

            return myRetVal;
        }

        private void MainImage_LayoutUpdated(object sender, EventArgs e)
        {
            this.UpdateClipAndCrop();
            this.SetCropSize();
        }

        void Hooks_DispatcherInactive(object sender, EventArgs e)
        {
            if (!this.imageLoaded && this.ImageLoaded != null)
            {
                this.imageLoaded = true;
                this.ImageLoaded.Invoke(this, EventArgs.Empty);
                this.MainImage.Visibility = System.Windows.Visibility.Visible;
            }
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

        public BitmapImage GetBitmapImage()
        {
            return this.GetChachedImage().BitmapImage;
        }

        public System.Drawing.Bitmap TakeSnapshot()
        {
            System.Drawing.Bitmap bmp = null;

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

        public void Dispose()
        {
            ImageBehavior.SetAnimatedSource(this.MainImage, null);
            this.MainImage.Source = null;
            this.imageCache.Clear();
            GC.Collect();
        }

        public double ScaleOriginalFactor { get; set; }

        private void MainImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.visibleMediaItem != null)
                this.ScaleOriginalFactor = (Math.Max(this.MainImage.RenderSize.Width, this.MainImage.RenderSize.Height) * this.ScaleFactor)
                    / Math.Max(this.visibleMediaItem.Width, this.visibleMediaItem.Height);
        }

        private void Control_LayoutUpdated(object sender, EventArgs e)
        {
            if (this.RenderSize.Width > 0)
            {
                double fontSize = (this.RenderSize.Width + this.RenderSize.Height + 30) / 60;
                this.InfoTextBlockTime.FontSize = fontSize;
                this.InfoTextBlockTimeBlur.FontSize = fontSize;            
            }
        }
    }
}
