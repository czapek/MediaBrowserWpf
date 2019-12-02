using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser4.Objects;
using System.Xml;

namespace MediaBrowser4.DB
{
    public abstract class DBProvider : IDisposable
    {
        public abstract event EventHandler<MediaItemCallbackArgs> OnInsert;
        public abstract event EventHandler<MediaItemNewThumbArgs> OnThumbUpdate;

        public abstract ITransaction MBTransaction { get; }
        public abstract ICommandHelper MBCommand { get; }
        public abstract bool ValidateDB();
        public abstract void CreateDB();

        public abstract string Guid { get; set; }
        public abstract string DBName { get; set; }
        public abstract string User { get; set; }
        public abstract string Host { get; set; }


        public abstract int DeleteGpsFile(DateTime fileTime);
        public abstract void InsertGpsPoints(List<GeoPoint> gpsList);
        public abstract GeoPoint GetGpsNearest(DateTime date);
        public abstract List<GpsFile> GetGpsFileList();
        public abstract List<GeoPoint> GetGpsList(DateTime from, DateTime to);
        public abstract List<Category> GetCategories(int variationId);
        public abstract List<MediaItemRequest> GetUserDefinedRequests();       
        public abstract void DeleteUserDefinedRequest(MediaItemRequest request);
        public abstract bool SaveUserDefinedRequest(MediaItemRequest request);
        public abstract int CanFoundByChecksum(string md5);
        public abstract List<Description> GetDescription(List<MediaItem> mList);
        public abstract string GetDescription(MediaItem mItem);
        public abstract Dictionary<MediaItem, XmlNode> CreateMediaItemFromXml(List<XmlNode> nodeList, Folder folder, List<Category> categoryImportList);
        public abstract void SetDescription(Description desc);
        public abstract void SetAttachment(List<Attachment> attachmentList);
        public abstract List<Attachment> GetAttachment(List<MediaItem> mediaItemList);
        public abstract MediaItemSqlRequest GetSqlRequest(List<MediaBrowser4.Objects.MediaItem> mediaItemList);
        public abstract bool IsDublicate(MediaBrowser4.Objects.MediaItem mItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria);
        public abstract PreviewObject GetImagePreviewDB(int variationId, System.Drawing.Size size);
        public abstract MediaProcessing.FaceDetection.Faces GetFaceDetectionPreviewDB(int variationId, System.Drawing.Size size);
        public abstract Dictionary<int, int> GetAllFaces(System.Drawing.Size size, double min, double max, int countMin, int countMax, List<MediaItem> itemList);
        public abstract bool IsImageInPreviewDB(int variationId, System.Drawing.Size size);
        public abstract void DeleteFromPreviewDB(List<MediaItem> fmediaItemList, System.Drawing.Size size);
        public abstract void WriteToPreviewDB(List<string> fileList, System.Drawing.Size size);
        public abstract List<int> GetPreviewDBVariationIdList(System.Drawing.Size size);
        public abstract void CleanDB();
        public abstract void SetRole(List<MediaBrowser4.Objects.MediaItem> mList, int roleId);
        public abstract void SetPriority(List<MediaBrowser4.Objects.MediaItem> mList, int priority);
        public abstract int ExecuteNonQuery(string sql);
        public abstract bool RemoveVariation(MediaBrowser4.Objects.MediaItem mItem);
        public abstract bool RemoveVariation(MediaBrowser4.Objects.MediaItem mItem, string name);
        public abstract void RenameMediaItem(MediaItem mediaItem, string newName);
        public abstract bool SetVariationDefault(MediaItem mItem, Variation variation);
        public abstract bool EditVariation(List<Variation> variationList);
        public abstract Exception RemoveAndRecycle(MediaItem mItem);
        public abstract void AddMediaItemAsync(MediaBrowser4.Objects.MediaItem mItems);
        public abstract void Rotate90(MediaItem mItem);
        public abstract Variation SetNewVariation(MediaBrowser4.Objects.MediaItem mItem, string name, bool setDefault, string layerAction, string layerEdit, byte[] thumbData);
        public abstract List<MetaData> GetMetaDataFromMediaItems(List<MediaItem> mItemList);
        public abstract List<MediaBrowser4.Objects.Variation> GetVariations(MediaBrowser4.Objects.MediaItem mItem, bool getThumbdata);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItems(MediaItemRequest request);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromFolders(List<MediaBrowser4.Objects.Folder> folderList, string sortString, int limtRequest);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingFiles(string sortString, int limtRequest);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingThumbs(string sortString, int limtRequest);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromSearchToken(MediaBrowser4.Objects.SearchToken searchToken, bool storeView, string sortString, int limtRequest);
        public abstract void GetThumbsForAllMediaItems(List<MediaBrowser4.Objects.MediaItem> mediaItemList);
        public abstract List<MediaBrowser4.Objects.Folder> GetFolderlist();
        public abstract void CopyToFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, MediaBrowser4.Objects.Folder folder);
        public abstract Dictionary<int, string> GetDublicates(MediaBrowser4.Objects.MediaItem mediaItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetDublicates(List<MediaBrowser4.Objects.MediaItem> mList, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria);
        public abstract List<MediaBrowser4.Objects.Role> GetRoleList();
        public abstract CategoryTree GetCategoryTree();
        public abstract Dictionary<string, DateTime> LastAddedFolders();
        public abstract List<Layer> GetLayers(Variation variation);
        public abstract byte[] GetThumbJpegData(int variationId);
        public abstract List<MediaBrowser4.Objects.Category> GetCategoriesGeoData(double longitute, double width, double latitude, double height);
        public abstract Dictionary<MediaBrowser4.Objects.Category, int> GetCategoriesFromMediaItems(List<MediaItem> mItemList);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromCategories(List<MediaBrowser4.Objects.Category> categoryList, bool isIntersection, bool isRecursive, string sortString, int limtRequest);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromTrashfolder(string sortString, int limtRequest);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromBookmarks(string sortString, int limtRequest);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromChecksum(string checksum);
        public abstract MediaItem GetMediaItemFromFile(string file);
        public abstract List<MediaItem> GetMediaItemsFromFileName(string filename);
        public abstract void SetBookmark(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set);
        public abstract void SetDeleted(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set);
        public abstract void UpdateDublicate(MediaBrowser4.Objects.MediaItem mediaItem);
        public abstract void ReplaceVariations(Dictionary<MediaItem, List<Variation>> mediaItemDic);
        public abstract void SetCategory(Category category);
        public abstract void SetFolder(Folder folder);
        public abstract MediaBrowser4.Objects.Category SetCategoryByPath(string catPath, string catSortPath);
        public abstract bool RemoveCategory(MediaBrowser4.Objects.Category category);
        public abstract void RemoveFolder(Folder folder);
        public abstract int CountItemsInFolder(Folder folder);
        public abstract void AddViewTime(MediaBrowser4.Objects.MediaItem mediaItem, int seconds);
        public abstract void SetTrashFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set);
        public abstract void AdjustMediaDate(List<MediaBrowser4.Objects.MediaItem> mediaItemList);
        public abstract void RemoveFromDB(List<MediaBrowser4.Objects.MediaItem> mediaItemList);
        public abstract List<MediaBrowser4.Objects.MediaItem> GetMediaItemsForRequestTicket(long requestTicket, string sortString, int limtRequest);
        public abstract List<string> GetCategoryIdentifiersFromMediaItem(MediaBrowser4.Objects.MediaItem mItem);
        public abstract List<string> GetMetadataKeyList();
        public abstract List<MediaBrowser4.Objects.Layer> GetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem);
        public abstract void SetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem);
        public abstract void SetLayersForMediaItems(List<MediaItem> mItemList);
        public abstract void CategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList);
        public abstract void UnCategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList);
        public abstract void SaveDBProperties(Dictionary<string, string> propertyTable);
        public abstract void SetThumbForMediaItem(System.Drawing.Bitmap bmp, MediaBrowser4.Objects.MediaItem mItem);
        public abstract void UpdateMediaItem(MediaBrowser4.Objects.MediaItem mItem);
        public abstract string GetSQLFromRequestTicket(long ticket);
        public abstract void SetSQLForRequestTicket(long ticket, string sql);
        public abstract void InsertMeteorologyData(SortedDictionary<DateTime, Tuple<double, double>> data);
        public abstract SortedDictionary<DateTime, Tuple<double, double>> GetMeteorologyData(DateTime start, DateTime stop);

        void IDisposable.Dispose()
        {
          
        }

    }
}
