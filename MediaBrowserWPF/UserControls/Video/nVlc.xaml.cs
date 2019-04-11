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
using Declarations;
using Declarations.Media;
using Implementation;
using Declarations.Events;
using MediaBrowser4;

namespace MediaBrowserWPF.UserControls.Video
{
    /// <summary>
    /// Interaktionslogik für nVlc.xaml
    /// </summary>
    public partial class nVlc : UserControl, IVideoControl
    {
        public event EventHandler EndReached;
        public event EventHandler PositionChanged;

        IMediaPlayerFactory m_factory = null;
        Declarations.Players.IDiskPlayer m_player = null;
        IMedia m_media;

        public nVlc()
        {
            //InitializeComponent();

            //System.Windows.Forms.Panel p = new System.Windows.Forms.Panel();
            //p.BackColor = System.Drawing.Color.Black;
            //windowsFormsHost1.Child = p;
            //windowsFormsHost1.IsEnabled = false;


            //var args = new string[]
            //{
            //    "-I", 
            //    "dumy",  
            //    "--ignore-config", 
            //    "--no-video-title-show",
            //    "--ffmpeg-hw",
            //    "--no-osd",
            //    "--disable-screensaver",
            //    "--plugin-path=./plugins" 
            //};
            //m_factory = new MediaPlayerFactory(args);

            //m_player = m_factory.CreatePlayer<Declarations.Players.IDiskPlayer>();

            //this.DataContext = m_player;

            //m_player.Events.PlayerPositionChanged += new EventHandler<MediaPlayerPositionChanged>(Events_PlayerPositionChanged);
            //m_player.Events.MediaEnded += new EventHandler(Events_MediaEnded);

            //m_player.WindowHandle = p.Handle;
        }

        void Events_MediaEnded(object sender, EventArgs e)
        {
            if (this.IsLoop)
            {
                m_player.Stop();
                m_player.Play();
            }

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (this.EndReached != null)
                    this.EndReached.Invoke(this, EventArgs.Empty);
            }));
        }

        void Events_PlayerPositionChanged(object sender, MediaPlayerPositionChanged e)
        {
            if (this.PositionChanged != null)
                this.PositionChanged.Invoke(this, EventArgs.Empty);
        }

  

        public double Speedratio
        {
            get
            {
                return this.m_player.PlaybackRate;
            }

            set
            {
                this.m_player.PlaybackRate = (float)value;
            }
        }

        public void Play()
        {
            m_player.Play();
        }

        public void Stop()
        {
            m_player.Stop();
        }

        public bool IsPlaying
        {
            get { return m_player.IsPlaying; }
        }

        public bool IsLoop
        {
            get;
            set;
        }

        public void SetVideoTrack(TrackInfo trackInfo)
        {
            if (m_player != null && m_player.CurrentMedia != null)
            {
                if (trackInfo.Type == TrackType.Ton)
                    m_player.AudioTrack = trackInfo.Id;
                else if (trackInfo.Type == TrackType.Untertitel)
                    m_player.SubTitle = trackInfo.Id;
            }
        }

        public List<TrackInfo> TrackInfo
        {
            get
            {
                List<TrackInfo> trackInfoList = new List<TrackInfo>();
                if (m_player != null && m_player.CurrentMedia != null)
                {
                    foreach (TrackDescription track in m_player.SubtitleTracksInfo)
                    {
                        TrackInfo trackInfo = new TrackInfo();
                        trackInfo.Name = track.Name;
                        trackInfo.Id = track.Id;
                        trackInfo.Type = TrackType.Untertitel;
                        trackInfo.IsSelected = m_player.SubTitle == trackInfo.Id;
                        trackInfoList.Add(trackInfo);
                    }

                    foreach (TrackDescription track in m_player.AudioTracksInfo)
                    {
                        TrackInfo trackInfo = new TrackInfo();
                        trackInfo.Name = track.Name;
                        trackInfo.Id = track.Id;
                        trackInfo.Type = TrackType.Ton;
                        trackInfo.IsSelected = m_player.AudioTrack == trackInfo.Id;
                        trackInfoList.Add(trackInfo);
                    }
                }

                return trackInfoList;
            }
        }

        public void Pause()
        {
            m_player.Pause();
        }

        public void NextFrame()
        {
            m_player.NextFrame();
        }

        public long TimeMilliseconds
        {
            get
            {
                return m_player.Time;
            }
            set
            {
                m_player.Time = value;
                this.Events_PlayerPositionChanged(this, null);
            }
        }

        public float Position
        {
            get
            {
                return m_player.Position;
            }
            set
            {
                m_player.Position = (float)value;
                this.Events_PlayerPositionChanged(this, null);
            }
        }

        public string Source
        {
            set
            {
                if (value == null)
                {
                    m_player.Stop();
                    return;
                }

                //var mediaList = new List<string>()
                //{
                //     value
                //};

                //IMediaList list = m_factory.CreateMediaList<IMediaList>(mediaList);
                //Declarations.Players.IMediaListPlayer listPlayer = m_factory.CreateMediaListPlayer<Declarations.Players.IMediaListPlayer>(list);
                //listPlayer.PlaybackMode = PlaybackMode.Loop;
                //listPlayer.Play();

                m_media = m_factory.CreateMedia<IMediaFromFile>(value);
                m_player.Open(m_media);
                m_media.Parse(true);
                m_player.Play();
            }
        }

        public System.Drawing.Bitmap TakeSnapshot()
        {
            System.Drawing.Bitmap bmp = null;

            try
            {
                string path = MediaBrowserContext.DBTempFolder + "\\Vlc" + DateTime.Now.Ticks;
                this.m_player.TakeSnapShot(0, path);

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
                return m_player.Volume;
            }

            set
            {
                //m_player.Volume = value;
            }
        }

        public void Dispose()
        {
            m_player.Stop();
            m_factory.Dispose();
            m_media.Dispose();
            m_player.Dispose();
        }

        public void Reset()
        {

        }

        public void KeyTabDown()
        {
    
        }
    }
}
