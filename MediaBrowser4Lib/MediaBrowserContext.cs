using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


using MediaBrowser4.DB;
using MediaBrowser4.Objects;
using MediaBrowser4.DB.SQLite;
using System.Collections.ObjectModel;
using System.Xml;
using System.Windows.Media;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using MediaBrowser4.Utilities;

namespace MediaBrowser4
{
    public static class MediaBrowserContext
    {
        public static event EventHandler<MediaItemArg> CategoriesChanged;

        public static System.Windows.Media.Brush DeleteBrush = System.Windows.Media.Brushes.Gold;
        public static System.Windows.Media.Brush FileNotFoundBrush = System.Windows.Media.Brushes.Red;
        public static System.Windows.Media.Brush BookmarkBrush = System.Windows.Media.Brushes.Green;
        public static System.Windows.Media.Brush BackGroundBrush = System.Windows.Media.Brushes.Black;
        public static System.Windows.Media.Brush BackGroundDirectShowBrush = System.Windows.Media.Brushes.SaddleBrown;
        public static System.Windows.Media.Brush DublicateShowBrush = System.Windows.Media.Brushes.Pink;
        public static int DefaultRoleID = 1;

        public const int LimitRequest = 1000000;
        public const string CategoryTagsName = "Verschlagwortung";
        public const string CategoryHistoryName = "Verlauf";
        public const string CategoryLocationsName = "Orte";
        public const string CategoryDiary = "Tagebuch";
        public const string DefaultMediaArchivRoot = @"D:\Fotos\Alle Bilder";

        public static CopyItemProperties CopyItemProperties = new CopyItemProperties();
        public static SearchToken SearchTokenGlobal;
        private static System.Drawing.Size previewSize = new System.Drawing.Size(1024, 600);
        public static int PreviewJpegQuality = 65;
        //private static System.Drawing.Size previewSize = new System.Drawing.Size(1920, 1080);
        //public static int PreviewJpegQuality = 75;
        private static int thumbnailSize = 150;
        private static int thumbnailJPEGQuality = 80;
        internal static bool abortInsert = false;
        private static int checkSumMaxLength = 52428800; //50MB
        private static int commitInsertSeconds = 5;
        private static DBProvider mainDBProvider;
        internal static List<string> rgbExtensions;
        internal static List<string> audioExtraFiles;
        internal static long lastRequestTicket = DateTime.Now.Ticks;
        internal static List<string> directShowExtensions;
        public static event EventHandler<MediaItemCallbackArgs> OnInsert;
        public static event EventHandler<MediaItemNewThumbArgs> OnThumbUpdate;
        private static Dictionary<string, string> propertyTable;
        public static string MediaDateDefaultFormatString = "yyMMdd-HHmm-ss";
        private static Stack<MediaItem> DeletedHistory;
        private static List<Role> allRoles;
        private static List<Role> connectedRoles;
        private static CategoryTree categoryTreeSingleton;
        private static FolderTree folderTreeSingleton;
        private static ObservableCollection<MediaItem> bookmarkedSingleton, deletedSingleton;
        public static MediaItemCache GlobalMediaItemCache = new MediaItemCache();
        public static List<string> ReleaseWriteProtectionOnClosing;

        public static bool IsCheckedMenuItemTrim, IsCheckedMenuItemOrientate, IsCheckedMenuItemCrop, IsCheckedMenuItemClip, IsCheckedMenuItemRotate, IsCheckedMenuItemLevels, IsCheckedMenuItemZoom, IsCheckedMenuItemFlip;
        public static bool IsCheckedMenuItemCategories = true;
        public static string DriveMappingLetter;
        public static bool HasGeodata = false;

        public static void ResetContext()
        {
            bookmarkedSingleton = null;
            deletedSingleton = null;
            GlobalMediaItemCache.Clear();
            folderTreeSingleton = null;
            categoryTreeSingleton = null;
        }

        public static void ResetCategoryTree()
        {
            categoryTreeSingleton = null;
        }

        public static void ResetFolderTree()
        {
            folderTreeSingleton = null;
        }

        private static Category parentCat;
        public static Category CategoryTagParent
        {
            get
            {
                foreach (Category searchCat in MediaBrowserContext.CategoryTreeSingelton.Children)
                {
                    if (searchCat.Name == MediaBrowserContext.CategoryTagsName)
                    {
                        parentCat = searchCat;
                        break;
                    }
                }

                if (parentCat == null)
                {
                    parentCat = new Category(MediaBrowserContext.CategoryTreeSingelton);
                    parentCat.Parent = null;
                    parentCat.Name = MediaBrowserContext.CategoryTagsName;
                    parentCat.Sortname = MediaBrowserContext.CategoryTagsName;
                    parentCat.Description = "Sammlung unsortierter Kategorien.";
                    MediaBrowserContext.SetCategory(parentCat);

                    MediaBrowserContext.CategoryTreeSingelton.ChangeVersion++;
                    MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Add(parentCat);
                }

                return parentCat;
            }
        }

        public enum MissingFileBehaviorType { DEFAULT, SHOW, IGNORE, DELETE };

