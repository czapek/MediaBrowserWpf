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
using System.Windows.Shapes;
using MediaBrowser4.Objects;
using System.Diagnostics;
using MediaBrowserWPF.UserControls;
using MediaBrowser4;
using System.IO;
using MediaBrowser4.Utilities;
using System.Windows.Threading;
using MouseGestures;
using System.Windows.Interop;
using MediaBrowserWPF.UserControls.Levels;
using System.Windows.Media.Animation;
using MediaBrowserWPF.UserControls.CategoryContainer;
using System.Globalization;
using MediaBrowserWPF.UserControls.Video;
using Microsoft.Windows.Controls;

namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für MediaViewer.xaml
    /// </summary>
    public partial class MediaViewer : Window, IMediaViewer, IViewer
    {
        private const int WM_SYSCOMMAND = 0x112;
        private const int SC_SCREENSAVE = 0xF140;
        private const int SC_MONITORPOWER = 0xF170;

        private enum RenderEffect { None, Panorama, Jump, Skew, Rotate, ZoomIn };
        private RenderEffect EffectType;
        bool isMediaLoading = false;
        bool positionSetByUser = false;
        MediaViewerContextMenu mediaViewerContextMenu;
        const int mouseHideTime = 4000;
        MediaViewerItemList mediaItemList;

        DispatcherTimer stepTimer;
        DispatcherTimer mediaTimer;
        DispatcherTimer toolTipTimer;
        DispatcherTimer randomVideoJumpTimer;
        DispatcherTimer panoramaMoveTimer;
        Stopwatch stopWatchMouseWheel;
        Stopwatch slideshowTimeStopWatch = new Stopwatch();
        Stopwatch viewTimeStopWatch = new Stopwatch();
        Stopwatch cursorStopWatch = new Stopwatch();

        private MouseGestures.MouseGestures mouseGestures;
        public event EventHandler<MediaItemArgument> OnMediaItemChanged;

        public ViewerState ViewerState
        {
            get
            {
                if (this.MediaControl != null)
                    return this.MediaControl.ViewerState;
                else
                    return Viewer.ViewerState.None;
            }

            set
            {
                if (this.MediaControl != null)
                    this.MediaControl.ViewerState = value;

                this.AdornedControlGrid.IsAdornerVisible = value == ViewerState.CropRotate || value == ViewerState.Rotate;
            }
        }

        private IMediaControl MediaControl
        {
            get
            {
                if (this.VisibleMediaItem == null)
                    return null;
                else
                    return this.VisibleMediaItem is MediaItemBitmap ?
                     (IMediaControl)this.ImagePlayer : (IMediaControl)this.VideoPlayer.VideoPlayerIntern;
            }
        }

        double slideShowFactor = 1.0;
        private bool isSlideShow;
        public bool IsSlideShow
        {
            get
            {
                return this.isSlideShow;
            }

            set
            {
                this.isSlideShow = value;
                this.VideoPlayer.VideoPlayerIntern.IsLoop = !this.isSlideShow;
                this.mediaViewerContextMenu.IsSlideShow = this.IsSlideShow;

                if (!this.mediaViewerContextMenu.AnimateSlideshow)
                    this.InfoTextToolTip = "Diaschau: " + (value ? "an" : "aus");

                if (value)
                {
                    this.slideshowTimeStopWatch.Restart();
                }

                if (this.isSlideShow && this.EffectType == RenderEffect.Panorama && this.VisibleMediaItem is MediaItemBitmap)
                {
                    this.ResetAnimation();
                    this.SetPanoramaAnimation();
                }
            }
        }

        public bool ShowDeleted
        {
            get
            {
                return this.mediaViewerContextMenu.ShowDeleted;
            }

            set
            {
                this.mediaViewerContextMenu.ShowDeleted = value;
                this.mediaItemList.ShowDeleted = this.ShowDeleted;

                if (this.mediaItemList.CountUndeleted == 0 && !this.ShowDeleted)
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.Close();
                    }));
            }
        }

        /// <summary>
        /// Gibt das sichtbare MediaItem zurück oder legt dieses fest.
        /// </summary>
        public MediaItem VisibleMediaItem
        {
            get
            {
                return this.mediaItemList.SelectedMediaItem;
            }

            set
            {
                this.mediaItemList.SelectedMediaItem = value;
            }
        }

        private MediaViewer()
        {
            InitializeComponent();
        }

        public MediaViewer(List<MediaItem> mediaItemList, MediaItem currentMediaItem)
        {
            Init(new List<MediaItem>(mediaItemList), currentMediaItem);
        }

        private long initialVideoStartPosition;
        public MediaViewer(List<MediaItem> mediaItemList, MediaItem currentMediaItem, long initialVideoStartPosition)
        {
            this.initialVideoStartPosition = initialVideoStartPosition;
            Init(mediaItemList, currentMediaItem);
        }

        private void Init(List<MediaItem> mediaItemList, MediaItem currentMediaItem)
        {
            InitializeComponent();

            this.ThumblistNavigator.MediaItemList = mediaItemList;
            this.ThumblistNavigator.MouseDown += new EventHandler<MediaItemCallbackArgs>(ThumblistNavigator_MouseDown);

            this.mouseGestures = new MouseGestures.MouseGestures(true);
            this.mouseGestures.Gesture += new MouseGestures.MouseGestures.GestureHandler(this.mouseGestures_Gesture);

            this.mediaViewerContextMenu = new MediaViewerContextMenu();
            this.ContextMenuOpening += new ContextMenuEventHandler(MediaViewer_ContextMenuOpening);

            UserControls.Video.VideoPlayer.Player player;
            if (!Enum.TryParse(MediaBrowserContext.SelectedVideoPlayer, out player))
            {
                player = UserControls.Video.VideoPlayer.Player.MediaElement;
            }

            MediaBrowserContext.CategoriesChanged += new EventHandler<MediaItemArg>(MediaBrowserContext_CategoriesChanged);

            this.mediaViewerContextMenu.SelectedPlayer = player;
            this.VideoPlayer.VideoPlayerIntern.SelectedPlayer = this.mediaViewerContextMenu.SelectedPlayer;
            this.VideoPlayer.VideoPlayerIntern.IsLoop = !this.IsSlideShow;
            this.VideoPlayer.VideoPlayerIntern.EndReached += new EventHandler(VideoPlayer_EndReached);
            this.VideoPlayer.VideoPlayerIntern.VideoLoaded += new EventHandler(VideoPlayerIntern_VideoLoaded);
            this.VideoPlayer.StatusbarVisible += new EventHandler<UserControls.Video.VideoControl.StatusbarVisibleArgs>(VideoPlayer_StatusbarVisible);

            this.ImagePlayer.ImageLoaded += new EventHandler(ImagePlayer_ImageLoaded);

            this.ContextMenu = this.mediaViewerContextMenu;
            this.mediaViewerContextMenu.SelectedVideoPlayerChanged += new EventHandler(mediaViewerContextMenu_SelectedVideoPlayerChanged);
            this.mediaViewerContextMenu.ViewerStateChanged += new EventHandler<MediaViewerContextMenu.ViewerStateArgs>(mediaViewerContextMenu_ViewerStateChanged);
            this.mediaViewerContextMenu.CategoriesCopied += new EventHandler<MediaItemArg>(mediaViewerContextMenu_CategoriesCopied);

            this.stopWatchMouseWheel = new Stopwatch();
            this.stopWatchMouseWheel.Start();

            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight / 2;
            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth / 2;

            this.mediaItemList = new MediaViewerItemList(mediaItemList, currentMediaItem) { VariationType = this.mediaViewerContextMenu.VariationType };
            this.mediaItemList.ShowDeleted = this.ShowDeleted;
            this.mediaItemList.OnSelectedItemChanged += new EventHandler<EventArgs>(mediaItemList_OnSelectedItemChanged);

            this.mediaTimer = new DispatcherTimer();
            this.mediaTimer.IsEnabled = false;
            this.mediaTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            this.mediaTimer.Tick += new EventHandler(mediaTimer_Tick);

            this.toolTipTimer = new DispatcherTimer();
            this.toolTipTimer.IsEnabled = false;
            this.toolTipTimer.Interval = new TimeSpan(0, 0, 0, 3, 0);
            this.toolTipTimer.Tick += new EventHandler(toolTipTimer_Tick);

            this.randomVideoJumpTimer = new DispatcherTimer();
            this.randomVideoJumpTimer.IsEnabled = false;
            this.randomVideoJumpTimer.Interval = new TimeSpan(0, 0, 0, 10, 0);
            this.randomVideoJumpTimer.Tick += new EventHandler(randomVideoJumpTimer_Tick);

            this.stepTimer = new DispatcherTimer();
            this.stepTimer.IsEnabled = false;
            this.stepTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            this.stepTimer.Tick += new EventHandler(stepTimer_Tick);

            this.panoramaMoveTimer = new DispatcherTimer();
            this.panoramaMoveTimer.IsEnabled = false;
            this.panoramaMoveTimer.Interval = new TimeSpan(0, 0, 0, 0, 300);
            this.panoramaMoveTimer.Tick += new EventHandler(panoramaMoveTimer_Tick);

            this.viewTimeStopWatch.Restart();

            SourceInitialized += delegate
            {
                HwndSource source = (HwndSource)
                    PresentationSource.FromVisual(this);
                source.AddHook(Hook);
            };

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                this.ViewMediaItem();
            }), DispatcherPriority.ApplicationIdle);

            //this.ToggleFullscreen();
        }

        void ThumblistNavigator_MouseDown(object sender, MediaItemCallbackArgs e)
        {
            if (this.mediaViewerContextMenu.IsAutoSave)
                this.Save();
            else
            {
                this.mediaViewerContextMenu.IsNavigateMode = true;
                this.ViewerState = Viewer.ViewerState.None;
                this.SetEditMode();
            }

            if (this.EffectType != RenderEffect.None)
                this.ResetAnimation();

            this.mediaItemList.SelectedMediaItem = e.MediaItem;
        }

        List<Category> CategoryQuickPanelList = new List<Category>();
        List<Category> lastCategories = new List<Category>();
        void MediaBrowserContext_CategoriesChanged(object sender, MediaItemArg e)
        {
            this.SetInfoText();

            if (e.MediaItemList.Contains(this.VisibleMediaItem))
                this.InfoTextToolTip = this.VisibleMediaItem.Categories.Count > 0 ? "Kategorien: "
                    + String.Join(", ", this.VisibleMediaItem.Categories.Select(x => x.Name))
                    : "Alle Kategorien entfernt";

            if (!e.RemoveCategory)
                AddToCategoryQuickPanel(e.CategoryList);

            mediaCategoriezed = true;
            SetCategoryQuickPanelColor();
        }

        void mediaViewerContextMenu_CategoriesCopied(object sender, MediaItemArg e)
        {
            AddToCategoryQuickPanel(e.CategoryList);
        }

        private void AddToCategoryQuickPanel(List<Category> categoryList)
        {
            int size = this.CategoryQuickPanelList.Count;
            foreach (Category cat in categoryList)
            {
                if (!this.CategoryQuickPanelList.Contains(cat))
                    this.CategoryQuickPanelList.Add(cat);
            }

            if (size != this.CategoryQuickPanelList.Count
                && this.CategoryQuickPanel.Visibility == System.Windows.Visibility.Visible)
            {
                this.CategoryQuickPanelList = this.CategoryQuickPanelList.OrderBy(x => x.Name).ToList();
                this.SetCategoryQuickPanel();
            }
        }

        private void SetCategoryQuickPanel()
        {
            this.CategoryQuickPanel.Children.Clear();

            if (this.lastCategories.Count > 0 && this.CategoryQuickPanelList.Count > 0)
            {
                CategoryButton button = new CategoryButton();
                button.Content = "Letzte Zuordnung (" + this.lastCategories.Count + "x)";
                button.ToolTip = String.Join(", ", this.lastCategories.Select(x => x.Name));
                button.CategoryList = this.lastCategories;
                button.Focusable = false;
                button.Opacity = .7;
                button.Margin = new Thickness(0, 3, 0, 0);
                button.Click += new RoutedEventHandler(button_Click);
                this.CategoryQuickPanel.Children.Add(button);
            }

            foreach (Category cat in this.CategoryQuickPanelList)
            {
                CategoryButton button = new CategoryButton();
                if (cat.Parent != null)
                    button.ToolTip = cat.FullName;
                button.Content = cat.Name;
                button.Category = cat;
                button.Focusable = false;
                button.Opacity = .7;
                button.Margin = new Thickness(0, 3, 0, 0);
                button.Click += new RoutedEventHandler(button_Click);
                this.CategoryQuickPanel.Children.Add(button);
            }

            SetCategoryQuickPanelColor();
        }

        private void SetCategoryQuickPanelColor()
        {
            if (this.CategoryQuickPanel.Visibility == System.Windows.Visibility.Visible)
            {
                foreach (UIElement obj in this.CategoryQuickPanel.Children)
                {
                    CategoryButton button = obj as CategoryButton;

                    if (button != null)
                    {
                        if (button.CategoryList != null)
                            button.Background = Brushes.LightCoral;
                        else
                            button.Background = this.VisibleMediaItem.Categories.Contains(button.Category) ? Brushes.LightGreen : Brushes.LightSteelBlue;
                    }
                }
            }
        }

        void button_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.UserControls.CategoryContainer.CategoryButton button = sender as MediaBrowserWPF.UserControls.CategoryContainer.CategoryButton;
            if (button != null)
            {
                if (button.CategoryList == null)
                {
                    if (this.VisibleMediaItem.Categories.Contains(button.Category))
                        MediaBrowserContext.UnCategorizeMediaItems(new List<MediaItem>() { this.VisibleMediaItem }, new List<Category>() { button.Category });
                    else
                        MediaBrowserContext.CategorizeMediaItems(new List<MediaItem>() { this.VisibleMediaItem }, new List<Category>() { button.Category });
                }
                else
                {
                    MediaBrowserContext.CategorizeMediaItems(new List<MediaItem>() { this.VisibleMediaItem }, button.CategoryList);
                }
            }
        }

        void MediaViewer_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (this.mediaViewerContextMenu != null)
                this.mediaViewerContextMenu.MediaViewer = this;
        }

        void VideoPlayer_StatusbarVisible(object sender, UserControls.Video.VideoControl.StatusbarVisibleArgs e)
        {
            if (e.MarginBottom == 0)
            {
                this.InfoLeftBottom.Margin = new Thickness(10, 0, 0, 5);
                this.InfoLeftBottomBlur.Margin = new Thickness(13, 3, 0, 5);
            }
            else
            {
                this.InfoLeftBottom.Margin = new Thickness(10, 0, 0, e.MarginBottom);
                this.InfoLeftBottomBlur.Margin = new Thickness(13, 3, 0, e.MarginBottom);
            }
        }

        private static IntPtr Hook(IntPtr hwnd,

            int msg, IntPtr wParam, IntPtr lParam,
            ref bool handled)
        {
            if (msg == WM_SYSCOMMAND &&
                ((((long)wParam & 0xFFF0) == SC_SCREENSAVE) ||
                ((long)wParam & 0xFFF0) == SC_MONITORPOWER))
                handled = true;

            return IntPtr.Zero;
        }

        private void Storyboard_FadeOut_Completed(object sender, EventArgs e)
        {
            this.fadeInDone = false;
        }


        /// <summary>
        /// Lädt das aktuelle MediaItem vollständig neu in den Viewer
        /// </summary>
        public void ViewMediaItem()
        {
            if (this.mediaViewerContextMenu.MediaViewer == null)
                this.mediaViewerContextMenu.MediaViewer = this;

            if (this.VisibleMediaItem == null)
                return;

            if ((MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE) && !this.IsSlideShow)
                this.IsSlideShow = true;

            Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeOut"];
            storyBoardPanorama.Begin();

            this.ThumblistNavigator.SelectedItem = this.VisibleMediaItem;

            this.Title = String.Format("{0}, {1}x{2}Pixel, {3:0,0}KB", VisibleMediaItem.Filename,
                this.VisibleMediaItem.Width,
                this.VisibleMediaItem.Height,
                this.VisibleMediaItem.FileLength / 1024);

            if (!File.Exists(this.VisibleMediaItem.FullName))
            {
                if (!MediaBrowserContext.IsImageInPreviewDB(this.VisibleMediaItem.VariationId))
                {
                    this.InfoTextCenter.Text = "Datei nicht gefunden:\r\n" + this.VisibleMediaItem.FileObject.FullName;
                    this.InfoTextCenter.Visibility = System.Windows.Visibility.Visible;
                    this.InfoTextCenterBlur.Text = "Datei nicht gefunden:\r\n" + this.VisibleMediaItem.FileObject.FullName;
                    this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Visible;
                }

                this.VisibleMediaItem.IsFileNotFound = true;
            }
            else
            {
                this.InfoTextToolTip = null;
                this.VisibleMediaItem.IsFileNotFound = false;
            }

            this.isMediaLoading = true;
            this.mediaTimer.IsEnabled = false;

            if (Mouse.OverrideCursor != Cursors.None)
            {
                Mouse.OverrideCursor = Cursors.Wait;
            }

            if (this.mediaViewerContextMenu.AnimateSlideshow)
            {
                if (this.mediaViewerContextMenu.AnimateRandom)
                    this.NextRandomAnimation();
                else
                    this.NextAnimation();
            }

            this.PanoElement.Visibility = System.Windows.Visibility.Collapsed;
            this.VideoElement.Visibility = System.Windows.Visibility.Collapsed;
            this.ImageElement.Visibility = System.Windows.Visibility.Collapsed;

            this.slideshowTimeStopWatch.Restart();
            this.SetEditMode();

            //if (this.InfoLeftTop.Visibility == System.Windows.Visibility.Visible)
            //    this.SetInfoText();

            this.mediaViewerContextMenu.IsMarkedDeleted = this.VisibleMediaItem.IsDeleted;
            this.mediaViewerContextMenu.IsBookmarked = this.VisibleMediaItem.IsBookmarked;

            if (this.VisibleMediaItem.FindLayer("PANO") != null)
            {
                this.ViewMediaItemPano(this.VisibleMediaItem);
            }
            else if (this.VisibleMediaItem is MediaItemBitmap)
            {
                this.ViewMediaItemRgb(this.VisibleMediaItem as MediaItemBitmap);
            }
            else if (this.VisibleMediaItem is MediaItemVideo)
            {
                this.VideoPlayer.VideoPlayerIntern.IsVideoLoading = true;
                this.ViewMediaItemDirectShow(this.VisibleMediaItem as MediaItemVideo);
            }

            this.MediaControl.Background = this.VisibleMediaItem.IsDeleted ? MediaBrowserContext.DeleteBrush : MediaBrowserContext.BackGroundBrush;

            if (Mouse.OverrideCursor == Cursors.Wait)
                Mouse.OverrideCursor = null;

            if (infoState != 0) this.SetInfoText();
            else if (this.VisibleMediaItem.IsDeleted)
            {
                this.InfoTextCenter.Text = "als gelöscht markiert: ja";
                this.InfoTextCenter.Visibility = System.Windows.Visibility.Visible;
                this.InfoTextCenterBlur.Text = "als gelöscht markiert: ja";
                this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Visible;
            }

            Point mousePoint = Mouse.GetPosition(this);

            if (mousePoint.X > 0 && mousePoint.Y > 0)
                this.Activate();

            if (this.mediaViewerContextMenu.IsAutoSave)
                this.InfoTextToolTip = "Änderung gespeichert";

            SetCategoryQuickPanelColor();

            if (this.mediaViewerContextMenu.IsAutoRandomVideoJump)
                this.RandomJumpVideo();

            this.isMediaLoading = false;
        }

        private void ViewMediaItemPano(MediaItem mItem)
        {
            PanoPlayer.OpenMedia(mItem);
            this.PanoElement.Visibility = System.Windows.Visibility.Visible;
        }

        private void ViewMediaItemDirectShow(MediaItemVideo mItem)
        {
            if (!this.isSoundFromShake)
                this.StopAudio();

            try
            {
                if (!System.Windows.Forms.SystemInformation.ComputerName.ToString().Equals("HÄSEKEN-PC"))
                {
                    if (mItem.FindLayer("AVSY") != null || mItem.FindLayer("MPLY") != null)
                    {
                        this.VideoPlayer.SelectedPlayer = UserControls.Video.VideoPlayer.Player.Multiplayer;
                    }
                    else
                    {
                        this.VideoPlayer.SelectedPlayer = this.mediaViewerContextMenu.SelectedPlayer;
                    }
                }

                this.ImagePlayer.MediaItemSource = null;

                if (!File.Exists(mItem.FullName))
                {
                    if (MediaBrowserContext.IsImageInPreviewDB(mItem.VariationId))
                    {
                        this.VideoPlayer.VideoPlayerIntern.MediaItemSource = null;
                        this.ImageElement.Visibility = System.Windows.Visibility.Visible;
                        this.ImagePlayer.MediaItemSource = mItem;
                    }
                    return;
                }
                else
                {
                    this.VideoPlayer.VideoPlayerIntern.MediaItemSource = mItem;
                    this.VideoPlayer.VideoPlayerIntern.Speedratio = 1;
                    this.VideoPlayer.VideoPlayerIntern.PlayInterval = Tuple.Create<double, double>(mItem.DirectShowInfo.StartPosition, mItem.DirectShowInfo.StopPosition);
                }

                this.mediaViewerContextMenu.IsPause = false;

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
                this.InfoTextToolTip = "Fehler beim beim öffnen von " + this.VisibleMediaItem.Filename;
            }

            this.positionSetByUser = false;
            this.mediaTimer.IsEnabled = true;
        }

        DateTime lastPlayedAudioDate;
        private void ViewMediaItemRgb(MediaItemBitmap mItem)
        {
            this.VideoPlayer.VideoPlayerIntern.MediaItemSource = null;

            bool inPreviewDbNotExist = false;
            if (!File.Exists(mItem.FullName))
            {
                if (!MediaBrowserContext.IsImageInPreviewDB(mItem.VariationId))
                    return;
                else
                    inPreviewDbNotExist = true;
            }

            string soundFile = null;
            if (!inPreviewDbNotExist)
            {
                string pattern = System.IO.Path.GetFileNameWithoutExtension(mItem.Filename);
                foreach (string name in Directory.GetFiles(mItem.FileObject.DirectoryName, pattern + ".*"))
                {
                    if (!name.Equals(mItem.FullName, StringComparison.InvariantCultureIgnoreCase)
                        && (name.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        soundFile = name;
                        break;
                    }
                }

                if (soundFile == null)
                    foreach (Attachment attachment in MediaBrowserContext.GetAttachment(new List<MediaItem>() { mItem }))
                    {
                        if (attachment.FullName.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase) || attachment.FullName.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase))
                        {
                            soundFile = attachment.FullName;
                            break;
                        }
                    }


                if (Math.Abs((mItem.MediaDate - lastPlayedAudioDate).TotalMinutes) > 3 && !this.isSoundFromShake)
                {
                    this.StopAudio();
                }
            }

            try
            {
                this.ImagePlayer.MediaItemSource = mItem;
                this.ImageElement.Visibility = System.Windows.Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Mouse.OverrideCursor = Cursors.None;
                this.InfoTextToolTip = "Fehler beim beim öffnen von " + this.VisibleMediaItem.Filename;
            }

            this.mediaTimer.IsEnabled = true;

            if (!inPreviewDbNotExist)
            {
                if (soundFile != null && (this.audioMediaElement == null || !this.audioMediaIsPlaying
                    || this.audioMediaElement.Source == null ||
                    !this.audioMediaElement.Source.LocalPath.Equals(soundFile, StringComparison.InvariantCultureIgnoreCase)))
                {
                    this.PlayAudio(soundFile);
                    this.isSoundFromShake = false;
                    lastPlayedAudioDate = mItem.MediaDate;
                }
            }
        }

        void ImagePlayer_ImageLoaded(object sender, EventArgs e)
        {
            if (this.VisibleMediaItem is MediaItemBitmap)
            {
                Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
                storyBoardPanorama.Begin();

                if (this.VisibleMediaItem.Height < this.MediaControl.RenderSize.Height / 5 && this.VisibleMediaItem.Width < this.MediaControl.RenderSize.Width / 5)
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.MediaControl.ScaleFactor = 2 / this.MediaControl.ScaleOriginalFactor;
                    }), DispatcherPriority.ApplicationIdle);
                }
                else if (this.VisibleMediaItem.Height > this.MediaControl.RenderSize.Height / 5 && this.VisibleMediaItem.Width > this.MediaControl.RenderSize.Width / 5)
                {
                    MediaBrowserWPF.UserControls.RgbImage.ImageControl imagePlayer = sender as MediaBrowserWPF.UserControls.RgbImage.ImageControl;
                    if (!this.mediaViewerContextMenu.AnimateSlideshow && !Double.IsNaN(imagePlayer.MediaRenderSize.Width)
                        && !Double.IsInfinity(imagePlayer.MediaRenderSize.Width)
                        && imagePlayer.MediaRenderSize.Width > 0
                        && imagePlayer.MediaRenderSize.Width / imagePlayer.MediaRenderSize.Height > 2)
                    {
                        //this.SetPanoramaAnimation();
                    }
                }
            }
        }

        void VideoPlayerIntern_VideoLoaded(object sender, EventArgs e)
        {

        }

        private void WatchRgb(MediaItem mItem)
        {
            if (mItem != null && this.IsSlideShow && EffectType != RenderEffect.Panorama
                && this.slideshowTimeStopWatch.ElapsedMilliseconds > (long)(mItem.Duration * 1000 * this.slideShowFactor))
            {
                this.StepNext(1);
            }
        }

        bool fadeInDone = false;
        private void WatchDirectShow(MediaItemVideo mItem)
        {
            if (mItem == null)
                return;

            if (this.IsSlideShow && this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer
                && mItem.DirectShowInfo.StopPosition > 0
                && (this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds > mItem.DirectShowInfo.StopPositionMilliseconds && !this.MediaControl.UsePreviewDb))
            {
                this.StepNext(1);
                return;
            }

            if (!fadeInDone && this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds > mItem.DirectShowInfo.StartPositionMilliseconds)
            {
                fadeInDone = true;
                this.VideoPlayer.VideoPlayerIntern.IsVideoLoading = false;

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
                    storyBoardPanorama.Begin();
                }), DispatcherPriority.Render);
            }

            bool setPosition = false;
            if (!this.MediaControl.UsePreviewDb && mItem.DirectShowInfo.StartPosition > 0)
            {
                if (this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds < mItem.DirectShowInfo.StartPositionMilliseconds)
                {
                    if (this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
                        setPosition = true;
                }
            }

            if (!this.MediaControl.UsePreviewDb && mItem.DirectShowInfo.StopPosition > 0)
            {
                if (this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds > mItem.DirectShowInfo.StopPositionMilliseconds)
                {
                    if (this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
                        setPosition = true;

                    if (!this.positionSetByUser && this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
                    {
                        Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeOut"];
                        storyBoardPanorama.Begin();
                    }
                }
            }

            if (this.initialVideoStartPosition > 0 && this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds > 0)
            {
                this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds = this.initialVideoStartPosition;
                this.initialVideoStartPosition = 0;
            }
            else if (setPosition)
            {
                if (!this.positionSetByUser)
                {
                    this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds = mItem.DirectShowInfo.StartPositionMilliseconds;
                }
            }
            else
            {
                this.positionSetByUser = false;
            }
        }

        public void SetBitmapScalingMode()
        {
            if (this.VisibleMediaItem is MediaItemBitmap)
            {
                this.ImagePlayer.SetBitmapScalingMode(MediaBrowserWPF.UserControls.RgbImage.ImageControl.DefaultBitmapScalingMode);
            }
        }

        public string InfoTextToolTip
        {
            set
            {
                if (value == null || value.Trim().Length == 0)
                {
                    this.InfoTextCenter.Text = null;
                    this.InfoTextCenter.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoTextCenterBlur.Text = null;
                    this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Collapsed;
                    return;
                }

                if (infoState == 2)
                {
                    this.SetInfoText();
                    return;
                }

                this.InfoTextCenter.Text = value;
                this.InfoTextCenter.Visibility = System.Windows.Visibility.Visible;
                this.InfoTextCenterBlur.Text = value;
                this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Visible;
                toolTipTimer.Stop();
                toolTipTimer.Start();
                toolTipTimer.IsEnabled = true;
            }
        }

        public void Save()
        {
            if (this.MediaControl.UsePreviewDb)
                return;

            Save(this.VisibleMediaItem);

            this.mediaViewerContextMenu.IsNavigateMode = true;
            this.ViewerState = Viewer.ViewerState.None;
            this.SetEditMode();
        }

        private void Save(MediaItem mItem)
        {
            if (this.MediaControl.UsePreviewDb)
                return;

            if (mItem == null)
                return;

            if (mItem is MediaItemVideo)
            {
                MediaItemVideo mItemVideo = mItem as MediaItemVideo;

                if (mItemVideo.DirectShowInfo.StartPosition == 0 && mItemVideo.DirectShowInfo.StopPosition == 0)
                {
                    mItemVideo.RemoveLayer("TRIM");
                }
                else
                {
                    MediaBrowser4.Objects.Layer layer = mItemVideo.FindLayer("TRIM");

                    if (layer == null)
                        layer = mItemVideo.AddDefaultLayer("TRIM", mItemVideo.Layers.Count);

                    string crop = mItemVideo.DirectShowInfo.StartPosition.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                    + " " + mItemVideo.DirectShowInfo.StopPosition.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

                    layer.Action = crop;
                }

                if (mItemVideo.AvisynthScript != null)
                {
                    if (mItemVideo.AvisynthScript.Trim().Length == 0)
                    {
                        mItemVideo.RemoveLayer("AVSY");
                    }
                    else
                    {
                        MediaBrowser4.Objects.Layer layer = mItemVideo.FindLayer("AVSY");

                        if (layer == null)
                            layer = mItemVideo.AddDefaultLayer("AVSY", mItemVideo.Layers.Count);

                        layer.Action = mItemVideo.AvisynthScript;
                        mItemVideo.AvisynthScript = null;
                    }
                }

                if (this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
                {
                    mItemVideo.RemoveLayer("MPLY");
                }
                else
                {
                    MediaBrowser4.Objects.Layer layer = mItemVideo.FindLayer("MPLY");

                    if (layer == null)
                        layer = mItemVideo.AddDefaultLayer("MPLY", mItemVideo.Layers.Count);

                    layer.Action = "";
                }
            }

            if (this.MediaControl.RotateAngle == 0)
            {
                mItem.RemoveLayer("ROT");
            }
            else
            {
                mItem.RemoveLayer("ROTC");
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ROT");

                if (layer == null)
                    layer = mItem.AddDefaultLayer("ROT", mItem.Layers.Count);

                layer.Action = this.MediaControl.RotateAngle.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (!this.MediaControl.FlipHorizontal && !this.MediaControl.FlipVertical)
            {
                mItem.RemoveLayer("FLIP");
            }
            else
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("FLIP");

                if (layer == null)
                    layer = mItem.AddDefaultLayer("FLIP", mItem.Layers.Count);

                layer.Action = (this.MediaControl.FlipHorizontal ? "1" : "0") + " " + (this.MediaControl.FlipVertical ? "1" : "0");
            }

            if (this.MediaControl.CropLeftRel == 0
                 && this.MediaControl.CropTopRel == 0
                  && this.MediaControl.CropRightRel == 0
                && this.MediaControl.CropBottomRel == 0)
            {
                mItem.RemoveLayer("CROP");
            }
            else
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("CROP");

                if (layer == null)
                    layer = mItem.AddDefaultLayer("CROP", mItem.Layers.Count);

                layer.Action = this.MediaControl.CropLeftRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.CropTopRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.CropRightRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.CropBottomRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (this.MediaControl.ClipLeftRel == 0
                 && this.MediaControl.ClipTopRel == 0
                  && this.MediaControl.ClipRightRel == 0
                && this.MediaControl.ClipBottomRel == 0)
            {
                mItem.RemoveLayer("CLIP");
            }
            else
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("CLIP");

                if (layer == null)
                    layer = mItem.AddDefaultLayer("CLIP", mItem.Layers.Count);

                layer.Action = this.MediaControl.ClipLeftRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.ClipTopRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.ClipRightRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.ClipBottomRel.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            }


            if (this.MediaControl.TranslateX == 0
                && this.MediaControl.TranslateY == 0
                 && this.MediaControl.ScaleFactor == 1.0
               && this.MediaControl.ScaleXDistortFactor == 1.0)
            {
                mItem.RemoveLayer("ZOOM");
            }
            else
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ZOOM");

                if (layer == null)
                    layer = mItem.AddDefaultLayer("ZOOM", mItem.Layers.Count);

                layer.Action = this.MediaControl.TranslateX.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.TranslateY.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.ScaleFactor.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                     + " " + this.MediaControl.ScaleXDistortFactor.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (mItem is MediaItemBitmap)
            {
                String gammString = this.ImagePlayer.LevelsResultString;
                if (gammString == null)
                {
                    mItem.RemoveLayer("LEVELS");
                }
                else
                {
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("LEVELS");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("LEVELS", mItem.Layers.Count);

                    layer.Action = gammString;
                }
            }

            MediaBrowserContext.SetLayersForMediaItem(mItem);

            if (mItem.Orientation != this.MediaControl.Orientation)
            {
                mItem.Orientation = this.MediaControl.Orientation;
                MediaBrowserContext.Rotate90(mItem);
            }

            this.RefreshThumbnail();

            this.InfoTextToolTip = "Änderung gespeichert";

            this.SetInfoText();
        }

        public void SetVariation(Variation variation)
        {
            this.mediaItemList.SetVariation(variation);
            this.InfoTextToolTip = "Variante: " + variation.Name;
        }

        public void NewVariation(Variation variation)
        {
            this.mediaItemList.ChangeVariationType(this.mediaViewerContextMenu.VariationType);
            if (variation != null)
                this.mediaItemList.NewVariation(variation);
            this.SetInfoText();
        }

        public void RefreshThumbnail()
        {
            System.Drawing.Bitmap bmp = this.MediaControl.TakeSnapshot();

            if (bmp == null)
                bmp = MediaBrowser4.Utilities.ResultMedia.GetRGB(this.VisibleMediaItem);

            MediaBrowserContext.SetThumbForMediaItem(bmp, this.VisibleMediaItem);
        }

        public void ScreenshotToClipBoard()
        {
            Clipboard.SetImage(this.MediaControl.TakeRenderTargetBitmap());
        }

        public void ScreenshotToDesktop()
        {
            System.Drawing.Bitmap bmp = this.MediaControl.TakeSnapshot();

            if (bmp != null)
            {
                try
                {
                    string path = System.IO.Path.Combine(MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder(), System.IO.Path.GetFileNameWithoutExtension(
                        this.VisibleMediaItem.FileObject.Name) + $"_{(int)this.MediaControl.MediaRenderSize.Width}x{(int)this.MediaControl.MediaRenderSize.Height}pix.jpg");

                    string path2 = System.IO.Path.Combine(MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder() + "\\thumbs", System.IO.Path.GetFileNameWithoutExtension(
                        this.VisibleMediaItem.FileObject.Name)) + ".jpg";

                    if (!Directory.Exists(MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder() + "\\thumbs"))
                        Directory.CreateDirectory(MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder() + "\\thumbs");

                    if (File.Exists(path))
                        File.Delete(path);

                    if (File.Exists(path2))
                        File.Delete(path2);

                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);

                    bmp = MediaProcessing.ResizeImage.ActionFitIn(bmp, 800);

                    bmp.Save(path2, System.Drawing.Imaging.ImageFormat.Jpeg);

                    Clipboard.SetText(path);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }
        }

        public void Reset()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            this.MediaControl.ResetDefaults();
            this.InfoTextToolTip = "Zurückgesetzt";

            Mouse.OverrideCursor = Cursors.None;
        }

        public void ResetDefaultCurrent()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            MediaItemVideo mItemD = this.VisibleMediaItem as MediaItemVideo;
            MediaItemBitmap mItemR = this.VisibleMediaItem as MediaItemBitmap;

            switch (this.ViewerState)
            {
                case Viewer.ViewerState.Clip:
                    this.MediaControl.ClipRectangle = null;
                    break;

                case Viewer.ViewerState.AspectRatio:
                    this.ResetCrop();
                    break;

                case Viewer.ViewerState.Crop:
                    this.MediaControl.CropTopRel = 0;
                    this.MediaControl.CropRightRel = 0;
                    this.MediaControl.CropBottomRel = 0;
                    this.MediaControl.CropLeftRel = 0;
                    break;

                case Viewer.ViewerState.Flip:
                    this.MediaControl.FlipVertical = false;
                    this.MediaControl.FlipHorizontal = false;
                    break;

                case Viewer.ViewerState.Orientate:
                    this.ResetOrientation();
                    break;

                case Viewer.ViewerState.Rotate:
                    this.MediaControl.RotateAngle = 0;
                    break;

                case Viewer.ViewerState.CropRotate:
                    this.MediaControl.RotateAngle = 0;
                    this.MediaControl.CropTopRel = 0;
                    this.MediaControl.CropRightRel = 0;
                    this.MediaControl.CropBottomRel = 0;
                    this.MediaControl.CropLeftRel = 0;
                    break;

                case Viewer.ViewerState.Zoom:
                    this.MediaControl.TranslateX = 0;
                    this.MediaControl.TranslateY = 0;
                    this.MediaControl.ScaleFactor = 1.0;
                    this.MediaControl.ScaleXDistortFactor = 1.0;
                    break;
            }

            if (mItemD != null)
            {
                switch (this.ViewerState)
                {
                    case Viewer.ViewerState.None:
                        this.RemoveStartPosition();
                        this.RemoveStopPosition();
                        break;
                }
            }

            this.InfoTextToolTip = "Standardwert für Auswahl";

            Mouse.OverrideCursor = Cursors.None;
        }

        private void ResetCrop()
        {
            MediaBrowser4.Objects.Layer layer = VisibleMediaItem.FindLayer("CROP");
            if (layer != null)
            {
                this.MediaControl.CropLeftRel = Convert.ToDouble(layer.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                this.MediaControl.CropTopRel = Convert.ToDouble(layer.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                this.MediaControl.CropRightRel = Convert.ToDouble(layer.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat);
                this.MediaControl.CropBottomRel = Convert.ToDouble(layer.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat);
            }
            else
            {
                this.MediaControl.CropTopRel = 0;
                this.MediaControl.CropRightRel = 0;
                this.MediaControl.CropBottomRel = 0;
                this.MediaControl.CropLeftRel = 0;
            }
        }

        public void ResetDefaultAll()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            MediaItemVideo mItemD = this.VisibleMediaItem as MediaItemVideo;
            MediaItemBitmap mItemR = this.VisibleMediaItem as MediaItemBitmap;

            this.MediaControl.ClipRectangle = null;

            this.MediaControl.CropTopRel = 0;
            this.MediaControl.CropRightRel = 0;
            this.MediaControl.CropBottomRel = 0;
            this.MediaControl.CropLeftRel = 0;

            this.MediaControl.FlipVertical = false;
            this.MediaControl.FlipHorizontal = false;

            this.ResetOrientation();

            this.MediaControl.RotateAngle = 0;

            this.MediaControl.TranslateX = 0;
            this.MediaControl.TranslateY = 0;
            this.MediaControl.ScaleFactor = 1.0;
            this.MediaControl.ScaleXDistortFactor = 1.0;

            if (this.ImagePlayer.HistoRemapRed != null
               || this.ImagePlayer.HistoRemapGreen != null
               || this.ImagePlayer.HistoRemapBlue != null)
            {
                this.ImagePlayer.HistoRemapRed = null;
                this.ImagePlayer.HistoRemapGreen = null;
                this.ImagePlayer.HistoRemapBlue = null;

                this.ImagePlayer.SetImage();
            }

            if (mItemD != null)
            {
                this.RemoveStartPosition();
                this.RemoveStopPosition();
            }

            this.InfoTextToolTip = "Standardwert für Alle";
            Mouse.OverrideCursor = Cursors.None;
        }

        private void ResetOrientation()
        {
            MetaData orientation = this.VisibleMediaItem.MetaData.Find("Orientation", "MDEX");

            if (orientation.Value != null)
            {
                int rot = 0;
                if (orientation.Value.ToLower().StartsWith("right"))
                    rot = 1;
                if (orientation.Value.ToLower().StartsWith("bottom"))
                    rot = 2;
                if (orientation.Value.ToLower().StartsWith("left"))
                    rot = 3;

                switch (rot)
                {
                    case 0:
                        this.MediaControl.Orientation = MediaItem.MediaOrientation.BOTTOMisBOTTOM;
                        break;
                    case 1:
                        this.MediaControl.Orientation = MediaItem.MediaOrientation.RIGHTisBOTTOM;
                        break;
                    case 2:
                        this.MediaControl.Orientation = MediaItem.MediaOrientation.TOPisBOTTOM;
                        break;
                    case 3:
                        this.MediaControl.Orientation = MediaItem.MediaOrientation.LEFTisBOTTOM;
                        break;
                }
            }
        }

        public void JumpToStartPosition()
        {
            this.VideoPlayer.VideoPlayerIntern.JumpToStartPosition();
            this.ShowABInfo();
        }

        public void JumpToStopPosition()
        {
            this.VideoPlayer.VideoPlayerIntern.JumpToStopPosition();
            this.ShowABInfo();
        }

        public void SetStartPosition()
        {
            this.VideoPlayer.VideoPlayerIntern.SetStartPosition();
            SetInfoText();
            this.ShowABInfo();
        }

        public void SetStopPosition()
        {
            this.VideoPlayer.VideoPlayerIntern.SetStopPosition();
            SetInfoText();
            this.ShowABInfo();
        }

        public void RemoveStartPosition()
        {
            this.VideoPlayer.VideoPlayerIntern.RemoveStartPosition();
            SetInfoText();
            this.ShowABInfo();
        }

        public void RemoveStopPosition()
        {
            this.VideoPlayer.VideoPlayerIntern.RemoveStopPosition();
            SetInfoText();
            this.ShowABInfo();
        }

        public void Stopp2Start()
        {
            this.VideoPlayer.VideoPlayerIntern.Stopp2Start();
            this.VideoPlayer.VideoPlayerIntern.JumpToStartPosition();
            SetInfoText();
            this.ShowABInfo();
        }

        public void Play()
        {
            this.VideoPlayer.VideoPlayerIntern.Play();
        }

        public void Pause()
        {
            this.VideoPlayer.VideoPlayerIntern.Pause();
        }

        public void RenameVariation(Variation variation)
        {
            this.mediaItemList.RenameVariation(variation);
            this.SetInfoText();
        }

        public void SetInfoText()
        {
            if (infoState == 0)
                return;

            StringBuilder sb;
            List<string> infoList = new List<string>();
            if (infoState != 0)
            {
                infoList.Add(this.mediaItemList.ToString()
                    + (infoState == 2 || this.mediaViewerContextMenu.VariationType == MediaViewerItemList.VariationTypeEnum.ALL ? " " + this.mediaItemList.VariationName : String.Empty) + (this.VisibleMediaItem.IsBookmarked ? " *" : "")
                    + (this.VisibleMediaItem.FindLayer("MPLY") != null ? " M" : String.Empty)
                    + (this.VisibleMediaItem.IsDeleted ? " als gelöscht markiert" :
                    (this.VisibleMediaItem is MediaItemVideo ? (" " + (((MediaItemVideo)this.VisibleMediaItem).DirectShowInfo.StartPosition > 0 ? "[" : string.Empty)
                    + (((MediaItemVideo)this.VisibleMediaItem).DirectShowInfo.StopPosition > 0 ? "]" : string.Empty)) : string.Empty)
                     ));
            }

            if (infoState == 1)
            {
                this.InfoLeftBottom.Text = MediaItemInfo.SimpleInfo(this.VisibleMediaItem);
                this.InfoLeftBottomBlur.Text = MediaItemInfo.SimpleInfo(this.VisibleMediaItem);
            }
            else if (infoState == 2)
            {
                MediaItemVideo mItemDs = this.VisibleMediaItem as MediaItemVideo;

                infoList.Add(this.VisibleMediaItem.Filename);
                infoList.Add(this.VisibleMediaItem.Foldername);
                infoList.Add(String.Format("{0:n0}", this.VisibleMediaItem.FileLength / 1024) + " KByte");
                infoList.Add(this.VisibleMediaItem.DimmensionToolTip);

                infoList.Add($"{this.VisibleMediaItem.MediaDate:D} - {this.VisibleMediaItem.MediaDate:T}");

                infoList.Add("Ausrichtung: " + MediaItem.OrientationInfo(this.ImagePlayer.Orientation));
                foreach (MediaBrowser4.Objects.Layer layer in this.VisibleMediaItem.Layers)
                {
                    infoList.Add(layer.EditGui + ": " + layer.ActionGui);
                }

                if (this.VisibleMediaItem.MetaData.Model != null)
                    infoList.Add(this.VisibleMediaItem.MetaData.Model);

                sb = new StringBuilder();
                if (this.VisibleMediaItem.MetaData.Aperture != null)
                    sb.Append(this.VisibleMediaItem.MetaData.Aperture + " ");

                if (this.VisibleMediaItem.MetaData.ExposureTime != null)
                    sb.Append(this.VisibleMediaItem.MetaData.ExposureTime + " ");

                if (this.VisibleMediaItem.MetaData.Iso != null)
                    sb.Append(this.VisibleMediaItem.MetaData.Iso + " ");

                if (this.VisibleMediaItem.MetaData.FocalLength35All != null)
                    sb.Append(this.VisibleMediaItem.MetaData.FocalLength35All);

                if (sb.Length > 0)
                    infoList.Add(sb.ToString());

                if (mItemDs != null)
                {
                    sb = new StringBuilder();
                    if (this.VisibleMediaItem.MetaData.VideoFormat != null)
                        sb.Append(this.VisibleMediaItem.MetaData.VideoFormat + " ");

                    if (this.VisibleMediaItem.MetaData.VideoCodec != null)
                        sb.Append(this.VisibleMediaItem.MetaData.VideoCodec + " ");

                    if (this.VisibleMediaItem.MetaData.FrameRate != null)
                        sb.Append(this.VisibleMediaItem.MetaData.FrameRate + " ");

                    if (sb.Length > 0)
                        infoList.Add(sb.ToString());

                    string audioFormat = this.VisibleMediaItem.MetaData.AudioFormat;
                    if (audioFormat.Trim().Length > 0)
                        infoList.Add(audioFormat);

                    infoList.Add(VideoDuration);
                    infoList.Add($"{this.VisibleMediaItem.FileLength / (this.VisibleMediaItem.Duration * 1024):n0} KBs = {this.VisibleMediaItem.FileLength * 8 / (this.VisibleMediaItem.Duration * 1024 * 1024):n1} Mbs\n");
                }

                this.InfoLeftBottom.Text = String.Join(", ", this.VisibleMediaItem.Categories.Where(x => !x.IsDate).Select(x => x.NameDate));
                this.InfoLeftBottomBlur.Text = String.Join(", ", this.VisibleMediaItem.Categories.Where(x => !x.IsDate).Select(x => x.NameDate));
            }

            this.InfoLeftTop.Text = string.Join("\r\n", infoList);
            this.InfoLeftTopBlur.Text = string.Join("\r\n", infoList);
        }

        private string VideoDuration
        {
            get
            {
                return GetVideoDuration(this.VisibleMediaItem);
            }
        }

        public static string GetVideoDuration(MediaItem mItem)
        {
            MediaItemVideo mediaItemVideo = mItem as MediaItemVideo;
            double duration = mediaItemVideo.Duration;
            double playDuration = mediaItemVideo != null ? mediaItemVideo.PlayDuration : duration;

            if (duration != playDuration)
            {
                return DurationString(duration) + " ("
                    + (mediaItemVideo.DirectShowInfo.StartPosition > 0 ? String.Format("{0:n1}", mediaItemVideo.DirectShowInfo.StartPosition)
                    + "s + " : String.Empty) + DurationString(playDuration) + ")"
                     + " " + (mediaItemVideo.DirectShowInfo.StartPosition > 0 ? "[" : string.Empty)
                 + (mediaItemVideo.DirectShowInfo.StopPosition > 0 ? "]" : string.Empty);
            }
            else
                return DurationString(duration);
        }

        private static string DurationString(double duration)
        {
            string result = null;
            if (duration > 120)
            {
                if (duration % 60 > 5 && duration < 1800)
                {
                    result = String.Format("{0:n0} Minuten, {1:n0} Sekunden", Math.Floor(duration / 60.0), duration % 60);
                }
                else
                {
                    result = String.Format("{0:n0} Minuten", duration / 60.0);
                }
            }
            else
            {
                result = String.Format("{0:n0} Sekunden", duration);
            }

            return result;
        }

        private const string videoTimeStamp = "Marke: ";
        public void SaveAutoVideoTimeStamp(bool force)
        {
            //Medien länger als 15 Minuten und mehr als eine Minute Betrachtet
            if (this.VisibleMediaItem != null && this.VisibleMediaItem is MediaItemVideo
                && ((this.VisibleMediaItem.Duration > 15 * 60
                && this.viewTimeStopWatch.ElapsedMilliseconds / 1000 > 60) || force))
            {
                List<Variation> variationList = MediaBrowserContext.GetVariations(this.VisibleMediaItem)
                    .Where(x => x.Name.StartsWith(videoTimeStamp)).OrderBy(x => x.Position).ToList();

                if (variationList.Count >= 1)
                {
                    Variation youngestVariation = variationList[variationList.Count - 1];

                    DateTime youngest;
                    if (DateTime.TryParse(youngestVariation.Name.Substring(videoTimeStamp.Length), out youngest))
                    {
                        //mindestens eine Minute seit dem letzten Versuch
                        if (Math.Abs((DateTime.Now - youngest).TotalMinutes) < 1)
                        {
                            if (force)
                                MediaBrowserContext.RemoveVariation(this.VisibleMediaItem, youngestVariation.Name);
                            else
                                return;
                        }
                    }
                }

                if (variationList.Count > 3)
                {
                    //älteste Version löschen
                    MediaBrowserContext.RemoveVariation(this.VisibleMediaItem, variationList[0].Name);
                }

                string crop = this.VideoPlayer.VideoPlayerIntern.TimeSeconds.ToString("0.000",
                    System.Globalization.CultureInfo.InvariantCulture) + " 0.000";

                byte[] newThumb = MediaProcessing.ResizeImage.GetThumbnail(this.MediaControl.TakeSnapshot(), MediaBrowserContext.ThumbnailSize, MediaBrowserContext.ThumbnailJPEGQuality);

                MediaBrowserContext.SetNewVariation(this.VisibleMediaItem, "Marke: " + DateTime.Now.ToString("f"), crop, "TRIM", newThumb);
                this.InfoTextToolTip = "Marke gesetzt";
            }
        }

        public void SetVideoTrack(TrackInfo trackInfo)
        {
            this.InfoTextToolTip = "Ändere " + trackInfo.Type + ": " + trackInfo.ToString();
            this.VideoPlayer.VideoPlayerIntern.SetVideoTrack(trackInfo);
        }

        public List<TrackInfo> TrackInfo
        {
            get
            {
                if (this.VisibleMediaItem is MediaItemVideo)
                    return this.VideoPlayer.VideoPlayerIntern.TrackInfo;
                else
                    return new List<TrackInfo>();
            }
        }
        #region events
        //http://stackoverflow.com/questions/4939219/wpf-full-sreen-on-maximize
        //bool _inStateChange;
        protected override void OnStateChanged(EventArgs e)
        {
            //if (WindowState == WindowState.Maximized && !_inStateChange)
            {
                //_inStateChange = true;
                //WindowState = WindowState.Normal;
                //WindowStyle = WindowStyle.None;
                //WindowState = WindowState.Maximized;
                //ResizeMode = ResizeMode.NoResize;
                //this.UpdateLayout();
                //_inStateChange = false;
            }
            base.OnStateChanged(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //_inStateChange = true;
            //WindowState = WindowState.Normal;
            //WindowStyle = WindowStyle.None;
            this.WindowState = System.Windows.WindowState.Maximized;
            //ResizeMode = ResizeMode.NoResize;
            //this.UpdateLayout();
            //_inStateChange = false;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.VisibleMediaItem == null)
                return;

            this.SaveAutoVideoTimeStamp(false);
            MediaBrowserContext.CategoryTreeSingelton.CategorizeByExifDate(new List<MediaItem>() { this.VisibleMediaItem }, DateTime.Now);
            MediaBrowserContext.AddViewTime(this.VisibleMediaItem, (int)(this.viewTimeStopWatch.ElapsedMilliseconds / 1000));

            this.VisibleMediaItem.AvisynthScript = null;
            this.mediaViewerContextMenu.AnimateSlideshow = false;
            this.ResetAnimation();

            if (this.mediaViewerContextMenu.IsAutoSave)
            {
                this.Save();
            }
            else if (this.VisibleMediaItem.IsThumbJpegDataOutdated)
            {
                this.RefreshThumbnail();
            }

            this.StopAudio();

            this.ImagePlayer.Dispose();
            this.VideoPlayer.VideoPlayerIntern.Dispose();

            this.stepTimer.Stop();
            this.mediaTimer.Stop();
            this.toolTipTimer.Stop();
            this.slideshowTimeStopWatch.Stop();
            this.cursorStopWatch.Stop();
            this.stopWatchMouseWheel.Stop();

            MediaBrowserContext.CategoriesChanged -= new EventHandler<MediaItemArg>(MediaBrowserContext_CategoriesChanged);

            Mouse.OverrideCursor = null;

            GC.Collect();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void Window_LayoutUpdated(object sender, EventArgs e)
        {
            if (this.RenderSize.Width > 0)
            {
                double fontSize = (this.RenderSize.Width + this.RenderSize.Height) / 60;
                this.InfoTextCenter.FontSize = fontSize;
                this.InfoTextCenterBlur.FontSize = fontSize;
                this.InfoLeftBottom.FontSize = fontSize;
                this.InfoLeftBottomBlur.FontSize = fontSize;
                this.InfoLeftTop.FontSize = fontSize;
                this.InfoLeftTopBlur.FontSize = fontSize;
                this.CategoryQuickPanel.Margin = new Thickness(10, fontSize + 18, 0, 0);
            }
        }

        void mediaViewerContextMenu_SelectedVideoPlayerChanged(object sender, EventArgs e)
        {
            if (sender == null)
            {
                this.ToggleMultiplayer();
            }
            else
            {
                this.VideoPlayer.VideoPlayerIntern.SelectedPlayer = this.mediaViewerContextMenu.SelectedPlayer;
                this.StepNext(0);
            }
        }

        void mediaTimer_Tick(object sender, EventArgs e)
        {
            if (this.VideoPlayer.VideoPlayerIntern.IsVideoLoading && Mouse.OverrideCursor != Cursors.None
                && this.VideoPlayer.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
            {
                Mouse.OverrideCursor = Cursors.Wait;
            }
            else if (this.cursorStopWatch.ElapsedMilliseconds > mouseHideTime)
            {
                if (Mouse.DirectlyOver.GetType().Name != "Border" && Mouse.DirectlyOver.GetType().Name != "ButtonChrome")
                {
                    Mouse.OverrideCursor = Cursors.None;
                }
            }
            else
            {
                Mouse.OverrideCursor = null;
            }

            if (this.ImageElement.Visibility == System.Windows.Visibility.Visible)
            {
                this.WatchRgb(this.VisibleMediaItem);
            }
            else if (this.VideoElement.Visibility == System.Windows.Visibility.Visible
                && this.VisibleMediaItem is MediaItemVideo)
            {
                this.WatchDirectShow((MediaItemVideo)this.VisibleMediaItem);
            }
        }

        void mediaItemList_OnSelectedItemChanged(object sender, EventArgs e)
        {
            try
            {
                if (this.OnMediaItemChanged != null)
                {
                    this.OnMediaItemChanged.Invoke(sender, new MediaItemArgument(this.VisibleMediaItem));
                }

                if (this.mediaItemList.LastSelectedMediaItem != this.VisibleMediaItem)
                {
                    //Besonders lange betrachtete Dateien sammeln
                    this.SaveAutoVideoTimeStamp(false);

                    //Verlauf aufzeichnen
                    MediaBrowserContext.CategoryTreeSingelton.CategorizeByExifDate(new List<MediaItem>() { this.VisibleMediaItem }, DateTime.Now);
                }

                ViewMediaItem();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        bool mediaCategoriezed = false;
        public void StepNext(int i)
        {
            this.StepNext(i, false);
        }

        public void StepNext(int i, bool deleteVariation)
        {
            if (i != 0)
            {
                MediaBrowserContext.AddViewTime(this.VisibleMediaItem, (int)(this.viewTimeStopWatch.ElapsedMilliseconds / 1000));
                viewTimeStopWatch.Restart();
            }

            if (!deleteVariation && this.ThumblistNavigator.MediaItemList.Count == 1 && MediaBrowserContext.GetVariations(this.VisibleMediaItem).Count == 1)
                return;

            //MediaBrowser4.Utilities.ScreenSaver.SetScreenSaverTimeout(MediaBrowser4.Utilities.ScreenSaver.GetScreenSaverTimeout());

            this.mediaItemList.ChangeVariationType(this.mediaViewerContextMenu.VariationType);
            if (this.VisibleMediaItem.Categories.Count > 0 && mediaCategoriezed)
            {
                this.lastCategories = this.VisibleMediaItem.Categories.ToList();
                this.SetCategoryQuickPanel();
            }
            mediaCategoriezed = false;

            if (i != 0)
            {
                this.VisibleMediaItem.AvisynthScript = null;
            }

            if (this.mediaViewerContextMenu.IsAutoSave)
                this.Save();
            else
            {
                if (i != 0 && this.VisibleMediaItem.IsThumbJpegDataOutdated)
                    this.RefreshThumbnail();

                this.mediaViewerContextMenu.IsNavigateMode = true;
                this.ViewerState = Viewer.ViewerState.None;
                this.SetEditMode();
            }

            this.mediaItemList.StepNext(i);
        }

        public void Redraw()
        {
            ViewMediaItem();
        }

        void VideoPlayer_EndReached(object sender, EventArgs e)
        {
            if (this.IsSlideShow)
            {
                this.stepTimer.IsEnabled = true;
            }
        }

        void stepTimer_Tick(object sender, EventArgs e)
        {
            this.stepTimer.IsEnabled = false;
            this.StepNext(1);
        }

        public bool IsNavigationBar
        {
            get
            {
                return this.ThumblistNavigator.Visibility
                      == System.Windows.Visibility.Visible;
            }

            set
            {
                this.ThumblistNavigator.Visibility = (value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden);
                this.ThumblistNavigator.SelectedItem = this.VisibleMediaItem;

                this.mediaViewerContextMenu.IsNavigationBar = value;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            slideshowTimeStopWatch.Restart();

            if (this.isMediaLoading)
            {
                e.Handled = true;
                return;
            }

            switch (e.SystemKey)
            {
                case Key.F10:
                    this.IsSlideShow = !this.IsSlideShow;
                    break;

                case Key.F4:
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                        Application.Current.Shutdown();
                    break;
            }

            if (e.SystemKey != Key.None)
            {
                e.Handled = true;
                return;
            }

            if (this.VisibleMediaItem is MediaItemVideo)
            {
                int number;
                if (e.Key.ToString().StartsWith("D")
                    && Int32.TryParse(e.Key.ToString().Substring(1), out number))
                {
                    this.VideoPlayer.VideoPlayerIntern.JumpToVideoPosition((double)number / 10);
                }
            }
            else if (e.Key == Key.D0)
            {
                this.slideshowTimeStopWatch.Restart();
            }

            switch (e.Key)
            {
                case Key.MediaPlayPause:
                    this.IsSlideShow = !this.IsSlideShow;
                    break;

                case Key.F4:
                    this.mediaViewerContextMenu.AutoCrop();
                    break;

                case Key.U:
                    this.IsNavigationBar = !this.IsNavigationBar;
                    break;

                case Key.M:
                    this.ToggleMultiplayer();
                    break;

                case Key.P:
                    this.TogglePanoPlayer();
                    break;

                case Key.Q:
                    this.NextFrame();
                    break;

                case Key.K:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                        this.mediaViewerContextMenu.Copy(new List<MediaItem>() { this.VisibleMediaItem });
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.mediaViewerContextMenu.Paste(new List<MediaItem>() { this.VisibleMediaItem });
                    break;

                case Key.F9:
                    this.EffectPanoramaAnimation();
                    break;

                case Key.F8:
                    this.EffectJumpAnimation();
                    break;

                case Key.F7:
                    this.EffectRotateAnimation();
                    break;

                case Key.F6:
                    this.EffectSkewAnimation();
                    break;

                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.Stopp2Start();
                    }
                    else if (this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.Multiplayer)
                    {
                        this.VideoPlayer.VideoPlayer.KeyTabDown();
                    }
                    break;

                case Key.F3:
                    this.VideoPlayer.VideoPlayerIntern.UsePreviewDb = !this.VideoPlayer.VideoPlayerIntern.UsePreviewDb;
                    this.ImagePlayer.UsePreviewDb = !this.ImagePlayer.UsePreviewDb;
                    this.StepNext(0);
                    this.InfoTextToolTip = "Vorschau-Datenbank: " + (this.MediaControl.UsePreviewDb ? "an" : "aus");
                    break;

                case Key.L:
                    if (this.EffectType == RenderEffect.None)
                    {
                        this.HasMagnifier = !this.HasMagnifier;
                        this.mediaViewerContextMenu.HasMagnifier = this.HasMagnifier;
                    }
                    break;

                case Key.Add:
                case Key.OemPlus:
                    if (this.VisibleMediaItem is MediaItemVideo)
                    {
                        this.VideoPlayer.VideoPlayerIntern.Speedratio *= 1.25;
                    }
                    else
                    {
                        this.slideShowFactor *= 1.25;
                        this.InfoTextToolTip = "Abspiel-Faktor: " + String.Format("{0:n2}x", this.slideShowFactor);
                        this.SetAnimationSpeedFactor();
                    }
                    break;

                case Key.Subtract:
                case Key.OemMinus:
                    if (this.VisibleMediaItem is MediaItemVideo)
                    {
                        this.VideoPlayer.VideoPlayerIntern.Speedratio *= .75;
                    }
                    else
                    {
                        this.slideShowFactor *= .75;
                        this.slideShowFactor = System.Math.Max(this.slideShowFactor, 0.01);
                        this.InfoTextToolTip = "Abspiel-Faktor: " + String.Format("{0:n2}x", this.slideShowFactor);
                        this.SetAnimationSpeedFactor();
                    }
                    break;

                case Key.T:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        Point mouse = Mouse.GetPosition(this);

                        if (Mouse.OverrideCursor == Cursors.None || mouse.X < 0 || mouse.Y < 0)
                            mouse = new Point(-150 + this.ActualWidth / 2, this.ActualHeight / 2);

                        if (mouse.X > this.Width - 350)
                            mouse.X -= 300;

                        if (mouse.Y > this.Height - 150)
                            mouse.Y -= 150;

                        MediaBrowserWPF.Dialogs.TagEditorSingleton.ShowTagEditor(this.VisibleMediaItem, this.PointToScreen(mouse), this);

                    }
                    break;

                case Key.A:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                    {
                        this.RemoveStartPosition();
                    }
                    else if (Keyboard.Modifiers == (ModifierKeys.Shift))
                    {
                        this.SetStartPosition();
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.SetOrientation(1);
                    }
                    else
                    {
                        this.IsOrientateMode = !this.IsOrientateMode;
                    }
                    break;

                case Key.B:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                    {
                        this.RemoveStopPosition();
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        this.SetStopPosition();
                    }
                    else
                    {
                        this.ToggleBookmark();
                    }
                    break;

                case Key.PageUp:
                case Key.MediaPreviousTrack:
                    this.mediaViewerContextMenu.AnimateSlideshow = false;
                    if (this.EffectType != RenderEffect.None)
                        this.ResetAnimation();
                    this.StepNext(-1);
                    break;

                case Key.PageDown:
                case Key.MediaNextTrack:
                    this.mediaViewerContextMenu.AnimateSlideshow = false;
                    if (this.EffectType != RenderEffect.None)
                        this.ResetAnimation();
                    this.StepNext(1);
                    break;

                case Key.Z:
                    this.IsCropMode = !this.IsCropMode;
                    return;

                case Key.R:
                    this.IsAspectRatioMode = !this.IsAspectRatioMode;
                    return;

                case Key.G:
                    this.IsLevelsMode = !this.IsLevelsMode;
                    break;

                case Key.H:
                    this.IsZoomMode = !this.IsZoomMode;
                    return;

                case Key.F:
                    this.IsFlipMode = !this.IsFlipMode;
                    return;

                case Key.Enter:
                case Key.F11:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        ToggleStrech();
                    }
                    else
                    {
                        ToggleFullscreen();
                    }
                    break;

                case Key.MediaStop:
                case Key.Escape:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.Shutdown();
                    else
                        this.Close();
                    break;

                case Key.Pause:      
                    this.mediaViewerContextMenu.IsPause = !this.mediaViewerContextMenu.IsPause;
                    if (this.mediaViewerContextMenu.IsPause)
                        this.Pause();
                    else
                        this.Play();
                    break;

                case Key.Space:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        this.RandomJumpVideo();
                    }
                    else
                    {
                        if (!this.IsNavigateMode)
                            this.IsNavigateMode = true;
                        this.mediaViewerContextMenu.AnimateSlideshow = false;
                        if (this.EffectType != RenderEffect.None)
                            this.ResetAnimation();
                        this.StepNext(Keyboard.Modifiers == ModifierKeys.Control ? -1 : 1);
                    }
                    break;

                case Key.Back:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.ResetDefaultAll();
                    }
                    else
                    {
                        this.ResetDefaultCurrent();
                    }
                    break;

                case Key.F2:
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
                        if ((new Dialogs.VariationDialog(this, true)).ShowDialog().Value)
                            this.InfoTextToolTip = "Variante umbenannt";
                    break;
                case Key.N:
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
                        this.CreateNewVariation();
                    else
                        this.IsCropRotateMode = !this.IsCropRotateMode;
                    return;

                case Key.D:
                    this.IsRotateMode = !this.IsRotateMode;
                    break;

                case Key.Home:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                        this.RemoveStartPosition();
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.SetStartPosition();
                    else
                        this.JumpToStartPosition();
                    break;

                case Key.End:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                        this.RemoveStopPosition();
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.SetStopPosition();
                    else
                        this.JumpToStopPosition();
                    break;

                case Key.S:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.Save();
                    }
                    break;


                case Key.Delete:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.UndoDelete();
                    else if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
                        this.DeleteVariation();
                    else
                        this.ToggleMarkDeleted();
                    break;

                case Key.I:
                    this.ToggleInfoText();
                    break;
            }

            KeyDownEditMode(e);
        }

        private void KeyDownEditMode(KeyEventArgs e)
        {
            if (this.ViewerState == Viewer.ViewerState.Orientate)
            {
                if (e.Key == Key.Left)
                {
                    this.SetOrientation(1);
                }
                else if (e.Key == Key.Right)
                {
                    this.SetOrientation(-1);
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Zoom)
            {
                if (e.Key == Key.D1)
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (this.MediaControl.ScaleOriginalFactor == 1)
                        {
                            this.MediaControl.ScaleFactor = 1;
                        }
                        else
                        {
                            this.MediaControl.ScaleFactor = this.MediaControl.ScaleFactor * 1 / (this.MediaControl.ScaleOriginalFactor);
                        }

                        this.InfoTextToolTip = String.Format("{0:n1}x", this.MediaControl.ScaleOriginalFactor);
                    }), DispatcherPriority.ApplicationIdle);
                }
                else if (e.Key == Key.Left
             || e.Key == Key.Right
             || e.Key == Key.Up
             || e.Key == Key.Down)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        switch (e.Key)
                        {
                            case Key.Left:
                                this.MediaControl.ScaleFactor *= 0.9;
                                break;

                            case Key.Right:
                                this.MediaControl.ScaleFactor *= 1.1;
                                break;

                            case Key.Up:
                                this.MediaControl.ScaleFactor *= 1.2;
                                break;

                            case Key.Down:
                                this.MediaControl.ScaleFactor *= 0.8;
                                break;
                        }

                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            this.InfoTextToolTip = String.Format("{0:n1}x", this.MediaControl.ScaleOriginalFactor);
                        }), DispatcherPriority.ApplicationIdle);
                    }
                    else
                    {
                        switch (e.Key)
                        {
                            case Key.Left:
                                this.MediaControl.TranslateX -= Keyboard.Modifiers == ModifierKeys.Control ? 1 : 5;
                                break;

                            case Key.Right:
                                this.MediaControl.TranslateX += Keyboard.Modifiers == ModifierKeys.Control ? 1 : 5;
                                break;

                            case Key.Up:
                                this.MediaControl.TranslateY -= Keyboard.Modifiers == ModifierKeys.Control ? 1 : 5;
                                break;

                            case Key.Down:
                                this.MediaControl.TranslateY += Keyboard.Modifiers == ModifierKeys.Control ? 1 : 5;
                                break;
                        }
                    }
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Flip)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        this.MediaControl.FlipHorizontal = false;
                        this.InfoTextToolTip = "Horizontal spiegeln: nein";
                        break;

                    case Key.Right:
                        this.MediaControl.FlipHorizontal = true;
                        this.InfoTextToolTip = "Horizontal spiegeln: ja";
                        break;

                    case Key.Up:
                        this.MediaControl.FlipVertical = true;
                        this.InfoTextToolTip = "Vertikal spiegeln: ja";
                        break;

                    case Key.Down:
                        this.MediaControl.FlipVertical = false;
                        this.InfoTextToolTip = "Vertikal spiegeln: nein";
                        break;
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Clip)
            {
                if (e.Key == Key.Left
                || e.Key == Key.Right
                || e.Key == Key.Up
                || e.Key == Key.Down)
                {
                    this.MediaControl.ResetClip();

                    switch (e.Key)
                    {
                        case Key.Left:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.ClipLeftRel -= 1;
                            else
                                this.MediaControl.ClipLeftRel += 1;
                            break;

                        case Key.Right:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.ClipRightRel -= 1;
                            else
                                this.MediaControl.ClipRightRel += 1;
                            break;

                        case Key.Up:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.ClipTopRel -= 1;
                            else
                                this.MediaControl.ClipTopRel += 1;
                            break;

                        case Key.Down:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.ClipBottomRel -= 1;
                            else
                                this.MediaControl.ClipBottomRel += 1;
                            break;
                    }


                    this.ShowClipInfo();
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Crop)
            {
                if (e.Key == Key.Left
                || e.Key == Key.Right
                || e.Key == Key.Up
                || e.Key == Key.Down)
                {
                    this.MediaControl.ResetCrop();

                    switch (e.Key)
                    {
                        case Key.Left:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.CropLeftRel -= 1;
                            else
                                this.MediaControl.CropLeftRel += 1;
                            break;

                        case Key.Right:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.CropRightRel -= 1;
                            else
                                this.MediaControl.CropRightRel += 1;
                            break;

                        case Key.Up:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.CropTopRel -= 1;
                            else
                                this.MediaControl.CropTopRel += 1;
                            break;

                        case Key.Down:
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                                this.MediaControl.CropBottomRel -= 1;
                            else
                                this.MediaControl.CropBottomRel += 1;
                            break;
                    }

                    this.ShowCropInfo();
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.AspectRatio)
            {
                if (e.Key == Key.Left
                || e.Key == Key.Right
                || e.Key == Key.Up
                || e.Key == Key.Down)
                {
                    switch (e.Key)
                    {
                        case Key.Left:
                        case Key.Up:
                            this.PreviousAspectRatio();
                            break;
                        case Key.Right:
                        case Key.Down:
                            this.NextAspectRatio();
                            break;
                    }
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Rotate)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        this.Edit(1);
                        break;

                    case Key.Right:
                        this.Edit(-1);
                        break;
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.CropRotate)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        this.Edit(1);
                        break;

                    case Key.Right:
                        this.Edit(-1);
                        break;
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.None)
            {
                if (this.EffectType == RenderEffect.Panorama)
                {
                    int ms = 0;
                    switch (e.Key)
                    {
                        case Key.Left:
                            ms = 900;
                            break;

                        case Key.Right:
                            ms = 300;
                            break;

                        case Key.Up:
                            ms = 1200;
                            break;

                        case Key.Down:
                            ms = 600;
                            break;
                    }

                    if (ms > 0)
                    {
                        this.panoramaMoveTimer.Interval = new TimeSpan(0, 0, 0, 0, ms);
                        Storyboard storyBoard = (Storyboard)Resources["StoryBoardPanoramaTransform"];
                        oldSpeedUpPanorama = storyBoard.SpeedRatio;
                        storyBoard.SetSpeedRatio(oldSpeedUpPanorama * 10);
                        panoramaMoveTimer.IsEnabled = true;
                    }
                }
                else if (this.HasMagnifier)
                {
                    if (e.Key == Key.Right)
                        this.Magnifier.ZoomFactor *= .95;
                    else if (e.Key == Key.Left)
                        this.Magnifier.ZoomFactor *= 1.05;
                    else if (e.Key == Key.Down)
                        this.Magnifier.Radius *= .95;
                    else if (e.Key == Key.Up)
                        this.Magnifier.Radius *= 1.05;

                    if (this.Magnifier.ZoomFactor > .7)
                        this.Magnifier.ZoomFactor = .7;

                    if (this.Magnifier.ZoomFactor < .01)
                        this.Magnifier.ZoomFactor = .01;

                    if (this.Magnifier.Radius > System.Math.Min(this.RenderSize.Width, this.RenderSize.Height) * .35)
                        this.Magnifier.Radius = System.Math.Min(this.RenderSize.Width, this.RenderSize.Height) * .35;

                    if (this.Magnifier.Radius < 10)
                        this.Magnifier.Radius = 10;

                    if (e.Key == Key.Left || e.Key == Key.Right)
                        this.InfoTextToolTip = (1 / this.Magnifier.ZoomFactor) < 3 ? String.Format("{0:n1}", 1 / this.Magnifier.ZoomFactor) : (int)(1 / this.Magnifier.ZoomFactor) + "x";
                }
                else if (this.VisibleMediaItem is MediaItemVideo)
                {
                    if (e.Key == Key.Left || e.Key == Key.Right)
                    {
                        double wind = this.VideoPlayer.VideoPlayerIntern.WindInit(e.Key == Key.Right,
                            Keyboard.Modifiers == ModifierKeys.Control ? .1 : (Keyboard.Modifiers == ModifierKeys.Shift ? 1 : 10));

                        if (wind != 0)
                            this.InfoTextToolTip = String.Format("{0:n" + (Keyboard.Modifiers == ModifierKeys.Control && System.Math.Abs(wind) < 3 ? "1" : "0") + "}s", wind);
                        else
                            this.InfoTextToolTip = e.Key == Key.Left ? "|<<" : ">>|";
                    }
                    else if (e.Key == Key.Up || e.Key == Key.Down)
                    {
                        SetVolume(e.Key == Key.Up ? 5 : -5);
                    }
                }
            }
        }

        public void SetVolume(int volume)
        {
            if (this.VideoPlayer != null && this.VideoPlayer.VideoPlayerIntern != null)
            {
                this.VideoPlayer.VideoPlayerIntern.Volume += volume;
                this.InfoTextToolTip = "Volume: " + this.VideoPlayer.VideoPlayerIntern.Volume;
            }
        }

        public void AutoCrop(double relation)
        {
            this.ResetEffect();
            this.MediaControl.ForceRelation(relation);
            this.ShowCropInfo();
        }

        private int selectedAspectRatio;
        private void NextAspectRatio()
        {
            this.selectedAspectRatio++;
            if (this.selectedAspectRatio > AspectRatio.GetAspectRatioList().Count)
                this.selectedAspectRatio = 0;

            this.SetAspectRatio();
        }

        private void PreviousAspectRatio()
        {
            this.selectedAspectRatio--;
            if (this.selectedAspectRatio < 0)
                this.selectedAspectRatio = AspectRatio.GetAspectRatioList().Count;

            this.SetAspectRatio();
        }

        private void SetAspectRatio()
        {
            this.ResetCrop();
            if (selectedAspectRatio == 0)
                this.InfoTextToolTip = "Seitenverhältnis: undefiniert";
            else
            {
                AspectRatio aspectRatio = AspectRatio.GetAspectRatioList()[selectedAspectRatio - 1];
                this.InfoTextToolTip = "Seitenverhältnis: " + aspectRatio.Name;
                double heightOrientation = (int)this.MediaControl.Orientation % 2 == 0 ? this.VisibleMediaItem.Height : this.VisibleMediaItem.Width;
                double widthOrientation = (int)this.MediaControl.Orientation % 2 == 0 ? this.VisibleMediaItem.Width : this.VisibleMediaItem.Height;

                double cropHeight = (100.0 - (this.MediaControl.CropTopRel + this.MediaControl.CropBottomRel)) / 100.0;
                double cropWidth = (100.0 - (this.MediaControl.CropLeftRel + this.MediaControl.CropRightRel)) / 100.0;
                double relation = (widthOrientation * cropWidth) / (heightOrientation * cropHeight);


                if (relation > 1.0)
                {//Breite größer Höhe
                    if (relation > aspectRatio.Ratio)
                    {//zu Breit
                        double newWidth = heightOrientation * cropHeight * aspectRatio.Ratio;
                        double newWidthRel = (newWidth / widthOrientation) * 100;
                        double difference = ((100 - newWidthRel) - (this.MediaControl.CropLeftRel + this.MediaControl.CropRightRel)) / 2.0;
                        this.MediaControl.CropLeftRel += difference;
                        this.MediaControl.CropRightRel += difference;
                    }
                    else
                    {//zu hoch
                        double newHeight = widthOrientation * cropWidth / aspectRatio.Ratio;
                        double newHeightRel = (newHeight / heightOrientation) * 100;
                        double difference = ((100 - newHeightRel) - (this.MediaControl.CropTopRel + this.MediaControl.CropBottomRel)) / 2.0;
                        this.MediaControl.CropBottomRel += difference;
                        this.MediaControl.CropTopRel += difference;
                    }
                }
                else
                {//Höhe größer Breite
                    if (relation > 1 / aspectRatio.Ratio)
                    {//zu Breit
                        double newWidth = heightOrientation * cropHeight * (1 / aspectRatio.Ratio);
                        double newWidthRel = (newWidth / widthOrientation) * 100;
                        double difference = ((100 - newWidthRel) - (this.MediaControl.CropLeftRel + this.MediaControl.CropRightRel)) / 2.0;
                        this.MediaControl.CropLeftRel += difference;
                        this.MediaControl.CropRightRel += difference;
                    }
                    else
                    {//zu Hoch
                        double newHeight = widthOrientation * cropWidth / (1 / aspectRatio.Ratio);
                        double newHeightRel = (newHeight / heightOrientation) * 100;
                        double difference = ((100 - newHeightRel) - (this.MediaControl.CropTopRel + this.MediaControl.CropBottomRel)) / 2.0;
                        this.MediaControl.CropBottomRel += difference;
                        this.MediaControl.CropTopRel += difference;
                    }
                }
            }
        }

        public void InitAutoRandomJumpVideo()
        {
            this.randomVideoJumpTimer.IsEnabled = this.mediaViewerContextMenu.IsAutoRandomVideoJump;
            if (this.mediaViewerContextMenu.IsAutoRandomVideoJump)
                RandomJumpVideo();
        }

        void randomVideoJumpTimer_Tick(object sender, EventArgs e)
        {
            RandomJumpVideo();
        }

        public void RandomJumpVideo()
        {
            MediaItemVideo vItem = this.VisibleMediaItem as MediaItemVideo;
            if (vItem != null)
            {
                int interval = vItem.CroppedDuration > 300 ? 20 : 10;

                if (vItem.CroppedDuration > interval)
                {
                    if (this.mediaViewerContextMenu.IsAutoRandomVideoJump)
                    {
                        this.randomVideoJumpTimer.Interval = new TimeSpan(0, 0, 0, interval, 0);
                        this.randomVideoJumpTimer.Stop();
                        this.randomVideoJumpTimer.Start();
                    }
                    Random ran = new Random();
                    double duration = (vItem.CroppedStopPositionRelativ - vItem.CroppedStartPositionRelativ) * ran.NextDouble();
                    float someSeconds = (float)((double)(interval + 10) / vItem.CroppedDuration);
                    float newPosition = vItem.CroppedStartPositionRelativ + (float)duration;
                    if (newPosition + someSeconds > vItem.CroppedStopPositionRelativ)
                        newPosition = vItem.CroppedStopPositionRelativ - someSeconds;

                    this.VideoPlayer.VideoPlayerIntern.Position = newPosition;
                }
            }
        }

        public void CreateNewVariation()
        {
            if (this.mediaViewerContextMenu.VariationType == MediaViewerItemList.VariationTypeEnum.NONE)
                this.mediaViewerContextMenu.VariationType = MediaViewerItemList.VariationTypeEnum.SAME_NAME;

            this.NewVariation(MediaBrowserContext.SetNewVariation(this.VisibleMediaItem,
               MediaBrowserContext.GetVariations(this.VisibleMediaItem)
               .FirstOrDefault(y => y.Id == this.VisibleMediaItem.VariationId).Name));

            this.InfoTextToolTip = "Neue Variante mit gleichem Namen erstellt";
        }

        public void DeleteVariation()
        {
            if (MediaBrowserContext.RemoveVariation(this.VisibleMediaItem))
            {
                this.StepNext(0, true);
                this.InfoTextToolTip = "Variante gelöscht";
            }
            else
            {
                this.InfoTextToolTip = "Hauptvariante kann nicht gelöscht werden";
            }
        }

        private void UndoDelete()
        {
            MediaItem mItem = MediaBrowserContext.UndoDeleted();
            if (mItem != null)
            {
                this.VisibleMediaItem = mItem;
                this.StepNext(0);
            }
        }

        private void TogglePanoPlayer()
        {
            if (this.VisibleMediaItem.FindLayer("PANO") != null)
            {
                this.VisibleMediaItem.RemoveLayer("PANO");
            }
            else
            {
                this.VisibleMediaItem.AddDefaultLayer("PANO", 0);                
            }            
            this.InfoTextToolTip = "360° Viewer: " + (this.VisibleMediaItem.FindLayer("PANO") != null ? "an" : "aus");
            ViewMediaItem();
        }

        private void ToggleMultiplayer()
        {
            if (this.VisibleMediaItem is MediaItemVideo)
            {
                if (this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.Multiplayer)
                    this.VideoPlayer.SelectedPlayer = UserControls.Video.VideoPlayer.Player.WpfMediaKit;// this.mediaViewerContextMenu.SelectedPlayer;
                else
                    this.VideoPlayer.SelectedPlayer = UserControls.Video.VideoPlayer.Player.Multiplayer;

                this.InfoTextToolTip = "Multiviewer: " + (this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.Multiplayer ? "an" : "aus");
                this.VideoPlayer.VideoPlayerIntern.MediaItemSource = this.VisibleMediaItem;

                double startPosition = ((MediaItemVideo)this.VisibleMediaItem).DirectShowInfo.StartPosition;
                double stopPosition = ((MediaItemVideo)this.VisibleMediaItem).DirectShowInfo.StopPosition;
                this.VideoPlayer.VideoPlayerIntern.PlayInterval = Tuple.Create<double, double>(startPosition, stopPosition);
            }
        }

        public void NextFrame()
        {
            if (this.VisibleMediaItem is MediaItemVideo)
            {
                if (!this.mediaViewerContextMenu.IsPause)
                {
                    this.VideoPlayer.VideoPlayerIntern.Pause();
                    this.mediaViewerContextMenu.IsPause = true;
                }

                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (this.VisibleMediaItem.Frames > 0 && this.VideoPlayer.VideoPlayerIntern.Position > 0)
                    {
                        float jump = 1.0f / this.VisibleMediaItem.Frames;
                        if (this.VideoPlayer.VideoPlayerIntern.Position > 10 * jump)
                            this.VideoPlayer.VideoPlayerIntern.Position = this.VideoPlayer.VideoPlayerIntern.Position - 10 * jump;
                        else
                            this.VideoPlayer.VideoPlayerIntern.Position = 0;
                    }
                }
                else
                    this.VideoPlayer.VideoPlayerIntern.NextFrame();

                this.InfoTextToolTip = String.Format("{0:n0} ms", this.VideoPlayer.VideoPlayerIntern.TimeMilliseconds);
            }
        }

        #region Animation
        private double oldSpeedUpPanorama;
        void panoramaMoveTimer_Tick(object sender, EventArgs e)
        {
            Storyboard storyBoard = (Storyboard)Resources["StoryBoardPanoramaTransform"];
            storyBoard.SetSpeedRatio(oldSpeedUpPanorama);
            panoramaMoveTimer.IsEnabled = false;
        }

        private void SetAnimationSpeedFactor()
        {
            Storyboard storyBoard;

            switch (this.EffectType)
            {
                case RenderEffect.Panorama:
                    storyBoard = (Storyboard)Resources["StoryBoardPanoramaTransform"];
                    storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2.5));
                    break;

                case RenderEffect.Jump:
                    storyBoard = (Storyboard)Resources["StoryBoardJumpTransform"];
                    storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2));
                    break;

                case RenderEffect.ZoomIn:
                    storyBoard = (Storyboard)Resources["StoryBoardZoomInTransform"];
                    break;

                case RenderEffect.Rotate:
                    storyBoard = (Storyboard)Resources["StoryBoardRotateTransform"];
                    storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2));
                    break;

                case RenderEffect.Skew:
                    storyBoard = (Storyboard)Resources["StoryBoardSkewTransform"];
                    storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2));
                    break;
            }

        }

        private RenderEffect ResetEffect()
        {
            RenderEffect oldEffect = this.EffectType;
            if (this.EffectType != RenderEffect.None)
                this.ResetAnimation();

            return oldEffect;
        }

        public void EffectPanoramaAnimation()
        {
            if (this.ResetEffect() != RenderEffect.Panorama)
                this.SetPanoramaAnimation();
        }

        public void EffectJumpAnimation()
        {
            if (this.ResetEffect() != RenderEffect.Jump)
                this.SetJumpAnimation();
        }

        public void EffectZoomInAnimation()
        {
            if (this.ResetEffect() != RenderEffect.ZoomIn)
                this.SetZoomInAnimation();
        }

        public void EffectRotateAnimation()
        {
            if (this.ResetEffect() != RenderEffect.Rotate)
                this.SetRotateAnimation();
        }

        public void EffectSkewAnimation()
        {
            if (this.ResetEffect() != RenderEffect.Skew)
                this.SetSkewAnimation();
        }

        MediaPlayer audioMediaElement;
        bool audioMediaIsPlaying;
        private void StopAudio()
        {
            if (this.audioMediaElement != null)
            {
                this.audioMediaElement.Stop();
                this.audioMediaIsPlaying = false;
                this.isSoundFromShake = false;
            }
        }

        private void PlayAudio(string path)
        {
            if (this.audioMediaElement == null)
            {
                this.audioMediaElement = new MediaPlayer();
                this.audioMediaElement.MediaEnded += new EventHandler(audioMediaElement_MediaEnded);
                this.audioMediaElement.MediaFailed += new EventHandler<ExceptionEventArgs>(audioMediaElement_MediaFailed);
            }
            this.audioMediaElement.Open(new Uri(path, UriKind.RelativeOrAbsolute));
            this.audioMediaElement.Play();
            this.audioMediaIsPlaying = true;
        }

        void audioMediaElement_MediaFailed(object sender, ExceptionEventArgs e)
        {
            this.audioMediaIsPlaying = false;
            this.isSoundFromShake = false;
        }

        void audioMediaElement_MediaEnded(object sender, EventArgs e)
        {
            this.audioMediaIsPlaying = false;
            this.isSoundFromShake = false;
        }

        private bool isAnimated;
        private bool isSoundFromShake;
        private string lastShakeSound;
        private void Shake(bool audio)
        {
            if (this.isAnimated)
                return;

            isSoundFromShake = true;
            isAnimated = true;
            Storyboard storyBoard = (Storyboard)Resources["StoryBoardShakeTransform"];
            DoubleAnimation transX = (DoubleAnimation)storyBoard.Children[0];
            DoubleAnimation transY = (DoubleAnimation)storyBoard.Children[1];

            Random ran = new Random();
            double scale = 1.01 + ran.NextDouble() / 4;
            int moveX = ran.Next(5, 20);
            int moveY = ran.Next(5, 20);
            int sign = Math.Sign(ran.Next(-100, 100));
            this.AnimateTranslateTransform.X = -moveX / 2;
            this.AnimateTranslateTransform.Y = (-moveY / 2) * sign;

            this.AnimateScaleTransform.ScaleX = scale;
            this.AnimateScaleTransform.ScaleY = scale;

            transX.By = moveX;
            transY.By = moveY * sign;
            Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, 50 + ran.Next(60)));
            transX.Duration = duration;
            transY.Duration = duration;

            storyBoard.RepeatBehavior = new RepeatBehavior(12);

            if (audio && MediaBrowserContext.DBSoundFolder != null)
            {
                String[] files = Directory.GetFiles(MediaBrowserContext.DBSoundFolder, "*.mp3");
                if (files.Length > 0)
                {
                    this.lastShakeSound = files[ran.Next(files.Length)];
                    this.PlayAudio(this.lastShakeSound);
                }
            }

            if (this.lastShakeSound != null && !audio && !this.audioMediaIsPlaying)
            {
                this.PlayAudio(this.lastShakeSound);
            }

            storyBoard.Begin();
        }

        private void Storyboard_Completed(object sender, EventArgs e)
        {
            this.ResetAnimation();
        }

        private void SetSkewAnimation()
        {
            if (this.isAnimated)
                return;

            isAnimated = true;
            this.EffectType = RenderEffect.Skew;

            Storyboard storyBoard = (Storyboard)Resources["StoryBoardSkewTransform"];

            DoubleAnimation scaleX = (DoubleAnimation)storyBoard.Children[0];
            DoubleAnimation scaleY = (DoubleAnimation)storyBoard.Children[1];
            DoubleAnimation skewAngleX = (DoubleAnimation)storyBoard.Children[2];
            DoubleAnimation skewAngleY = (DoubleAnimation)storyBoard.Children[3];

            Random ran = new Random();
            Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, ran.Next((int)(3000.0 * slideShowFactor), (int)(6000.0 * slideShowFactor))));
            skewAngleX.By = ran.Next(-25, 25);
            skewAngleX.Duration = duration;
            skewAngleY.By = ran.Next(-25, 25);
            skewAngleY.Duration = duration;
            scaleX.By = .82 + (ran.NextDouble() / 3.0);
            scaleX.Duration = duration;
            scaleY.By = .82 + (ran.NextDouble() / 3.0);
            scaleY.Duration = duration;
            storyBoard.AutoReverse = !this.mediaViewerContextMenu.AnimateSlideshow;
            storyBoard.Begin();
            storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2));
        }

        public void AnimateSlideshow()
        {
            this.InfoTextToolTip = "Animation Diaschau: " + (this.mediaViewerContextMenu.AnimateSlideshow ? "an" : "aus");

            if (!this.mediaViewerContextMenu.AnimateSlideshow)
            {
                this.ResetAnimation();
            }
            else
            {
                if (this.EffectType == RenderEffect.None)
                    if (this.mediaViewerContextMenu.AnimateRandom)
                        this.EffectType = RenderEffect.Jump;
                    else
                        this.EffectType = RenderEffect.ZoomIn;

                this.NextAnimation();
            }
        }

        private void StoryBoardSkewTransform_Completed(object sender, EventArgs e)
        {
            this.ResetAnimation();

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (this.mediaViewerContextMenu.AnimateSlideshow)
                {
                    this.StepNext(1);
                }
                else if (this.mediaViewerContextMenu.AnimateRandom)
                {
                    this.NextRandomAnimation();
                }
                else
                {
                    this.SetSkewAnimation();
                }

                this.isAnimated = false;

            }), DispatcherPriority.Render);
        }

        private void NextAnimation()
        {
            switch (this.EffectType)
            {
                case RenderEffect.Rotate:
                    this.SetRotateAnimation();
                    break;

                case RenderEffect.Skew:
                    this.SetSkewAnimation();
                    break;

                case RenderEffect.Jump:
                    this.SetJumpAnimation();
                    break;

                case RenderEffect.ZoomIn:
                    this.SetZoomInAnimation();
                    break;

                case RenderEffect.Panorama:
                    this.SetPanoramaAnimation();
                    break;
            }
        }

        private void NextRandomAnimation()
        {
            Random ran = new Random();
            switch (ran.Next(0, 3))
            {
                case 0:
                    this.SetRotateAnimation();
                    break;

                case 1:
                    this.SetSkewAnimation();
                    break;

                case 2:
                    this.SetJumpAnimation();
                    break;

                case 3:
                    this.SetPanoramaAnimation();
                    break;
            }
        }

        private void SetRotateAnimation()
        {
            if (this.isAnimated)
                return;

            isAnimated = true;
            this.EffectType = RenderEffect.Rotate;

            Storyboard storyBoard = (Storyboard)Resources["StoryBoardRotateTransform"];

            DoubleAnimation animationAngle = (DoubleAnimation)storyBoard.Children[0];
            DoubleAnimation scaleX = (DoubleAnimation)storyBoard.Children[1];
            DoubleAnimation scaleY = (DoubleAnimation)storyBoard.Children[2];

            Random ran = new Random();
            Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, ran.Next((int)(3000.0 * slideShowFactor), (int)(6000.0 * slideShowFactor))));
            animationAngle.By = ran.Next(-300, 300);
            animationAngle.Duration = duration;
            double scalefactor = (1.1 + (ran.NextDouble() * .5));

            scaleX.By = scalefactor;
            scaleX.Duration = duration;
            scaleY.By = scalefactor;
            scaleY.Duration = duration;
            storyBoard.AutoReverse = !this.mediaViewerContextMenu.AnimateSlideshow;
            storyBoard.Begin();
            storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2));
        }

        private void StoryBoardRotateTransform_Completed(object sender, EventArgs e)
        {
            this.ResetAnimation();

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (this.mediaViewerContextMenu.AnimateSlideshow)
                {
                    this.EffectType = RenderEffect.Rotate;
                    this.StepNext(1);
                }
                else if (this.mediaViewerContextMenu.AnimateRandom)
                {
                    this.NextRandomAnimation();
                }
                else
                {
                    this.SetRotateAnimation();
                }

                this.isAnimated = false;

            }), DispatcherPriority.Render);
        }

        private void SetJumpAnimation()
        {
            if (this.isAnimated)
                return;

            isAnimated = true;
            this.EffectType = RenderEffect.Jump;
            double scalefactor = this.MainMediaContainer.ActualWidth / this.MediaControl.MediaRenderSize.Width;

            if (this.MainMediaContainer.ActualWidth / this.MainMediaContainer.ActualHeight
             < this.MediaControl.MediaRenderSize.Width / this.MediaControl.MediaRenderSize.Height)
            {
                scalefactor = this.MainMediaContainer.ActualHeight / this.MediaControl.MediaRenderSize.Height;
            }

            Random ran = new Random();

            scalefactor *= (1 + (slideShowFactor * ran.NextDouble() / 3));

            this.AnimateScaleTransform.ScaleX = 1;
            this.AnimateScaleTransform.ScaleY = 1;

            double borderX1 = (scalefactor * this.MediaControl.MediaRenderSize.Width - this.MainMediaContainer.ActualWidth) / (2 * scalefactor);
            double borderY1 = (scalefactor * this.MediaControl.MediaRenderSize.Height - this.MainMediaContainer.ActualHeight) / (2 * scalefactor);
            double borderX2 = -borderX1;
            double borderY2 = -borderY1;

            Storyboard storyBoard = (Storyboard)Resources["StoryBoardJumpTransform"];

            DoubleAnimation animationX = (DoubleAnimation)storyBoard.Children[0];
            DoubleAnimation animationY = (DoubleAnimation)storyBoard.Children[1];
            DoubleAnimation scaleX = (DoubleAnimation)storyBoard.Children[2];
            DoubleAnimation scaleY = (DoubleAnimation)storyBoard.Children[3];

            Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, ran.Next((int)(3000.0 * slideShowFactor), (int)(6000.0 * slideShowFactor))));

            scaleX.By = scalefactor;
            scaleX.Duration = duration;
            scaleY.By = scalefactor;
            scaleY.Duration = duration;
            animationY.By = -(double)ran.Next((int)borderY2, (int)borderY1);
            animationY.Duration = duration;
            animationX.By = -(double)ran.Next((int)borderX2, (int)borderX1);
            animationX.Duration = duration;
            storyBoard.AutoReverse = !this.mediaViewerContextMenu.AnimateSlideshow;
            storyBoard.Begin();
            storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2));
        }

        private void StoryBoardJumpTransform_Completed(object sender, EventArgs e)
        {
            this.ResetAnimation();

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (this.mediaViewerContextMenu.AnimateSlideshow)
                {
                    this.EffectType = RenderEffect.Jump;
                    this.StepNext(1);
                }
                else if (this.mediaViewerContextMenu.AnimateRandom)
                {
                    this.NextRandomAnimation();
                }
                else
                {
                    this.SetJumpAnimation();
                }

                this.isAnimated = false;

            }), DispatcherPriority.Render);
        }


        private void SetZoomInAnimation()
        {
            if (this.isAnimated)
                return;

            isAnimated = true;
            this.EffectType = RenderEffect.ZoomIn;

            Storyboard storyBoard = (Storyboard)Resources["StoryBoardZoomInTransform"];

            DoubleAnimation scaleX = (DoubleAnimation)storyBoard.Children[0];
            DoubleAnimation scaleY = (DoubleAnimation)storyBoard.Children[1];

            Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, (int)(this.VisibleMediaItem.Duration * (this.slideShowFactor * 2) * 1000)));


            scaleX.By = 1.08;
            scaleX.Duration = duration;
            scaleY.By = 1.08;
            scaleY.Duration = duration;

            storyBoard.Begin();
        }

        private void StoryBoardZoomInTransform_Completed(object sender, EventArgs e)
        {
            this.ResetAnimation();

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (this.mediaViewerContextMenu.AnimateSlideshow)
                {
                    this.EffectType = RenderEffect.ZoomIn;
                    this.StepNext(1);
                }
                else if (this.mediaViewerContextMenu.AnimateRandom)
                {
                    this.NextRandomAnimation();
                }
                else
                {
                    this.SetZoomInAnimation();
                }

                this.isAnimated = false;

            }), DispatcherPriority.Render);
        }

        private void StoryBoardPanoramaTransform_Completed(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                this.ResetAnimation();
                this.StepNext(1);
                this.isAnimated = false;
            }));
        }

        private void SetPanoramaAnimation()
        {
            if (this.isAnimated)
                return;

            this.HasMagnifier = false;
            this.mediaViewerContextMenu.HasMagnifier = this.HasMagnifier;

            isAnimated = true;
            this.EffectType = RenderEffect.Panorama;
            Storyboard storyBoard = (Storyboard)Resources["StoryBoardPanoramaTransform"];

            if (this.IsSlideShow && this.VisibleMediaItem is MediaItemBitmap)
            {
                storyBoard.RepeatBehavior = new RepeatBehavior(1);
                storyBoard.AutoReverse = false;
            }
            else
            {
                storyBoard.RepeatBehavior = RepeatBehavior.Forever;
                storyBoard.AutoReverse = true;
            }

            if (this.MainMediaContainer.ActualWidth / this.MainMediaContainer.ActualHeight
                < this.MediaControl.MediaRenderSize.Width / this.MediaControl.MediaRenderSize.Height)
            {
                double scalefactor = this.MainMediaContainer.ActualHeight / this.MediaControl.MediaRenderSize.Height;

                this.AnimateScaleTransform.ScaleX = scalefactor;
                this.AnimateScaleTransform.ScaleY = scalefactor;

                AnimateTranslateTransform.X = (scalefactor * this.MediaControl.MediaRenderSize.Width - this.MainMediaContainer.ActualWidth) / (2 * scalefactor);

                DoubleAnimation animationX = (DoubleAnimation)storyBoard.Children[0];
                DoubleAnimation animationY = (DoubleAnimation)storyBoard.Children[1];

                animationY.By = 0;
                animationY.Duration = new Duration(new TimeSpan(0));
                animationX.By = -2 * AnimateTranslateTransform.X;
                animationX.Duration = new Duration(new TimeSpan(
                    (long)(90000000 * scalefactor * this.MediaControl.MediaRenderSize.Width / this.MainMediaContainer.ActualWidth)));
            }
            else
            {
                double scalefactor = this.MainMediaContainer.ActualWidth / this.MediaControl.MediaRenderSize.Width;

                this.AnimateScaleTransform.ScaleX = scalefactor;
                this.AnimateScaleTransform.ScaleY = scalefactor;

                AnimateTranslateTransform.Y = (scalefactor * this.MediaControl.MediaRenderSize.Height - this.MainMediaContainer.ActualHeight) / (2 * scalefactor);

                DoubleAnimation animationX = (DoubleAnimation)storyBoard.Children[0];
                DoubleAnimation animationY = (DoubleAnimation)storyBoard.Children[1];

                animationX.By = 0;
                animationX.Duration = new Duration(new TimeSpan(0));
                animationY.By = -2 * AnimateTranslateTransform.Y;
                animationY.Duration = new Duration(new TimeSpan(
                    (long)(90000000 * scalefactor * this.MediaControl.MediaRenderSize.Height / this.MainMediaContainer.ActualHeight)));
            }

            storyBoard.Begin();
            storyBoard.SetSpeedRatio(1 / (this.slideShowFactor * 2.5));
        }

        private void ResetAnimation()
        {
            this.EffectType = RenderEffect.None;
            Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardPanoramaTransform"];
            storyBoardPanorama.Stop();

            Storyboard storyBoardJumpTransform = (Storyboard)Resources["StoryBoardJumpTransform"];
            storyBoardJumpTransform.Stop();

            Storyboard storyBoardRotateTransform = (Storyboard)Resources["StoryBoardRotateTransform"];
            storyBoardRotateTransform.Stop();

            Storyboard storyBoardSkewTransform = (Storyboard)Resources["StoryBoardSkewTransform"];
            storyBoardSkewTransform.Stop();

            Storyboard storyBoardShake = (Storyboard)Resources["StoryBoardShakeTransform"];
            storyBoardShake.Stop();

            Storyboard storyBoardZoomIn = (Storyboard)Resources["StoryBoardZoomInTransform"];
            storyBoardZoomIn.Stop();

            this.AnimateTranslateTransform.X = 0;
            this.AnimateTranslateTransform.Y = 0;
            this.AnimateScaleTransform.ScaleX = 1;
            this.AnimateScaleTransform.ScaleY = 1;
            this.AnimateRotateTransform.Angle = 0;
            this.AnimateSkewTransform.AngleX = 0;
            this.AnimateSkewTransform.AngleY = 0;

            this.isAnimated = false;
        }
        #endregion

        private void SetOrientation(int steps)
        {
            if (this.VisibleMediaItem is MediaItemVideo)
            {
                this.InfoTextToolTip = "Ausrichtung: " + MediaItem.OrientationInfo(this.VideoPlayer.VideoPlayerIntern.Rotate90(steps));
            }
            else if (this.VisibleMediaItem is MediaItemBitmap)
            {
                this.InfoTextToolTip = "Ausrichtung: " + MediaItem.OrientationInfo(this.ImagePlayer.Rotate90(steps));
            }
        }

        private void SetLastCategorized()
        {
            try
            {
                if (MediaBrowserContext.CopyItemProperties.Categories != null)
                {
                    MediaBrowserContext.CategorizeMediaItems(new List<MediaItem>() { this.VisibleMediaItem }, MediaBrowserContext.CopyItemProperties.Categories);
                    this.InfoTextToolTip = "Kategorien: " + String.Join(", ", MediaBrowserContext.CopyItemProperties.Categories.Select(x => x.Name));
                }
                else
                {
                    this.InfoTextToolTip = "Keine Kategorien ausgewählt";
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, ex.Message,
                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void mouseGestures_Gesture(object sender, MouseGestureEventArgs e)
        {
            switch (e.Gesture.ToString())
            {
                case "L":
                    this.IsRotateMode = !this.IsRotateMode;
                    break;

                case "R":
                    this.IsCropRotateMode = !this.IsCropRotateMode;
                    break;

                case "D":
                    this.IsCropMode = !this.IsCropMode;
                    break;

                case "U":
                    this.IsOrientateMode = !this.IsOrientateMode;
                    break;

                case "LR":
                    if (!this.IsNavigateMode)
                        this.ResetDefaultCurrent();
                    break;

                //case "RL":
                //    this.Reset();
                //    break;

                case "DR":
                    this.Save();
                    break;

                case "DL":
                    this.SetLastCategorized();
                    break;

                case "RU":
                    this.Shake(true);
                    this.SetNavigationModeSilent();
                    break;

                case "LU":
                    this.Shake(false);
                    this.SetNavigationModeSilent();
                    break;

                default:
                    this.IsNavigateMode = true;
                    break;
            }
        }

        private void ShowABInfo()
        {
            MediaItemVideo mItem = this.VisibleMediaItem as MediaItemVideo;

            if (mItem == null || infoState == 2)
                return;

            StringBuilder sb = new StringBuilder();
            if (mItem.DirectShowInfo.StartPosition > 0)
                sb.Append("A: " + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(mItem.DirectShowInfo.StartPosition));
            else
                sb.Append("A: -");

            if (mItem.DirectShowInfo.StopPosition > 0)
                sb.Append("   B: " + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(mItem.DirectShowInfo.StopPosition));
            else
                sb.Append("   B: -");

            this.InfoTextToolTip = sb.ToString();
        }

        public void ToggleBookmark()
        {
            MediaBrowserContext.SetBookmark(new List<MediaItem>() { this.VisibleMediaItem }, !this.VisibleMediaItem.IsBookmarked);
            this.SetInfoText();
            this.InfoTextToolTip = "Lesezeichen " + (this.VisibleMediaItem.IsBookmarked ? "gesetzt" : "entfernt");
            this.mediaViewerContextMenu.IsBookmarked = this.VisibleMediaItem.IsBookmarked;
        }

        public void ToggleMarkDeleted()
        {
            MediaItem lastItem = this.VisibleMediaItem;
            MediaBrowserContext.SetDeleted(new List<MediaItem>() { this.VisibleMediaItem }, !this.VisibleMediaItem.IsDeleted);

            if (this.mediaItemList.CountUndeleted == 0 && !this.ShowDeleted)
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    this.Close();
                }));

            if (!this.ShowDeleted)
                this.StepNext(1);
            else
            {
                this.InfoTextToolTip = "als gelöscht markiert: " + (this.VisibleMediaItem.IsDeleted ? "ja" : "nein");
                this.MediaControl.Background = this.VisibleMediaItem.IsDeleted ? MediaBrowserContext.DeleteBrush : MediaBrowserContext.BackGroundBrush;
                this.SetInfoText();
            }

            this.mediaViewerContextMenu.IsMarkedDeleted = this.VisibleMediaItem.IsDeleted;
        }

        public byte InfoState
        {
            set
            {
                this.infoState = value;
                if (this.infoState > 2)
                    this.infoState = 0;

                if (infoState != 0)
                {
                    SetInfoText();
                    this.mediaViewerContextMenu.IsInfo = true;
                    this.InfoLeftTop.Visibility = System.Windows.Visibility.Visible;
                    this.InfoLeftTopBlur.Visibility = System.Windows.Visibility.Visible;
                    this.InfoLeftBottom.Visibility = System.Windows.Visibility.Visible;
                    this.InfoLeftBottomBlur.Visibility = System.Windows.Visibility.Visible;
                    this.VideoPlayer.ShowPlayTime = true;
                    this.ImagePlayer.ShowPlayTime = true;
                }
                else
                {
                    this.mediaViewerContextMenu.IsInfo = false;
                    this.InfoLeftTop.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoLeftTopBlur.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoLeftBottom.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoLeftBottomBlur.Visibility = System.Windows.Visibility.Collapsed;
                    this.VideoPlayer.ShowPlayTime = false;
                    this.ImagePlayer.ShowPlayTime = false;
                }

                if (infoState == 1)
                {
                    this.CategoryQuickPanel.Visibility = System.Windows.Visibility.Visible;
                    this.SetCategoryQuickPanel();
                }
                else
                {
                    this.CategoryQuickPanel.Visibility = System.Windows.Visibility.Collapsed;
                }
            }

            get
            {
                return this.infoState;
            }
        }

        private byte infoState = 0;
        public void ToggleInfoText()
        {
            this.InfoState++;
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Left || e.Key == Key.Right) && this.VisibleMediaItem is MediaItemVideo && this.ViewerState == Viewer.ViewerState.None)
            {
                this.VideoPlayer.VideoPlayerIntern.WindReset();
                this.InfoTextToolTip = "";
            }
        }

        public bool HasMagnifier
        {
            get
            {
                return this.Magnifier.Visibility == System.Windows.Visibility.Visible;
            }

            set
            {
                this.Magnifier.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                this.Magnifier.Radius = (this.RenderSize.Width + this.RenderSize.Height) / 16;
                this.Magnifier.ZoomFactor = .3;
            }
        }

        void mediaViewerContextMenu_ViewerStateChanged(object sender, MediaViewerContextMenu.ViewerStateArgs e)
        {
            switch (e.ViewerState)
            {
                case Viewer.ViewerState.None:
                    this.IsNavigateMode = e.On;
                    break;

                case Viewer.ViewerState.Clip:
                    this.IsClipMode = e.On;
                    break;

                case Viewer.ViewerState.Crop:
                    this.IsCropMode = e.On;
                    break;

                case Viewer.ViewerState.AspectRatio:
                    this.IsAspectRatioMode = e.On;
                    break;

                case Viewer.ViewerState.CropRotate:
                    this.IsCropRotateMode = e.On;
                    break;

                case Viewer.ViewerState.Orientate:
                    this.IsOrientateMode = e.On;
                    break;

                case Viewer.ViewerState.Rotate:
                    this.IsRotateMode = e.On;
                    break;

                case Viewer.ViewerState.Zoom:
                    this.IsZoomMode = e.On;
                    break;

                case Viewer.ViewerState.Flip:
                    this.IsFlipMode = e.On;
                    break;
            }
        }

        public bool IsNavigateMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.None;
            }

            set
            {
                if (!value)
                    throw new Exception("Makes no sense");

                this.mediaViewerContextMenu.IsNavigateMode = value;
                this.ViewerState = Viewer.ViewerState.None;
                this.SetEditMode();
                this.InfoTextToolTip = "Navigieren ein";
            }
        }

        private void SetNavigationModeSilent()
        {
            this.InfoTextToolTip = String.Empty;
            this.mediaViewerContextMenu.IsNavigateMode = true;
            this.ViewerState = Viewer.ViewerState.None;
            this.SetEditMode();
        }

        public bool IsCropRotateMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.CropRotate;
            }

            set
            {
                this.mediaViewerContextMenu.IsCropRotateMode = value;
                this.ViewerState = value ? Viewer.ViewerState.CropRotate : Viewer.ViewerState.None;
                this.SetEditMode();
                this.InfoTextToolTip = "Neigen " + (value ? "ein" : "aus");
            }
        }

        public bool IsOrientateMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Orientate;
            }

            set
            {
                this.mediaViewerContextMenu.IsOrientationMode = value;
                this.ViewerState = value ? Viewer.ViewerState.Orientate : Viewer.ViewerState.None;
                this.SetEditMode();
                this.InfoTextToolTip = "Ausrichten " + (value ? "ein" : "aus");
            }
        }

        public bool IsClipMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Clip;
            }

            set
            {
                this.mediaViewerContextMenu.IsClipMode = value;
                this.ViewerState = value ? Viewer.ViewerState.Clip : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Passepartout " + (value ? "ein" : "aus");
            }
        }

        public bool IsLevelsMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Levels;
            }

            set
            {
                this.mediaViewerContextMenu.IsLevelsMode = value;

                if (value && this.VisibleMediaItem is MediaItemBitmap && (levelsToolbox == null || !levelsToolbox.IsVisible))
                {
                    levelsToolbox = new LevelsToolbox(this.ImagePlayer.HistoRemapRed, this.ImagePlayer.HistoRemapGreen, this.ImagePlayer.HistoRemapBlue);
                    levelsToolbox.PreviewClicked += new EventHandler(levelsToolbox_PreviewClicked);
                    levelsToolbox.BitmapSource = this.ImagePlayer.GetBitmapImage();
                    levelsToolbox.Topmost = true;
                    levelsToolbox.Owner = this;

                    if (this.ImagePlayer.HistoRemapRed == null)
                        this.ImagePlayer.HistoRemapRed = new HistoRemap();

                    if (this.ImagePlayer.HistoRemapGreen == null)
                        this.ImagePlayer.HistoRemapGreen = new HistoRemap();

                    if (this.ImagePlayer.HistoRemapBlue == null)
                        this.ImagePlayer.HistoRemapBlue = new HistoRemap();

                    this.HistoRemapRedOld = (HistoRemap)this.ImagePlayer.HistoRemapRed.Clone();
                    this.HistoRemapGreenOld = (HistoRemap)this.ImagePlayer.HistoRemapGreen.Clone();
                    this.HistoRemapBlueOld = (HistoRemap)this.ImagePlayer.HistoRemapBlue.Clone();

                    bool? result = levelsToolbox.ShowDialog();

                    if (result ?? false)
                    {
                        if (this.HistoRemapRedOld != this.levelsToolbox.HistoRemapRed
                        || this.HistoRemapGreenOld != this.levelsToolbox.HistoRemapGreen
                        || this.HistoRemapBlueOld != this.levelsToolbox.HistoRemapBlue)
                        {
                            Mouse.OverrideCursor = Cursors.Wait;

                            this.ImagePlayer.HistoRemapRed = levelsToolbox.HistoRemapRed;
                            this.ImagePlayer.HistoRemapGreen = levelsToolbox.HistoRemapGreen;
                            this.ImagePlayer.HistoRemapBlue = levelsToolbox.HistoRemapBlue;

                            this.ImagePlayer.SetImage();
                            Mouse.OverrideCursor = null;
                        }
                    }
                    else if (this.HistoRemapRedOld != this.ImagePlayer.HistoRemapRed
                        || this.HistoRemapGreenOld != this.ImagePlayer.HistoRemapGreen
                        || this.HistoRemapBlueOld != this.ImagePlayer.HistoRemapBlue)
                    {
                        Mouse.OverrideCursor = Cursors.Wait;

                        this.ImagePlayer.HistoRemapRed = this.HistoRemapRedOld;
                        this.ImagePlayer.HistoRemapGreen = this.HistoRemapGreenOld;
                        this.ImagePlayer.HistoRemapBlue = this.HistoRemapBlueOld;

                        this.ImagePlayer.SetImage();
                        Mouse.OverrideCursor = null;
                    }

                    this.mediaViewerContextMenu.IsLevelsMode = false;
                    value = false;
                }

                this.ViewerState = value ? Viewer.ViewerState.Levels : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Kontrast, Gamma und Farben " + (value ? "ein" : "aus");
            }
        }

        LevelsToolbox levelsToolbox;
        HistoRemap HistoRemapRedOld, HistoRemapGreenOld, HistoRemapBlueOld;

        void levelsToolbox_PreviewClicked(object sender, EventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            this.ImagePlayer.HistoRemapRed = levelsToolbox.HistoRemapRed;
            this.ImagePlayer.HistoRemapGreen = levelsToolbox.HistoRemapGreen;
            this.ImagePlayer.HistoRemapBlue = levelsToolbox.HistoRemapBlue;

            this.ImagePlayer.SetImage();
            Mouse.OverrideCursor = null;
        }

        public bool IsCropMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Crop;
            }

            set
            {
                this.mediaViewerContextMenu.IsCropMode = value;
                this.ViewerState = value ? Viewer.ViewerState.Crop : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Zuschneiden " + (value ? "ein" : "aus");
            }
        }

        public bool IsAspectRatioMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.AspectRatio;
            }

            set
            {
                this.mediaViewerContextMenu.IsAspectRatioMode = value;
                this.selectedAspectRatio = 0;
                this.ViewerState = value ? Viewer.ViewerState.AspectRatio : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Seitenverhältnis " + (value ? "ein" : "aus");
            }
        }

        public bool IsFlipMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Flip;
            }

            set
            {
                this.mediaViewerContextMenu.IsFlipMode = value;
                this.ViewerState = value ? Viewer.ViewerState.Flip : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Spiegeln " + (value ? "ein" : "aus");
            }
        }


        public bool IsRotateMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Rotate;
            }

            set
            {
                this.mediaViewerContextMenu.IsRotateMode = value;
                this.ViewerState = value ? Viewer.ViewerState.Rotate : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Drehen " + (value ? "ein" : "aus");
                this.SetEditMode();
            }
        }

        public bool IsZoomMode
        {
            get
            {
                return this.ViewerState == Viewer.ViewerState.Zoom;
            }

            set
            {
                this.mediaViewerContextMenu.IsZoomMode = value;
                this.ViewerState = value ? Viewer.ViewerState.Zoom : Viewer.ViewerState.None;
                this.InfoTextToolTip = "Größe und Position " + (value ? "ein" : "aus");

                if (this.ViewerState == Viewer.ViewerState.Zoom)
                {
                    this.MainMediaContainer.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    this.MainMediaContainer.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                }
                else
                {
                    this.MainMediaContainer.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    this.MainMediaContainer.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                    this.MainMediaContainer.Margin = new Thickness(0, 0, 0, 0);

                    this.MainMediaContainer.Width = double.NaN;
                    this.MainMediaContainer.Height = double.NaN;
                }

                this.SetEditMode();
            }
        }

        protected void SetEditMode()
        {
            this.MainRotateTransform.Angle = 0;

            this.MainMediaContainer.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            this.MainMediaContainer.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            this.MainMediaContainer.Margin = new Thickness(0, 0, 0, 0);
            this.MainMediaContainer.Width = double.NaN;
            this.MainMediaContainer.Height = double.NaN;
        }

        private void Edit(double value)
        {
            bool keyCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) > 0;

            if (this.ViewerState == Viewer.ViewerState.Rotate
                || this.ViewerState == Viewer.ViewerState.CropRotate)
            {
                this.MediaControl.RotateAngle -= value * (keyCtrlPressed ? 0.1 : 1);
                this.InfoTextToolTip = String.Format("{0:n" + (keyCtrlPressed ? "1" : "0") + "}°",
                    this.MediaControl.RotateAngle);
            }
            else if (this.ViewerState == Viewer.ViewerState.Zoom)
            {
                double scaleFactor = (value > 0 ? (keyCtrlPressed ? 1.01 : 1.1) : (keyCtrlPressed ? 0.99 : 0.9));
                if ((Keyboard.Modifiers & ModifierKeys.Alt) > 0)
                {
                    this.MediaControl.ScaleXDistortFactor *= scaleFactor;
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.InfoTextToolTip = String.Format("Störung {0:n" + (keyCtrlPressed ? "2" : "1") + "}x", this.MediaControl.ScaleXDistortFactor);
                    }), DispatcherPriority.ApplicationIdle);
                }
                else
                {
                    this.MediaControl.ScaleFactor *= scaleFactor;
                    this.InfoTextToolTip = String.Format("{0:n" + (keyCtrlPressed ? "2" : "1") + "}x", this.MediaControl.ScaleOriginalFactor);
                }
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (this.ViewerState == Viewer.ViewerState.Clip)
            {
                this.MediaControl.ResetClip();
                double value = (double)Math.Sign(e.Delta) * 1;

                if ((Keyboard.Modifiers & ModifierKeys.Control) > 0)
                    value *= .2;

                Point pos = e.GetPosition(this.MediaControl);

                double border = (this.MediaControl.MediaRenderSize.Width + this.MediaControl.MediaRenderSize.Height) / 10;
                double top = pos.Y < border || pos.Y < this.MediaControl.ClipTop ? value : 0;
                double bottom = this.MediaControl.RenderSize.Height - pos.Y < border || this.MediaControl.RenderSize.Height - pos.Y < this.MediaControl.ClipBottom ? value : 0;
                double left = pos.X < border || pos.X < this.MediaControl.ClipLeft ? value : 0;
                double right = this.MediaControl.RenderSize.Width - pos.X < border || this.MediaControl.RenderSize.Width - pos.X < this.MediaControl.ClipRight ? value : 0;

                if (top == 0 && bottom == 0 && left == 0 && right == 0)
                {
                    top = value;
                    bottom = value;
                    left = value;
                    right = value;
                }

                this.MediaControl.ClipRightRel -= right;
                this.MediaControl.ClipTopRel -= top;
                this.MediaControl.ClipBottomRel -= bottom;
                this.MediaControl.ClipLeftRel -= left;

                this.ShowClipInfo();
            }
            else if (this.ViewerState == Viewer.ViewerState.Crop)
            {
                this.MediaControl.ResetCrop();
                double value = (double)Math.Sign(e.Delta) * 1;

                if ((Keyboard.Modifiers & ModifierKeys.Control) > 0)
                    value *= .2;

                Point pos = e.GetPosition(this.MediaControl);

                double borderX = (this.MediaControl.MediaRenderSize.Width) / 10;
                double borderY = (this.MediaControl.MediaRenderSize.Height) / 10;

                double top = pos.Y < borderY || pos.Y < this.MediaControl.CropTop ? value : 0;
                double bottom = this.MediaControl.RenderSize.Height - pos.Y < borderY || this.MediaControl.RenderSize.Height - pos.Y < this.MediaControl.CropBottom ? value : 0;
                double left = pos.X < borderX || pos.X < this.MediaControl.CropLeft ? value : 0;
                double right = this.MediaControl.RenderSize.Width - pos.X < borderX || this.MediaControl.RenderSize.Width - pos.X < this.MediaControl.CropRight ? value : 0;

                if (top == 0 && bottom == 0 && left == 0 && right == 0)
                {
                    top = value;
                    bottom = value;
                    left = value;
                    right = value;
                }

                this.MediaControl.CropRightRel -= right;
                this.MediaControl.CropTopRel -= top;
                this.MediaControl.CropBottomRel -= bottom;
                this.MediaControl.CropLeftRel -= left;

                this.ShowCropInfo();
            }
            else if (this.ViewerState == Viewer.ViewerState.AspectRatio)
            {
                if (e.Delta > 0)
                    this.PreviousAspectRatio();
                else
                    this.NextAspectRatio();

            }
            else if (this.ViewerState == Viewer.ViewerState.CropRotate
                || this.ViewerState == Viewer.ViewerState.Zoom
                || this.ViewerState == Viewer.ViewerState.Rotate)
            {
                this.Edit((double)Math.Sign(e.Delta));
            }
            else if (this.ViewerState == Viewer.ViewerState.Orientate)
            {
                this.SetOrientation(-Math.Sign(e.Delta));
            }
            else if (this.ViewerState == Viewer.ViewerState.Flip)
            {
                if (Math.Sign(e.Delta) < 0)
                {
                    this.MediaControl.FlipHorizontal = !this.MediaControl.FlipHorizontal;
                }
                else
                {
                    this.MediaControl.FlipVertical = !this.MediaControl.FlipVertical;
                }
            }
            else
            {
                if ((this.MediaControl.UsePreviewDb && this.stopWatchMouseWheel.ElapsedMilliseconds < 10) || this.stopWatchMouseWheel.ElapsedMilliseconds < 1000)
                    return;

                this.mediaViewerContextMenu.AnimateSlideshow = false;
                if (this.EffectType != RenderEffect.None)
                    this.ResetAnimation();

                if (e.Delta < 0)
                {
                    this.StepNext(1);
                }
                else
                {
                    this.StepNext(-1);
                }

                this.stopWatchMouseWheel.Restart();
            }

            e.Handled = true;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
            {
                if (this.WindowStyle == System.Windows.WindowStyle.None)
                {
                    this.Close();
                }
                else
                {
                    this.ToggleFullscreen();
                }
            }
            else if ((this.ViewerState == Viewer.ViewerState.Clip || this.ViewerState == Viewer.ViewerState.Crop || this.ViewerState == Viewer.ViewerState.AspectRatio)
                 && e.LeftButton == MouseButtonState.Pressed)
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        bool isStrech = false;
        public bool IsStrech
        {
            get
            {
                return this.WindowStyle == System.Windows.WindowStyle.None;
            }

            set
            {
                if (!isStrech)
                {
                    isStrech = true;

                    int height = System.Windows.Forms.Screen.AllScreens.Min(x => x.Bounds.Height);
                    int width = System.Windows.Forms.Screen.AllScreens.Sum(x => x.Bounds.Width);

                    this.Height = height + 80;
                    this.Width = width;
                    this.Left = 0;
                    this.Top = 0;

                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.WindowStyle = WindowStyle.None;
                        this.ResizeMode = ResizeMode.NoResize;
                        this.UpdateLayout();
                    }), DispatcherPriority.ApplicationIdle);
                }
                else
                {
                    isStrech = false;

                    this.Height = System.Windows.Forms.Screen.AllScreens[0].Bounds.Height / 2;
                    this.Width = System.Windows.Forms.Screen.AllScreens[0].Bounds.Width / 2;
                    this.Left = System.Windows.Forms.Screen.AllScreens[0].Bounds.Width / 4;
                    this.Top = System.Windows.Forms.Screen.AllScreens[0].Bounds.Height / 4;

                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.WindowStyle = System.Windows.WindowStyle.ToolWindow;
                        this.ResizeMode = this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.WpfMediaKit
                            || this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.Multiplayer ?
                            System.Windows.ResizeMode.CanResize : System.Windows.ResizeMode.CanResizeWithGrip; //CanResizeWithGrip führt dazu das die Videowiedergabe (MediaKit) gestoppt wird
                        this.UpdateLayout();
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
        }

        public bool IsFullscreen
        {
            get
            {
                return this.WindowStyle == System.Windows.WindowStyle.None;
            }

            set
            {
                if (!value)
                {
                    this.Height = System.Windows.Forms.Screen.AllScreens[0].Bounds.Height / 2;
                    this.Width = System.Windows.Forms.Screen.AllScreens[0].Bounds.Width / 2;
                    this.Left = System.Windows.Forms.Screen.AllScreens[0].Bounds.Width / 4;
                    this.Top = System.Windows.Forms.Screen.AllScreens[0].Bounds.Height / 4;

                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.WindowState = System.Windows.WindowState.Normal;
                        this.WindowStyle = System.Windows.WindowStyle.ToolWindow;
                        this.ResizeMode = this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.WpfMediaKit
                            || this.VideoPlayer.SelectedPlayer == UserControls.Video.VideoPlayer.Player.Multiplayer ?
                            System.Windows.ResizeMode.CanResize : System.Windows.ResizeMode.CanResizeWithGrip; //CanResizeWithGrip führt dazu das die Videowiedergabe (MediaKit) gestoppt wird
                        this.UpdateLayout();
                    }), DispatcherPriority.ApplicationIdle);
                }
                else
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        this.ResizeMode = System.Windows.ResizeMode.NoResize;
                        this.WindowStyle = System.Windows.WindowStyle.None;
                        this.WindowState = System.Windows.WindowState.Maximized;//muss am Ende stehen, sonst ist die Task-Bar sichtbar
                        this.UpdateLayout();
                        //if (this.mediaViewerContextMenu.IsResizeGripTweak)
                        //  this.ResizeMode = System.Windows.ResizeMode.CanResize; // Im Fullscreenmode manchmal weise Ränder löassen sich hiermit vermeiden, aber dann probleme mit Lupe und Video im wechsel Fullscreen/Normal
                    }), DispatcherPriority.ApplicationIdle);
                }

                this.mediaViewerContextMenu.IsFullscreen = value;
            }
        }

        private void ToggleFullscreen()
        {
            this.IsFullscreen = !this.IsFullscreen;
        }

        private void ToggleStrech()
        {
            this.IsStrech = !this.IsStrech;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            this.cursorStopWatch.Reset();
            Mouse.OverrideCursor = null;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            this.cursorStopWatch.Restart();
        }

        Point lastMousePosition;
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            this.Activate();
            this.slideshowTimeStopWatch.Restart();

            Point nowPosition = e.GetPosition(this);

            double moveX = lastMousePosition.X - nowPosition.X;
            double moveY = lastMousePosition.Y - nowPosition.Y;

            if (Math.Abs(lastMousePosition.X - nowPosition.X) > 2
                || Math.Abs(lastMousePosition.Y - nowPosition.Y) > 2)
            {
                this.cursorStopWatch.Restart();
                Mouse.OverrideCursor = null;
            }

            if (this.ViewerState == Viewer.ViewerState.Zoom)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Mouse.OverrideCursor = Cursors.Hand;
                    if (this.VisibleMediaItem is MediaItemBitmap)
                    {
                        this.ImagePlayer.TranslateX -= moveX;
                        this.ImagePlayer.TranslateY -= moveY;

                    }
                    else if (this.VisibleMediaItem is MediaItemVideo)
                    {
                        this.VideoPlayer.VideoPlayerIntern.TranslateX -= moveX;
                        this.VideoPlayer.VideoPlayerIntern.TranslateY -= moveY;
                    }
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Clip)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Mouse.OverrideCursor = Cursors.Hand;
                    this.MediaControl.ClipLeftRel -= 100 * moveX / this.MediaControl.MediaRenderSize.Width;
                    this.MediaControl.ClipRightRel += 100 * moveX / this.MediaControl.MediaRenderSize.Width;
                    this.MediaControl.ClipTopRel -= 100 * moveY / this.MediaControl.MediaRenderSize.Height;
                    this.MediaControl.ClipBottomRel += 100 * moveY / this.MediaControl.MediaRenderSize.Height;
                    this.ShowClipInfo();
                }
            }
            else if (this.ViewerState == Viewer.ViewerState.Crop || this.ViewerState == Viewer.ViewerState.AspectRatio)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Mouse.OverrideCursor = Cursors.Hand;
                    this.MediaControl.CropLeftRel += 100 * moveX / this.MediaControl.MediaRenderSize.Width;
                    this.MediaControl.CropRightRel -= 100 * moveX / this.MediaControl.MediaRenderSize.Width;
                    this.MediaControl.CropTopRel += 100 * moveY / this.MediaControl.MediaRenderSize.Height;
                    this.MediaControl.CropBottomRel -= 100 * moveY / this.MediaControl.MediaRenderSize.Height;
                    this.ShowCropInfo();
                }
            }

            lastMousePosition = e.GetPosition(this);
        }

        private void ShowCropInfo()
        {
            string formatString = (Keyboard.Modifiers & ModifierKeys.Control) > 0 ?
                "\t{1:n1}%\r\n{0:n1}%\t{4:n2}\t{2:n1}%\r\n\t{3:n1}%" : "\t{1:n0}%\r\n{0:n0}%\t{4:n2}\t{2:n0}%\r\n\t{3:n0}%";

            this.InfoTextToolTip = String.Format(formatString,
                System.Math.Max(0, this.MediaControl.CropLeftRel),
                System.Math.Max(0, this.MediaControl.CropTopRel),
                System.Math.Max(0, this.MediaControl.CropRightRel),
                System.Math.Max(0, this.MediaControl.CropBottomRel),
                (Math.Max(this.MediaControl.MediaRenderSize.Width, this.MediaControl.MediaRenderSize.Height) / Math.Min(this.MediaControl.MediaRenderSize.Width, this.MediaControl.MediaRenderSize.Height)));
        }

        private void ShowClipInfo()
        {
            string formatString = (Keyboard.Modifiers & ModifierKeys.Control) > 0 ?
                "\t{1:n1}%\r\n{0:n1}%\t\t{2:n1}%\r\n\t{3:n1}%" : "\t{1:n0}%\r\n{0:n0}%\t\t{2:n0}%\r\n\t{3:n0}%";

            this.InfoTextToolTip = String.Format(formatString,
                System.Math.Max(0, this.MediaControl.ClipLeftRel),
                System.Math.Max(0, this.MediaControl.ClipTopRel),
                System.Math.Max(0, this.MediaControl.ClipRightRel),
                System.Math.Max(0, this.MediaControl.ClipBottomRel));
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.OverrideCursor == Cursors.Hand)
                Mouse.OverrideCursor = null;
        }

        void toolTipTimer_Tick(object sender, EventArgs e)
        {
            this.InfoTextCenter.Visibility = System.Windows.Visibility.Collapsed;
            this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Collapsed;
            this.InfoTextCenter.Text = null;
            this.InfoTextCenterBlur.Text = null;
            toolTipTimer.IsEnabled = false;
        }
        #endregion

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.EffectType != RenderEffect.None)
                this.ResetAnimation();
        }

        private void InfoGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (infoState == 1)
            {
                this.ToggleBookmark();
                e.Handled = true;
            }
        }

        private void Window_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {

            if (e.FinalVelocities.LinearVelocity.X > 1)
            {
                this.StepNext(-1);
            }
            else if (e.FinalVelocities.LinearVelocity.X < -1)
            {
                this.StepNext(1);
            }

            if (e.FinalVelocities.LinearVelocity.Y < -1 || e.FinalVelocities.LinearVelocity.Y > 1)
            {
                this.Close();
            }
        }

        private void Window_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {

        }
    }
}
