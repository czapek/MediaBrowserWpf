using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Globalization;
using System.IO;
using MediaProcessing;
using MediaBrowser4.Objects;

namespace MediaBrowser4.Utilities
{
    public class ResultMedia
    {
        public static DirectShowInfo GetDirectShow(MediaBrowser4.Objects.MediaItem mItem, bool foreAviSynth)
        {
            return GetDirectShow(mItem, MediaBrowserContext.DBTempFolder + "\\play.avs", foreAviSynth);
        }
    
        public static string ReplaceAvisynth(MediaItem mItem, string avsSkript)
        {
            return avsSkript.Replace("[filename]", "\"" + mItem.FileObject.FullName + "\"")
                            .Replace("[fps]", mItem.Fps.ToString().Replace(',', '.'))
                            .Replace("[totalFrames]", mItem.Frames.ToString())
                            .Replace("[width]", mItem.Width.ToString())
                            .Replace("[height]", mItem.Height.ToString());
        }

        public static DirectShowInfo GetDirectShow(MediaBrowser4.Objects.MediaItem mItem, string path, bool foreAviSynth)
        {
            DirectShowInfo ds = new DirectShowInfo();
            ds.StartPosition = 0.0;
            ds.StopPosition = 0.0;
            ds.Filename = mItem.FileObject.FullName;

            string aviSynthScript = null;

            foreach (MediaBrowser4.Objects.Layer layer in mItem.Layers)
            {
                switch (layer.Edit)
                {
                    case "TRIM":
                        ds.StartPosition = Convert.ToDouble(layer.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                        ds.StopPosition = Convert.ToDouble(layer.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                        break;

                    case "AVSY":
                        aviSynthScript = layer.Action;
                        break;
                }
            }

            if (mItem.AvisynthScript != null)
            {
                aviSynthScript = mItem.AvisynthScript.Trim().Length > 0 ? mItem.AvisynthScript : null;
            }

            if (aviSynthScript != null)
            {
                ds.HasAvisynth = true;
                aviSynthScript = //"SetMemoryMax(64)\r\n" + 
                    ReplaceAvisynth(mItem, aviSynthScript);

                ds.Filename = path;
                System.IO.File.WriteAllText(ds.Filename, aviSynthScript);
            }
            else if ((mItem.Orientation != MediaBrowser4.Objects.MediaItem.MediaOrientation.BOTTOMisBOTTOM || foreAviSynth)
                && FindAviSynth())
            {
                ds.HasAvisynth = false;
                ds.Filename = path;
                System.IO.File.WriteAllText(ds.Filename, GetAVSFromVideoItem(mItem));
            }

            return ds;
        }

        public static DirectShowInfo GetDirectShow(Variation variation)
        {
            DirectShowInfo ds = new DirectShowInfo();
            ds.StartPosition = 0.0;
            ds.StopPosition = 0.0;

            Layer layer = variation.Layers.FirstOrDefault(x => x.Edit == "TRIM");

            if (layer != null)
            {
                ds.StartPosition = Convert.ToDouble(layer.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                ds.StopPosition = Convert.ToDouble(layer.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
            }    

            return ds;
        }

                public static bool FindAviSynth()
        {
            return System.IO.Directory.Exists(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles)
                + "\\AviSynth 2.5");
        }

        public static string GetAVSFromVideoItem(MediaBrowser4.Objects.MediaItem vItem)
        {
            string avs = "DirectShowSource(\"" + vItem.FileObject.FullName + "\", fps=" + vItem.Fps.ToString().Replace(',', '.') + ")\n";

            if (vItem.Fps == 0)
            {
                avs = "DirectShowSource(\"" + vItem.FileObject.FullName + "\")\n";
            }

            if (vItem.Orientation == MediaBrowser4.Objects.MediaItem.MediaOrientation.LEFTisBOTTOM)
            {
                avs += "TurnLeft";
            }
            else if (vItem.Orientation == MediaBrowser4.Objects.MediaItem.MediaOrientation.RIGHTisBOTTOM)
            {
                avs += "TurnRight";
            }
            else if (vItem.Orientation == MediaBrowser4.Objects.MediaItem.MediaOrientation.TOPisBOTTOM)
            {
                avs += "Turn180";
            }

            return avs;
        }

        public static Bitmap GetRGB(MediaBrowser4.Objects.MediaItem mItem)
        {
            return GetRGB(mItem, false);
        }

        public static Bitmap GetRGB(MediaBrowser4.Objects.MediaItem mItem, bool forcePreview)
        {
            Bitmap sourceImage = null;
            if (mItem is MediaBrowser4.Objects.MediaItemBitmap)
            {
                try
                {
                    sourceImage = ((MediaBrowser4.Objects.MediaItemBitmap)mItem).GetImage();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, mItem.FileObject.FullName);
                }
            }
            else if (mItem is MediaBrowser4.Objects.MediaItemVideo)
            {
                DirectShowInfo ds = Utilities.ResultMedia.GetDirectShow(mItem, false);
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("TRIM");
                double start = 0.0;
                if (layer != null)
                    start = Convert.ToDouble(layer.Action.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);

                if (start == 0.0)
                    start = mItem.Duration / 2;

                try
                {
                    using (DirectShow.FrameGrabber frameGrabber = new DirectShow.FrameGrabber(ds.Filename))
                    {
                        sourceImage = frameGrabber[start];
                        sourceImage = MediaProcessing.RotateImage.Action90Degrees(sourceImage, (int)mItem.Orientation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, mItem.FileObject.FullName);
                }

                if (sourceImage == null)
                {
                    try
                    {
                        FFMpeg ffmpeg = new FFMpeg();
                        if (ffmpeg.ApplicationExists)
                        {
                            sourceImage =
                            ffmpeg.GetFrameFromVideo(
                             mItem.FileObject.FullName,
                             MediaBrowserContext.DBTempFolder + "\\fg" + DateTime.Now.Ticks, 0.0);

                            sourceImage = MediaProcessing.RotateImage.Action90Degrees(sourceImage, (int)mItem.Orientation);

                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, mItem.FileObject.FullName);
                    }
                }

                if (sourceImage != null && !ds.Filename.ToLower().EndsWith(".avs"))
                {
                    mItem.Height = sourceImage.Height;
                    mItem.Width = sourceImage.Width;
                }
            }

            return sourceImage;
        }
    }

    public class DirectShowInfo
    {
        public bool HasAvisynth { get; set; }
        public long StartPositionTicks
        {
            get
            {
                return (long)(this.StartPosition * (long)10000000);
            }

            set
            {
                this.StartPosition = (double)value / 10000000;
            }
        }

        public long StopPositionTicks
        {
            get
            {
                return (long)(this.StopPosition * (long)10000000);
            }

            set
            {
                this.StopPosition = (double)value / 10000000;
            }
        }

        public long StartPositionMilliseconds
        {
            get
            {
                return (long)(this.StartPosition * (long)1000);
            }

            set
            {
                this.StartPosition = (double)value / 1000;
            }
        }

        public long StopPositionMilliseconds
        {
            get
            {
                return (long)(this.StopPosition * (long)1000);
            }

            set
            {
                this.StopPosition = (double)value / 1000;
            }
        }

        public double StartPosition, StopPosition;
        public string Filename;
    }
}
