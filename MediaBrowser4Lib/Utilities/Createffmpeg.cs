using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using MediaBrowser4.Objects;
using System.Globalization;
using System.IO;

namespace MediaBrowser4.Utilities
{
    [DefaultPropertyAttribute("ContainerFormat")]
    public class Createffmpeg
    {
        public enum H264Quality { EXTRALOW = 28, LOW = 24, GOOD = 22, HIGH = 20, EXTRAGOOD = 18 }
        public enum XvidQuality { EXTRALOW = 10, LOW = 6, GOOD = 4, HIGH = 3, EXTRAGOOD = 2 }
        public enum VorbisQuality { EXTRALOW = 1, LOW = 3, GOOD = 5, HIGH = 8, EXTRAGOOD = 10 }
        private int index;

        public string[] PredefinedValues
        {
            get { return new string[] { "Default H264 / MP3 no resize", "Default XVid / MP3 no resize", "IPod Touch 1drG 480x320" }; }
        }

        public int DefaultPredefinedValue
        {
            get { return 0; }
        }

        public void SetByPredefinedValue(int index)
        {
            if (index == 1)
            {
                deinterlace = false;
                qscale = (int)XvidQuality.GOOD;
                videoSize = new System.Drawing.Size(0, 0);
                audioBitrate = 112;
                audioCodec = "libmp3lame";
                videoCodec = "libxvid";
                containerFormat = "avi";
                extraPrameters = "";
            }
            else if (index == 0)
            {
                deinterlace = false;
                qscale = (int)H264Quality.EXTRAGOOD;
                videoSize = new System.Drawing.Size(0, 0);
                audioBitrate = 112;
                audioCodec = "libvo_aacenc";
                videoCodec = "libx264 -preset slow";
                containerFormat = "mp4";
            }
            else if (index == 2)
            {
                deinterlace = false;
                this.Padding = true;
                videoSize = new System.Drawing.Size(480, 320);
                audioCodec = "aac";
                videoCodec = "libx264";
                containerFormat = "mp4";
            }
            else if (index == 3)
            {
                deinterlace = false;
                qscale = (int)VorbisQuality.HIGH;
                videoSize = new System.Drawing.Size(0, 0);
                audioCodec = "libvorbis";
                videoCodec = "libtheora";
                containerFormat = "ogv";
            }
            else if (index == 4)
            {
                videoSize = new System.Drawing.Size(0, 0);
                audioCodec = null;
                videoCodec = null;
                containerFormat = "mkv";
            }
            else if (index == 5)
            {
                videoSize = new System.Drawing.Size(720, 576);
                audioCodec = "mp2";
                qscale = 6;
                videoCodec = "mpeg2video";
                containerFormat = "vob";
            }
            else if (index == 6)
            {
                videoSize = new System.Drawing.Size(0, 0);
                audioCodec = null;
                videoCodec = null;
                containerFormat = "image2";
            }
            else if (index == 200)
            {
                videoSize = new System.Drawing.Size(0, 0);
                audioBitrate = 112;
                audioCodec = "libmp3lame";
                videoCodec = null;
                containerFormat = "mp3";
            }

            this.index = index;
        }

        string extraPrameters = "";
        public string ExtraParameters
        {
            get { return extraPrameters; }
            set { extraPrameters = value; }
        }

        string exportPath;
        public string ExportPath
        {
            get { return exportPath; }
            set { exportPath = value; }
        }

        string ffmpegPath = FindFFmpeg();
        public string FFmpegPath
        {
            get { return ffmpegPath; }
            set { ffmpegPath = value; }
        }

        string containerFormat = "avi";
        public string ContainerFormat
        {
            get { return containerFormat; }
            set { containerFormat = value; }
        }

        public bool IsPreviewDb { get; set; }

        bool padding = false;
        public bool Padding
        {
            get { return padding; }
            set { padding = value; }
        }

        bool deinterlace = false;
        public bool Deinterlace
        {
            get { return deinterlace; }
            set { deinterlace = value; }
        }

        int qscale = 5;
        public int VideoQuality
        {
            get { return qscale; }
            set { qscale = value; }
        }

        System.Drawing.Size videoSize = new System.Drawing.Size(0, 0);
        public System.Drawing.Size VideoSize
        {
            get { return videoSize; }
            set { videoSize = value; }
        }

