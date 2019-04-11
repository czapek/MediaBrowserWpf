using System;
using System.Collections.Generic;
using System.Text;
using MediaProcessing;
using MediaBrowser4.Utilities;

namespace MediaBrowser4.Objects
{
    public class MediaItemVideo : MediaItem
    {
        internal MediaItemVideo()
            : base()
        {

        }

        internal MediaItemVideo(System.IO.FileInfo fileObject)
            : base(fileObject)
        {

        }

        override public System.Windows.Media.Brush MediaColor
        {
            get
            {
                if (this.IsDeleted)
                {
                    return MediaBrowserContext.DeleteBrush;
                }
                else if (this.IsFileNotFound)
                {
                    return MediaBrowserContext.FileNotFoundBrush;
                }
                else if (this.IsBookmarked)
                {
                    return MediaBrowserContext.BookmarkBrush;
                }
                else if (this.IsDublicate(DublicateCriteria.CHECKSUM))
                {
                    return MediaBrowserContext.DublicateShowBrush;
                }
                else
                {
                    return MediaBrowserContext.BackGroundDirectShowBrush;
                }
            }
        }

        public double PlayDuration
        {
            get
            {
                return this.DirectShowInfo.StopPosition > 0 ?
                        this.DirectShowInfo.StopPosition - this.DirectShowInfo.StartPosition
                        : this.Duration - this.DirectShowInfo.StartPosition;
            }
        }

        public double CroppedStartPosition
        {
            get
            {
                return this.DirectShowInfo.StartPosition > 0 ? this.DirectShowInfo.StartPosition : 0.0;
            }
        }

        public double CroppedStopPosition
        {
            get
            {
                return this.DirectShowInfo.StopPosition > 0 ? this.DirectShowInfo.StopPosition : this.Duration;
            }
        }

        public double CroppedDuration
        {
            get
            {
                return this.CroppedStopPosition - this.CroppedStartPosition;
            }
        }

        public float CroppedStopPositionRelativ
        {
            get
            {
                return (float)(this.CroppedStopPosition / this.Duration);
            }
        }

        public float CroppedStartPositionRelativ
        {
            get
            {
                return (float)(this.CroppedStartPosition / this.Duration);
            }
        }

        private DirectShowInfo directShowInfo;
        public DirectShowInfo DirectShowInfo
        {
            get
            {
                if (this.directShowInfo == null)
                    this.directShowInfo = ResultMedia.GetDirectShow(this, false);

                return this.directShowInfo;
            }
        }

        public void ReloadDirectShowInfo()
        {
            this.directShowInfo = null;
        }

        public override void GetThumbnail()
        {
            string thmFile = System.IO.Path.GetDirectoryName(this.FileObject.FullName) + "\\" +
                    System.IO.Path.GetFileNameWithoutExtension(this.FileObject.FullName) + ".THM";

            Dictionary<string, Dictionary<string, string>> exif =
                    MediaProcessing.ImageExif.GetAllTags(thmFile);

            if (exif != null)
            {
                this.MetaData = MetaDataList.GetList(exif, "MDEX");
                this.SetOrientationByNumber(MediaProcessing.ImageExif.GetExifOrientation(exif));
            }

            try
            {
                GetMediaInfo();
            }
            catch (Exception ex)
            { 
                Log.Exception(ex, this.FileObject.FullName);
            }

            System.Drawing.Bitmap sourceImage = null;

            try
            {    
                MediaProcessing.NReco.FrameGrabber nReco = new MediaProcessing.NReco.FrameGrabber(this.FileObject.FullName, this.Duration, this.Fps, this.Width, this.Height);

                sourceImage = nReco.SourceImage;
                this.Duration = nReco.Duration;
                this.Fps = nReco.Fps;
                this.Frames = nReco.Frames;
                this.Width = nReco.Width;
                this.Height = nReco.Height;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, this.FileObject.FullName);
            }

            if (sourceImage == null)
            {
                try
                {
                    using (DirectShow.FrameGrabber frameGrabber = new DirectShow.FrameGrabber(this.FileObject.FullName))
                    {
                        try
                        {
                            sourceImage = frameGrabber[frameGrabber.MediaLength > 60 ? 30 : frameGrabber.MediaLength / 2];
                        }
                        catch
                        {
                            sourceImage = frameGrabber[0];
                        }

                        this.Duration = frameGrabber.MediaLength;
                        this.Fps = frameGrabber.FrameRate;
                        this.Frames = frameGrabber.FrameCount;
                        this.Width = frameGrabber.Width;
                        this.Height = frameGrabber.Height;
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, this.FileObject.FullName);
                }
            }

