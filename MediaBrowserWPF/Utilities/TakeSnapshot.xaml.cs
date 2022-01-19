using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MediaBrowser4;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;
using MediaBrowserWPF.UserControls.RgbImage;
using MediaBrowserWPF.UserControls.Video;
using MediaProcessing;
using System.Runtime.Serialization.Formatters.Binary;
using MediaProcessing.FaceDetection;
using System.Diagnostics;

namespace MediaBrowserWPF.Utilities
{
    /// <summary>
    /// Interaktionslogik für TakeSnapshot.xaml
    /// </summary>
    public partial class TakeSnapshot : UserControl, IDisposable
    {
        ImageControl imageControl;
        IVideoControl videoControl;

        public bool CopyVideo { get; set; } = true;

        public TakeSnapshot()
        {
            InitializeComponent();
        }


        public void ExportImage(List<MediaItem> mediaItems, double exportSize, double borderRel,
            double relation, int jpegQuality, SharpenImage.Quality sharpenQuality, bool lightbox,
            bool fullname, bool isPreviewDb, List<int> previewDbVariationIdList, bool isForcedCrop, double? forcedHeight = null, double relforcedPos = .5)
        {
            double border = exportSize * borderRel / 100;
            string path = isPreviewDb ? FilesAndFolders.CreateDesktopPreviewDbFolder() : FilesAndFolders.CreateDesktopExportFolder();
            int countVideo = mediaItems.Count(x => x is MediaItemVideo);

            if (isPreviewDb)
                jpegQuality = MediaBrowserContext.PreviewJpegQuality;

            if (lightbox)
            {
                if (countVideo > 0)
                {
                    FilesAndFolders.CopyRessource(@"lightbox\base.gif", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\html5boxplayer.swf", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\html5lightbox.js", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\jquery.js", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\close.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\loading.gif", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\next.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\pause.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\play.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\playvideo_64.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox\skins\default\prev.png", FilesAndFolders.DesktopExportFolder);
                }
                else
                {
                    FilesAndFolders.CopyRessource(@"lightbox2\css\lightbox.css", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\images\base.gif", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\images\close.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\images\loading.gif", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\images\next.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\images\prev.png", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\js\jquery-1.7.2.min.js", FilesAndFolders.DesktopExportFolder);
                    FilesAndFolders.CopyRessource(@"lightbox2\js\lightbox.js", FilesAndFolders.DesktopExportFolder);
                }
            }

            MainWindow.BussyIndicatorContent = "Exportiere";
            MainWindow.BussyIndicatorIsBusy = true;

            Thread thread = new Thread(() =>
            {
                imageControl = new ImageControl();
                imageControl.IsInvisibleRender = true;

                if (!this.CopyVideo)
                {
                    //throw new NotImplementedException("nVlc entfernt");
                    //videoControl = new nVlc();
                    ////videoControl = new VideoLanDotNet();
                    videoControl = new WpfMediaElement();
                    ////videoControl = new WpfMediaKit();
                    videoControl.Volume = 0;
                    videoControl.PositionChanged += new EventHandler(videoControl_PositionChanged);
                }

                imageControl.ImageLoaded += new EventHandler(imageControl_ImageLoaded);
                imageControl.SetBitmapScalingMode(BitmapScalingMode.HighQuality);
                imageControl.NoCache = true;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(countVideo > 0 ? html1 : html2);

                int cnt = 0;
                int cntError = 0;
                bool faceDetection = isPreviewDb;
                OpenCVFaceDetector.SimpleFaceDetector simpleFaceDetector = null;
                BinaryFormatter binaryFormatter = null;

                if (faceDetection)
                    try
                    {
                        simpleFaceDetector = new OpenCVFaceDetector.SimpleFaceDetector();
                        binaryFormatter = new BinaryFormatter();
                    }
                    catch { faceDetection = false; }

                foreach (MediaItem mItem in mediaItems)
                {
                    cnt++;
                    Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                    {
                        MainWindow.BussyIndicatorContent = "Exportiere " + cnt + " von " + mediaItems.Count + " Dateien ..."
                          + (cntError > 0 ? " (" + cntError + " Fehler)" : "");
                    }));

                    List<Variation> vList = MediaBrowserContext.GetVariations(mItem);

                    if (!isPreviewDb)
                        vList = vList.Where(x => x.Name.Equals(
                                vList.FirstOrDefault(y => y.Id == mItem.VariationIdDefault).Name
                                , StringComparison.InvariantCultureIgnoreCase)).ToList();

                    foreach (Variation variation in vList)
                    {
                        if (isPreviewDb && previewDbVariationIdList.Contains(variation.Id))
                            continue;

                        try
                        {
                            if (!mItem.FileObject.Exists)
                                continue;

                            System.Windows.Size size = isPreviewDb ?
                                  new System.Windows.Size(MediaBrowserContext.PreviewSize.Width, MediaBrowserContext.PreviewSize.Height)
                                : new System.Windows.Size(lightbox ? ((double)mItem.Width / (double)mItem.Height > 2 ? 1600 : exportSize) : mItem.Relation < 2 ? exportSize : exportSize * (mItem.Relation - 1),
                                                                                                 lightbox ? exportSize * (768.0 / 1024.0) : mItem.Relation < 2 ? exportSize : exportSize * (mItem.Relation - 1)
                                               );

                            if (mItem.Width < exportSize && mItem.Height < exportSize)
                            {
                                size = new System.Windows.Size(System.Math.Max(mItem.Width, mItem.Height), System.Math.Max(mItem.Width, mItem.Height));
                            }

                            string newName = GetExportName(exportSize, jpegQuality, sharpenQuality, fullname, isPreviewDb, mItem, vList, variation);

                            if (mItem is MediaItemVideo && !lightbox && !isPreviewDb)
                            {
                                if (this.CopyVideo)
                                {
                                    if (!System.IO.File.Exists(System.IO.Path.Combine(path, mItem.Filename)))
                                        mItem.FileObject.CopyTo(System.IO.Path.Combine(path, mItem.Filename));
                                }
                                else
                                {
                                    string newFullname = System.IO.Path.Combine(path,
                                        System.IO.Path.GetFileNameWithoutExtension(mItem.Filename) + "_" + System.Math.Round(mItem.Duration) + (vList.Count == 1 ? String.Empty : "_v" + variation.Position) + "sec.jpg");

                                    if (System.IO.File.Exists(newFullname))
                                        continue;

                                    videoControl.Source = mItem.FileObject.FullName;
                                    videoControl.Volume = 0;
                                    videoControl.Play();
                                    //videoControl.Position = .5f;
                                    ((UserControl)videoControl).Measure(size);
                                    ((UserControl)videoControl).Arrange(new Rect(size));
                                    ((UserControl)videoControl).UpdateLayout();

                                    this.videoControlChanged = false;
                                    int cntTimeout = 0;
                                    while (!this.videoControlChanged && cntTimeout < 100)
                                    {
                                        Thread.Sleep(100);
                                        cntTimeout++;
                                    }

                                    if (!this.videoControlChanged && cntTimeout >= 100)
                                    {
                                        throw new Exception("Timout while finding Video-Position: " + mItem.FileObject.FullName);
                                    }

                                    videoControl.Pause();
                                    System.Drawing.Bitmap bmp = videoControl.TakeSnapshot();
                                    videoControl.Stop();
                                    WriteBitmap(bmp, jpegQuality, SharpenImage.Quality.SOFT, RenderSize, 0, 0, mItem.FileObject.FullName, newFullname);
                                }
                            }
                            else if (mItem is MediaItemBitmap)
                            {
                                if (!System.IO.File.Exists(System.IO.Path.Combine(path, newName)))
                                {
                                    mItem.ChangeVariation(variation);
                                    imageControl.MediaItemSource = mItem;

                                    if (imageControl.IsAnimatedGif)
                                    {
                                        if (!isPreviewDb || mItem.FileLength < 3 * 1024 * 1024)
                                        {
                                            if (!isPreviewDb)
                                                newName = mItem.Filename;

                                            if (!System.IO.File.Exists(System.IO.Path.Combine(path, newName)))
                                                mItem.FileObject.CopyTo(System.IO.Path.Combine(path, isPreviewDb ? variation.Id + ".agif.prv" : newName));
                                        }
                                        else
                                        {
                                            Bitmap bmp = ((MediaItemBitmap)mItem).GetImage();
                                            bmp = MediaProcessing.ResizeImage.ActionFitIn(bmp, 800);
                                            WriteBitmap(bmp, jpegQuality, SharpenImage.Quality.SOFT, size, 0, 0, mItem.FileObject.FullName, System.IO.Path.Combine(path, newName));
                                        }
                                    }
                                    else
                                    {
                                        if (isPreviewDb && (double)mItem.Width / (double)mItem.Height > 2)
                                        {
                                            System.Windows.Size panoramaSize = new System.Windows.Size(MediaBrowserContext.PreviewSize.Height * ((double)mItem.Width / (double)mItem.Height), MediaBrowserContext.PreviewSize.Height);

                                            if (mItem.Height <= panoramaSize.Height)
                                                panoramaSize = new System.Windows.Size(mItem.Width, mItem.Height);

                                            imageControl.Measure(panoramaSize);
                                            imageControl.Arrange(new Rect(panoramaSize));
                                        }
                                        else
                                        {
                                            if (border > 0 && border * 2 < Math.Min(size.Width, size.Height))
                                            {
                                                System.Windows.Size reducedSize = new System.Windows.Size(size.Width - 2 * border, size.Height - 2 * border);
                                                imageControl.Measure(reducedSize);
                                                imageControl.Arrange(new Rect(reducedSize));

                                                if ((imageControl.MediaRenderSize.Width + 2 * border) / (imageControl.MediaRenderSize.Height + 2 * border) < relation)
                                                {
                                                    reducedSize = new System.Windows.Size(size.Width - 2 * border, (size.Width / relation) - 2 * border);
                                                    imageControl.Measure(reducedSize);
                                                    imageControl.Arrange(new Rect(reducedSize));
                                                }

                                                if (isForcedCrop && relation >= 1.0)
                                                {
                                                    // throw new Exception("Zuschnitt mit Rahmen nicht richtig implementiert"); 
                                                    imageControl.ForceRelation(relation * (1 + (0.009 * borderRel * borderRel)));
                                                }
                                            }
                                            else
                                            {
                                                imageControl.Measure(size);
                                                imageControl.Arrange(new Rect(size));

                                                if (isForcedCrop && relation > 0)
                                                {
                                                    imageControl.ForceRelation(relation);
                                                }
                                            }
                                        }

                                        if (relation > 0)
                                            imageControl.Orientation = MediaItem.MediaOrientation.BOTTOMisBOTTOM;

                                        imageControl.UpdateLayout();
                                        System.Drawing.Bitmap bmp = imageControl.TakeSnapshot();

                                        System.Drawing.Size sizecache = new System.Drawing.Size(bmp.Width, bmp.Height);

                                        WriteBitmap(bmp, jpegQuality, sharpenQuality, size, border, relation, mItem.FileObject.FullName, System.IO.Path.Combine(path, newName), forcedHeight.HasValue ? (System.Drawing.Size?)new System.Drawing.Size((int)exportSize, (int)forcedHeight.Value) : null, relforcedPos);

                                        if (mItem is MediaItemBitmap)
                                        {
                                            string soundffmpeg = mItem.LocalExtraFiles.FirstOrDefault(x => x.ToLower().EndsWith(".mp3") || x.ToLower().EndsWith(".wav"));
                                            if (soundffmpeg != null)
                                            {
                                                string ffmpegPath = Createffmpeg.FindFFmpeg();
                                                if (!String.IsNullOrWhiteSpace(ffmpegPath))
                                                {
                                                    MediaInfoLib sInfo = new MediaInfoLib(soundffmpeg);
                                                    string playTime = (float.Parse(sInfo.PlayTime) / 1000).ToString().Replace(',', '.');

                                                    Process process = new Process
                                                    {
                                                        StartInfo = new ProcessStartInfo
                                                        {
                                                            FileName = ffmpegPath,
                                                            Arguments = $"-loop 1 -i \"{System.IO.Path.Combine(path, newName)}\" -i \"{soundffmpeg}\" -t {playTime} -c:v libx264 -crf 24 -c:a libvo_aacenc -ab 112k \"{System.IO.Path.Combine(path, newName)}.mp4\""
                                                        }
                                                    };
                                                    process.Start();
                                                    process.WaitForExit();

                                                    File.WriteAllText(System.IO.Path.Combine(path, newName) + ".bat", $"\"{ffmpegPath}\" -loop 1 -i \"{System.IO.Path.Combine(path, newName)}\" -i \"{soundffmpeg}\" -t {playTime} -c:v libx264 -crf 24 -c:a libvo_aacenc -ab 112k \"{System.IO.Path.Combine(path, newName)}.mp4\"");
                                                }
                                            }
                                        }

                                        if (faceDetection)
                                        {
                                            try
                                            {
                                                Faces faces = simpleFaceDetector.DetectFace(System.IO.Path.Combine(path, newName));

                                                faces.Width = sizecache.Width;
                                                faces.Height = sizecache.Height;

                                                Stream stream = File.Open(System.IO.Path.Combine(path, newName) + ".fcd", FileMode.Create);
                                                binaryFormatter.Serialize(stream, faces);
                                                stream.Close();
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }

                            string lightboxFolder = countVideo > 0 ? "lightbox" : "lightbox2";

                            if (lightbox)
                            {
                                string thumbPath = "tn_" + System.IO.Path.GetFileNameWithoutExtension(mItem.Filename) + (vList.Count == 1 ? String.Empty : "_v" + variation.Position) + ".jpg";
                                if (!File.Exists(System.IO.Path.Combine(path + "\\" + lightboxFolder, thumbPath)))
                                {
                                    File.WriteAllBytes(System.IO.Path.Combine(path + "\\" + lightboxFolder, thumbPath), MediaBrowserContext.MainDBProvider.GetThumbJpegData(variation.Id));
                                }

                                string resultMessage = String.Empty;
                                MediaBrowserContext.GetDescription(mItem);
                                if (!String.IsNullOrWhiteSpace(mItem.Description))
                                    resultMessage = mItem.Description;// + " (" + mItem.MediaDate.ToString("g") + ")";
                                else
                                {
                                    resultMessage = mItem.MediaDate.ToString("f");
                                    Category locCat = mItem.Categories.FirstOrDefault(x => x.IsLocation);

                                    if (locCat != null)
                                    {
                                        resultMessage += ", " + locCat.Name + (locCat.Parent != null ? " (" + locCat.Parent.Name + ")" : String.Empty);
                                    }

                                    var m = MediaItemInfo.MeteoData(mItem);

                                    if (m != null)
                                    {
                                        resultMessage += ", " + String.Format("{0}°C, {1}% rel.", Math.Round(m.Item1), Math.Round(m.Item2));
                                    }
                                }

                                int width = Math.Max(640, mItem.WidthOrientation);
                                int height = (int)(width / mItem.Relation);

                                if (mItem.WidthOrientation < mItem.HeightOrientation)
                                {
                                    height = Math.Max(480, mItem.HeightOrientation);
                                    width = (int)(height * mItem.Relation);
                                }

                                if (countVideo > 0)
                                {
                                    sb.AppendLine(String.Format("<div class=\"thumbnail\"><a title=\"{0}\" class=\"html5lightbox\" data-group=\"mygroup\" "
                                        + (mItem is MediaItemVideo ? "data-width=\"" + width + "\" data-height=\"" + height + "\" " : String.Empty)
                                        + "href=\"{1}\"><img class=\"thumbnail\" alt=\"{2}\" src=\"{3}\"></a></div>",
                                        System.Web.HttpUtility.HtmlEncode(resultMessage),
                                        (mItem is MediaItemVideo ? System.IO.Path.GetFileNameWithoutExtension(mItem.Filename) + (vList.Count == 1 ? String.Empty : "_v" + variation.Position) + ".mp4" : newName),
                                        System.Web.HttpUtility.HtmlEncode(mItem.Filename),
                                        lightboxFolder + "/" + thumbPath));
                                }
                                else
                                {
                                    sb.AppendLine("<div class=\"thumbnail\"><a title=\"" + System.Web.HttpUtility.HtmlEncode(resultMessage) + "\" rel=\"lightbox[exportgroup]\""
                                                 + " href=\"" + newName + "\"><img class=\"thumbnail\" alt=\"" + System.Web.HttpUtility.HtmlEncode(mItem.Filename)
                                                 + "\" src=\"" + lightboxFolder + "/" + thumbPath + "\"></a></div>");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex);
                            cntError++;
                        }
                    }
                }

                if (countVideo > 0 && !isPreviewDb)
                {
                    Createffmpeg cf = new Createffmpeg();
                    cf.ExportPath = path;
                    cf.SetByPredefinedValue(0);
                    cf.VideoSize = new System.Drawing.Size(0, 0); ;
                    cf.Start(new List<MediaItem>(mediaItems));
                }

                if (lightbox)
                {
                    sb.AppendLine("</body>");
                    sb.AppendLine("</html>");
                    File.WriteAllText(System.IO.Path.Combine(path, "index.html"), sb.ToString(), Encoding.UTF8);
                }

                cnt++;
                Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    MainWindow.BussyIndicatorIsBusy = false;
                }));
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static string GetExportName(double exportSize, int jpegQuality, SharpenImage.Quality sharpenQuality, bool fullname, bool isPreviewDb, MediaItem mItem, List<Variation> vList, Variation variation)
        {
            return isPreviewDb ? variation.Id + ".jpg.prv" :
                (fullname ? System.IO.Path.GetFileNameWithoutExtension(mItem.Filename) + (vList.Count == 1 ? String.Empty : "_v" + variation.Position) + "_" + exportSize.ToString().PadLeft(4, '0') + "pix_"
                + jpegQuality.ToString().PadLeft(3, '0') + "jpg_" + ((int)sharpenQuality) + "Xsharp.jpg"
                : System.IO.Path.GetFileNameWithoutExtension(mItem.Filename) + (vList == null || vList.Count == 1 ? String.Empty : "_v" + variation.Position) + ".jpg");
        }

        public void ExportImage(MediaItem mItem, string exportFilename, double exportSize, int jpegQuality, bool overWrite)
        {
            if (this.imageControl == null)
            {
                this.imageControl = new ImageControl();
                this.imageControl.IsInvisibleRender = true;
                this.imageControl.ImageLoaded += new EventHandler(imageControl_ImageLoaded);
                this.imageControl.SetBitmapScalingMode(BitmapScalingMode.HighQuality);
                this.imageControl.NoCache = true;
            }

            if (!mItem.FileObject.Exists)
                return;

            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(exportFilename)))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(exportFilename));
            }

            System.Windows.Size size = new System.Windows.Size(exportSize, exportSize);
            if (mItem.Width < exportSize && mItem.Height < exportSize)
            {
                size = new System.Windows.Size(System.Math.Max(mItem.Width, mItem.Height), System.Math.Max(mItem.Width, mItem.Height));
            }

            if (mItem is MediaItemBitmap)
            {
                if (System.IO.File.Exists(exportFilename) && overWrite)
                {
                    System.IO.File.Delete(exportFilename);
                }

                if (System.IO.File.Exists(exportFilename))
                    return;

                imageControl.MediaItemSource = mItem;

                if (imageControl.IsAnimatedGif)
                {
                    mItem.FileObject.CopyTo(exportFilename);
                }
                else
                {
                    imageControl.Measure(size);
                    imageControl.Arrange(new Rect(size));
                    imageControl.UpdateLayout();

                    System.Drawing.Bitmap bmp = imageControl.TakeSnapshot();
                    WriteBitmap(bmp, jpegQuality, SharpenImage.Quality.SOFT, size, 0, 0, mItem.FileObject.FullName, exportFilename);
                }
            }
        }

