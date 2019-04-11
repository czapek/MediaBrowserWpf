using NReco.VideoConverter;
using NReco.VideoInfo;
using System.Drawing;
using System.IO;
using System.Linq;
using static NReco.VideoInfo.MediaInfo;

namespace MediaProcessing.NReco
{
    //http://www.nrecosite.com/video_converter_net.aspx
    public class FrameGrabber
    {
        public System.Drawing.Bitmap SourceImage { get; private set; }
        public double Duration { get; private set; }
        public double Fps { get; private set; }
        public int Frames { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }


        public FrameGrabber(string pathToVideoFile, double duration, double fps, int width, int height)
        {
            this.Height = height;
            this.Width = width;
            this.Fps = fps;
            this.Duration = duration;

            //ffprobe nur falls nötig bemühen
            if (duration == 0 || this.Fps == 0 || this.Width == 0 || this.Height == 0)
            {
                FFProbe ffProbe = new FFProbe();
                MediaInfo videoInfo = ffProbe.GetMediaInfo(pathToVideoFile);

                StreamInfo videoStream = videoInfo.Streams.FirstOrDefault(x => x.CodecType.ToLower() == "video");

                if (videoStream != null)
                {
                    this.Duration = videoInfo.Duration.TotalSeconds;
                    this.Width = videoStream.Width;
                    this.Height = videoStream.Height;
                    this.Frames = (int)(videoStream.FrameRate * this.Duration);
                    this.Fps = videoStream.FrameRate;
                }
            }

            float thumbPos = this.Duration > 600 ? 300 : (float)(this.Duration / 2);

            using (MemoryStream outputS = new MemoryStream())
            {
                FFMpegConverter ffMpeg = new FFMpegConverter();
                ffMpeg.GetVideoThumbnail(pathToVideoFile, outputS, thumbPos);

                outputS.Position = 0;
                this.SourceImage = new Bitmap(outputS);
            }
        }
    }
}
