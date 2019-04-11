//using System;
//using System.Collections.Generic;
//using System.Text;
//using MediaBrowser4.Objects;

//namespace MediaBrowser4.DB.SQLServer
//{
//    public class SQLServerProvider 
//    {
//        public override event EventHandler<MediaItemCallbackArgs> OnInsert;

//        public override string Guid { get; set; }
//        public override string DBName { get; set; }
//        public override string User { get; set; }
//        public override string Host { get; set; }

//        void sqlInsert_OnInsert(object sender, MediaItemCallbackArgs e)
//        {
//            if (this.OnInsert != null)
//                this.OnInsert(this, e);
//        }

//        public override event EventHandler<MediaItemNewThumbArgs> OnThumbUpdate;
//        void sqlInsert_OnThumbUpdate(object sender, MediaItemNewThumbArgs e)
//        {
//            if (this.OnThumbUpdate != null)
//                this.OnThumbUpdate(this, e);
//        }

//        public override ITransaction MBTransaction
//        {
//            get
//            {
//                return null;
//            }
//        }

//        public override ICommandHelper MBCommand
//        {
//            get
//            {
//                return null;
//            }
//        }

//        public override int ExecuteNonQuery(string sql)
//        {
//            return 0;
//        }

//        public override bool ValidateDB()
//        {
//            return true;
//        }

//        public override List<MediaItemRequest> GetUserDefinedRequests()
//        {
//            return null;
//        }

//        public override void DeleteUserDefinedRequest(MediaItemRequest request) { }
//        public override bool SaveUserDefinedRequest(MediaItemRequest request) { return false;}

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItems(MediaItemRequest request)
//        {
//            return null;
//        }

//        public override System.Drawing.Bitmap GetImagePreviewDB(MediaBrowser4.Objects.MediaItem mItem, System.Drawing.Size size)
//        {
//            return null;
//        }

//        public override void SetDeleted(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
//        {
//        }

//        public override void WriteToPreviewDB(List<MediaBrowser4.Objects.Folder> mList, System.Drawing.Size size)
//        {
//        }     

//        public override List<MediaBrowser4.Objects.Variation> GetVariations(List<MediaBrowser4.Objects.MediaItem> mList)
//        {
//            return null;
//        }

//        public override List<MetaData> GetMetaDataFromMediaItems(List<MediaItem> mItemList)
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.Folder> GetFolderlist()
//        {
//            return null;
//        }

//        public override Exception RemoveAndRecycle(MediaItem mItem)
//        {
//            return null;
//        }

//        public override Dictionary<string, DateTime> LastAddedFolders()
//        {
//            return null;
//        }

//        public override void CleanDB()
//        {
//        }

//        public override void CreateDB()
//        {
//        }

//        public override List<Description> GetDescription(List<MediaItem> mList) { return null; }
//        public override void SetDescription(Description desc) { }
//        public override void SetAttachment(List<Attachment> attachmentList) { }
//        public override string GetDescription(MediaItem mItem) { return null; }
//        public override bool IsDublicate(MediaBrowser4.Objects.MediaItem mItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
//        {
//            return false;
//        }

//        public override void ReplaceVariations(Dictionary<MediaItem, List<Variation>> mediaItemDic)
//        {
//        }

//        public override void RemoveVariations(MediaBrowser4.Objects.MediaItem mItem, List<MediaBrowser4.Objects.Variation> variationList)
//        {
//        }

//        public override List<Attachment> GetAttachment(List<MediaItem> mediaItemList)
//        {
//            return null;
//        }

//        public override void RenameVariation(MediaBrowser4.Objects.Variation variation) { }

//        public override void AddMediaItemAsync(MediaBrowser4.Objects.MediaItem mItems)
//        {
//        }

//        public override void SetRole(List<MediaBrowser4.Objects.MediaItem> mList, string roleId)
//        {
//        }

//        public override void SetPriority(List<MediaBrowser4.Objects.MediaItem> mList, int priority)
//        {
//        }

//        public override List<Layer> GetLayers(Variation variation)
//        {
//            return null;
//        }

//        public override byte[] GetThumbJpegData(string variationId)
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromFolders(List<MediaBrowser4.Objects.Folder> folderList, string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override CategoryTree GetCategoryTree()
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.Variation> GetVariations(MediaBrowser4.Objects.MediaItem mItem)
//        {
//            return null;
//        }

//        public override void SetNewVariation(List<MediaBrowser4.Objects.MediaItem> mList, string name)
//        {
//        }

//        public override void GetThumbsForAllMediaItems(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
//        {

//        }   

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromCategories(List<MediaBrowser4.Objects.Category> categoryList, bool isIntersection, string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override void UpdateDublicate(MediaBrowser4.Objects.MediaItem mediaItem)
//        {

//        }
//        public override MediaBrowser4.Objects.Category SetCategoryByPath(string catPath, string catSortPath)
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromTrashfolder(string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromBookmarks(string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromFileList(string[] fileList, string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override void UpdateMediaItem(MediaBrowser4.Objects.MediaItem mItem)
//        {

//        }

//        public override void SetBookmark(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
//        {
//        }

//        public override void SetTrashFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
//        {
//        }

//        public override void AdjustMediaDate(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
//        {
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsForRequestTicket(long requestTicket, string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override List<string> GetCategoryIdentifiersFromMediaItem(MediaBrowser4.Objects.MediaItem mItem)
//        {
//            return null;
//        }

//        public override void RenameMediaItem(MediaBrowser4.Objects.MediaItem mediaItem, string newName)
//        {
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetDublicates(List<MediaBrowser4.Objects.MediaItem> mList, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
//        {
//            return null;
//        }

//        public override void CategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList)
//        {
//        }

//        public override void UnCategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList)
//        {
//        }

//        public override void RemoveFromDB(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
//        {
//        }

//        public override void Rotate90(int rotate, MediaBrowser4.Objects.MediaItem mItem)
//        {

//        }

//        public override void SetCategory(List<MediaBrowser4.Objects.Category> categoryList)
//        {
//        }

//        public override List<MediaBrowser4.Objects.Layer> GetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
//        {
//            return null;
//        }

//        public override void SetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
//        {
//        }

//        public override Dictionary<MediaBrowser4.Objects.Category, int> GetCategoriesFromMediaItems(List<MediaItem> mItemList)
//        {
//            return null;
//        }

//        public override void SetThumbForMediaItem(System.Drawing.Bitmap bmp, MediaBrowser4.Objects.MediaItem mItem)
//        {
//        }

//        public override void RemoveCategory(MediaBrowser4.Objects.Category category)
//        {
//        }

//        public override List<MediaBrowser4.Objects.Role> GetRoleList()
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromSearchToken(MediaBrowser4.Objects.SearchToken searchToken, bool storeView, string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override Dictionary<string, string> GetDublicates(MediaBrowser4.Objects.MediaItem mediaItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
//        {
//            return null;
//        }

//        public override List<string> GetMetadataKeyList()
//        {
//            return null;
//        }

//        public override void UpdateFolders(List<MediaBrowser4.Objects.Folder> allFolders)
//        {
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingThumbs(string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingFiles(string sortString, int limtRequest)
//        {
//            return null;
//        }

//        public override void CopyToFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, MediaBrowser4.Objects.Folder folder)
//        {
//        }

//        public override void AddViewTime(MediaBrowser4.Objects.MediaItem mediaItem, int seconds)
//        {
//        }

//        public override string GetSQLFromRequestTicket(long ticket)
//        {
//            return null;
//        }

//        public override void SetSQLForRequestTicket(long ticket, string sql)
//        {
//        }
//    }
//}