        public static Bitmap AddBorder(Bitmap sourceImage, int borderWidth, System.Drawing.Color borderColor)
        {
            Bitmap resultImage = new Bitmap(sourceImage.Width + (2 * borderWidth), sourceImage.Height + (2 * borderWidth));
            Graphics g = Graphics.FromImage((System.Drawing.Image)resultImage);
            g.Clear(borderColor);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawImage(sourceImage, borderWidth, borderWidth, sourceImage.Width, sourceImage.Height);
            g.Dispose();
            sourceImage = null;
            GC.Collect();

            return resultImage;
        }

        void imageControl_ImageLoaded(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new System.Windows.Threading.DispatcherOperationCallback(delegate
            {
                return null;
            }), null);
        }

        bool videoControlChanged;
        void videoControl_PositionChanged(object sender, EventArgs e)
        {
            if (this.videoControl != null && this.videoControl.Position > 0f)
            {
                this.videoControlChanged = true;
            }
        }

        private static void WriteBitmap(System.Drawing.Bitmap bmp, int jpegQuality, SharpenImage.Quality sharpeQuality, System.Windows.Size size, double border, double relation, string fullName, string newFullname, System.Drawing.Size? forcedSize = null, double relforcedPos = .5)
        {
            if (bmp == null)
            {
                throw new Exception("MediaItem konnte nicht exportiert werden: " + fullName);
            }

            bmp = SharpenImage.Work(bmp, sharpeQuality);

            if (forcedSize.HasValue)
            {
                Bitmap resultImage = new Bitmap(forcedSize.Value.Width, forcedSize.Value.Height);
                Graphics g = Graphics.FromImage((System.Drawing.Image)resultImage);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                double rel = (double)bmp.Width / (double)bmp.Height;
                double realHight = resultImage.Width / rel;
                double diff = realHight - resultImage.Height;
                int posY = (int)(diff * relforcedPos);
                Rectangle rec = new Rectangle(0, -posY, resultImage.Width, (int)(resultImage.Width / rel));
                g.DrawImage(bmp, rec);

                g.Dispose();
                bmp = resultImage;
            }
            else if (bmp.Width < size.Width)
            {
                Bitmap resultImage = null;

                if (relation == 0)
                {
                    resultImage = new Bitmap((int)(bmp.Width + 2 * border), (int)(bmp.Height + 2 * border));
                }
                else
                {
                    if (bmp.Width > bmp.Height)
                    {
                        resultImage = new Bitmap((int)size.Width, (int)(size.Width / relation));
                    }
                    else
                    {
                        resultImage = new Bitmap((int)(size.Height / relation), (int)size.Height);
                    }
                }

                Graphics g = Graphics.FromImage((System.Drawing.Image)resultImage);
                g.Clear(System.Drawing.Color.Black);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                g.DrawImage(bmp, (int)((resultImage.Width - bmp.Width) / 2), (int)((resultImage.Height - bmp.Height) / 2), bmp.Width, bmp.Height);
                g.Dispose();
                bmp = resultImage;
            }

            if (bmp != null)
            {
                if (Path.GetExtension(newFullname).ToLower() == ".jpg")
                {
                    MediaProcessing.EncodeImage.SaveJPGFile(bmp, newFullname, jpegQuality);
                }
                else
                {
                    bmp.Save(newFullname);
                }
            }
            else
            {
                throw new Exception("MediaItem konnte nicht exportiert werden: " + fullName);
            }
        }

