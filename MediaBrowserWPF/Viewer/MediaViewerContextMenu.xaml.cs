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
using MediaBrowser4;
using MediaBrowserWPF.UserControls.Video;

namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für MediaViewerContextMenu.xaml
    /// </summary>
    public partial class MediaViewerContextMenu : ContextMenu
    {
        public class ViewerStateArgs : EventArgs
        {
            public ViewerState ViewerState { get; set; }
            public bool On { get; set; }
        }

        IMediaViewer mediaViewer;
        public event EventHandler SelectedVideoPlayerChanged;
        public event EventHandler<ViewerStateArgs> ViewerStateChanged;
        public event EventHandler<MediaItemArg> CategoriesCopied;

        public MediaBrowserWPF.UserControls.Video.VideoPlayer.Player SelectedPlayer
        {
            get
            {
                if (this.SelectPlayerVideoLan.IsChecked)
                {
                    return UserControls.Video.VideoPlayer.Player.VideoLan;
                }
                else if (this.SelectPlayerWpfMediaElement.IsChecked)
                {
                    return UserControls.Video.VideoPlayer.Player.MediaElement;
                }
                else if (this.SelectPlayerWpfMediaKit.IsChecked)
                {
                    return UserControls.Video.VideoPlayer.Player.WpfMediaKit;
                }
                else if (this.SelectPlayernVlc.IsChecked)
                {
                    return UserControls.Video.VideoPlayer.Player.nVlc;
                }
                else if (this.SelectPlayerMultiplayer.IsChecked)
                {
                    return UserControls.Video.VideoPlayer.Player.Multiplayer;
                }
                else
                {
                    return UserControls.Video.VideoPlayer.Player.None;
                }
            }

            set
            {
                this.SelectPlayerWpfMediaElement.IsChecked = false;
                this.SelectPlayerVideoLan.IsChecked = false;
                this.SelectPlayerWpfMediaKit.IsChecked = false;
                this.SelectPlayernVlc.IsChecked = false;
                this.SelectPlayerMultiplayer.IsChecked = false;

                switch (value)
                {
                    case UserControls.Video.VideoPlayer.Player.nVlc:
                        this.SelectPlayernVlc.IsChecked = true;
                        break;

                    case UserControls.Video.VideoPlayer.Player.MediaElement:
                        this.SelectPlayerWpfMediaElement.IsChecked = true;
                        break;

                    case UserControls.Video.VideoPlayer.Player.VideoLan:
                        this.SelectPlayerVideoLan.IsChecked = true;
                        break;

                    case UserControls.Video.VideoPlayer.Player.WpfMediaKit:
                        this.SelectPlayerWpfMediaKit.IsChecked = true;
                        break;

                    case UserControls.Video.VideoPlayer.Player.Multiplayer:
                        this.SelectPlayerMultiplayer.IsChecked = true;
                        break;
                }
            }
        }

        public bool AnimateSlideshow
        {
            get
            {
                return this.MenuItemAnimateSlideshow.IsChecked;
            }

            set
            {
                this.MenuItemAnimateSlideshow.IsChecked = value;
                if (!value)
                {
                    this.MenuItemAnimateRandom.IsChecked = false;
                }
            }
        }

        public MediaViewerItemList.VariationTypeEnum VariationType
        {
            get
            {
                if (this.MenuItemVariationsShowAll.IsChecked)
                {
                    return MediaViewerItemList.VariationTypeEnum.ALL;
                }
                else if (this.MenuItemVariationsShowName.IsChecked)
                {
                    return MediaViewerItemList.VariationTypeEnum.SAME_NAME;
                }
                else
                {
                    return MediaViewerItemList.VariationTypeEnum.NONE;
                }
            }

            set
            {
                this.MenuItemVariationsShowAll.IsChecked = false;
                this.MenuItemVariationsShowName.IsChecked = false;
                this.MenuItemVariationsShowNone.IsChecked = false;

                switch (value)
                {
                    case MediaViewerItemList.VariationTypeEnum.ALL:
                        this.MenuItemVariationsShowAll.IsChecked = true;
                        break;

                    case MediaViewerItemList.VariationTypeEnum.SAME_NAME:
                        this.MenuItemVariationsShowName.IsChecked = true;
                        break;

                    case MediaViewerItemList.VariationTypeEnum.NONE:
                        this.MenuItemVariationsShowNone.IsChecked = true;
                        break;
                }
            }
        }

        public bool AnimateRandom
        {
            get
            {
                return this.MenuItemAnimateRandom.IsChecked;
            }

            set
            {
                this.MenuItemAnimateRandom.IsChecked = value;
            }
        }

        public bool IsAutoRandomVideoJump
        {
            get
            {
                return this.MenuItemRandomVideoJumpAuto.IsChecked;
            }

            set
            {
                this.MenuItemRandomVideoJumpAuto.IsChecked = value;
            }
        }



        public bool IsInfo
        {
            get
            {
                return this.MenuItemInfo.IsChecked;
            }

            set
            {
                this.MenuItemInfo.IsChecked = value;
            }
        }

        public bool IsMarkedDeleted
        {
            get
            {
                return this.MenuItemMarkDeleted.IsChecked;
            }

            set
            {
                this.MenuItemMarkDeleted.IsChecked = value;
            }
        }

        public bool ShowDeleted
        {
            get
            {
                return !this.MenuItemShowDeleted.IsChecked;
            }

            set
            {
                this.MenuItemShowDeleted.IsChecked = !value;
            }
        }

        public bool HasMagnifier
        {
            get
            {
                return this.MenuItemMagnifier.IsChecked;
            }

            set
            {
                this.MenuItemMagnifier.IsChecked = value;
            }
        }

        public bool IsCropMode
        {
            get
            {
                return this.MenuItemCrop.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemCrop.IsChecked = value;
            }
        }

        public bool IsAspectRatioMode
        {
            get
            {
                return this.MenuItemAspectRatio.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemAspectRatio.IsChecked = value;
            }
        }

        public bool IsFullscreen
        {
            get
            {
                return this.MenuItemFullscreen.IsChecked;
            }

            set
            {
                this.MenuItemFullscreen.IsChecked = value;
            }
        }

        public bool IsFlipMode
        {
            get
            {
                return this.MenuItemFlip.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemFlip.IsChecked = value;
            }
        }

        public bool IsClipMode
        {
            get
            {
                return this.MenuItemClip.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemClip.IsChecked = value;
            }
        }

        public bool IsLevelsMode
        {
            get
            {
                return this.MenuItemLevels.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemLevels.IsChecked = value;
            }
        }

        public bool IsZoomMode
        {
            get
            {
                return this.MenuItemZoom.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemZoom.IsChecked = value;
            }
        }

        public bool IsCropRotateMode
        {
            get
            {
                return this.MenuItemCropRotate.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemCropRotate.IsChecked = value;
            }
        }

        public bool IsOrientationMode
        {
            get
            {
                return this.MenuItemOrientate.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemOrientate.IsChecked = value;
            }
        }

        public bool IsNavigateMode
        {
            get
            {
                foreach (MenuItem item in this.MenuItemViewerState.Items)
                    if (item.IsChecked)
                        return false;

                return true;
            }

            set
            {
                this.ResetEditState();
            }
        }

        public bool IsRotateMode
        {
            get
            {
                return this.MenuItemRotate.IsChecked;
            }

            set
            {
                this.ResetEditState();
                this.MenuItemRotate.IsChecked = value;
            }
        }

        public bool IsBookmarked
        {
            get
            {
                return this.MenuItemBookmarked.IsChecked;
            }

            set
            {
                this.MenuItemBookmarked.IsChecked = value;
            }
        }

        public bool IsAutoSave
        {
            get
            {
                return this.MenuItemAutoSave.IsChecked;
            }

            set
            {
                this.MenuItemAutoSave.IsChecked = value;
            }
        }

        public bool IsPause
        {
            get
            {
                return this.MenuItemSPause.IsChecked;
            }

            set
            {
                this.MenuItemSPause.IsChecked = value;
            }
        }

        public bool IsNavigationBar
        {
            get
            {
                return this.MenuItemShowNavigationBar.IsChecked;
            }

            set
            {
                this.MenuItemShowNavigationBar.IsChecked = value;
            }
        }

        public MediaViewerContextMenu()
        {
            InitializeComponent();
            this.SelectedPlayer = UserControls.Video.VideoPlayer.Player.MediaElement;
            this.MenuItemVariationsShowName.IsChecked = true;
            this.CopyMenuItem.PasteAction += new EventHandler<UserControls.ThumbListContainer.CopyMenuItem.CopyPropertiesArgs>(CopyMenuItem_PasteAction);
            this.CopyMenuItem.CopyAction += new EventHandler<UserControls.ThumbListContainer.CopyMenuItem.CopyPropertiesArgs>(CopyMenuItem_CopyAction);
        }

        public IMediaViewer MediaViewer
        {
            set
            {
                this.CategorizeMenuItem.MediaItemList = new List<MediaItem>() { value.VisibleMediaItem };
                this.CopyMenuItem.MediaItemList = this.CategorizeMenuItem.MediaItemList;
                this.mediaViewer = value;
            }

            get
            {
                return this.mediaViewer;
            }
        }

        public void Copy(List<MediaItem> list)
        {
            this.CopyMenuItem.Copy(list);
        }

        void CopyMenuItem_CopyAction(object sender, UserControls.ThumbListContainer.CopyMenuItem.CopyPropertiesArgs e)
        {
            if (e.CopiedCategories != null && e.CopiedCategories.Count > 0)
            {
                this.CategoriesCopied.Invoke(this,
                    new MediaItemArg() { CategoryList = MediaBrowserContext.CopyItemProperties.Categories });
            }

            this.mediaViewer.InfoTextToolTip = "Kopiert: " + e.Message;
        }

        public void Paste(List<MediaItem> list)
        {
            this.CopyMenuItem.Paste(list);
        }

        void CopyMenuItem_PasteAction(object sender, UserControls.ThumbListContainer.CopyMenuItem.CopyPropertiesArgs e)
        {
            if (e.MustRedraw)
                this.mediaViewer.Redraw();

            this.mediaViewer.InfoTextToolTip = "Eingefügt: " + e.Message;
        }

        private void MenuItemSave_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.Save();
        }

        private void MenuItemReset_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.Reset();
            this.mediaViewer.InfoTextToolTip = "Zurückgesetzt";
        }

        private void MenuItemSetStartPosition_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.SetStartPosition();
        }

        private void MenuItemSetStoppPosition_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.SetStopPosition();
        }

        private void MenuItemRemoveStartPosition_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.RemoveStartPosition();
        }

        private void MenuItemRemoveStopPosition_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.RemoveStopPosition();
        }

        private void MenuItemJumpStartPosition_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.JumpToStartPosition();
        }

        private void MenuItemJumpStopPosition_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.JumpToStopPosition();
        }

        private void MenuItem_Stopp2Start_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.Stopp2Start();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            MenuItemRandomVideoSeparator.Visibility = MenuItemRandomVideoJumpAuto.Visibility = MenuItemRandomVideoJump.Visibility = MenuItemEditVideo.Visibility = (this.mediaViewer.VisibleMediaItem is MediaItemVideo ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed);
            MenuItemSelectVideoPlayer.Visibility = MenuItemEditVideo.Visibility;
            MenuItemVideoAudioTrack.Visibility = MenuItemEditVideo.Visibility;
            MenuItemVideoSubtitleTrack.Visibility = MenuItemEditVideo.Visibility;
            //  MenuItemAvisynthVideo.Visibility = MenuItemEditVideo.Visibility;
            MenuItemSelectBitmapScalingMode.Visibility = (this.mediaViewer.VisibleMediaItem is MediaItemBitmap ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed);
            this.CheckBitmapScalingMode();
            // MenuItemEditRgb.Visibility = (this.mediaViewer.VisibleMediaItem is MediaItemRGB ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed);
        }

        private void MenuItemSPause_Click(object sender, RoutedEventArgs e)
        {
            if (MenuItemSPause.IsChecked)
            {
                this.mediaViewer.Pause();
            }
            else
            {
                this.mediaViewer.Play();
            }
        }

        private void SelectPlayerVideoLan_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedPlayer = UserControls.Video.VideoPlayer.Player.VideoLan;
            this.SelectPlayer();
        }

        private void SelectPlayerWpfMediaElement_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedPlayer = UserControls.Video.VideoPlayer.Player.MediaElement;
            this.SelectPlayer();
        }

        private void SelectPlayerWpfMediaKit_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedPlayer = UserControls.Video.VideoPlayer.Player.WpfMediaKit;
            this.SelectPlayer();
        }

        private void SelectPlayernVlc_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedPlayer = UserControls.Video.VideoPlayer.Player.nVlc;
            this.SelectPlayer();
        }

        private void SelectPlayerMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedPlayer = UserControls.Video.VideoPlayer.Player.Multiplayer;
            this.SelectPlayer();
        }

        private void MenuItemMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            if (this.SelectedVideoPlayerChanged != null)
            {
                this.SelectedVideoPlayerChanged.Invoke(null, EventArgs.Empty);
            }
        }

        public void SelectPlayer(UserControls.Video.VideoPlayer.Player player)
        {
            this.SelectedPlayer = player;
            if (this.SelectedPlayer != UserControls.Video.VideoPlayer.Player.Multiplayer)
            {
                MediaBrowserContext.SelectedVideoPlayer = this.SelectedPlayer.ToString();     
            }

            if (this.SelectedVideoPlayerChanged != null)
            {
                this.SelectedVideoPlayerChanged.Invoke(this, EventArgs.Empty);
            }
        }

        private void SelectPlayer()
        {
            this.SelectPlayer(this.SelectedPlayer);
        }

        private void MenuItemSlideshow_Click(object sender, RoutedEventArgs e)
        {
            if (this.MenuItemSlideshow.IsChecked)
                this.MenuItemAnimateSlideshow.IsChecked = false;

            this.mediaViewer.IsSlideShow = this.MenuItemSlideshow.IsChecked;
        }

        public bool IsSlideShow
        {
            get
            {
                return this.MenuItemSlideshow.IsChecked;
            }

            set
            {
                this.MenuItemSlideshow.IsChecked = value;
                if (this.MenuItemSlideshow.IsChecked)
                    this.MenuItemAnimateSlideshow.IsChecked = false;
            }
        }

        private void MenuItemInfo_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ToggleInfoText();
        }

        private void MenuItemBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ToggleBookmark();
        }

        private void MenuItemMarkDeleted_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ToggleMarkDeleted();
        }

        private void MenuItemShowDeleted_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ShowDeleted = !this.MenuItemShowDeleted.IsChecked;
        }

        private void MenuItemSaveScreenshot_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ScreenshotToDesktop();
        }

        private void MenuItemScreenshotToClipBoard_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ScreenshotToClipBoard();
        }

        private void MenuItemClippboardScreenshot_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ScreenshotToClipBoard();
        }

        private void MenuItemAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.AspectRatio);
        }

        private void MenuItemCrop_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Crop);
        }

        private void MenuItemRotate_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Rotate);
        }

        private void MenuItemCropRotate_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.CropRotate);
        }

        private void MenuItemOrientate_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Orientate);
        }

        private void MenuItemMainFocus_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.SetMainFocus();
        }

        private void MenuItemZoom_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Zoom);
        }

        private void MenuItemLevels_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Levels);
        }

        private void MenuItemClip_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Clip);
        }

        private void MenuItemFlip_Click(object sender, RoutedEventArgs e)
        {
            this.SetEditState(sender, ViewerState.Flip);
        }

        private void ResetEditState()
        {
            foreach (object item in this.MenuItemViewerState.Items)
            {
                MenuItem menuItem = item as MenuItem;
                if (menuItem != null && menuItem.IsCheckable)
                {
                    menuItem.IsChecked = false;
                }
            }
        }

        private void SetEditState(object sender, ViewerState viewerState)
        {
            bool on = false;

            foreach (object item in this.MenuItemViewerState.Items)
            {
                MenuItem menuItem = item as MenuItem;

                if (menuItem != null && menuItem.IsCheckable)
                {
                    menuItem.IsChecked = menuItem == sender ? menuItem.IsChecked : false;
                    on = on || menuItem.IsChecked;
                }
            }

            if (this.ViewerStateChanged != null)
                this.ViewerStateChanged.Invoke(this, new ViewerStateArgs() { ViewerState = viewerState, On = on });
        }

        private void MenuItemMagnifier_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.HasMagnifier = MenuItemMagnifier.IsChecked;
        }

        private void MenuItemResetDefault_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ResetDefaultCurrent();
        }

        private void MenuItemResetDefaultAll_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.ResetDefaultAll();
        }

        //Fant
        private void BitmapScalingModeHighQuality_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.UserControls.RgbImage.ImageControl.DefaultBitmapScalingMode = BitmapScalingMode.Fant;
            this.mediaViewer.SetBitmapScalingMode();
            this.CheckBitmapScalingMode();
        }

        //Linear
        private void BitmapScalingModeLowQuality_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.UserControls.RgbImage.ImageControl.DefaultBitmapScalingMode = BitmapScalingMode.Linear;
            this.mediaViewer.SetBitmapScalingMode();
            this.CheckBitmapScalingMode();
        }

        private void BitmapScalingModeNearestNeighbor_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.UserControls.RgbImage.ImageControl.DefaultBitmapScalingMode = BitmapScalingMode.NearestNeighbor;
            this.mediaViewer.SetBitmapScalingMode();
            this.CheckBitmapScalingMode();
        }

        private void BitmapScalingModeUnspecified_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.UserControls.RgbImage.ImageControl.DefaultBitmapScalingMode = BitmapScalingMode.Unspecified;
            this.mediaViewer.SetBitmapScalingMode();
            this.CheckBitmapScalingMode();
        }

        private void CheckBitmapScalingMode()
        {
            foreach (MenuItem item in this.MenuItemSelectBitmapScalingMode.Items)
            {
                item.IsChecked = false;
            }

            GetBitmapScalingMode().IsChecked = true;
        }

        private MenuItem GetBitmapScalingMode()
        {
            switch (MediaBrowserWPF.UserControls.RgbImage.ImageControl.DefaultBitmapScalingMode)
            {
                case BitmapScalingMode.HighQuality:
                    return this.BitmapScalingModeHighQuality;
                case BitmapScalingMode.LowQuality:
                    return this.BitmapScalingModeLowQuality;
                case BitmapScalingMode.NearestNeighbor:
                    return this.BitmapScalingModeNearestNeighbor;
                case BitmapScalingMode.Unspecified:
                    return this.BitmapScalingModeUnspecified;
                default:
                    return this.BitmapScalingModeHighQuality;

            }
        }

        private void MenuItemPanoramaView_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.EffectPanoramaAnimation();
        }

        private void MenuItemJumpView_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.EffectJumpAnimation();
        }

        private void MenuItemRotateAnimation_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.EffectRotateAnimation();
        }

        private void MenuItemSkewAnimation_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.EffectSkewAnimation();
        }

        private void MenuItemZoomInAnimation_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.EffectZoomInAnimation();
        }

        private void MenuItemAnimateSlideshow_Click(object sender, RoutedEventArgs e)
        {
            if (this.MenuItemAnimateSlideshow.IsChecked)
            {
                this.MenuItemSlideshow.IsChecked = false;
                this.mediaViewer.IsSlideShow = false;
            }

            this.mediaViewer.AnimateSlideshow();
        }

        private void MenuItemNextFrame_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.NextFrame();
        }

        private void MenuItemShowNavigationBar_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.IsNavigationBar = this.MenuItemShowNavigationBar.IsChecked;
        }

        private void MenuItemAvisynthSplit4Second1_Click(object sender, RoutedEventArgs e)
        {
            this.SetAvisSynthLayer(
                AviSythSplit4.Replace("[step]", ((int)(this.mediaViewer.VisibleMediaItem.Fps)).ToString()));
        }

        private void MenuItemAvisynthSplit4Seconds10_Click(object sender, RoutedEventArgs e)
        {
            this.SetAvisSynthLayer(
                AviSythSplit4.Replace("[step]", ((int)(this.mediaViewer.VisibleMediaItem.Fps * 10)).ToString()));
        }

        private void MenuItemAvisynthSplit4Seconds30_Click(object sender, RoutedEventArgs e)
        {
            this.SetAvisSynthLayer(
                AviSythSplit4.Replace("[step]", ((int)(this.mediaViewer.VisibleMediaItem.Fps * 30)).ToString()));
        }

        private void MenuItemAvisynthSplit4Seconds120_Click(object sender, RoutedEventArgs e)
        {
            this.SetAvisSynthLayer(
              AviSythSplit4.Replace("[step]", ((int)(this.mediaViewer.VisibleMediaItem.Fps * 120)).ToString()));
        }

        private void MenuItemAvisynthPreview30_Click(object sender, RoutedEventArgs e)
        {
            this.SetAvisSynthLayer(@"
step=[step]
offset=[offsetFrames]
framesPS=[fps]
total=([totalFrames])-offset
video = DirectShowSource([filename], fps=framesPS, audio=true)
video = video.Trim(offset,  total-3*step)
videoOverlay = DirectShowSource([filename], fps=framesPS, audio=false)
videoOverlay = videoOverlay.Trim(offset+step,  total-2*step)
videoOverlay = videoOverlay.BicubicResize(video.width/4,video.height/4)
Overlay(video, videoOverlay, x=5, y=video.height-videoOverlay.height-5)
".Replace("[step]", ((int)(this.mediaViewer.VisibleMediaItem.Fps * (this.mediaViewer.VisibleMediaItem.Duration < 30 ? this.mediaViewer.VisibleMediaItem.Duration / 2 : 30))).ToString()));
        }

        private void MenuItemAvisynthSlow1_Click(object sender, RoutedEventArgs e)
        {
            this.SetAvisSynthLayer(@"framesPS=[fps]
DirectShowSource([filename], fps=framesPS).Trim([offsetFrames],[offsetFrames]+[totalFrames])
ConvertFPS(125).AssumeFPS(framesPS,sync_audio=true).ResampleAudio(44100)
ShowFrameNumber(x=40, y=30)
ShowTime(x=100, y=60)");
        }

        private void MenuItemAvisynthReset_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.VisibleMediaItem.AvisynthScript = String.Empty;
            this.mediaViewer.StepNext(0);
        }

        private const string AviSythSplit4 = @"
step=[step]
offset=[offsetFrames]
framesPS=[fps]
total=([totalFrames])-offset
videoL = DirectShowSource([filename], fps=framesPS, audio=false)
videoL = videoL.Trim(offset,  total-3*step)
videoR = DirectShowSource([filename], fps=framesPS, audio=false)
videoR = videoR.Trim(offset+step,  total-2*step)
videoL2 = DirectShowSource([filename], fps=framesPS, audio=false)
videoL2  = videoL2 .Trim(offset+2*step,  total-step)
videoR2 = DirectShowSource([filename], fps=framesPS, audio=true)
videoR2 = videoR2.Trim(offset+3*step,  total)
videoT = StackHorizontal(videoR, videoL)
videoB = StackHorizontal(videoR2, videoL2)
StackVertical(videoB, videoT)";

        private void SetAvisSynthLayer(string value)
        {
            string skript = value.Trim().Replace("[offsetFrames]", "0");

            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                this.mediaViewer.VisibleMediaItem.AvisynthScript = skript;
                this.mediaViewer.StepNext(0);
            }
            else
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(
                    MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder(),
                    this.mediaViewer.VisibleMediaItem.FileObject.Name + ".avs"),
                           MediaBrowser4.Utilities.ResultMedia.ReplaceAvisynth(this.mediaViewer.VisibleMediaItem, skript), Encoding.Default);
            }
        }

        private void MenuItemAvisynthSave_Click(object sender, RoutedEventArgs e)
        {
            bool noModifiers = Keyboard.Modifiers == ModifierKeys.None;
            MediaItemVideo mItem = this.mediaViewer.VisibleMediaItem as MediaItemVideo;
            if (mItem != null)
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.DefaultExt = ".avs";
                dlg.Title = "Avisynth-Datei speichern";
                dlg.Filter = "Avisynth-Datei (.avs)|*.avs";
                dlg.InitialDirectory = MediaBrowserWPF.Utilities.FilesAndFolders.DesktopExportFolder;
                dlg.FileName = this.mediaViewer.VisibleMediaItem.FileObject.Name + ".avs";
                Nullable<bool> result = dlg.ShowDialog();

                if (result == true)
                {
                    if (!String.IsNullOrEmpty(this.mediaViewer.VisibleMediaItem.AvisynthScript))
                    {
                        if (this.mediaViewer.VisibleMediaItem.AvisynthScript.Trim().Length > 0)
                            System.IO.File.WriteAllText(dlg.FileName,
                                noModifiers ? this.mediaViewer.VisibleMediaItem.AvisynthScript :
                        MediaBrowser4.Utilities.ResultMedia.ReplaceAvisynth(this.mediaViewer.VisibleMediaItem, this.mediaViewer.VisibleMediaItem.AvisynthScript), Encoding.Default);
                    }
                    else
                    {
                        foreach (MediaBrowser4.Objects.Layer layer in mItem.Layers)
                        {
                            switch (layer.Edit)
                            {
                                case "AVSY":
                                    System.IO.File.WriteAllText(dlg.FileName,
                                        noModifiers ? layer.Action :
                                         MediaBrowser4.Utilities.ResultMedia.ReplaceAvisynth(this.mediaViewer.VisibleMediaItem, layer.Action)
                                        , Encoding.Default);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void MenuItemAvisynthLoad_Click(object sender, RoutedEventArgs e)
        {
            MediaItemVideo mItem = this.mediaViewer.VisibleMediaItem as MediaItemVideo;
            if (mItem != null)
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.DefaultExt = ".avs";
                dlg.Title = "Avisynth-Datei öffnen";
                dlg.Filter = "Avisynth-Datei (.avs)|*.avs";
                dlg.InitialDirectory = MediaBrowserWPF.Utilities.FilesAndFolders.DesktopExportFolder;
                Nullable<bool> result = dlg.ShowDialog();

                if (result == true)
                {
                    this.mediaViewer.VisibleMediaItem.AvisynthScript = System.IO.File.ReadAllText(dlg.FileName, Encoding.Default);
                    this.mediaViewer.StepNext(0);
                }
            }
        }

        private void MenuItemFullscreen_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.IsFullscreen = !this.mediaViewer.IsFullscreen;
        }

        private void MenuItemVariationsShow_Checked(object sender, RoutedEventArgs e)
        {
            this.MenuItemVariationsShowAll.IsChecked = sender.Equals(this.MenuItemVariationsShowAll);
            this.MenuItemVariationsShowName.IsChecked = sender.Equals(this.MenuItemVariationsShowName);
            this.MenuItemVariationsShowNone.IsChecked = sender.Equals(this.MenuItemVariationsShowNone);

            if (this.mediaViewer != null)
                this.mediaViewer.NewVariation(null);
        }

        private void MenuItemVariationsNew_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.CreateNewVariation();
        }

        private void MenuItemVariationsNewName_Click(object sender, RoutedEventArgs e)
        {
            if (this.VariationType != MediaViewerItemList.VariationTypeEnum.ALL)
                this.VariationType = MediaViewerItemList.VariationTypeEnum.ALL;

            if ((new Dialogs.VariationDialog(this.mediaViewer, false)).ShowDialog().Value)
            {
                this.mediaViewer.InfoTextToolTip = "Neue Variante erstellt";
            }
        }

        private void MenuItemVariationsDelete_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.DeleteVariation();
        }

        private void MenuItemVariationsSetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (MediaBrowserContext.SetVariationDefault(this.mediaViewer.VisibleMediaItem))
            {
                this.mediaViewer.InfoTextToolTip = "Hauptvariante gesetzt";
                this.mediaViewer.SetInfoText();
            }
            else
            {
                this.mediaViewer.InfoTextToolTip = "Variante ist bereits Hauptvariante";
            }
        }

        private void MenuItemVariationsRename_Click(object sender, RoutedEventArgs e)
        {
            if ((new Dialogs.VariationDialog(this.mediaViewer, true)).ShowDialog().Value)
                this.mediaViewer.InfoTextToolTip = "Variante umbenannt";
        }

        private void MenuItemRandomVideoJump_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.RandomJumpVideo();
        }

        private void MenuItemRandomVideoJumpAuto_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.InitAutoRandomJumpVideo();
        }

        private void MenuItemClose_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.Close();
        }

        private void MenuItemAutoCrop_Click(object sender, RoutedEventArgs e)
        {
            this.AutoCrop();
        }

        public void AutoCrop()
        {
            this.mediaViewer.AutoCrop(this.ImageRelation);
        }

        private double ImageRelation
        {
            get
            {
                if (this.MenuItemAutoCropOptions11.IsChecked)
                    return 1.0;
                else if (this.MenuItemAutoCropOptions32.IsChecked)
                    return 3.0 / 2.0;
                else if (this.MenuItemAutoCropOptions43.IsChecked)
                    return 4.0 / 3.0;
                else if (this.MenuItemAutoCropOptions169.IsChecked)
                    return 16.0 / 9.0;

                return 0.0;
            }
        }

        private void MenuItemMenuItemAutoCropOptions_Checked(object sender, RoutedEventArgs e)
        {
            this.MenuItemAutoCropOptions11.IsChecked = this.MenuItemAutoCropOptions11 == sender;
            this.MenuItemAutoCropOptions169.IsChecked = this.MenuItemAutoCropOptions169 == sender;
            this.MenuItemAutoCropOptions32.IsChecked = this.MenuItemAutoCropOptions32 == sender;
            this.MenuItemAutoCropOptions43.IsChecked = this.MenuItemAutoCropOptions43 == sender;
        }

        private void MenuItemMenuItemAutoCropOptions_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.MenuItemAutoCropOptions11.IsChecked
                    && !this.MenuItemAutoCropOptions169.IsChecked
                    && !this.MenuItemAutoCropOptions32.IsChecked
                    && !this.MenuItemAutoCropOptions43.IsChecked)
                this.MenuItemAutoCropOptions169.IsChecked = true;
        }

        private void MenuItemVariationsVideoTimestamp_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.SaveAutoVideoTimeStamp(true);
        }

        private void MenuItemVariations_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            MenuItem variationMenu = MenuItemVariationsAll;

            List<MenuItem> itemList = new List<MenuItem>();
            foreach (object item in variationMenu.Items)
            {
                if (item is Separator)
                    break;

                if (item is MenuItem)
                    itemList.Add(item as MenuItem);
            }

            foreach (MenuItem item in itemList)
                variationMenu.Items.Remove(item);

            foreach (Variation variation in MediaBrowserContext.GetVariations(this.mediaViewer.VisibleMediaItem, true))
            {
                MenuItem newitem = new MenuItem();
                newitem.Click += new RoutedEventHandler(VariationSelected_Click);

                List<Layer> layers = MediaBrowserContext.GetLayers(variation);
                variation.InfoText = "\r\n" + (layers.Count > 0 ? String.Join("\r\n", layers.Select(x => x.ToString())) : "Keine Aktion");
                newitem.Header = variation;
         
                if (variation.ThumbJpegData != null)
                {
                    System.Windows.Controls.Image td = new System.Windows.Controls.Image();
                    td.Source = variation.Bitmap;
                    newitem.Icon = td;
                }

                variationMenu.Items.Insert(0, newitem);
            }
        }       

        void VariationSelected_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;

            if (item != null)
            {
                Variation variation = item.Header as Variation;

                if (variation != null)
                {
                    if (!this.MenuItemVariationsShowAll.IsChecked)
                    {
                        List<Variation> vList = MediaBrowserContext.GetVariations(this.mediaViewer.VisibleMediaItem);

                        vList = vList.Where(x => x.Name.Equals(
                                vList.FirstOrDefault(y => y.Id == this.mediaViewer.VisibleMediaItem.VariationIdDefault).Name
                                , StringComparison.InvariantCultureIgnoreCase)).ToList();

                        if (vList.FirstOrDefault(x => x.Name.Equals(variation.Name, StringComparison.InvariantCultureIgnoreCase)) == null)
                        {
                            this.MenuItemVariationsShowAll.IsChecked = true;
                            this.MenuItemVariationsShowName.IsChecked = false;
                            this.MenuItemVariationsShowNone.IsChecked = false;
                        }
                    }

                    if (this.mediaViewer != null)
                        this.mediaViewer.SetVariation(variation);
                }
            }
        }

        private void MenuItemConfigureApplication_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            MenuItemVideoAudioTrack.Items.Clear();
            MenuItemVideoSubtitleTrack.Items.Clear();

            foreach (TrackInfo trackInfo in this.mediaViewer.TrackInfo)
            {
                MenuItem newItem = new MenuItem();
                newItem.Click += new RoutedEventHandler(SetVideoTrack_Click);
                newItem.Header = trackInfo;
                newItem.IsCheckable = true;
                newItem.IsChecked = trackInfo.IsSelected;

                if (trackInfo.Type == TrackType.Ton)
                    MenuItemVideoAudioTrack.Items.Add(newItem);
                else if (trackInfo.Type == TrackType.Untertitel)
                    MenuItemVideoSubtitleTrack.Items.Add(newItem);
            }

            MenuItemVideoSubtitleTrack.Visibility = MenuItemVideoSubtitleTrack.Items.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            MenuItemVideoAudioTrack.Visibility = MenuItemVideoAudioTrack.Items.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        void SetVideoTrack_Click(object sender, RoutedEventArgs e)
        {
            this.mediaViewer.SetVideoTrack(((MenuItem)sender).Header as TrackInfo);
        }
    }
}