        private static MissingFileBehaviorType missingFileBehavior;
        public static MissingFileBehaviorType MissingFileBehavior
        {
            get
            {
                if (missingFileBehavior == MissingFileBehaviorType.DEFAULT)
                {
                    missingFileBehavior = MissingFileBehaviorType.SHOW;
                    MissingFileBehaviorType mb;
                    if (MediaBrowserContext.GetDBProperty("MissingFileBehavior") != null)
                    {
                        if (Enum.TryParse(MediaBrowserContext.GetDBProperty("MissingFileBehavior").Trim(), out mb))
                        {
                            missingFileBehavior = mb;
                        }
                    }
                }

                return missingFileBehavior;
            }

            set
            {
                if (value != MissingFileBehaviorType.DELETE)
                    MediaBrowserContext.SetDBProperty("MissingFileBehavior", value.ToString());

                if (missingFileBehavior != value)
                    GlobalMediaItemCache.Clear();

                missingFileBehavior = value;
            }
        }

        public static ObservableCollection<MediaItem> BookmarkedSingleton
        {
            get
            {
                if (bookmarkedSingleton == null && MainDBProvider != null)
                {
                    bookmarkedSingleton = new ObservableCollection<MediaItem>(GetBookmarkedMediaItems());
                }

                return bookmarkedSingleton;
            }
        }

        public static ObservableCollection<MediaItem> DeletedSingleton
        {
            get
            {
                if (deletedSingleton == null && MainDBProvider != null)
                {
                    deletedSingleton = new ObservableCollection<MediaItem>(GetDeletedMediaItems());
                }

                return deletedSingleton;
            }
        }