        public void Dispose()
        {
            if (imageControl != null)
                imageControl.Dispose();

            if (videoControl != null)
                videoControl.Dispose();
        }

        const string html1 = @"
<!doctype html>
<html lang=""en-us"">
<head>
    <meta charset=""utf-8"">
    <title>MediabrowserWpf</title>
    <meta name=""author"" content=""Sebastian Czapek"">
    <link rel=""shortcut icon"" type=""image/ico"" href=""lightbox/base.gif"" />
    <style type=""text/css"">
        div.thumbnail { float:left; width:170px; height:158px; text-align:center; vertical-align:middle;  }
        .clearBoth { clear:both; }      
        img.thumbnail {border:3px solid white;}
        body {background-color:black;}
    </style>
</head>
<body>
<script type=""text/javascript"" src=""lightbox/jquery.js""></script>
<script type=""text/javascript"" src=""lightbox/html5lightbox.js""></script>
";

        const string html2 = @"
<!doctype html>
<html lang=""en-us"">
<head>
    <meta charset=""utf-8"">
    <title>MediabrowserWpf</title>
    <meta name=""author"" content=""Sebastian Czapek"">
    <meta name=""viewport"" content=""width=device-width"">
    <link rel=""shortcut icon"" type=""image/ico"" href=""lightbox2/images/base.gif"" />
    <link rel=""stylesheet"" href=""lightbox2/css/lightbox.css"" type=""text/css"" media=""screen"" />
    <style type=""text/css"">
        div.thumbnail { float:left; width:170px; height:158px; text-align:center; vertical-align:middle;  }
        .clearBoth { clear:both; }      
        img.thumbnail {border:3px solid white;}
        body {background-color:black;}
    </style>
</head>
<body>
    <script src=""lightbox2/js/jquery-1.7.2.min.js""></script>
    <script src=""lightbox2/js/lightbox.js""></script>
";
    }
}
