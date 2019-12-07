using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using MediaBrowser4.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MediaBrowser4.Objects
{
    public abstract class MediaItem : INotifyPropertyChanged, IEquatable<MediaItem>
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler CategoriesChanged;

        public enum MediaOrientation
        {
            BOTTOMisBOTTOM = 0,
            RIGHTisBOTTOM = 1,
            TOPisBOTTOM = 2,
            LEFTisBOTTOM = 3
        }

        public String ImageCachePath { get; set;  }

        public static string OrientationInfo(MediaOrientation orientation)
        {
            switch (orientation)
            {
                case MediaItem.MediaOrientation.BOTTOMisBOTTOM:
                    return "normal";

                case MediaItem.MediaOrientation.RIGHTisBOTTOM:
                    return "rechts";

                case MediaItem.MediaOrientation.TOPisBOTTOM:
                    return "auf dem Kopf";

                case MediaItem.MediaOrientation.LEFTisBOTTOM:
                    return "links";

                default:
                    return String.Empty;
            }
        }

        public enum DublicateCriteria { CHECKSUM, FILENAME, CONTAINS }

        internal abstract string DBType { get; }
        public abstract System.Windows.Media.Brush MediaColor { get; }

        public bool IsSelected { get; set; } //Zum Berechnen der Dublikate

        public int RoleId
        {
            get;
            internal set;
        }

        public int Id
        {
            get;
            internal set;
        }

        public int? DescriptionId
        {
            get;
            internal set;
        }

        private string _sortorder;
        public string Sortorder
        {
            get { return _sortorder; }
            internal set
            {
                _sortorder = value;
                this.OnPropertyChanged("Sortorder");
            }
        }

        public string Tag
        {
            get;
            set;
        }

        public int ThumbnailSize
        {
            get;
            internal set;
        }

        public int ShuffleValue
        {
            get;
            set;
        }

        public int Frames
        {
            get;
            internal set;
        }

        public DateTime CreationDate
        {
            get;
            internal set;
        }

        public DateTime LastWriteDate
        {
            get;
            internal set;
        }

        public DateTime MediaDate
        {
            get;
            set;
        }

        public DateTime InsertDate
        {
            get;
            internal set;
        }

        public MediaOrientation Orientation
        {
            get;
            set;
        }

        public int VariationId
        {
            get;
            internal set;
        }

        public int VariationIdDefault
        {
            get;
            internal set;
        }

        public void ResetVariation()
        {
            this.VariationId = this.VariationIdDefault;
            this.layers = MediaBrowserContext.GetLayersForMediaItem(this);
        }

        public void ChangeVariation(Variation variation)
        {
            if (this.VariationId != variation.Id)
                this.categories = null;

            this.VariationId = variation.Id;
            this.layers = MediaBrowserContext.GetLayersForMediaItem(this);
        }

        public string Md5Value
        {
            get;
            internal set;
        }

        private bool isMd5Dublicate;
        public bool IsMd5Dublicate
        {
            get { return isMd5Dublicate; }
            internal set
            {
                this.isMd5Dublicate = value;
                this.OnPropertyChanged("MediaColor");
            }
        }

        public int Priority
        {
            get;
            internal set;
        }

        public double Fps
        {
            get;
            internal set;
        }

        private double duration;
        public double Duration
        {
            get { return duration >= 0 ? duration : 0; }
            set { duration = value; }
        }

        public long DurationTicks
        {
            get
            {
                return (long)(this.Duration * 10000000);
            }
        }

        public string Description
        {
            get;
            internal set;
        }


        private string _fileName;
        public string Filename
        {
            get { return _fileName; }
            internal set
            {
                _fileName = value;
                this.OnPropertyChanged("ToolTip");
                this.OnPropertyChanged("Filename");
            }
        }

        public long FileLength
        {
            get;
            internal set;
        }

        public string AvisynthScript { get; set; }

        private string foldername;
        public string Foldername
        {
            get { return foldername; }
            set
            {
                foldername = value;
                this.OnPropertyChanged("ToolTip");
                this.OnPropertyChanged("Foldername");
                this.OnPropertyChanged("TextBoxContent");
            }
        }

        /// <summary>
        /// Gibt die Breite des Originalbildes an
        /// </summary>
        public int Width
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gibt die Höhe des Originalbildes an
        /// </summary>
        public int Height
        {
            get;
            internal set;
        }

        public int HeightOrientation
        {
            get
            {
                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
             || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    return this.Height;
                else
                    return this.Width;
            }
        }

        public int WidthOrientation
        {
            get
            {
                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
             || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    return this.Width;
                else
                    return this.Height;
            }
        }

        public Size CalculatedSize
        {
            get
            {
                Size size = new Size(this.WidthOrientation, this.HeightOrientation);
                Layer layer = this.FindLayer("CROP");

                if (layer != null)
                {
                    double left = Convert.ToDouble(layer.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                    double top = Convert.ToDouble(layer.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                    double right = Convert.ToDouble(layer.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat);
                    double bottom = Convert.ToDouble(layer.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat);

                    size.Width *= (100.0 - (left + right)) / 100;
                    size.Height *= (100.0 - (top + bottom)) / 100;
                }

                return size;
            }
        }

        /// <summary>
        /// Gibt das Seitenverhaltnis unter Berücksichtigung der Orientierung aus
        /// </summary>
        public double Relation
        {
            get
            {
                if (this.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                  || this.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    return (double)this.Width / (double)this.Height;
                else
                    return (double)this.Height / (double)this.Width;
            }
        }

        /// <summary>
        /// Gibt Abmessung unter Berücksichtigung der Orientierung und des Zuschnittes aus
        /// </summary>
        public System.Drawing.Size? CroppedSize
        {
            get
            {
                MediaBrowser4.Objects.Layer layer = this.FindLayer("CROP");

                if (layer != null)
                {
                    double left = Convert.ToDouble(layer.Action.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                    double top = Convert.ToDouble(layer.Action.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat);
                    double right = Convert.ToDouble(layer.Action.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat);
                    double bottom = Convert.ToDouble(layer.Action.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat);


                    int width = (int)(this.Width * (100 - (left + right)) / 100);
                    int height = (int)(this.Height * (100 - (top + bottom)) / 100);

                    if (this.Orientation == MediaItem.MediaOrientation.LEFTisBOTTOM
                    || this.Orientation == MediaItem.MediaOrientation.RIGHTisBOTTOM)
                    {
                        width = (int)(this.Height * (100 - (left + right)) / 100);
                        height = (int)(this.Width * (100 - (top + bottom)) / 100);
                    }

                    return new System.Drawing.Size(width, height);
                }

                return null;
            }

        }

        /// <summary>
        /// Gibt das Seitenverhaltnis unter Berücksichtigung der Orientierung und des Zuschnittes aus
        /// </summary>
        public double AspectRatioCropped
        {
            get
            {

                if (this.FindLayer("CROP") == null)
                {
                    return (double)this.WidthOrientation / this.HeightOrientation;
                }
                else
                {
                    System.Drawing.Size? size = this.CroppedSize;
                    return (double)size.Value.Width / size.Value.Height;
                }
            }
        }

        public string AspectRatioStringCropped
        {
            get
            {

                if (this.FindLayer("CROP") == null)
                {
                    return " - ";
                }
                else
                {
                    System.Drawing.Size? size = this.CroppedSize;
                    return CalculateAspectString((double)Math.Max(size.Value.Width, size.Value.Height) / (double)Math.Min(size.Value.Width, size.Value.Height));
                }
            }
        }

        public string AspectRatioString
        {
            get
            {
                return CalculateAspectString((double)Math.Max(this.Width, this.Height) / (double)Math.Min(this.Width, this.Height));
            }
        }

        private static string CalculateAspectString(double aspect)
        {
            if (aspect > 2.4)
            {
                return "panorama";
            }
            else if (aspect > 2.1)
            {
                return "7:3";//2,33
            }
            else if (aspect > 1.85)
            {
                return "2:1";//2
            }
            else if (aspect > 1.7)
            {
                return "16:9";//1,77
            }
            else if (aspect > 1.63)
            {
                return "5:3";//1,66
            }
            else if (aspect > 1.58)
            {
                return "8:5";//1,6
            }
            else if (aspect > 1.53)
            {
                return "14:9";//1,55
            }
            else if (aspect > 1.4)
            {
                return "3:2";//1,5
            }
            else if (aspect > 1.25)
            {
                return "4:3";//1,33
            }
            else if (aspect > 1.1)
            {
                return "5:4";//1,23
            }
            else if (aspect > 0.9)
            {
                return "1:1";
            }
            else
            {
                return " - ";
            }
        }

        private bool isDeleted;
        public bool IsDeleted
        {
            get { return isDeleted; }
            set
            {
                this.isDeleted = value;
                this.OnPropertyChanged("MediaColor");
            }
        }

        private bool isFileNotFound;
        public bool IsFileNotFound
        {
            get { return isFileNotFound; }
            set
            {
                this.isFileNotFound = value;
                this.OnPropertyChanged("MediaColor");
            }
        }

        private bool isBookmarked;
        public bool IsBookmarked
        {
            get { return isBookmarked; }
            internal set
            {
                this.isBookmarked = value;
                this.OnPropertyChanged("MediaColor");
            }
        }

        private int viewed;
        public int Viewed
        {
            get { return viewed; }
            internal set
            {
                this.viewed = value;
                this.OnPropertyChanged("ToolTip");
                this.OnPropertyChanged("TextBoxContent");
            }
        }

        public double? Longitude { get; set; }

        public double? Latitude { get; set; }

        public string ToolTip
        {
            get
            {
                return this.GetToolTip();
            }
        }

        public string TextBoxContent
        {
            get
            {
                return this.GetToolTipPartTextContent();
            }
        }

        public string TextBoxCategories
        {
            get
            {
                return String.Join(", ", this.Categories.Where(item => !item.IsDate).Select(x => x.ToString()));
            }
        }


        private string GetToolTipPartTextContent()
        {
            return (this.Viewed > 0 ? MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(this.Viewed, 0)
                + " betrachtet\n" : String.Empty) + GetToolTipPartB();
        }

        public bool IsThumbJpegDataOutdated { get; set; }

        private byte[] thumbJpegData;
        public byte[] ThumbJpegData
        {
            get
            {
                if (this.thumbJpegData == null)
                    this.thumbJpegData = MediaBrowserContext.GetThumbJpegData(this.VariationId);
                return thumbJpegData;
            }

            internal set
            {
                this.thumbJpegData = value;
                this.OnPropertyChanged("ThumbJpegData");
            }
        }

        private ObservableCollection<Category> categories;
        public ObservableCollection<Category> Categories
        {
            get
            {
                if (this.categories == null)
                {
                    List<Category> catlist = MediaBrowserContext.GetCategoriesFromMediaItem(this).Where(x => !x.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName)).ToList();
                    catlist.Sort((a, b) => String.Compare(a.Name, b.Name, true));
                    this.categories = new ObservableCollection<Category>(catlist);
                    this.categories.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(categories_CollectionChanged);
                }
                return this.categories;
            }

            set
            {
                this.categories = value;
                this.OnPropertyChanged("ToolTip");
                this.OnPropertyChanged("TextBoxContent");
                this.OnPropertyChanged("TextBoxCategories");
            }
        }

        private List<string> localExtraFiles;
        public List<string> LocalExtraFiles
        {
            get
            {
                if (this.localExtraFiles == null)
                {
                    this.localExtraFiles = ExtraData.GetLocalStickyFiles(this, null);
                }
                return this.localExtraFiles;
            }
            set
            {
                this.localExtraFiles = value;
            }
        }

        private List<MediaBrowser4.Objects.Layer> layers;
        public List<MediaBrowser4.Objects.Layer> Layers
        {
            get
            {
                if (this.layers == null)
                {
                    this.layers = MediaBrowserContext.GetLayersForMediaItem(this);
                }
                return this.layers;
            }

            set
            {
                this.layers = value;
            }
        }

        public Folder Folder
        {
            get
            {
                return MediaBrowserContext.FolderTreeSingelton.GetFolderById(this.FolderId);
            }
        }

        public int FolderId
        {
            get;
            internal set;
        }

        private MetaDataList metaData;
        public MetaDataList MetaData
        {
            get
            {
                if (this.metaData == null && this.Id > 0)
                    this.RefreshMetaData();

                return this.metaData;
            }

            set
            {
                this.metaData = value;
            }
        }

        public string FullName
        {
            get
            {
                return this.foldername + "\\" + this.Filename;
            }
        }

        private FileInfo fileObject;
        public System.IO.FileInfo FileObject
        {
            get
            {
                if (this.fileObject == null)
                    this.fileObject = new System.IO.FileInfo(this.FullName);
                return this.fileObject;
            }

            set
            {
                this.fileObject = value;
                this.OnPropertyChanged("FileObject");
                if (this.fileObject != null)
                    this.Filename = this.fileObject.Name;
            }
        }

        internal MediaItem()
            : base()
        {

        }

        protected MediaItem(System.IO.FileInfo fileObject)
        {
            this.fileObject = fileObject;
            this.Filename = fileObject.Name;
            this.foldername = fileObject.DirectoryName;
            this.Sortorder = fileObject.Name;
            this.duration = 8.0;
        }

        void categories_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.OnPropertyChanged("ToolTip");
            this.OnPropertyChanged("TextBoxContent");
            this.OnPropertyChanged("TextBoxCategories");
        }

        public void InvokeCategoriesChanged()
        {
            if (this.CategoriesChanged != null)
            {
                this.CategoriesChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public Layer AddDefaultLayer(string edit, int pos)
        {
            Layer layer = Layer.GetDefaultLayer(edit, pos);

            if (layer != null)
                this.layers.Add(layer);

            return layer;
        }

        public Layer FindLayer(string edit)
        {
            foreach (Layer layer in Layers)
            {
                if (layer.Edit == edit)
                    return layer;
            }

            return null;
        }

        public void RemoveLayer(string edit)
        {
            Layer layer = FindLayer(edit);
            if (layer != null)
            {
                layers.Remove(layer);
            }
        }

        public bool IsDublicate(DublicateCriteria criteria)
        {
            switch (criteria)
            {
                case DublicateCriteria.CHECKSUM:
                    return this.IsMd5Dublicate;

                default:
                    return MediaBrowserContext.IsDublicate(this, criteria);
            }
        }

        private void RefreshMetaData()
        {
            this.metaData = new MetaDataList();
            using (MediaBrowser4.DB.ICommandHelper com = MediaBrowserContext.MainDBProvider.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT TYPE, GROUPNAME, NAME, VALUE, ISVISIBLE FROM METADATANAME, "
                    + "METADATA WHERE METADATANAME.ID = METADATA.METANAME_FK AND MEDIAFILES_FK=" + this.Id + " ORDER BY TYPE, GROUPNAME, NAME, VALUE");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    metaData.Add(
                        new MetaData(row["name"].ToString(),
                        row["GROUPNAME"].ToString(),
                        row["VALUE"].ToString(),
                        row["TYPE"].ToString(),
                        (bool)row["ISVISIBLE"]));
                }
            }
        }

        public MediaOrientation SetOrientationByNumber(int number)
        {
            switch (number)
            {
                case 0:
                    this.Orientation = MediaOrientation.BOTTOMisBOTTOM;
                    break;
                case 1:
                    this.Orientation = MediaOrientation.RIGHTisBOTTOM;
                    break;
                case 2:
                    this.Orientation = MediaOrientation.TOPisBOTTOM;
                    break;
                case 3:
                    this.Orientation = MediaOrientation.LEFTisBOTTOM;
                    break;
                default:
                    this.Orientation = MediaOrientation.BOTTOMisBOTTOM;
                    break;

            }
            return Orientation;
        }

        internal static MediaItem GetMediaItemFromDBType(string dbType)
        {
            switch (dbType)
            {
                case "rgb":
                    return new MediaItemBitmap();

                case "dsh":
                    return new MediaItemVideo();

                default:
                    return null;
            }
        }

        public abstract void GetThumbnail();

        private static Dictionary<string, SolidColorBrush> virtualFolderColors = new Dictionary<string, SolidColorBrush>();

        private static List<SolidColorBrush> solidColorBrushList = new List<SolidColorBrush>()
        {
            Brushes.LightGoldenrodYellow, Brushes.LightBlue,Brushes.LightCoral, Brushes.LightCyan,Brushes.LightGray, Brushes.LightGreen, Brushes.LightPink,
            Brushes.LightSalmon,Brushes.LightSeaGreen, Brushes.LightSkyBlue,Brushes.LightSlateGray,Brushes.LightSteelBlue, Brushes.LightYellow
        };

        public void SetInfoThumbnail()
        {
            FormattedText formattedText = new FormattedText(
                this.ToolTip,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Courier new"),
                12.5,
                Brushes.Black);

            formattedText.MaxTextWidth = 150;
            formattedText.MaxTextHeight = 150;

            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContext = drawingVisual.RenderOpen();

            if (!virtualFolderColors.ContainsKey(this.Foldername))
                virtualFolderColors.Add(this.Foldername, solidColorBrushList[virtualFolderColors.Count % 13]);

            drawingContext.DrawRectangle(virtualFolderColors[this.Foldername], null, new Rect(0, 0, 150, 150));
            drawingContext.DrawText(formattedText, new Point(2, 2));
            drawingContext.Close();

            RenderTargetBitmap targetBmp = new RenderTargetBitmap(150, 150, 96, 96, PixelFormats.Pbgra32);
            targetBmp.Render(drawingVisual);

            PngBitmapEncoder png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(targetBmp));

            MemoryStream imageStream = new MemoryStream();
            png.Save(imageStream);
            imageStream.Position = 0;

            this.thumbJpegData = new Byte[imageStream.Length];
            imageStream.Position = 0;
            imageStream.Read(this.thumbJpegData, 0, (int)imageStream.Length);
            imageStream.Close();
        }

        public void Rename(string fullname)
        {
            this.Filename = System.IO.Path.GetFileName(fullname);
            this.foldername = Path.GetDirectoryName(fullname);
            this.Sortorder = this.Filename;
            this.FileObject = new System.IO.FileInfo(fullname);
        }

        protected void GetThumbnail(System.Drawing.Bitmap sourceImage)
        {
            sourceImage = MediaProcessing.ResizeImage.ActionFitIn(sourceImage, this.ThumbnailSize);
            sourceImage = MediaProcessing.RotateImage.Action90Degrees(sourceImage, (int)this.Orientation);
            sourceImage = MediaProcessing.SharpenImage.Work(sourceImage, MediaProcessing.SharpenImage.Quality.SOFT);

            System.IO.MemoryStream imageStream = new System.IO.MemoryStream();
            MediaProcessing.EncodeImage.SaveJPGStream(sourceImage, imageStream, MediaBrowserContext.ThumbnailJPEGQuality);

            this.thumbJpegData = new Byte[imageStream.Length];
            imageStream.Position = 0;
            imageStream.Read(this.thumbJpegData, 0, (int)imageStream.Length);
            imageStream.Close();
        }

        private string GetToolTip()
        {
            return GetToolTipPartA() + GetToolTipPartB() + GetToolTipPartC();
        }

        private string GetToolTipPartA()
        {
            return Path.GetFileNameWithoutExtension(this.Filename) + " (" + Path.GetExtension(this.Filename).TrimStart('.') + ") "
                 + (this.Viewed > 0 ? MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(this.Viewed, 0) + " betrachtet" : String.Empty) + "\n";
        }

        private string GetToolTipPartB()
        {
            return (this.Foldername.Length > 50 ? (this.foldername.Substring(0, 10) + " ... " + this.foldername.Substring(this.foldername.Length - 30, 30)) : this.Foldername) + "\n"
                + string.Format("{0:n0}", (this.FileLength / 1024)) + " KB, "
                + DimmensionToolTip
                + (this is MediaBrowser4.Objects.MediaItemVideo ?  $"{MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(this.Duration)} Spielzeit, {this.FileLength / (this.Duration*1024):n0} KBs = {this.FileLength * 8 / (this.Duration * 1024 * 1024):n1} Mbs\n" : "")
                + $"{this.MediaDate:dddd} {this.MediaDate:g}";
        }
       
        public string DimmensionToolTip
        {
           get
            {        
                return $"{this.Width:n0} x {this.Height:n0} = {((double)this.Width * (double)this.Height) / 1000000:n1} Mio Pixel ({AspectRatioToolTip})\n";
            }
        }

        public string AspectRatioToolTip
        {
            get
            {
                double calcRel = CalcualtedRelation;
                double rel = (double)Math.Max(this.Width, this.Height) / (double)Math.Min(this.Width, this.Height);

                string aspCalc = CalculateAspectString(calcRel);
                string asp = CalculateAspectString(rel);    

                if (rel != calcRel)
                {
                    return $"{asp}  --> {aspCalc}";
                }
                else
                {
                    return $"{asp}";
                }               
            }
        }

        public double CalcualtedRelation
        {
            get
            {
                System.Windows.Size size = this.CalculatedSize;
                return Math.Max(size.Width, size.Height) / Math.Min(size.Width, size.Height);
            }
        }      

        private string GetToolTipPartC()
        {
            return (this.Categories.Count > 0 ? "\n\nKategorien:\n" + String.Join("\n", this.Categories.Select(item => (item.IsDate ? MediaBrowserContext.GetDBProperty("DiaryCategorizeFolder")
                + ": " + item.Date.ToLongDateString() : item.ToString()))) : "")
                + (String.IsNullOrWhiteSpace(this.Description) ? "" : "\n\"" + (this.Description.Length > 50 ? this.Description.Substring(0, 46) + " ..." : this.Description).Trim('"') + "\"");
        }

        public override string ToString()
        {
            return this.Filename;
        }

        public override bool Equals(object obj)
        {
            MediaItem mItem = obj as MediaItem;
            if (mItem == null)
            {
                return false;
            }
            return this.Id.Equals(mItem.Id);
        }

        bool IEquatable<MediaItem>.Equals(MediaItem other)
        {
            return this.Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return this.Id;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