        public static string MyDocumentsFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MediaBrowserWpf");
            }
        }

        /// <summary>
        /// Liefert true, wenn die Datenbank existent und valide ist.
        /// </summary>
        /// <param name="dbPath"></param>
        /// <returns></returns>
        public static bool Init(string dbPath)
        {
            try
            {
                if (dbPath == null || !Directory.Exists(Path.GetDirectoryName(dbPath)))
                {
                    dbPath = MyDocumentsFolder;
                    Directory.CreateDirectory(dbPath);

                    dbPath = Path.Combine(dbPath, "MediaBrowser4DB.mb4");
                }

                if (!System.IO.File.Exists(dbPath))
                {
                    DBAdministration.CreateDB(dbPath);
                }

                SQLiteProvider sqliteProvider = new SQLiteProvider(dbPath);

                if (sqliteProvider.ValidateDB())
                {
                    MainDBProvider = sqliteProvider;
                }
                else
                {
                    MediaBrowser4.MediaBrowserContext.MainDBProvider = null;
                    return false;
                }

                if (!dbPath.ToLower().StartsWith("d:\\")
                    && !dbPath.ToLower().StartsWith("l:\\")
                    && !dbPath.ToLower().StartsWith("c:\\"))
                {
                    DriveMappingLetter = Path.GetPathRoot(dbPath);
                }

                propertyTable = null;

                CheckSumMaxLength = Convert.ToInt32(GetDBProperty("CheckSumMaxLength"));
                CommitInsertSeconds = Convert.ToInt32(GetDBProperty("CommitInsertSeconds"));
                DefaultRoleID = Convert.ToInt32(GetDBProperty("DefaultRoleID"));
                ThumbnailJPEGQuality = Convert.ToInt32(GetDBProperty("ThumbnailJPEGQuality"));
                ThumbnailSize = Convert.ToInt32(GetDBProperty("ThumbnailSize"));
                AutoCategorizeDate = GetDBProperty("AutoCategorizeDate") == "1";
                
                if (Directory.Exists(GPSLoggerPath))
                {
                    List<GpsFile> fileList = GetGpsFileList();
                    List<GeoPoint> gpsList = KmlHelper.ParseFolder(GPSLoggerPath, fileList);
                    HasGeodata = fileList.Count > 0 || gpsList.Count > 0;
                    InsertGpsPoints(gpsList);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }

            return false;
        }

        public static string GPSLoggerPath
        {
            get
            {
                string gpsLoggerPath = @"H:\Google Drive\GPSLog";// GetDBProperty("GPSLoggerPath");

                if (!Directory.Exists(gpsLoggerPath))
                {
                    gpsLoggerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Google Drive\GPSLog\");
                }

                return gpsLoggerPath;
            }
        }

        public static string MapPathRoot(string path)
        {
            if (DriveMappingLetter == null || path == null)
            {
                return path;
            }
            else
            {
                if (path.Length < DriveMappingLetter.Length) return DriveMappingLetter;
                return DriveMappingLetter + path.Substring(DriveMappingLetter.Length);
            }
        }

        public static string MapPathRootReverse(string mappedPath)
        {
            if (DriveMappingLetter == null || mappedPath == null)
            {
                return mappedPath;
            }
            else
            {
                return "D:\\" + mappedPath.Substring(3);
            }
        }

        public static List<MediaItem> GetMediaItems(MediaItemRequest request)
        {
            if (request is MediaItemVirtualRequest)
            {
                List<MediaItem> mediaItemlist = new List<MediaItem>();
                foreach (string path in ((MediaItemVirtualRequest)request).FileList)
                {
                    MediaItem mItem = MediaBrowserContext.GetMediaItemFromFile(path);

                    if (mItem != null)
                    {
                        mItem.FileLength = mItem.FileObject.Length;
                        mItem.CreationDate = mItem.FileObject.CreationTime;
                        mItem.MediaDate = mItem.FileObject.CreationTime;

                        mItem.GetThumbnail();

                        mediaItemlist.Add(mItem);
                        mItem.Id = mediaItemlist.Count * -1;
                        mItem.VariationId = mItem.Id;
                        mItem.VariationIdDefault = mItem.Id;

                        mItem.SetInfoThumbnail();
                    }
                }

                return mediaItemlist;
            }
            else if (MainDBProvider != null)
            {
                return MainDBProvider.GetMediaItems(request);
            }
            else
                return null;
        }


        public static List<MediaItemRequest> GetUserDefinedRequests()
        {
            if (MainDBProvider != null)
            {
                return MainDBProvider.GetUserDefinedRequests();
            }
            else
                return null;
        }

        public static MediaItemSqlRequest GetSqlRequest(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            if (MainDBProvider != null)
            {
                return MainDBProvider.GetSqlRequest(mediaItemList);
            }
            else
                return null;
        }

        public static void DeleteUserDefinedRequest(MediaItemRequest request)
        {
            if (mainDBProvider != null)
                mainDBProvider.DeleteUserDefinedRequest(request);
        }

        /// <summary>
        /// Liefert true, wenn ein neues Element angelegt wurde,
        /// false wenn dieses nur aktualisiert wurde.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static bool SaveUserDefinedRequest(MediaItemRequest request)
        {
            if (mainDBProvider != null)
                return mainDBProvider.SaveUserDefinedRequest(request);
            else
                return false;
        }

        public static List<Role> AllRoles
        {
            get
            {
                if (allRoles == null && mainDBProvider != null)
                {
                    allRoles = mainDBProvider.GetRoleList();
                    connectedRoles = new List<Role>();
                    foreach (Role role in allRoles)
                    {
                        if (role.Id == Convert.ToInt32(GetDBProperty("DefaultRoleID")))
                        {
                            connectedRoles.Add(role);
                            break;
                        }
                    }
                }
                return allRoles;
            }
        }

        public static void InvokeCategoriesChanged(List<MediaItem> mItemList, List<MediaBrowser4.Objects.Category> categoryChangeList, bool removeCategory)
        {
            if (CategoriesChanged != null)
            {
                CategoriesChanged.Invoke(null, new MediaItemArg() { MediaItemList = mItemList, CategoryList = categoryChangeList, RemoveCategory = removeCategory });
            }
        }

        public static List<Role> ConnectedRoles
        {
            get
            {
                if (connectedRoles == null)
                {
                    List<Role> a = AllRoles;
                }
                return connectedRoles;
            }
        }

        public static void AbortInsert()
        {
            abortInsert = true;
        }

        public static System.Drawing.Size PreviewSize
        {
            get
            {
                return previewSize;
            }

            set
            {
                previewSize = value;
            }
        }

        public static bool AutoCategorizeDate;

        public static int ThumbnailSize
        {
            get
            {
                return thumbnailSize;
            }

            set
            {
                thumbnailSize = value;
            }
        }

        public static long LastRequestTicket
        {
            get
            {
                return lastRequestTicket;
            }
        }

        public static int CheckSumMaxLength
        {
            get { return checkSumMaxLength; }
            set { checkSumMaxLength = value; }
        }

        public static int ThumbnailJPEGQuality
        {
            get { return thumbnailJPEGQuality; }
            set { thumbnailJPEGQuality = value; }
        }

        public static int CommitInsertSeconds
        {
            get { return commitInsertSeconds; }
            set { commitInsertSeconds = value; }
        }

        public static string DBGuid
        {
            get
            {
                if (mainDBProvider != null)
                    return mainDBProvider.Guid;
                else
                    return null;
            }
        }

        public static void CleanDB()
        {
            if (mainDBProvider != null)
                mainDBProvider.CleanDB();
        }

        public static string DBName
        {
            get
            {
                if (mainDBProvider != null)
                    return mainDBProvider.DBName;
                else
                    return null;
            }
        }

        public static string DBUser
        {
            get
            {
                if (mainDBProvider != null)
                    return mainDBProvider.User;
                else
                    return null;
            }
        }

        public static string DBHost
        {
            get
            {
                if (mainDBProvider != null)
                    return mainDBProvider.Host;
                else
                    return null;
            }
        }

        public static bool SetVariationDefault(MediaItem mItem, Variation variation)
        {
            if (mainDBProvider != null)
                return mainDBProvider.SetVariationDefault(mItem, variation);
            else
                return false;
        }

        public static void RenameMediaItem(MediaItem mediaItem, string newName)
        {
            if (MainDBProvider != null)
                mainDBProvider.RenameMediaItem(mediaItem, newName);
        }

        public static bool SetVariationDefault(MediaItem mItem)
        {
            if (mainDBProvider != null)
                return mainDBProvider.SetVariationDefault(mItem, null);
            else
                return false;
        }

        public static bool RemoveVariation(MediaItem mItem)
        {
            if (mainDBProvider != null)
                return mainDBProvider.RemoveVariation(mItem);
            else
                return false;
        }

        public static bool RemoveVariation(MediaItem mItem, string name)
        {
            if (mainDBProvider != null)
                return mainDBProvider.RemoveVariation(mItem, name);
            else
                return false;
        }

        public static void ReplaceVariations(Dictionary<MediaItem, List<Variation>> mediaItemDic)
        {
            if (mainDBProvider != null)
                mainDBProvider.ReplaceVariations(mediaItemDic);

        }

        public static bool EditVariation(List<Variation> variationList)
        {
            if (mainDBProvider != null)
                return mainDBProvider.EditVariation(variationList);
            else
                return false;
        }

        public static Variation SetNewVariation(MediaItem mediaItem, string name)
        {
            if (mainDBProvider != null)
                return mainDBProvider.SetNewVariation(mediaItem, name, false, null, null, null);
            else
                return null;
        }

        public static Variation SetNewVariation(MediaItem mediaItem, string name, string layerAction, string layerEdit, byte[] thumbData)
        {
            if (mainDBProvider != null)
                return mainDBProvider.SetNewVariation(mediaItem, name, false, layerAction, layerEdit, thumbData);
            else
                return null;
        }

        public static Variation SetNewVariation(MediaItem mediaItem, string name, bool setDefault)
        {
            if (mainDBProvider != null)
                return mainDBProvider.SetNewVariation(mediaItem, name, setDefault, null, null, null);
            else
                return null;
        }

        public static bool IsDublicate(MediaBrowser4.Objects.MediaItem mItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
        {
            if (mainDBProvider != null)
                return mainDBProvider.IsDublicate(mItem, criteria);
            else
                return false;
        }

        public static PreviewObject GetImagePreviewDB(int variationId)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetImagePreviewDB(variationId, previewSize);
            else
                return null;
        }

        public static MediaProcessing.FaceDetection.Faces GetFaceDetectionPreviewDB(int variationId)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetFaceDetectionPreviewDB(variationId, previewSize);
            else
                return new MediaProcessing.FaceDetection.Faces();
        }

        public static Dictionary<int, int> GetAllFaces(double min, double max, int countMin, int countMax, List<MediaItem> itemList)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetAllFaces(previewSize, min, max, countMin, countMax, itemList);
            else
                return null;
        }

        public static bool IsImageInPreviewDB(int variationId)
        {
            if (mainDBProvider != null)
                return mainDBProvider.IsImageInPreviewDB(variationId, previewSize);
            else
                return false;
        }

        public static void WriteToPreviewDB(List<string> fileList)
        {
            if (mainDBProvider != null)
                mainDBProvider.WriteToPreviewDB(fileList, previewSize);
        }

        public static void DeleteFromPreviewDB(List<MediaItem> mediaItemList)
        {
            if (mainDBProvider != null)
                mainDBProvider.DeleteFromPreviewDB(mediaItemList, previewSize);
        }

        public static List<int> GetPreviewDBVariationIdList()
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetPreviewDBVariationIdList(previewSize);
            else
                return new List<int>();
        }

        public static List<MediaBrowser4.Objects.Variation> GetVariations(MediaBrowser4.Objects.MediaItem mItem)
        {
            if (mainDBProvider != null)
            {
                return mainDBProvider.GetVariations(mItem, false);
            }
            else
                return null;
        }

        public static List<MediaBrowser4.Objects.Variation> GetVariations(MediaBrowser4.Objects.MediaItem mItem, bool getThumbData)
        {
            if (mainDBProvider != null)
            {
                return mainDBProvider.GetVariations(mItem, getThumbData);
            }
            else
                return null;
        }

        public static void SetRole(List<MediaBrowser4.Objects.MediaItem> mList, int roleId)
        {
            if (mainDBProvider != null)
                mainDBProvider.SetRole(mList, roleId);
        }

        public static void SetPriority(List<MediaBrowser4.Objects.MediaItem> mList, int priority)
        {
            if (mainDBProvider != null)
                mainDBProvider.SetPriority(mList, priority);
        }

        public static List<Description> GetDescription(List<MediaItem> mList)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetDescription(mList);
            else
                return new List<Description>();
        }

        public static string GetDescription(MediaItem mItem)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetDescription(mItem);
            else
                return null;
        }

        public static Dictionary<MediaItem, XmlNode> CreateMediaItemFromXml(List<XmlNode> nodeList, Folder folder, List<Category> categoryImportList)
        {
            if (mainDBProvider != null)
                return mainDBProvider.CreateMediaItemFromXml(nodeList, folder, categoryImportList);
            else
                return null;
        }

        public static void SetDescription(Description desc)
        {
            if (mainDBProvider != null)
                mainDBProvider.SetDescription(desc);
        }

        public static void SetAttachment(List<Attachment> attachmentList)
        {
            if (mainDBProvider != null)
                mainDBProvider.SetAttachment(attachmentList);
        }

        public static List<Attachment> GetAttachment(List<MediaItem> mediaItemList)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetAttachment(mediaItemList);
            else
                return new List<Attachment>();
        }

        public static List<Category> GetCategories(int variationId)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetCategories(variationId);
            else
                return null;
        }

        public static List<GpsFile> GetGpsFileList()
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetGpsFileList();
            else
                return null;
        }

        public static List<GeoPoint> GetGpsList(DateTime from, DateTime to)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetGpsList(from, to);
            else
                return null;
        }

        public static GeoPoint GetGpsNearest(DateTime date)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetGpsNearest(date);
            else
                return null;
        }

        public static void SetGeodata(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            if (mainDBProvider != null)
                mainDBProvider.SetGeodata(mediaItemList);   
        }

        public static void InsertGpsPoints(List<GeoPoint> gpsList)
        {
            if (mainDBProvider != null)
                mainDBProvider.InsertGpsPoints(gpsList);
        }

        public static int DeleteGpsFile(DateTime fileTime)
        {
            if (mainDBProvider != null)
                return mainDBProvider.DeleteGpsFile(fileTime);
            else
                return -1;
        }












        public static List<Layer> GetLayers(Variation variation)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetLayers(variation);
            else
                return null;
        }


        public static Dictionary<string, DateTime> LastAddedFolders()
        {
            if (mainDBProvider != null)
                return mainDBProvider.LastAddedFolders();
            else
                return null;
        }

        public static byte[] GetThumbJpegData(int variationId)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetThumbJpegData(variationId);
            else
                return null;
        }

        public static string DBPath
        {
            get
            {
                if (mainDBProvider != null)
                    return ((MediaBrowser4.DB.SQLite.SQLiteProvider)mainDBProvider).DBPath;
                else
                    return null;
            }
        }

        public static string WorkingDirectory
        {
            get
            {
                if (DBPath == null || DBPath.Trim().Length == 0 || !Directory.Exists(Path.GetDirectoryName(DBPath)))
                {
                    string dbPath = MyDocumentsFolder;
                    Directory.CreateDirectory(dbPath);

                    return dbPath;
                }
                else if (Path.GetDirectoryName(DBPath) == null)
                    return DBPath;
                else if (Path.GetDirectoryName(Path.GetDirectoryName(DBPath)) == null)
                    return Path.GetDirectoryName(DBPath);
                else
                    return Path.GetDirectoryName(Path.GetDirectoryName(DBPath));
            }
        }

        public static string DBPreviewPath
        {
            get
            {
                return DBAdministration.GetThumbNailDBPath(DBPath, previewSize);
            }
        }

        static string soundFolder;
        public static string DBSoundFolder
        {
            get
            {
                if (soundFolder == null || !Directory.Exists(soundFolder))
                {
                    soundFolder = MediaBrowserContext.DBTempFolder;

                    while (soundFolder != null && soundFolder.Length != 0)
                    {
                        if (System.IO.Directory.GetDirectories(soundFolder, "sound").Length > 0)
                        {
                            soundFolder = System.IO.Directory.GetDirectories(soundFolder, "sound")[0];
                            break;
                        }
                        soundFolder = System.IO.Path.GetDirectoryName(soundFolder);
                    }
                }

                return soundFolder;
            }
        }

        public static string DBTempFolder
        {
            get
            {
                string path = null;
                if (mainDBProvider != null && mainDBProvider is MediaBrowser4.DB.SQLite.SQLiteProvider)
                {
                    string tmp = System.IO.Path.GetDirectoryName(
                        ((MediaBrowser4.DB.SQLite.SQLiteProvider)mainDBProvider).DBPath) + "\\" +
                        System.IO.Path.GetFileNameWithoutExtension(
                        ((MediaBrowser4.DB.SQLite.SQLiteProvider)mainDBProvider).DBPath + "TMP");

                    try
                    {
                        if (!System.IO.Directory.Exists(tmp))
                            System.IO.Directory.CreateDirectory(tmp);
                        path = tmp;
                    }
                    catch { }

                }
                return path;
            }
        }

        public static DBProvider MainDBProvider
        {
            get { return mainDBProvider; }
            set
            {
                mainDBProvider = value;
                propertyTable = null;
                if (mainDBProvider != null)
                {
                    mainDBProvider.OnInsert += new EventHandler<MediaItemCallbackArgs>(mainDBProvider_OnInsert);
                    mainDBProvider.OnThumbUpdate += new EventHandler<MediaItemNewThumbArgs>(mainDBProvider_OnThumbUpdate);
                }
            }
        }

        static void mainDBProvider_OnThumbUpdate(object sender, MediaItemNewThumbArgs e)
        {
            if (OnThumbUpdate != null)
                OnThumbUpdate(sender, e);
        }

        static void mainDBProvider_OnInsert(object sender, MediaItemCallbackArgs e)
        {
            if (OnInsert != null)
                OnInsert(sender, e);
        }

        public static string SetRGBExtensions
        {
            set
            {
                rgbExtensions = GetExtensionList(value);
            }
        }

        public static string SetAudioExtraFiles
        {
            set
            {
                audioExtraFiles = GetExtensionList(value);
            }
        }

        public static string SetDirectShowExtensions
        {
            set
            {
                directShowExtensions = GetExtensionList(value);
            }
        }

        public static List<MediaBrowser4.Objects.Folder> GetFolderlist()
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetFolderlist();
            else
                return null;
        }

        public static List<MediaBrowser4.Objects.MediaItem> GetDublicates(List<MediaBrowser4.Objects.MediaItem> mList, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetDublicates(mList, criteria);
            else
                return null;
        }

        public static List<MetaData> GetMetaDataFromMediaItems(List<MediaItem> mItemList)
        {
            if (mainDBProvider != null)
                return mainDBProvider.GetMetaDataFromMediaItems(mItemList);
            else
                return null;
        }

        public static List<MediaItem> GetMediaItemsFromFolders(List<Folder> folderList, string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsFromFolders(folderList, sortString, limtRequest);
            else
                return null;
        }

        public static void CopyToFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, MediaBrowser4.Objects.Folder folder)
        {
            if (MainDBProvider != null)
                MainDBProvider.CopyToFolder(mediaItemList, folder);
        }

        public static List<MediaItem> GetBookmarkedMediaItems()
        {
            return GetMediaItemsFromBookmarks(null, LimitRequest);
        }

        public static List<MediaItem> GetMediaItemsFromBookmarks(string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsFromBookmarks(sortString, limtRequest);
            else
                return null;
        }

        public static List<MediaItem> GetMediaItemFromFileName(string filename)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsFromFileName(filename);
            else
                return null;
        }

        public static MediaItem GetMediaItemFromFullname(string file)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemFromFile(file);
            else
                return null;
        }

        public static List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromSearchToken(
            MediaBrowser4.Objects.SearchToken searchToken, bool storeView, string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsFromSearchToken(searchToken, storeView, sortString, limtRequest);
            else
                return null;
        }

        public static List<Category> GetCategoriesFromMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            if (MainDBProvider != null)
            {
                List<MediaItem> mItemList = new List<MediaItem>();
                mItemList.Add(mItem);
                return new List<Category>(mainDBProvider.GetCategoriesFromMediaItems(mItemList).Keys);
            }
            else
                return new List<Category>();
        }

        public static List<Category> GetCategoriesLocationGeoData(double longitute, double width, double latitude, double height)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetCategoriesLocationGeoData(longitute, width, latitude, height);
            else
                return new List<Category>();
        }

        public static List<Category> GetCategoriesDiaryGeoData(double longitute, double width, double latitude, double height)
        {
            if (MainDBProvider != null)            
                return mainDBProvider.GetCategoriesDiaryGeoData(longitute, width, latitude, height);            
            else
                return new List<Category>();
        }

        public static List<MediaItem> GetMediaItemsGeoData(double longitute, double width, double latitude, double height)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsGeoData(longitute, width, latitude, height, LimitRequest);
            else
                return new List<MediaItem>();
        }

        public static Dictionary<MediaBrowser4.Objects.Category, int> GetCategoriesFromMediaItems(List<MediaItem> mItemList)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetCategoriesFromMediaItems(mItemList);
            else
                return new Dictionary<MediaBrowser4.Objects.Category, int>();
        }

        public static void UpdateDublicate(MediaBrowser4.Objects.MediaItem mediaItem)
        {
            if (MainDBProvider != null)
                mainDBProvider.UpdateDublicate(mediaItem);
        }

        public static void SetThumbForMediaItem(System.Drawing.Bitmap bmp, MediaBrowser4.Objects.MediaItem mItem)
        {
            if (MainDBProvider != null && mItem != null && bmp != null)
                mainDBProvider.SetThumbForMediaItem(bmp, mItem);
        }

        public static List<MediaItem> GetDeletedMediaItems()
        {
            return GetMediaItemsFromTrashfolder(null, LimitRequest);
        }

        public static int CanFoundByChecksum(string md5)
        {
            if (MainDBProvider != null)
                return mainDBProvider.CanFoundByChecksum(md5);
            else
                return -1;
        }

        public static List<MediaItem> GetMediaItemsFromTrashfolder(string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsFromTrashfolder(sortString, limtRequest);
            else
                return null;
        }

        public static void SetFolder(Folder folder)
        {
            if (MainDBProvider != null)
                mainDBProvider.SetFolder(folder);
        }

        public static List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingThumbs(string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsWithMissingThumbs(sortString, limtRequest);
            else
                return null;
        }

        public static int CountItemsInFolder(Folder folder)
        {
            if (MainDBProvider != null)
                return mainDBProvider.CountItemsInFolder(folder);
            else
                return 0;
        }

        public static void RemoveFolder(Folder folder)
        {
            if (MainDBProvider != null)
                mainDBProvider.RemoveFolder(folder);
        }

        public static List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingFiles(string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsWithMissingFiles(sortString, limtRequest);
            else
                return null;
        }

        public static List<MediaItem> GetMediaItemsForRequestTicket(long requestTicket, string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsForRequestTicket(requestTicket, sortString, limtRequest);
            else
                return null;
        }

        public static List<MediaItem> GetMediaItemsFromCategories(List<Category> categoryList, bool isIntersection, string sortString, int limtRequest)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMediaItemsFromCategories(categoryList, isIntersection, true, sortString, limtRequest);
            else
                return null;
        }

        public static void RemoveFromDB(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            if (MainDBProvider != null)
                mainDBProvider.RemoveFromDB(mediaItemList);
        }

        public static Exception RemoveAndRecycle(MediaItem mediaItem)
        {
            if (MainDBProvider != null)
                return mainDBProvider.RemoveAndRecycle(mediaItem);
            else
                return null;
        }

        public static string GetSQLFromRequestTicket(long ticket)
        {
            if (MainDBProvider != null)
            {
                return MainDBProvider.GetSQLFromRequestTicket(ticket);
            }
            else
            {
                return null;
            }
        }

        public static void SetSQLForRequestTicket(long ticket, string sql)
        {
            if (MainDBProvider != null)
            {
                MainDBProvider.SetSQLForRequestTicket(ticket, sql);
            }
        }

        public static void InsertMeteorologyData(SortedDictionary<DateTime, Tuple<double, double>> data)
        {
            if (MainDBProvider != null)
            {
                MainDBProvider.InsertMeteorologyData(data);
            }
        }

        public static SortedDictionary<DateTime, Tuple<double, double>> GetMeteorologyData(DateTime start, DateTime stop)
        {
            if (MainDBProvider != null)
            {
                return MainDBProvider.GetMeteorologyData(start, stop);
            }
            else
                return new SortedDictionary<DateTime, Tuple<double, double>>();
        }

        public static void UpdateMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            if (MainDBProvider != null)
                mainDBProvider.UpdateMediaItem(mItem);
        }

        public static void GetThumbsForAllMediaItems(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            if (MainDBProvider != null)
                mainDBProvider.GetThumbsForAllMediaItems(mediaItemList);
        }

        public static void SetBookmark(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            if (MainDBProvider != null)
                mainDBProvider.SetBookmark(mediaItemList, set);
        }

        public static void SetDeleted(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            if (MainDBProvider != null)
            {
                mainDBProvider.SetDeleted(mediaItemList, set);

                if (DeletedHistory == null)
                {
                    DeletedHistory = new Stack<MediaItem>();
                }

                if (set)
                    foreach (MediaItem mItem in mediaItemList)
                        DeletedHistory.Push(mItem);
            }
        }

        public static MediaItem UndoDeleted()
        {
            MediaItem mItem = null;
            if (MainDBProvider != null && DeletedHistory != null)
            {
                while (DeletedHistory.Count > 0 && (mItem == null || !mItem.IsDeleted))
                {
                    mItem = DeletedHistory.Pop();
                }

                if (mItem != null && mItem.IsDeleted)
                {
                    SetDeleted(new List<MediaItem>() { mItem }, false);
                }
                else
                {
                    mItem = null;
                }
            }
            return mItem;
        }

        public static void SetCategory(Category category)
        {
            if (MainDBProvider != null)
            {
                mainDBProvider.SetCategory(category);
            }
        }

        public static MediaBrowser4.Objects.Category SetCategoryByPath(string catPath, string catSortPath)
        {
            try
            {
                if (mainDBProvider != null)
                    return mainDBProvider.SetCategoryByPath(catPath, catSortPath);
                else
                    return null;
            }
            catch { }

            return null;
        }

        public static bool RemoveCategory(MediaBrowser4.Objects.Category category)
        {
            if (MainDBProvider != null)
            {
                if (mainDBProvider.RemoveCategory(category))
                {
                    category.Remove();
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public static void SetTrashFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            if (MainDBProvider != null)
                mainDBProvider.SetTrashFolder(mediaItemList, set);
        }

        public static void AdjustMediaDate(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            if (MainDBProvider != null)
                mainDBProvider.AdjustMediaDate(mediaItemList);
        }


        public static FolderTree FolderTreeSingelton
        {
            get
            {
                if (folderTreeSingleton == null && MainDBProvider != null)
                {
                    List<Folder> folderList = mainDBProvider.GetFolderlist();
                    if (folderList != null)
                    {
                        folderTreeSingleton = new FolderTree(folderList);
                    }
                }

                return folderTreeSingleton;
            }
        }

        public static CategoryTree CategoryTreeSingelton
        {
            get
            {
                if (categoryTreeSingleton == null && MainDBProvider != null)
                {
                    categoryTreeSingleton = mainDBProvider.GetCategoryTree();
                    categoryTreeSingleton.FullCategoryCollection.CollectionChanged += FullCategoryCollection_CollectionChanged;
                }
                else if (categoryTreeSingleton == null && MainDBProvider == null)
                {
                    categoryTreeSingleton = new CategoryTree(new CategoryCollection(), new Dictionary<int, MediaBrowser4.Objects.Category>());
                    categoryTreeSingleton.FullCategoryCollection.CollectionChanged += FullCategoryCollection_CollectionChanged;
                }

                return categoryTreeSingleton;
            }
        }

        public static bool UpdateCatecoryCalendar = false;
        private static void FullCategoryCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var cat in e.NewItems)
                {
                    if (cat is Category && ((Category)cat).IsDate && !((Category)cat).FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                    {
                        UpdateCatecoryCalendar = true;
                        break;
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (var cat in e.OldItems)
                {
                    if (cat is Category && ((Category)cat).IsDate && !((Category)cat).FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                    {
                        UpdateCatecoryCalendar = true;
                        break;
                    }
                }
            }
        }

        public static void Rotate90(MediaItem mItem)
        {
            if (MainDBProvider != null)
                mainDBProvider.Rotate90(mItem);
        }

        public static Dictionary<int, string> GetDublicates(MediaBrowser4.Objects.MediaItem mediaItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetDublicates(mediaItem, criteria);
            else
                return new Dictionary<int, string>();
        }

        public static void SetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            if (MainDBProvider != null)
                mainDBProvider.SetLayersForMediaItem(mItem);
        }

        public static void SetLayersForMediaItems(List<MediaItem> mItemList)
        {
            if (MainDBProvider != null)
                mainDBProvider.SetLayersForMediaItems(mItemList);
        }

        public static List<MediaBrowser4.Objects.Layer> GetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetLayersForMediaItem(mItem);
            else
                return new List<MediaBrowser4.Objects.Layer>();
        }

        public static List<string> GetCategoryIdentifiersFromMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetCategoryIdentifiersFromMediaItem(mItem);
            else
                return null; ;
        }

        public static void CategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList)
        {
            if (MainDBProvider != null)
            {
                mainDBProvider.CategorizeMediaItems(mList, cList);
            }
        }

        public static void UnCategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList)
        {
            if (MainDBProvider != null)
                mainDBProvider.UnCategorizeMediaItems(mList, cList);
        }


        public static void SaveDBProperties()
        {
            try
            {
                if (MainDBProvider != null)
                    mainDBProvider.SaveDBProperties(propertyTable);
            }
            catch
            {
            }
        }

        internal static int ExecuteNonQuery(string sql)
        {
            if (MainDBProvider != null)
                return mainDBProvider.ExecuteNonQuery(sql);
            else
                return 0;
        }

        public static List<string> GetMetadataKeyList()
        {
            if (MainDBProvider != null)
                return mainDBProvider.GetMetadataKeyList();
            else
                return null;
        }

        public static void AddViewTime(MediaBrowser4.Objects.MediaItem mediaItem, int seconds)
        {
            try
            {
                if (MainDBProvider != null)
                    MainDBProvider.AddViewTime(mediaItem, seconds);
            }
            catch { }
        }

        public static List<string> InsertMediaItems(List<string> fileList)
        {
            List<string> updateList = new List<string>();
            if (MainDBProvider != null)
            {
                foreach (string file in fileList)
                {
                    MediaItem mItem = GetMediaItemFromFile(file);

                    if (mItem != null)
                    {
                        mItem.ThumbnailSize = (thumbnailSize == 0 ? 150 : thumbnailSize);
                        mainDBProvider.AddMediaItemAsync(mItem);
                        updateList.Add(file);
                    }
                }
            }
            return updateList;
        }

        public static void InsertMediaItems(List<MediaItem> mediaItemList)
        {
            List<string> updateList = new List<string>();
            if (MainDBProvider != null)
            {
                foreach (MediaItem mItem in mediaItemList)
                {
                    mItem.ThumbnailSize = (thumbnailSize == 0 ? 150 : thumbnailSize);
                    mainDBProvider.AddMediaItemAsync(mItem);

                }
            }
        }

        public static void SetDBProperty(string key, string value)
        {
            if (propertyTable == null)
            {
                LoadPropertyTable();
            }

            if (propertyTable.ContainsKey(key))
                propertyTable[key] = value;
            else
                propertyTable.Add(key, value);
        }

        public static string SelectedVideoPlayer
        {
            get
            {
                if (String.IsNullOrEmpty(GetDBProperty("SelectedVideoPlayer")))
                {
                    SetDBProperty("SelectedVideoPlayer", "MediaElement");
                    SaveDBProperties();
                }

                return GetDBProperty("SelectedVideoPlayer");
            }

            set
            {
                SetDBProperty("SelectedVideoPlayer", value);
            }
        }

        public static string GetDBProperty(string key)
        {
            if (MainDBProvider == null)
                return null;

            if (propertyTable == null)
            {
                LoadPropertyTable();
            }

            if (propertyTable.ContainsKey(key))
                return propertyTable[key];
            else
                return null;
        }

        private static void LoadPropertyTable()
        {
            propertyTable = new Dictionary<string, string>();

            using (MediaBrowser4.DB.ICommandHelper com = MainDBProvider.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT KEY, VALUE FROM DBPROPERTIES");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    propertyTable.Add(row["KEY"].ToString().Trim(), row["VALUE"].ToString().Trim());
                }
            }
        }

        public static bool HasKnownMediaExtension(string path)
        {
            if (directShowExtensions.Contains(Path.GetExtension(path).ToLower()))
            {
                return true;
            }
            else if (rgbExtensions.Contains(Path.GetExtension(path).ToLower()))
            {
                return true;
            }
            return false;
        }

        public static MediaItem GetMediaItemFromFile(string path)
        {
            MediaItem mItem = null;

            if (path.Split('\t').Length > 1)
            {
                path = path.Split('\t')[0];
            }

            if (!File.Exists(path))
                return null;

            if (directShowExtensions.Contains(Path.GetExtension(path).ToLower()))
            {
                mItem = new MediaItemVideo(new System.IO.FileInfo(path));
            }
            else if (rgbExtensions.Contains(Path.GetExtension(path).ToLower()))
            {
                mItem = new MediaItemBitmap(new System.IO.FileInfo(path));
            }

            return mItem;
        }

        public static void SetTemporaryWriteProtection(string filePath)
        {
            if (MediaBrowser4.Utilities.FilesAndFolders.SetReadOnlyAttribute(filePath, true))
            {
                if (MediaBrowserContext.ReleaseWriteProtectionOnClosing == null)
                    MediaBrowserContext.ReleaseWriteProtectionOnClosing = new List<string>();

                MediaBrowserContext.ReleaseWriteProtectionOnClosing.Add(filePath);
            }
        }

        public static void RealeaseWriteprotection()
        {
            try
            {
                if (MediaBrowserContext.ReleaseWriteProtectionOnClosing != null)
                    foreach (string filePath in MediaBrowserContext.ReleaseWriteProtectionOnClosing)
                        MediaBrowser4.Utilities.FilesAndFolders.SetReadOnlyAttribute(filePath, false);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        private static List<string> GetExtensionList(string extensions)
        {
            List<string> extensionList = new List<string>();

            foreach (string ext in extensions.Split(','))
            {
                extensionList.Add("." + ext.Trim().ToLower());
            }

            return extensionList;
        }
    }
}
