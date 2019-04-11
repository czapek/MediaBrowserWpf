using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace MediaBrowserWPF.UserControls.Video
{
    interface IVideoControl : IDisposable
    {
        event EventHandler EndReached;
        event EventHandler PositionChanged;

        void Play();
        void VolumeMute();
        bool IsPlaying { get; }
        bool IsLoop { get; set; }
        void Pause();
        void Stop();
        void Reset();
        void NextFrame();
        void KeyTabDown();
        long TimeMilliseconds { get; set; }
        double Speedratio { get; set; }
        float Position { get; set; }
        int Volume { get; set; }
        string Source { set; }
        Bitmap TakeSnapshot();
        List<TrackInfo> TrackInfo { get; }
        void SetVideoTrack(TrackInfo trackInfo);
    }
}