            //if (this.Fps == 0 || this.Duration == 0)
            //{
            //    try
            //    {
            //        FFMpeg ffmpeg = new FFMpeg();
            //        if (ffmpeg.ApplicationExists)
            //        {
            //            string result = ffmpeg.GetInfo(this.FileObject.FullName);
            //            if (this.Fps == 0)
            //            {
            //                this.Fps = ffmpeg.ParseFPS(result);
            //            }

            //            if (this.Duration == 0)
            //            {
            //                this.Duration = ffmpeg.ParseDuration(result);
            //            }
            //            this.Frames = (int)(this.Duration * this.Fps);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Log.Exception(ex, this.FileObject.FullName);
            //    }
            //}

            //if (sourceImage == null)
            //{
            //    try
            //    {
            //        FFMpeg ffmpeg = new FFMpeg();
            //        if (ffmpeg.ApplicationExists)
            //        {
            //            sourceImage = ffmpeg.GetFrameFromVideo(
            //             this.FileObject.FullName,
            //             MediaBrowserContext.DBTempFolder + "\\fg" + DateTime.Now.Ticks, 0.0);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Log.Exception(ex, this.FileObject.FullName);
            //    }
            //}

            //if (sourceImage != null)
            //{
                //if (this.Width == 0 || this.Height == 0)
                //{
                //    this.Height = sourceImage.Height;
                //    this.Width = sourceImage.Width;
                //}

                if (sourceImage != null)
                    GetThumbnail(sourceImage);
            //}
        }

        private void GetMediaInfo()
        {
            MediaProcessing.MediaInfoLib mediaInfo
                = new MediaProcessing.MediaInfoLib(this.FileObject.FullName);

            if (mediaInfo.DataList == null || mediaInfo.DataList.Count == 0)
                return;

            if (this.MetaData == null)
                this.MetaData = new MetaDataList();

            int pos;
            string mkey, mValue;
            string mkeyName;
            mkeyName = mediaInfo.DataList[0].Trim();

            foreach (string name in mediaInfo.DataList)
            {
                pos = name.IndexOf(":");
                if (pos >= 0)
                {
                    mkey = name.Substring(0, pos).Trim();
                    mValue = name.Substring(pos + 1).Trim();

                    this.MetaData.Add(new MetaData(mkey, "MediaInfoLib " + mkeyName, mValue, "MLIB", true));
                }
                else if (name.Trim().Length > 0)
                {
                    mkeyName = name.Trim();
                }
            }


            if (mediaInfo.PlayTime.Length > 0)
            {
                try
                {
                    this.Duration = (Convert.ToDouble(mediaInfo.PlayTime) / 1000.0);
                }
                catch { }
            }

            if (mediaInfo.FrameRate.Length > 0)
            {
                try
                {
                    this.Fps = Convert.ToDouble(mediaInfo.FrameRate);
                }
                catch { }
            }

            if (mediaInfo.FrameCount.Length > 0)
            {
                try
                {
                    this.Frames = Convert.ToInt32(mediaInfo.FrameCount);
                }
                catch { }
            }


            if (mediaInfo.Height.Length > 0)
            {
                try
                {
                    this.Height = Convert.ToInt32(mediaInfo.Height);
                }
                catch { }
            }

            if (mediaInfo.Width.Length > 0)
            {
                try
                {
                    this.Width = Convert.ToInt32(mediaInfo.Width);
                }
                catch { }
            }

            if (this.Frames == 0 && this.Fps != 0 && this.Duration != 0)
            {
                this.Frames = (int)(this.Fps * this.Duration);
            }

            if (this.Frames != 0 && this.Fps == 0 && this.Duration != 0)
            {
                this.Fps = ((double)this.Frames / this.Duration);
            }

            if (this.Frames != 0 && this.Fps != 0 && this.Duration == 0)
            {
                this.Duration = ((double)this.Frames / this.Fps);
            }
        }

        internal override string DBType
        {
            get { return "dsh"; }
        }
    }
}
