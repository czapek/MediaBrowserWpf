using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using MediaBrowserWPF.UserControls.Video;

namespace MediaBrowserWPF.Viewer
{
    public interface IMediaViewer
    {
        void StepNext(int i);
        bool ShowDeleted { get; set; }
        bool HasMagnifier { get; set; }
        bool IsNavigationBar { get; set; }
        bool IsFullscreen { get; set; }
        string InfoTextToolTip { set; }
        void AutoCrop(double relation);
        void SetMainFocus();
        void Save();
        void Reset();
        void Redraw();
        void SetInfoText();
        void RenameVariation(Variation variation);
        void NewVariation(Variation variation);
        void SetVariation(Variation variation);
        void DeleteVariation();
        void RandomJumpVideo();
        void InitAutoRandomJumpVideo();
        void CreateNewVariation();
        void ResetDefaultCurrent();
        void ResetDefaultAll();
        void JumpToStartPosition();
        void JumpToStopPosition();
        void SetStartPosition();
        void SetStopPosition();
        void RemoveStartPosition();
        void RemoveStopPosition();
        void Stopp2Start();
        void Pause();
        void Play();
        void Close();
        void RefreshThumbnail();
        void ToggleInfoText();
        void ToggleMarkDeleted();
        void ToggleBookmark();
        void ScreenshotToDesktop();
        void ScreenshotToClipBoard();
        void SetBitmapScalingMode();
        void EffectPanoramaAnimation();
        void EffectJumpAnimation();
        void EffectRotateAnimation();
        void EffectSkewAnimation();
        void EffectZoomInAnimation();
        void AnimateSlideshow();
        void NextFrame();
        void SaveAutoVideoTimeStamp(bool force);
        MediaItem VisibleMediaItem { get; }
        bool IsSlideShow { get; set; }
        List<TrackInfo> TrackInfo { get; }
        void SetVideoTrack(TrackInfo trackInfo);
    }
}
