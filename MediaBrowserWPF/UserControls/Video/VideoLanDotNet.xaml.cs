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
using Vlc.DotNet.Core.Medias;
using Vlc.DotNet.Core;
using MediaBrowser4;

namespace MediaBrowserWPF.UserControls.Video
{
    /// <summary>
    /// Interaktionslogik für VideoLanDotNet.xaml
    /// </summary>
    public partial class VideoLanDotNet : UserControl, IVideoControl, IDisposable
    {
        public event EventHandler EndReached;
        public event EventHandler PositionChanged;

        public VideoLanDotNet()
        {
            //Set libvlc.dll and libvlccore.dll directory path
            VlcContext.LibVlcDllsPath = @".\";// CommonStrings.LIBVLC_DLLS_PATH_DEFAULT_VALUE_AMD64;
            //Set the vlc plugins directory path
            VlcContext.LibVlcPluginsPath = @".\plugins";// CommonStrings.PLUGINS_PATH_DEFAULT_VALUE_AMD64;

            //Set the startup options
            VlcContext.StartupOptions.IgnoreConfig = true;
            //VlcContext.StartupOptions.LogOptions.LogInFile = true;
            //VlcContext.StartupOptions.LogOptions.ShowLoggerConsole = true;
            //VlcContext.StartupOptions.LogOptions.Verbosity = VlcLogVerbosities.Debug;
            VlcContext.StartupOptions.AddOption("--no-video-title-show");
            VlcContext.StartupOptions.AddOption("--ffmpeg-hw");

            //Initialize the VlcContext
            VlcContext.Initialize();

            InitializeComponent();

            this.VideoControl.IsEnabled = false; //damit die Mouse events durchschlagen

            this.VideoControl.EndReached += new Vlc.DotNet.Core.VlcEventHandler<Vlc.DotNet.Wpf.VlcControl, EventArgs>(VideoControl_EndReached);
            this.VideoControl.PositionChanged += new Vlc.DotNet.Core.VlcEventHandler<Vlc.DotNet.Wpf.VlcControl, float>(VideoControl_PositionChanged);
        }

        public void Play()
        {
            this.VideoControl.Play();
        }

        public bool IsPlaying
        {
            get { return this.VideoControl.IsPlaying; }
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
                this.VideoControl.PlaybackMode = this.isLoop ? Vlc.DotNet.Core.Interops.Signatures.LibVlc.MediaListPlayer.PlaybackModes.Repeat : Vlc.DotNet.Core.Interops.Signatures.LibVlc.MediaListPlayer.PlaybackModes.Default;
            }
        }

        public void SetVideoTrack(TrackInfo trackInfo)
        {
            if (this.VideoControl != null && this.VideoControl.Media != null)
            {
                if (trackInfo.Type == TrackType.Ton)
                    this.VideoControl.AudioProperties.Track = trackInfo.Id;
                else if (trackInfo.Type == TrackType.Untertitel)
                    this.VideoControl.VideoProperties.CurrentSpuIndex = trackInfo.Id;
            }
        }