        string videoCodec = "libxvid";
        public string VideoCodec
        {
            get { return videoCodec; }
            set { videoCodec = value; }
        }

        int audioBitrate = 0;
        public int AudioBitrate
        {
            get { return audioBitrate; }
            set { audioBitrate = value; }
        }

        string audioCodec = "libmp3lame";
        public string AudioCodec
        {
            get { return audioCodec; }
            set { audioCodec = value; }
        }

        string genre = "Video";
        public string Genre
        {
            get { return genre; }
            set { genre = value; }
        }

        string album = "%foldername%";
        public string Album
        {
            get { return album; }
            set { album = value; }
        }

        string comment = "Generated by ffmpeg and MediaBrowser4";
        public string Comment
        {
            get { return comment; }
            set { comment = value; }
        }

        string author = "%dbowner%";
        public string Author
        {
            get { return author; }
            set { author = value; }
        }

        string title = "%medianame%";
        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public int SampleLength
        {
            get;
            set;
        }

        public List<int> PreviewDbVariationIdList
        {
            get;
            set;
        }

        //http://ffmpeg.org/trac/ffmpeg/wiki/FilteringGuide     
        public string Start(List<MediaItem> itemList)
        {
            StringBuilder sb = new StringBuilder();

            if (ContainerFormat.Trim().Length == 0)
                ContainerFormat = "avi";

            foreach (MediaItem mItem in itemList)
            {
                MediaItemVideo mItemVideo = mItem as MediaItemVideo;

                if (mItemVideo == null)
                    continue;

                List<Variation> vList = MediaBrowserContext.GetVariations(mItem);

                if (!IsPreviewDb)
                    vList = vList.Where(x => x.Name.Equals(
                            vList.FirstOrDefault(y => y.Id == mItem.VariationIdDefault).Name
                            , StringComparison.InvariantCultureIgnoreCase)).ToList();

                foreach (Variation variation in vList)
                {
                    if (this.IsPreviewDb && this.PreviewDbVariationIdList != null
                        && this.PreviewDbVariationIdList.Contains(variation.Id))
                        continue;

                    sb.Append("\"" + FFmpegPath + "\" ");

                    Utilities.DirectShowInfo ds = ResultMedia.GetDirectShow(variation);
                    double duration = ds.StopPosition > 0 ? ds.StopPosition - ds.StartPosition : ds.StopPosition;
                    duration = duration == 0 ? mItem.Duration : duration;
                    double startPosition = ds.StartPosition;
                    double stopPosition = ds.StopPosition;

                    if (SampleLength > 0 && duration > this.SampleLength)
                    {
                        startPosition = startPosition + (duration - this.SampleLength) / 2;
                        stopPosition = startPosition + this.SampleLength;
                    }

                    if (IsPreviewDb && duration > 8.0 && containerFormat != "image2")
                    {
                        if (startPosition == 0.0)
                            startPosition = (duration / 2) - 2.5;

                        stopPosition = startPosition + 5.0;
                    }

                    if (IsPreviewDb)
                    {
                        this.VideoQuality = (int)XvidQuality.LOW;
                    }

                    if (startPosition > 0)
                    {
                        sb.Append(String.Format("-ss {0} ", startPosition.ToString().Replace(',', '.')));
                    }

                    sb.Append("-i \"" + mItem.FileObject.FullName + "\" "
                        + (videoCodec != null && videoCodec.Trim() == "libtheora" ? String.Empty : " -f " + (containerFormat == "mkv" ? "matroska" : ContainerFormat) + " "));

                    if (stopPosition > 0)
                    {
                        sb.Append(String.Format("-t {0} ", (stopPosition - startPosition).ToString().Replace(',', '.')));
                    }

                    if (containerFormat == "mp3")
                    {
                        sb.Append(" -c:a " + audioCodec.Trim() + (this.AudioBitrate == 0 ? String.Empty : " -ab " + AudioBitrate + "k"));
                    }
                    else if (containerFormat != "image2")
                    {
                        if (videoCodec == null)
                        {
                            sb.Append(" -c:v copy");
                        }
                        else if (videoCodec.Trim().Length > 0)
                        {
                            sb.Append(" -c:v " + videoCodec.Trim() + (videoCodec.Trim() == "libx264" ? " -crf " : " -q:v ") + qscale);
                        }
                        else
                        {
                            sb.Append(" -c:v copy");
                        }

                        if (audioCodec == null)
                        {
                            sb.Append(" -c:a copy");
                        }
                        else if (audioCodec.Trim().Length > 0)
                        {
                            if (this.index == 2)
                            {
                                sb.Append(" -acodec aac -ac 2 -ar 48000 -ab 192k -strict experimental");
                            }
                            else
                            {
                                sb.Append(" -c:a " + audioCodec.Trim() + (this.AudioBitrate == 0 ? String.Empty : " -ab " + AudioBitrate + "k"));
                            }
                        }
                        else
                        {
                            sb.Append(" -c:a copy");
                        }
                    }

                    List<string> videoFilter = new List<string>();

                    switch (mItem.Orientation)
                    {
                        case MediaItem.MediaOrientation.LEFTisBOTTOM:
                            videoFilter.Add("transpose=2");
                            break;

                        case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                            videoFilter.Add("transpose=1");
                            break;

                        case MediaItem.MediaOrientation.TOPisBOTTOM:
                            videoFilter.Add("vflip,hflip");
                            break;
                    }

                    MediaBrowser4.Objects.Layer layer = variation.Layers.FirstOrDefault(x => x.Edit == "CROP");

                    if (layer != null)
                    {
                        int left = (int)((double)mItem.WidthOrientation * Convert.ToDouble(layer.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat) / 100);
                        int top = (int)((double)mItem.HeightOrientation * Convert.ToDouble(layer.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat) / 100);
                        int right = (int)((double)mItem.WidthOrientation * Convert.ToDouble(layer.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat) / 100);
                        int bottom = (int)((double)mItem.HeightOrientation * Convert.ToDouble(layer.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat) / 100);

                        videoFilter.Add(String.Format("crop={0}:{1}:{2}:{3}",
                            mItem.WidthOrientation - left - right,
                            mItem.HeightOrientation - top - bottom, left, top));
                    }

                    int width = mItem.CroppedSize == null ? mItem.WidthOrientation : mItem.CroppedSize.Value.Width;
                    int height = mItem.CroppedSize == null ? mItem.HeightOrientation : mItem.CroppedSize.Value.Height;
                    if (videoSize.Width > 10 && videoSize.Height > 10)
                    {
                        int videoResultWidth, videoResultHeight;

                        double relFrom = (double)width / (double)height;
                        double relTo = (double)videoSize.Width / (double)videoSize.Height;

                        if (width > videoSize.Width || height > videoSize.Height)
                        {
                            if (relFrom > relTo)
                            {
                                videoResultHeight = (int)(videoSize.Width / relFrom);
                                videoResultWidth = videoSize.Width;

                                if (videoResultHeight % 2 > 0)
                                    videoResultHeight++;

                                videoFilter.Add("scale=" + videoSize.Width + ":" + videoResultHeight);
                            }
                            else
                            {
                                videoResultWidth = (int)(videoSize.Height * relFrom);
                                videoResultHeight = videoSize.Height;

                                if (videoResultWidth % 2 > 0)
                                    videoResultWidth++;

                                videoFilter.Add("scale=" + videoResultWidth + ":" + videoSize.Height);
                            }

                            if (this.Padding)
                            {
                                if (mItem.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM || mItem.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                                    videoFilter.Add("pad=" + videoSize.Width + ":" + videoSize.Height + ":" + (videoSize.Width - videoResultWidth) / 2 + ":" + (videoSize.Height - videoResultHeight) / 2);
                                else
                                    videoFilter.Add("pad=" + videoSize.Height + ":" + videoSize.Width + ":" + (videoSize.Height - videoResultHeight) / 2 + ":" + (videoSize.Width - videoResultWidth) / 2);
                            }
                        }
                    }

                    if (videoFilter.Count > 0)
                    {
                        sb.Append(" -vf " + String.Join(",", videoFilter));
                    }

                    if (deinterlace)
                    {
                        sb.Append(" -deinterlace");
                    }

                    if (extraPrameters.Trim().Length > 0)
                    {
                        sb.Append(" " + extraPrameters.Trim());
                    }

                    if (containerFormat != "image2")
                    {

                        string description = "";

                        if (Title.Trim().Length > 0)
                        {
                            description += (" -metadata title=\"" + Title + "\"");
                        }

                        if (Genre.Trim().Length > 0)
                        {
                            description += (" -metadata genre=\"" + Genre + "\"");
                        }

                        if (Album.Trim().Length > 0)
                        {
                            description += (" -metadata album=\"" + Album + "\"");
                        }

                        if (Author.Trim().Length > 0)
                        {
                            description += (" -metadata  author=\"" + Author + "\"");
                        }

                        if (Comment.Trim().Length > 0)
                        {
                            description += (" -metadata comment=\"" + Comment + "\"");
                        }

                        description = Replace(mItem, description);
                        sb.Append(description);

                        sb.AppendLine(" -" + (IsPreviewDb ? "n" : "y") + " \"" + ExportPath + "\\"
                            + (IsPreviewDb ? variation.Id + ".avi.prv" : System.IO.Path.GetFileNameWithoutExtension(mItem.Filename)
                            + (vList.Count == 1 ? String.Empty : " v" + variation.Position) + "."
                             + (containerFormat == "vob" ? "mpg" : ContainerFormat)) + "\"");
                    }
                    else
                    {
                        string previewFolder = ExportPath + "\\" + variation.Id + ".avi.prv";
                        if (IsPreviewDb && containerFormat == "image2")
                        {
                            if (!System.IO.Directory.Exists(previewFolder))
                                System.IO.Directory.CreateDirectory(previewFolder);
                        }
                        sb.AppendLine(" -vf " + (IsPreviewDb && containerFormat == "image2" ? "scale=200:-1," : String.Empty)
                            + "fps=fps=1/" + (duration / 24.5).ToString().Replace(",", ".") + " \"" + ExportPath + "\\"
                            + (IsPreviewDb && containerFormat == "image2" ? variation.Id + ".avi.prv\\" : String.Empty)
                            + (IsPreviewDb ? variation.Id + "_%%03d.png" : System.IO.Path.GetFileNameWithoutExtension(mItem.Filename)
                            + (vList.Count == 1 ? " " : " v" + variation.Position + " ") + "%%03d.png\""));
                    }
                }
            }

            if (sb.Length > 0)
            {
                if (!System.IO.Directory.Exists(ExportPath))
                    System.IO.Directory.CreateDirectory(ExportPath);

                if (sb.ToString().Contains("-tune film"))
                {
                    sb.AppendLine(sb.ToString().Replace("-tune film", "-tune grain").Replace(".mp4\"\r\n", "_grain.mp4\"\r\n"));
                }

                string skriptName = String.Format("ffmpeg_{0}_{1}.bat", (IsPreviewDb ? "PreviewDb" : this.VideoCodec), VideoSize.Width > 0 ? VideoSize.Width + "x" + VideoSize.Height : "NoResize");

                Encoding winDosCodePage = Encoding.GetEncoding(850);
                if (System.IO.Directory.Exists(ExportPath))
                    System.IO.File.WriteAllText(ExportPath + "\\" + skriptName, sb.ToString(), winDosCodePage);

                return ExportPath + "\\" + skriptName;
            }
            else
            {
                return null;
            }

        }

        public static string Replace(MediaBrowser4.Objects.MediaItem mItem, string content)
        {
            if (mItem.Description == null)
                MediaBrowserContext.GetDescription(mItem);

            return content.Replace("%medianame%", System.IO.Path.GetFileNameWithoutExtension(mItem.Filename))
                .Replace("%dbowner%", MediaBrowserContext.GetDBProperty("DBOwner"))
                .Replace("%description%", mItem.Description == null ? String.Empty : mItem.Description.Replace("\r\n", " ").Replace("\r", " ").Replace("\"", "'"))
                .Replace("%foldername%", System.IO.Path.GetFileName(mItem.FileObject.DirectoryName));
        }

        public static string FindApplication(string dirStartsWith, string contains)
        {
            string app = System.IO.Path.Combine(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dirStartsWith), contains);

            if (File.Exists(app))
                return app;

            foreach (string look in System.IO.Directory.GetDirectories(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), dirStartsWith + "*"))
                {
                    if (System.IO.File.Exists(look + "\\" + contains))
                        return look + "\\" + contains;
                    else if (System.IO.File.Exists(look + "\\bin\\" + contains))
                        return look + "\\bin\\" + contains;
                }
            return null;
        }

        public static string FindFFmpeg()
        {
            string result = FindApplication("ffmpeg", "ffmpeg.exe");
            if (result == null)
                result = "ffmpeg.exe";

            return result;
        }
    }
}