        public List<TrackInfo> TrackInfo
        {
            get
            {
                List<TrackInfo> trackInfoList = new List<TrackInfo>();

                if (this.VideoControl != null && this.VideoControl.Media != null)
                {
                    TrackInfo trackInfoDisabled = new TrackInfo();
                    trackInfoDisabled.Name = "Disable";
                    trackInfoDisabled.Id = -1;
                    trackInfoDisabled.Type = TrackType.Ton;
                    trackInfoDisabled.IsSelected = this.VideoControl.AudioProperties.Track < 0;
                    trackInfoList.Add(trackInfoDisabled);

                    trackInfoDisabled = new TrackInfo();
                    trackInfoDisabled.Name = "Disable";
                    trackInfoDisabled.Id = -1;
                    trackInfoDisabled.Type = TrackType.Untertitel;
                    trackInfoDisabled.IsSelected = this.VideoControl.VideoProperties.CurrentSpuIndex < 0;
                    trackInfoList.Add(trackInfoDisabled);

                    foreach (Vlc.DotNet.Core.Interops.Signatures.LibVlc.Media.MediaTrackInfo
                        track in this.VideoControl.Media.TrackInfos)
                    {
                        TrackInfo trackInfo = new TrackInfo();

                        if (track.Type == Vlc.DotNet.Core.Interops.Signatures.LibVlc.Media.TrackTypes.Audio)
                        {
                            trackInfo.Name = "Spur " + track.Id + " " + track.CodecName;
                            trackInfo.Id = track.Id;
                            trackInfo.Type = TrackType.Ton;
                            trackInfo.IsSelected = this.VideoControl.AudioProperties.Track == trackInfo.Id;  
                            trackInfoList.Add(trackInfo);
                           
                        }
                        else if (track.Type == Vlc.DotNet.Core.Interops.Signatures.LibVlc.Media.TrackTypes.Text)
                        {
                            trackInfo.Name = "Spur " + track.Id + " " + track.CodecName;
                            trackInfo.Id = track.Id;
                            trackInfo.Type = TrackType.Untertitel;
                            trackInfo.IsSelected = this.VideoControl.VideoProperties.CurrentSpuIndex == trackInfo.Id;     
                            trackInfoList.Add(trackInfo);
                        }
                    }
                }

                return trackInfoList;
            }
        }

        public double Speedratio
        {
            get
            {
                return this.VideoControl.Rate;
            }

            set
            {
                this.VideoControl.Rate = (float)value;
            }
        }

        public void Pause()
        {
            this.VideoControl.Pause();
        }

        public void Stop()
        {
            this.VideoControl.Stop();
        }

        public void NextFrame()
        {
            this.VideoControl.NextFrame();
        }

        public long TimeMilliseconds
        {
            get
            {
                if (this.VideoControl.Time.Ticks == 0)
                {
                    return (long)(((double)this.VideoControl.Duration.Ticks * this.VideoControl.Position)/10000);
                }
                else
                {
                    return this.VideoControl.Time.Ticks / 10000;
                }
            }

            set
            {
                this.VideoControl.Time = new TimeSpan(value * 10000);
            }
        }

        public float Position
        {
            get
            {
                return this.VideoControl.Position;
            }

            set
            {
                this.VideoControl.Position = value;
            }
        }

        public string Source
        {
            set
            {
                if (value == null)
                {
                    VideoControl.Stop();
                    return;
                }

                this.VideoControl.Media = new PathMedia(value);
            }
        }

        public System.Drawing.Bitmap TakeSnapshot()
        {
            System.Drawing.Bitmap bmp = null;

            try
            {
                string path = MediaBrowserContext.DBTempFolder + "\\Vlc" + DateTime.Now.Ticks;
                this.VideoControl.TakeSnapshot(path, (uint)this.VideoControl.VideoBrush.ImageSource.Width,
                    (uint)this.VideoControl.VideoBrush.ImageSource.Height);

                int cnt = 0;
                while (!System.IO.File.Exists(path) && cnt < 20)
                {
                    System.Threading.Thread.Sleep(50);
                    cnt++;
                }

                if (System.IO.File.Exists(path))
                {
                    bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(path);
                }
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
                return VideoControl.AudioProperties.Volume;
            }

            set
            {
                VideoControl.AudioProperties.Volume = value;
            }
        }

        private void VideoControl_PositionChanged(Vlc.DotNet.Wpf.VlcControl sender, Vlc.DotNet.Core.VlcEventArgs<float> e)
        {
            if (this.PositionChanged != null)
                this.PositionChanged.Invoke(this, EventArgs.Empty);
        }

        private void VideoControl_EndReached(Vlc.DotNet.Wpf.VlcControl sender, Vlc.DotNet.Core.VlcEventArgs<EventArgs> e)
        {
            if (this.EndReached != null)
                this.EndReached.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            this.VideoControl.Dispose();
            VlcContext.CloseAll();
        }

        public void Reset()
        {

        }

        public void KeyTabDown()
        {
   
        }
    }
}
