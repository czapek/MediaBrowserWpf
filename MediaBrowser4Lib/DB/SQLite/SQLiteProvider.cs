using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using MediaBrowser4;
using System.Data.SQLite;
using System.Data;
using System.IO;
using MediaBrowser4.Objects;
using System.Runtime.Serialization.Formatters.Binary;
using System.Data.Common;
using System.Collections.ObjectModel;
using MediaBrowser4.Utilities;
using System.Xml;

namespace MediaBrowser4.DB.SQLite
{
    public class SQLiteProvider : MediaBrowser4.DB.DBProvider
    {
        private string dbPath;
        private SQLiteInsert sqlInsert;
        Dictionary<long, string> requestArchiv = new Dictionary<long, string>();
        public override event EventHandler<MediaItemCallbackArgs> OnInsert;
        public override event EventHandler<MediaItemNewThumbArgs> OnThumbUpdate;

        private SQLiteProvider()
        {
        }

        internal string DBPath
        {
            get
            {
                return dbPath;
            }
        }

        public SQLiteProvider(string dbPath)
        {
            this.dbPath = dbPath;
            this.DBName = dbPath;
            sqlInsert = new SQLiteInsert(dbPath);
            sqlInsert.OnInsert += new EventHandler<MediaItemCallbackArgs>(sqlInsert_OnInsert);
        }

        public override int ExecuteNonQuery(string sql)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                return com.ExecuteNonQuery(sql);
            }
        }
        public override MediaItemSqlRequest GetSqlRequest(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            return mediaItemList != null && mediaItemList.Count > 0 ? new MediaItemSqlRequest($"FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK WHERE MEDIAFILES.ID IN ({String.Join(",", mediaItemList.Select(x => x.Id))}) AND HISTORYVERSION = 0", new List<SQLiteParameter>(), String.Empty, MediaBrowserContext.LimitRequest) : null;
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItems(MediaItemRequest request)
        {
            List<MediaBrowser4.Objects.MediaItem> resulList = null;

            if (request is MediaItemFilesRequest)
            {
                resulList = GetMediaItemsFromFileRequest((MediaItemFilesRequest)request);
            }
            else if (request is MediaItemFolderRequest)
            {
                resulList = GetMediaItemsFromFolders(new List<Folder>(((MediaItemFolderRequest)request).FoldersComplete),
                    "", request.LimitRequest == 0 ? MediaBrowserContext.LimitRequest : request.LimitRequest, (MediaItemFolderRequest)request);
            }
            else if (request is MediaItemCategoryRequest)
            {
                MediaItemCategoryRequest categoryRequest = (MediaItemCategoryRequest)request;

                if (categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_DATE
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_OTHER
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_LOCATION
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY)
                {
                    resulList = GetMediaItemsFromMissingCategories(categoryRequest);
                }
                else
                {
                    resulList = GetMediaItemsFromCategories(new List<Category>((categoryRequest).Categories),
                        request.RequestType == MediaItemRequestType.INTERSECT, request.RequestType != MediaItemRequestType.SINGLE, "", request.LimitRequest == 0 ? MediaBrowserContext.LimitRequest : request.LimitRequest, categoryRequest);
                }
            }
            else if (request is MediaItemSearchRequest)
            {
                resulList = GetMediaItemsFromSearchToken(((MediaItemSearchRequest)request).SearchToken, false,
                    "", request.LimitRequest == 0 ? MediaBrowserContext.LimitRequest : request.LimitRequest, request);
            }
            else if (request is MediaItemSortRequest)
            {
                resulList = GetMediaItemsFromSortRequest(request as MediaItemSortRequest);
            }
            else if (request is MediaItemDublicatesRequest)
            {
                resulList = GetDublicates(request as MediaItemDublicatesRequest);
            }
            else if (request is MediaItemSqlRequest)
            {
                resulList = LoadMediaItems(request as MediaItemSqlRequest);
            }
            else
            {
                throw new Exception("Not Implemented: " + request);
            }

            return resulList;
        }


        private List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromMissingCategories(MediaItemCategoryRequest request)
        {
            string sqlCategory = "";

            if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_DATE)
            {
                sqlCategory = "ISDATE=1";
            }
            else if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_LOCATION)
            {
                sqlCategory = "ISLOCATION=1";
            }
            else if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY)
            {
            }
            else if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_OTHER)
            {
                sqlCategory = "ISLOCATION<>1 AND ISDATE<>1";
            }
            else
            {
                throw new Exception("Not Implemented: " + MediaItemCategoryRequestType.NO_DATE);
            }

            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems(@"FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK "
                + searchTokenSql.JoinSql
                + @" WHERE MEDIAFILES.ID NOT IN 
(SELECT VARIATIONS.MEDIAFILES_FK FROM VARIATIONS
INNER JOIN MEDIAFILES ON VARIATIONS.MEDIAFILES_FK=MEDIAFILES.ID
INNER JOIN CATEGORIZE ON CATEGORIZE.VARIATIONS_FK=VARIATIONS.ID"
    + (request.CategoryRequestType != MediaItemCategoryRequestType.NO_CATEGORY ?
    " WHERE CATEGORIZE.CATEGORY_FK IN (SELECT ID FROM CATEGORY WHERE " + sqlCategory + ")" : "")
    + ") " + (searchTokenSql.IsValid ? "AND " + searchTokenSql.WhereSql : "")
    + " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, "", request.LimitRequest, request);

        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromFolders(List<MediaBrowser4.Objects.Folder> folderList, string sortString, int limtRequest)
        {
            return GetMediaItemsFromFolders(folderList, sortString, limtRequest, null);
        }

        private List<MediaBrowser4.Objects.MediaItem> GetDublicates(MediaItemDublicatesRequest request)
        {
            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            //using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            //{
            //    com.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0");
            //    com.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=1 WHERE MEDIAFILES.MD5VALUE IN (SELECT MD5VALUE FROM MEDIAFILES GROUP BY MD5VALUE HAVING COUNT(MD5VALUE) > 1)");
            //}

            List<MediaBrowser4.Objects.MediaItem> mList = LoadMediaItems("FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK "
                 + searchTokenSql.JoinSql
                 + " WHERE MEDIAFILES.MD5VALUE IN (SELECT MD5VALUE FROM MEDIAFILES GROUP BY MD5VALUE HAVING COUNT(MD5VALUE) > 1) "
                 + (searchTokenSql.IsValid ? "AND " + searchTokenSql.WhereSql : "")
                 + " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, "", request.LimitRequest, request);

            //foreach (MediaItem mItem in MediaBrowserContext.GlobalMediaItemCache.Values)
            //    mItem.IsMd5Dublicate = false;

            //foreach (MediaItem mItem in mList)
            //    mItem.IsMd5Dublicate = true;


            return mList;
        }

        private List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromFileRequest(MediaItemFilesRequest request)
        {
            if (request.FilesRequestType == MediaItemFilesRequestType.FilesNotExist)
            {
                List<int> itemIds = new List<int>();
                SearchTokenSql searchTokenSql = new SearchTokenSql(request);

                using (ICommandHelper cmd = this.MBCommand)
                {
                    if (searchTokenSql.ParameterList != null)
                    {
                        foreach (SQLiteParameter param in searchTokenSql.ParameterList)
                        {
                            cmd.SetParameter(param);
                        }
                    }

                    using (DbDataReader reader = cmd.ExecuteReader("SELECT MEDIAFILES.ID, FILENAME, FOLDERNAME FROM FOLDERS INNER JOIN MEDIAFILES ON FOLDERS.ID=MEDIAFILES.FOLDERS_FK "
                        + searchTokenSql.JoinSql
                        + (searchTokenSql.IsValid ? " WHERE " + searchTokenSql.WhereSql : "") + " AND HISTORYVERSION=0 "))
                    {
                        while (reader.Read())
                        {
                            if (!File.Exists(MediaBrowserContext.MapPathRoot(reader.GetString(2)) + "\\" + reader.GetString(1)))
                            {
                                itemIds.Add(reader.GetInt32(0));
                            }
                        }
                    }
                }

                if (itemIds.Count > 0)
                {
                    return LoadMediaItems("FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK "
                      + " WHERE MEDIAFILES.ID IN ("
                      + String.Join(",", itemIds)
                      + ") "
                      + " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, "", request.LimitRequest, request);
                }
                else
                {
                    return new List<MediaItem>();
                }
            }
            else if (request.FilesRequestType == MediaItemFilesRequestType.FilesFromList)
            {
                List<MediaItem> mediaItems = new List<MediaItem>();
                foreach (String line in request.FileList)
                {
                    mediaItems.AddRange(GetMediaItemsFromFileName(line));
                }

                return mediaItems;
            }
            else if (request.FilesRequestType == MediaItemFilesRequestType.FilesFromChecksum)
            {
                List<MediaItem> mediaItems = new List<MediaItem>();
                foreach (String line in request.FileList)
                {
                    mediaItems.AddRange(GetMediaItemsFromChecksum(line));
                }

                return mediaItems;
            }

            return null;
        }

        public override int DeleteGpsFile(DateTime fileTime)
        {
            int rows = 0;
            using (ICommandHelper cmd = this.MBCommand)
            {
                cmd.SetParameter("@fileTime", fileTime, DbType.DateTime);
                rows = cmd.ExecuteNonQuery("DELETE FROM DATALOGGER_GPS WHERE FILETIME = @fileTime");
            }
            return rows;
        }

        public override void InsertGpsPoints(List<GeoPoint> gpsList)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                foreach (GeoPoint geoPoint in gpsList)
                {
                    trans.SetParameter("@FileDate", geoPoint.GpsFile.FileTime, DbType.DateTime);
                    trans.SetParameter("@Filesize", geoPoint.GpsFile.Filesize, DbType.UInt64);
                    trans.SetParameter("@Date", geoPoint.LocalTime, DbType.DateTime);
                    trans.SetParameter("@UtcDate", geoPoint.UtcTime, DbType.DateTime);
                    trans.SetParameter("@Lon", geoPoint.Longitude, DbType.Decimal);
                    trans.SetParameter("@Lat", geoPoint.Latitude, DbType.Decimal);
                    trans.SetParameter("@Alt", geoPoint.Altitude, DbType.Decimal);

                    trans.ExecuteNonQuery("INSERT INTO DATALOGGER_GPS (FILETIME, FILESIZE, UTCTIME, LOCALTIME, LONGITUDE, LATITUDE, ALTITUDE) VALUES (@FileDate, @Filesize, @UtcDate, @Date, @Lon, @Lat, @Alt)");
                }
            }
        }

        public override List<GpsFile> GetGpsFileList()
        {
            List<GpsFile> fileList = new List<GpsFile>();
            using (ICommandHelper cmd = this.MBCommand)
            {
                using (DbDataReader reader = cmd.ExecuteReader("SELECT DISTINCT FILETIME, FILESIZE FROM DATALOGGER_GPS"))
                {
                    while (reader.Read())
                    {
                        fileList.Add(new GpsFile() { FileTime = reader.GetDateTime(0), Filesize = reader.GetInt64(1) });
                    }
                }
            }

            return fileList;
        }

        public override List<GeoPoint> GetGpsList(DateTime from, DateTime to)
        {
            List<GeoPoint> gpsList = new List<GeoPoint>();
            using (ICommandHelper cmd = this.MBCommand)
            {
                cmd.SetParameter("@from", from, DbType.DateTime);
                cmd.SetParameter("@to", to, DbType.DateTime);
                using (DbDataReader reader = cmd.ExecuteReader("SELECT FILETIME, FILESIZE, LOCALTIME, LONGITUDE, LATITUDE, ALTITUDE, UTCTIME FROM DATALOGGER_GPS WHERE LOCALTIME BETWEEN @from AND @to ORDER BY LOCALTIME"))
                {
                    GeoPoint pointLast = null;
                    while (reader.Read())
                    {
                        GeoPoint point = new GeoPoint()
                        {
                            LocalTime = reader.GetDateTime(2),
                            Longitude = reader.GetDouble(3),
                            Latitude = reader.GetDouble(4),
                            Altitude = reader.GetDouble(5),
                            UtcTime = reader.GetDateTime(6),
                            GpsFile = new GpsFile() { FileTime = reader.GetDateTime(0), Filesize = reader.GetInt64(1) },
                            Predecessor = pointLast
                        };

                        if (point.Altitude == 0 && point.Predecessor != null)
                        {
                           point.Altitude = point.Predecessor.Altitude;
                        }

                        //if (pointLast != null && (point.LocalTime - pointLast.LocalTime).TotalMinutes > 2)
                        //{
                        //    GeoPoint interpolatetPoint = new GeoPoint()
                        //    {
                        //        LocalTime = point.LocalTime.AddMinutes(-1),
                        //        UtcTime = point.UtcTime.AddMinutes(-1),
                        //        Longitude = pointLast.Longitude,
                        //        Latitude = pointLast.Latitude,
                        //        Altitude = pointLast.Altitude,
                        //        GpsFile = new GpsFile() { FileTime = pointLast.GpsFile.FileTime, Filesize = pointLast.GpsFile.Filesize },
                        //        Predecessor = pointLast
                        //    };

                        //    point.Predecessor = interpolatetPoint;
                        //    // gpsList.Add(interpolatetPoint);
                        //}

                        //wenn man nicht gerade im Flugzeug sitzt, passiert das eigentlich nicht ohne dass der geopoint wild springt
                        //wenn doch TODO: finde he´raus, was die mittlere Beschleunigung in diesem Track ist
                        // if (Math.Abs(point.Acceleration) < 0.5)
                        gpsList.Add(point);
                        pointLast = point;

                    }
                }
            }

            return gpsList;
        }

        public override GeoPoint GetGpsNearest(DateTime date)
        {
            GeoPoint geoPoint = null;
            using (ICommandHelper cmd = this.MBCommand)
            {
                cmd.SetParameter("@from", date.AddDays(-1), DbType.DateTime);
                cmd.SetParameter("@to", date, DbType.DateTime);
                using (DbDataReader reader = cmd.ExecuteReader("SELECT FILETIME, FILESIZE, LOCALTIME, LONGITUDE, LATITUDE, ALTITUDE FROM DATALOGGER_GPS WHERE LOCALTIME BETWEEN @from AND @to ORDER BY LOCALTIME DESC limit 1"))
                {
                    while (reader.Read())
                    {
                        geoPoint = new GeoPoint()
                        {
                            LocalTime = reader.GetDateTime(2),
                            Longitude = reader.GetDouble(3),
                            Latitude = reader.GetDouble(4),
                            Altitude = reader.GetDouble(5),
                            GpsFile = new GpsFile() { FileTime = reader.GetDateTime(0), Filesize = reader.GetInt64(1) }
                        };
                        break;
                    }
                }
            }

            return geoPoint;
        }

        private List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromFolders(List<MediaBrowser4.Objects.Folder> folderList, string sortString, int limtRequest, MediaItemFolderRequest request)
        {
            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems("FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK "
              + searchTokenSql.JoinSql
              + " WHERE FOLDERS.ID IN ("
              + String.Join(", ", folderList.Where(x => x.Id >= 0).Select(x => x.Id)) + ") "
              + (searchTokenSql.IsValid ? "AND " + searchTokenSql.WhereSql : "")
              + " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, sortString, limtRequest, request);
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromCategories(List<MediaBrowser4.Objects.Category> categoryList, bool isIntersection, bool isRecursive, string sortString, int limtRequest)
        {
            return GetMediaItemsFromCategories(categoryList, isIntersection, isRecursive, sortString, limtRequest, null);
        }

        private List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromCategories(List<MediaBrowser4.Objects.Category> categoryList, bool isIntersection, bool isRecursive, string sortString, int limtRequest, MediaItemCategoryRequest request)
        {
            //Die Unique-Bäume erstellen     
            Dictionary<Category, List<Category>> categoryUniqueTrees = new Dictionary<Category, List<Category>>();
            Dictionary<Category, List<Category>> uniqueNodes = new Dictionary<Category, List<Category>>();
            if (isIntersection)
            {
                foreach (Category cat in categoryList)
                {
                    if (cat.IsUnique)
                    {
                        if (categoryUniqueTrees.ContainsKey(cat.UniqueRoot))
                            continue;

                        categoryUniqueTrees.Add(cat.UniqueRoot, cat.UniqueRoot.AllChildrenRecursive());
                    }
                }

                foreach (Category cat in categoryList)
                {
                    bool isUnion = false;
                    foreach (KeyValuePair<Category, List<Category>> kv in categoryUniqueTrees)
                    {
                        if (kv.Value.Contains(cat))
                        {

                            if (!uniqueNodes.ContainsKey(kv.Key))
                            {
                                uniqueNodes[kv.Key] = new List<Category>();
                            }

                            uniqueNodes[kv.Key].Add(cat);
                            isUnion = true;
                            break;
                        }
                    }

                    if (!isUnion)
                    {
                        uniqueNodes[cat] = null;
                    }
                }
            }
            else
            {
                foreach (Category cat in categoryList)
                {
                    uniqueNodes[cat] = null;
                }
            }

            List<string> sqlList = new List<string>();
            StringBuilder sb = null;
            foreach (KeyValuePair<Category, List<Category>> kv in uniqueNodes)
            {
                if (kv.Value == null)
                {
                    sqlList.Add("SELECT VARIATIONS_FK FROM CATEGORIZE WHERE CATEGORIZE.CATEGORY_FK IN ("
                        + (isRecursive ? ConcatCategoryIds(kv.Key.AllChildrenRecursive()) : kv.Key.Id.ToString()) + ")");
                }
                else
                {
                    sb = new StringBuilder();
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        sb.AppendLine("SELECT VARIATIONS_FK FROM CATEGORIZE WHERE CATEGORIZE.CATEGORY_FK IN ("
                        + (isRecursive ? ConcatCategoryIds(kv.Value[i].AllChildrenRecursive()) : kv.Value[i].Id.ToString()) + ")");

                        if (i < kv.Value.Count - 1)
                        {
                            sb.AppendLine("UNION");
                        }
                    }

                    sqlList.Add(sb.ToString());
                }
            }

            sb = new StringBuilder();

            for (int i = 0; i < sqlList.Count; i++)
            {
                sb.AppendLine("SELECT VARIATIONS_FK FROM (" + sqlList[i] + ")");

                if (i < sqlList.Count - 1)
                {
                    sb.AppendLine(isIntersection ? "INTERSECT" : "UNION");
                }
            }

            string allVariationsSqlString = "(SELECT VARIATIONS_FK FROM (" + sb.ToString() + "))";

            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems(", VARIATIONS.ID AS VARIATIONSID FROM VARIATIONS "
               + "INNER JOIN MEDIAFILES ON VARIATIONS.MEDIAFILES_FK=MEDIAFILES.ID "
               + "INNER JOIN FOLDERS ON FOLDERS.ID=MEDIAFILES.FOLDERS_FK "
               + "INNER JOIN CATEGORIZE ON CATEGORIZE.VARIATIONS_FK=VARIATIONS.ID "
               + searchTokenSql.JoinSqlCategory
               + "WHERE " + (request.Header.EndsWith(" (!)") ? "MEDIAFILES.ISBOOKMARKED = 0 AND " : String.Empty)
               + "VARIATIONS.ID IN " + allVariationsSqlString
               + (searchTokenSql.IsValid ? " AND " + searchTokenSql.WhereSql : "")
               + " AND HISTORYVERSION=0", searchTokenSql.ParameterList, sortString, limtRequest, request);

        }

        private List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromSortRequest(MediaItemSortRequest sortRequest)
        {
            List<string> sortList = new List<string>();

            if (sortRequest.ShuffleType == MediaItemRequestShuffleType.NONE)
            {
                foreach (Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection> sortTupel in sortRequest.SortTypeList)
                {
                    sortList.Add("VIEWED" + (sortTupel.Item2 == MediaItemRequestSortDirection.DESCENDING ? " DESC" : ""));
                }
            }
            else
            {
                sortList.Add("RANDOM()");
            }

            SearchTokenSql searchTokenSql = new SearchTokenSql(sortRequest);

            string mediaItemListSql = "";
            if (sortRequest.MediaItemList != null && sortRequest.MediaItemList.Count > 0)
            {
                mediaItemListSql = "MEDIAFILES.ID IN (" + ConcatMediaItemIDs(sortRequest.MediaItemList) + ") AND ";
            }

            if (dbPath.EndsWith("mediafilesNew.mb4") && sortRequest.Header == "200 zufällig ausgewählte")
            {
                mediaItemListSql = "(FOLDERS.FOLDERNAME LIKE '%\\Alle Bilder\\%' OR FOLDERS.FOLDERNAME LIKE '%\\Foto-Temp\\%') and FOLDERS.FOLDERNAME NOT LIKE '%\\panorama%' AND ";
            }

            return LoadMediaItems("FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK "
              + searchTokenSql.JoinSql
              + " WHERE "
              + mediaItemListSql
              + (searchTokenSql.IsValid ? searchTokenSql.WhereSql + " AND " : "")
              + "HISTORYVERSION=0 ", searchTokenSql.ParameterList, "ORDER BY " + String.Join(", ", sortList), sortRequest.LimitRequest, sortRequest);
        }

        public override List<MediaItemRequest> GetUserDefinedRequests()
        {
            List<MediaItemRequest> requestList = new List<MediaItemRequest>();

            using (ICommandHelper cmd = this.MBCommand)
            {
                using (DbDataReader reader = cmd.ExecuteReader("SELECT ID, REQUEST FROM USERDEFINEDREQUESTS"))
                {
                    while (reader.Read())
                    {
                        long id = (long)reader["ID"];
                        byte[] binaryData = (byte[])reader["REQUEST"];

                        MemoryStream stream = new MemoryStream(binaryData);
                        MediaItemRequest request = (MediaItemRequest)new BinaryFormatter().Deserialize(stream);
                        stream.Close();

                        if (request is MediaItemCategoryRequest)
                        {
                            ((MediaItemCategoryRequest)request).RefreshCategories();
                        }
                        else if (request is MediaItemFolderRequest)
                        {
                            ((MediaItemFolderRequest)request).RefreshFolders();
                        }



                        request.UserDefinedId = (int)id;

                        requestList.Add(request);
                    }
                }
            }
            return requestList;
        }

        public override void DeleteUserDefinedRequest(MediaItemRequest request)
        {

            if (request == null | request.UserDefinedId == 0)
            {
                return;
            }

            using (ICommandHelper cmd = this.MBCommand)
            {
                cmd.ExecuteNonQuery("DELETE FROM USERDEFINEDREQUESTS WHERE ID=" + request.UserDefinedId);
            }
        }

        public override bool SaveUserDefinedRequest(MediaItemRequest request)
        {
            if (request is MediaItemObservableCollectionRequest)
                return false;

            if (request is MediaItemVirtualRequest)
                return false;

            MemoryStream stream = new MemoryStream();
            new BinaryFormatter().Serialize(stream, request);

            Byte[] binaryData = new Byte[stream.Length];
            stream.Position = 0;
            stream.Read(binaryData, 0, (int)stream.Length);
            stream.Close();

            using (ICommandHelper cmd = this.MBCommand)
            {
                cmd.SetParameter("@binaryData", binaryData, DbType.Binary);

                if (request.UserDefinedId == 0
                || cmd.ExecuteScalar<long>("SELECT COUNT(*) FROM USERDEFINEDREQUESTS WHERE ID=" + request.UserDefinedId) == 0)
                {
                    cmd.ExecuteNonQuery("INSERT INTO USERDEFINEDREQUESTS (REQUEST) VALUES(@binaryData)");
                    request.UserDefinedId = (int)cmd.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");
                    return true;
                }
                else
                {
                    cmd.ExecuteNonQuery("UPDATE USERDEFINEDREQUESTS SET REQUEST=@binaryData WHERE ID=" + request.UserDefinedId);
                    return false;
                }
            }
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromTrashfolder(string sortString, int limtRequest)
        {
            return LoadMediaItems("FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND HISTORYVERSION=0 AND MEDIAFILES.ISDELETED=1 ", sortString, limtRequest);
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromBookmarks(string sortString, int limtRequest)
        {
            return LoadMediaItems("FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND HISTORYVERSION=0 AND MEDIAFILES.ISBOOKMARKED=1 ", sortString, limtRequest);
        }

        public override List<MediaItem> GetMediaItemsFromChecksum(string checksum)
        {
            List<SQLiteParameter> paramList = new List<SQLiteParameter>();
            paramList.Add(new SQLiteParameter("@MD5VALUE", DbType.String) { Value = Path.GetFileName(checksum).ToLower().Trim() });
            List<MediaItem> result = LoadMediaItems("FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND MD5VALUE=LOWER(@MD5VALUE) AND HISTORYVERSION=0 ", paramList, String.Empty, 1, null);

            return result;
        }

        public override List<MediaItem> GetMediaItemsFromFileName(string filename)
        {
            List<SQLiteParameter> paramList = new List<SQLiteParameter>();
            paramList.Add(new SQLiteParameter("@FILENAME", DbType.String) { Value = Path.GetFileName(filename).ToLower().Trim() });
            List<MediaItem> result = LoadMediaItems("FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND FILENAME=LOWER(@FILENAME) AND HISTORYVERSION=0 ", paramList, String.Empty, 1, null);

            return result;
        }

        public override MediaItem GetMediaItemFromFile(string filename)
        {
            List<SQLiteParameter> paramList = new List<SQLiteParameter>();
            paramList.Add(new SQLiteParameter("@FOLDERNAME", DbType.String) { Value = MediaBrowserContext.MapPathRootReverse(Path.GetDirectoryName(filename)) });
            paramList.Add(new SQLiteParameter("@FILENAME", DbType.String) { Value = Path.GetFileName(filename) });

            List<MediaItem> result = LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND FOLDERNAME=@FOLDERNAME AND FILENAME=@FILENAME AND HISTORYVERSION=0 ",
                paramList, String.Empty, 1, null);

            if (result.Count == 1)
                return result[0];
            else
                return null;
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsForRequestTicket(long requestTicket, string sortString, int limtRequest)
        {
            List<MediaBrowser4.Objects.MediaItem> mList = null;
            if (requestArchiv.ContainsKey(requestTicket))
            {
                mList = LoadMediaItems(requestArchiv[requestTicket], sortString, limtRequest);
                requestArchiv.Remove(requestTicket);
            }

            return mList;
        }

        public override string GetSQLFromRequestTicket(long ticket)
        {
            if (requestArchiv.ContainsKey(ticket))
            {
                return requestArchiv[ticket];
            }
            else
            {
                return "";
            }
        }

        public override void SetSQLForRequestTicket(long ticket, string sql)
        {
            if (requestArchiv.ContainsKey(ticket))
            {
                requestArchiv[ticket] = sql;
            }
        }

        private static int previewDbCacheVarId;
        private bool previewDbCacheResult;
        public override bool IsImageInPreviewDB(int variationId, System.Drawing.Size size)
        {
            if (previewDbCacheVarId == variationId)
                return previewDbCacheResult;

            previewDbCacheVarId = variationId;
            previewDbCacheResult = false;

            if (!File.Exists(DBAdministration.GetThumbNailDBPath(dbPath, size)))
            {
                return false;
            }

            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";

            using (SQLiteConnection con = new SQLiteConnection(connectionString))
            {
                con.Open();
                SQLiteCommand cmd = con.CreateCommand();
                cmd.CommandText = "SELECT Extension FROM THUMBS WHERE VARIATIONS_FK=" + variationId;
                object obj = cmd.ExecuteScalar();

                if (obj != null && obj != DBNull.Value)
                    previewDbCacheResult = true;
            }

            return previewDbCacheResult;
        }

        public override void RenameMediaItem(MediaBrowser4.Objects.MediaItem mediaItem, string newName)
        {
            using (MediaBrowser4.DB.ICommandHelper comm = MediaBrowserContext.MainDBProvider.MBCommand)
            {
                comm.SetParameter("@FILENAME", newName.Trim());
                comm.ExecuteNonQuery("UPDATE MEDIAFILES SET FILENAME=@FILENAME, SORTORDER=@FILENAME WHERE ID=" + mediaItem.Id);
                mediaItem.Filename = newName.Trim();
                mediaItem.Sortorder = mediaItem.Filename;
                mediaItem.FileObject = new System.IO.FileInfo(mediaItem.Foldername + "\\" + newName.Trim());
            }
        }

        public override void DeleteFromPreviewDB(List<MediaItem> mediaItemList, System.Drawing.Size size)
        {
            if (!File.Exists(DBAdministration.GetThumbNailDBPath(dbPath, size)))
            {
                return;
            }

            List<int> idList = new List<int>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                using (DbDataReader reader = com.ExecuteReader("SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK IN (" + String.Join(",", mediaItemList.Select(x => x.Id)) + ")"))
                {
                    while (reader.Read())
                    {
                        idList.Add(reader.GetInt32(0));
                    }
                }
            }

            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";

            if (idList.Count > 0)
                using (SQLiteConnection con = new SQLiteConnection(connectionString))
                {
                    con.Open();
                    SQLiteCommand cmd = con.CreateCommand();
                    cmd.CommandText = "DELETE FROM THUMBS WHERE VARIATIONS_FK IN (" + String.Join(",", idList) + ")";
                    cmd.ExecuteNonQuery();
                }
        }

        public override List<int> GetPreviewDBVariationIdList(System.Drawing.Size size)
        {
            List<int> resultList = new List<int>();
            if (!File.Exists(DBAdministration.GetThumbNailDBPath(dbPath, size)))
            {
                return resultList;
            }

            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";

            using (SQLiteConnection con = new SQLiteConnection(connectionString))
            {
                con.Open();
                SQLiteCommand cmd = con.CreateCommand();
                cmd.CommandText = "SELECT VARIATIONS_FK FROM THUMBS";

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        resultList.Add(reader.GetInt32(0));
                    }
                }
            }

            return resultList;
        }

        public override MediaProcessing.FaceDetection.Faces GetFaceDetectionPreviewDB(int variationId, System.Drawing.Size size)
        {
            MediaProcessing.FaceDetection.Faces faces = new MediaProcessing.FaceDetection.Faces()
            {
                Facelist = new List<System.Drawing.Rectangle>()
            };

            if (!File.Exists(DBAdministration.GetThumbNailDBPath(dbPath, size)))
            {
                return faces;
            }

            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";
            using (SQLiteConnection con = new SQLiteConnection(connectionString))
            {
                con.Open();
                SQLiteCommand cmd = con.CreateCommand();

                cmd.CommandText = "SELECT FACES.X, FACES.Y, FACES.WIDTH, FACES.HEIGHT, THUMBS.WIDTH, THUMBS.HEIGHT FROM FACES INNER JOIN THUMBS ON FACES.VARIATIONS_FK=THUMBS.VARIATIONS_FK WHERE FACES.VARIATIONS_FK=" + variationId;
                SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    faces.Facelist.Add(new System.Drawing.Rectangle()
                    {
                        X = reader.GetInt32(0),
                        Y = reader.GetInt32(1),
                        Width = reader.GetInt32(2),
                        Height = reader.GetInt32(3)
                    });

                    faces.Width = reader.GetInt32(4);
                    faces.Height = reader.GetInt32(5);
                }

                reader.Close();
            }

            return faces;
        }

        public override PreviewObject GetImagePreviewDB(int variationId, System.Drawing.Size size)
        {
            PreviewObject previewObject = new PreviewObject();

            if (!File.Exists(DBAdministration.GetThumbNailDBPath(dbPath, size)))
            {
                return null;
            }

            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";
            using (SQLiteConnection con = new SQLiteConnection(connectionString))
            {
                con.Open();
                SQLiteCommand cmd = con.CreateCommand();

                cmd.CommandText = "SELECT THUMB, Extension FROM THUMBS WHERE VARIATIONS_FK=" + variationId;
                SQLiteDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    previewObject.Extension = reader.GetString(1);
                    previewObject.TempPath = Path.Combine(MediaBrowserContext.DBTempFolder, variationId + "." + previewObject.Extension.Replace("agif", "gif"));
                    if (previewObject.Extension == "jpg" || !File.Exists(previewObject.TempPath))
                    {
                        Object obj = reader[0];
                        if (obj != null)
                        {
                            if (previewObject.Extension == "jpg" || previewObject.Extension == "agif")
                            {
                                previewObject.Binary = (byte[])obj;
                            }
                            else
                            {
                                File.WriteAllBytes(previewObject.TempPath, (byte[])obj);
                            }
                        }
                    }
                }

                reader.Close();
            }

            return previewObject;
        }

        public override void WriteToPreviewDB(List<string> fileList, System.Drawing.Size size)
        {
            DBAdministration.CreateThumbnailDB(this.DBPath, size, false);
            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";

            using (MediaBrowser4.DB.ITransaction trans = new MediaBrowser4.DB.SQLite.Transaction(
                new SimpleConnection(DBAdministration.GetThumbNailDBPath(dbPath, size))
                ))
            {

                int cnt = 0;
                foreach (string file in fileList)
                {
                    int varId;
                    string filename = System.IO.Path.GetFileNameWithoutExtension(file);
                    string extension = System.IO.Path.GetExtension(filename).TrimStart('.');
                    filename = System.IO.Path.GetFileNameWithoutExtension(filename);

                    if (extension != "jpg")
                        continue;//TOTO: andere Datentypen für vorschaudatenbank

                    if (Int32.TryParse(filename, out varId))
                    {
                        System.Windows.Media.Imaging.BitmapFrame frame = null;

                        if (extension == "jpg")
                        {
                            try
                            {
                                System.Windows.Media.Imaging.BitmapDecoder decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(new Uri(file), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.None);
                                frame = decoder.Frames[0];
                            }
                            catch { }
                        }

                        trans.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK=" + varId);
                        Byte[] binaryDataThumbnail = File.ReadAllBytes(file);
                        Byte[] binaryDataFaceDetection = File.Exists(file + ".fcd") ? File.ReadAllBytes(file + ".fcd") : null;

                        trans.SetParameter("@THUMBNAIL", binaryDataThumbnail, DbType.Binary);

                        if (frame == null)
                        {
                            trans.ExecuteNonQuery("INSERT INTO THUMBS(VARIATIONS_FK, THUMB, Extension) VALUES (" + varId + ", @THUMBNAIL, '" + extension + "')");
                        }
                        else
                        {
                            trans.ExecuteNonQuery("INSERT INTO THUMBS(VARIATIONS_FK, THUMB, Extension, WIDTH, HEIGHT) VALUES (" + varId + ", @THUMBNAIL, '" + extension + "'," + frame.Width + "," + frame.Height + ")");

                            if (binaryDataFaceDetection != null)
                            {
                                BinaryFormatter bFormatter = new BinaryFormatter();
                                MemoryStream stream = new MemoryStream(binaryDataFaceDetection);
                                MediaProcessing.FaceDetection.Faces faces = (MediaProcessing.FaceDetection.Faces)bFormatter.Deserialize(stream);

                                foreach (System.Drawing.Rectangle rect in faces.Facelist)
                                {
                                    trans.SetParameter("@REL_SIZE", 100 * ((double)rect.Width * (double)rect.Height) / (double)(frame.Width * frame.Height), DbType.Double);
                                    trans.ExecuteNonQuery("INSERT INTO FACES(VARIATIONS_FK, X, Y, WIDTH, HEIGHT, REL_SIZE) VALUES ("
                                        + varId + "," + rect.X + ", " + rect.Y + "," + rect.Width + "," + rect.Height + ", @REL_SIZE)");
                                }

                                stream.Close();
                            }
                        }
                    }

                    cnt++;

                    if (this.OnInsert != null)
                        this.OnInsert(this, new MediaItemCallbackArgs(cnt, fileList.Count, null));
                }

                if (this.OnInsert != null)
                    this.OnInsert(this, new MediaItemCallbackArgs(cnt, fileList.Count, null, true, false));
            }
        }

        public override Dictionary<int, int> GetAllFaces(System.Drawing.Size size, double min, double max, int countMin, int countMax, List<MediaItem> itemList)
        {
            Dictionary<int, int> faceList = new Dictionary<int, int>();

            string restrict = itemList != null && itemList.Count > 0 ? "AND VARIATIONS_FK IN (" + String.Join(",", itemList.Select(x => x.VariationId.ToString())) + ")" : String.Empty;

            string connectionString = @"Data Source=" + DBAdministration.GetThumbNailDBPath(dbPath, size) + ";New=False;UTF8Encoding=True;Version=3";
            using (SQLiteConnection con = new SQLiteConnection(connectionString))
            {
                con.Open();
                SQLiteCommand cmd = con.CreateCommand();

                cmd.Parameters.Add(new SQLiteParameter("@min", DbType.Double) { Value = min });
                cmd.Parameters.Add(new SQLiteParameter("@max", DbType.Double) { Value = max });
                cmd.Parameters.Add(new SQLiteParameter("@countMin", DbType.Int32) { Value = countMin });
                cmd.Parameters.Add(new SQLiteParameter("@countMax", DbType.Int32) { Value = countMax });

                cmd.CommandText = "SELECT VARIATIONS_FK, COUNT(VARIATIONS_FK) FROM FACES WHERE REL_SIZE >= @min AND REL_SIZE <= @max " + restrict
                        + " GROUP BY VARIATIONS_FK HAVING COUNT(VARIATIONS_FK) >= @countMin AND COUNT(VARIATIONS_FK) <= @countMax";

                SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    faceList.Add(reader.GetInt32(0), reader.GetInt32(1));
                }

                reader.Close();
            }

            return faceList;
        }

        public override void SetPriority(List<MediaBrowser4.Objects.MediaItem> mList, int priority)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("UPDATE MEDIAFILES SET PRIORITY=" + priority + " WHERE ID IN (" + ConcatMediaItemIDs(mList) + ")");
            }

            foreach (MediaItem mItem in mList)
                mItem.Priority = priority;
        }

        public override void SetRole(List<MediaBrowser4.Objects.MediaItem> mList, int roleId)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("UPDATE MEDIAFILES SET ROLES_FK=" + roleId + " WHERE ID IN (" + ConcatMediaItemIDs(mList) + ")");
            }

            foreach (MediaItem mItem in mList)
                mItem.RoleId = roleId;
        }

        public override void CleanDB()
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("ATTACH '" + DBAdministration.GetThumbNailDBPath(dbPath) + "' AS thmb;");
                com.ExecuteScalar<long>("DELETE FROM thmb.THUMBS WHERE VARIATIONS_FK NOT IN  (SELECT ID FROM VARIATIONS)");
            }

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                //maximal 3 Datumsebenen
                com.ExecuteNonQuery("DELETE FROM CATEGORY WHERE IsDate=1 AND ID NOT IN (SELECT CATEGORY_FK FROM CATEGORIZE) AND ID NOT IN (SELECT PARENT FROM CATEGORY)");
                com.ExecuteNonQuery("DELETE FROM CATEGORY WHERE IsDate=1 AND ID NOT IN (SELECT CATEGORY_FK FROM CATEGORIZE) AND ID NOT IN (SELECT PARENT FROM CATEGORY)");
                com.ExecuteNonQuery("DELETE FROM CATEGORY WHERE IsDate=1 AND ID NOT IN (SELECT CATEGORY_FK FROM CATEGORIZE) AND ID NOT IN (SELECT PARENT FROM CATEGORY)");
                com.ExecuteNonQuery("DELETE FROM FOLDERS WHERE ID NOT IN (SELECT FOLDERS_FK FROM MEDIAFILES)");
                com.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE FOLDERS_FK NOT IN (SELECT ID FROM FOLDERS)");
                com.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE HISTORYVERSION > 0");
                com.ExecuteNonQuery("DELETE FROM ATTACHMENTS WHERE ID NOT IN (SELECT DISTINCT ATTACHMENTS_FK FROM ATTACHED)");
                com.ExecuteNonQuery("DELETE FROM DESCRIPTION WHERE ID NOT IN (SELECT DESCRIPTION_FK FROM MEDIAFILES WHERE DESCRIPTION_FK IS NOT NULL)");
                com.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0");
                com.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=1 WHERE MEDIAFILES.MD5VALUE IN (SELECT MD5VALUE FROM MEDIAFILES GROUP BY MD5VALUE HAVING COUNT(MD5VALUE) > 1)");
                com.ExecuteNonQuery("vacuum");
            }

            using (MediaBrowser4.DB.ICommandHelper com = new MediaBrowser4.DB.SQLite.CommandHelper(
                        new MediaBrowser4.DB.SQLite.SimpleConnection(
                            DBAdministration.GetThumbNailDBPath(dbPath))
                            ))
            {
                com.ExecuteNonQuery("vacuum");
            }
        }

        public override Exception RemoveAndRecycle(MediaItem mItem)
        {
            List<string> idList = new List<string>();
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                try
                {
                    using (DbDataReader reader = trans.ExecuteReader("SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id))
                    {
                        while (reader.Read())
                        {
                            idList.Add(reader[0].ToString());
                        }
                    }

                    trans.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE ID=" + mItem.Id);

                    UpdateDublicates(mItem, trans);

                    if (mItem.FileObject.Exists)
                    {
                        MediaBrowser4.Utilities.RecycleBin.Recycle(mItem.FullName, false);

                        foreach (Attachment attachment in FilesAndFolders.GetAttachments(mItem))
                        {
                            if (!attachment.IsShared)
                                MediaBrowser4.Utilities.RecycleBin.Recycle(attachment.FullName, false);
                        }
                    }

                    Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mItem.FolderId);

                    if (folder != null)
                    {
                        folder.ItemCount--;
                        while (folder != null)
                        {
                            folder.UpdateItemInfo();
                            folder = folder.Parent;
                        }
                    }

                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return ex;
                }
            }

            MediaBrowserContext.GlobalMediaItemCache.Remove(mItem);

            if (idList.Count == 0)
                return null;

            using (MediaBrowser4.DB.ICommandHelper command = new MediaBrowser4.DB.SQLite.CommandHelper(
            new MediaBrowser4.DB.SQLite.SimpleConnection(
                DBAdministration.GetThumbNailDBPath(dbPath))
                ))
            {
                command.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK IN (" + String.Join(",", idList) + ")");
            }

            return null;
        }

        private static void UpdateDublicates(MediaItem mItem, ITransaction trans)
        {
            if (trans.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE " +
               "MD5VALUE='" + mItem.Md5Value + "'") == 1)
            {
                int id = (int)trans.ExecuteScalar<long>("SELECT ID FROM MEDIAFILES WHERE " +
                    "MD5VALUE='" + mItem.Md5Value + "'");

                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0 WHERE ID=" + id);

                if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(id))
                    MediaBrowserContext.GlobalMediaItemCache[id].IsMd5Dublicate = false;
            }
        }

        public override int CanFoundByChecksum(string md5)
        {
            int id = -1;
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                id = (int)com.ExecuteScalar<long>("SELECT ID FROM MEDIAFILES WHERE " + "MD5VALUE='" + md5 + "'");
            }

            return id;
        }

        public override void RemoveFromDB(List<MediaItem> mediaItemList)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("ATTACH '" + DBAdministration.GetThumbNailDBPath(dbPath) + "' AS thmb;");
                string mediaIdList = ConcatMediaItemIDs(mediaItemList);

                com.ExecuteNonQuery("DELETE FROM thmb.THUMBS WHERE VARIATIONS_FK IN "
                    + "(SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK IN (" + mediaIdList + "))");
                com.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE ID IN (" + mediaIdList + ")");
            }

            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                foreach (MediaItem mItem in mediaItemList.ToList())
                {
                    MediaBrowserContext.GlobalMediaItemCache.Remove(mItem);
                    UpdateDublicates(mItem, trans);

                    Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mItem.FolderId);

                    if (folder != null)
                    {
                        folder.ItemCount--;
                        while (folder != null)
                        {
                            folder.UpdateItemInfo();
                            folder = folder.Parent;
                        }
                    }
                }
            }
        }

        private void GetBasics()
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                Guid = com.ExecuteScalar("SELECT VALUE FROM DBPROPERTIES WHERE KEY='GUID'").ToString();
                User = com.ExecuteScalar("SELECT VALUE FROM DBPROPERTIES WHERE KEY='USER'").ToString();
                Host = com.ExecuteScalar("SELECT VALUE FROM DBPROPERTIES WHERE KEY='HOST'").ToString();
            }
        }

        void sqlInsert_OnInsert(object sender, MediaItemCallbackArgs e)
        {
            if (this.OnInsert != null)
                this.OnInsert(this, e);
        }

        public override string Guid { get; set; }
        public override string DBName { get; set; }
        public override string User { get; set; }
        public override string Host { get; set; }

        public override bool ValidateDB()
        {
            try
            {
                (new MediaBrowser4.DB.SQLite.SimpleConnection(this.dbPath)).Validate();
            }
            catch (MediaBrowser4.UnvalidDBException)
            {
                return false;
            }

            this.GetBasics();
            return true;
        }

        public override void CreateDB()
        {
            DBAdministration.CreateDB(dbPath);
        }

        public override ITransaction MBTransaction
        {
            get
            {
                return new MediaBrowser4.DB.SQLite.Transaction(
                    new MediaBrowser4.DB.SQLite.SimpleConnection(dbPath)
                    );
            }
        }

        public override ICommandHelper MBCommand
        {
            get
            {
                return new MediaBrowser4.DB.SQLite.CommandHelper(
                    new MediaBrowser4.DB.SQLite.SimpleConnection(dbPath)
                    );
            }
        }

        public override void AddMediaItemAsync(MediaBrowser4.Objects.MediaItem mItems)
        {
            sqlInsert.AddMediaItem(mItems);
        }

        public override void AddViewTime(MediaBrowser4.Objects.MediaItem mediaItem, int seconds)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("UPDATE MEDIAFILES SET VIEWED=VIEWED+" + seconds + " WHERE ID = " + mediaItem.Id);
            }

            mediaItem.Viewed += seconds;
        }

        public override Dictionary<string, DateTime> LastAddedFolders()
        {
            Dictionary<string, DateTime> folders = new Dictionary<string, DateTime>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.Common.DbDataReader reader = com.ExecuteReader(
                               @"
SELECT FOLDERS.FOLDERNAME, MAX(MEDIAFILES.MEDIADATE) FROM FOLDERS, MEDIAFILES
WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK
GROUP BY FOLDERS.FOLDERNAME
HAVING COUNT(*)>0
ORDER BY MAX(MEDIAFILES.MEDIADATE) DESC
LIMIT 10"
);
                while (reader.Read())
                {
                    folders.Add(MediaBrowserContext.MapPathRoot(reader.GetString(0)), reader.GetDateTime(1));
                }

                reader.Close();

            }
            return folders;
        }

        public override List<MediaBrowser4.Objects.Variation> GetVariations(MediaBrowser4.Objects.MediaItem mItem, bool getThumbData)
        {
            List<MediaBrowser4.Objects.Variation> resultList = new List<MediaBrowser4.Objects.Variation>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.Common.DbDataReader reader = com.ExecuteReader("SELECT ID, POSITION, NAME FROM VARIATIONS WHERE MEDIAFILES_FK ="
                    + mItem.Id + " ORDER BY POSITION, NAME");
                while (reader.Read())
                {
                    MediaBrowser4.Objects.Variation var = new MediaBrowser4.Objects.Variation()
                    {
                        Id = reader.GetInt32(0),
                        Position = reader.GetInt32(1),
                        Name = reader[2] == DBNull.Value || String.IsNullOrWhiteSpace(reader.GetString(2)) ? "Default" : reader.GetString(2),
                        MediaItemId = mItem.Id
                    };

                    resultList.Add(var);
                }

                reader.Close();
            }

            if (getThumbData)
            {
                using (MediaBrowser4.DB.ICommandHelper commandThumbnails = new MediaBrowser4.DB.SQLite.CommandHelper(
                                new MediaBrowser4.DB.SQLite.SimpleConnection(
                 DBAdministration.GetThumbNailDBPath(dbPath))
                 ))
                {
                    foreach (Variation variation in resultList)
                    {
                        variation.ThumbJpegData = commandThumbnails.ExecuteScalar<byte[]>("SELECT THUMB FROM THUMBS WHERE VARIATIONS_FK=" + variation.Id);
                    }
                }
            }

            return resultList;
        }


        public override void ReplaceVariations(Dictionary<MediaItem, List<Variation>> mediaItemDic)
        {
            using (MediaBrowser4.DB.ITransaction transThumbnails = new MediaBrowser4.DB.SQLite.Transaction(
              new MediaBrowser4.DB.SQLite.SimpleConnection(
                  DBAdministration.GetThumbNailDBPath(dbPath))
                  ))
            {
                using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
                {
                    foreach (KeyValuePair<MediaItem, List<Variation>> kv in mediaItemDic)
                    {
                        this.ReplaceVariations(kv.Key, kv.Value, trans, transThumbnails);
                    }
                }
            }
        }

        public void ReplaceVariations(MediaBrowser4.Objects.MediaItem mediaItem, List<Variation> variationList,
            ITransaction trans, ITransaction transThumbnails)
        {
            if (variationList.Count == 0)
                throw new Exception("Variationlist is empty!");

            foreach (Variation oldVariation in this.GetVariations(mediaItem, false))
            {
                trans.ExecuteNonQuery("DELETE FROM VARIATIONS WHERE ID=" + oldVariation.Id);
                transThumbnails.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK=" + oldVariation.Id);
            }

            long newDefaultVariation = 0;
            long varId = 0;
            foreach (Variation newVariation in variationList)
            {
                trans.SetParameter("@NAME", newVariation.Name);

                if (trans.ExecuteNonQuery("INSERT INTO VARIATIONS (MEDIAFILES_FK, NAME) VALUES (" + mediaItem.Id + ",@NAME)") == 1)
                {
                    varId = trans.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");

                    if (newVariation.Id == mediaItem.VariationId)
                        newDefaultVariation = varId;

                    foreach (Layer newLayer in newVariation.Layers)
                    {
                        trans.SetParameter("@ACTION", newLayer.Action);
                        trans.ExecuteNonQuery("INSERT INTO LAYERS (VARIATIONS_FK, POSITION, EDIT, ACTION) VALUES("
                            + varId + "," + newLayer.Position + ",'" + newLayer.Edit + "', @ACTION)");
                    }
                }

                if (newVariation.ThumbJpegData.Length > 0)
                {
                    transThumbnails.SetParameter("@Thumb", newVariation.ThumbJpegData, System.Data.DbType.Binary);
                    transThumbnails.ExecuteNonQuery("INSERT INTO THUMBS(THUMB, VARIATIONS_FK) VALUES(@Thumb, " + varId + ")");
                }
            }

            if (newDefaultVariation == 0)
                newDefaultVariation = varId;

            trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ORIENTATION="
                + (int)mediaItem.Orientation + ", CURRENTVARIATION=" + newDefaultVariation
                + " WHERE ID=" + mediaItem.Id);

        }

        public override string GetDescription(MediaItem mItem)
        {
            if (mItem.DescriptionId == null)
                return null;

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                mItem.Description = com.ExecuteScalar<string>("SELECT VALUE FROM DESCRIPTION WHERE ID=" + mItem.DescriptionId);
                return mItem.Description;
            }
        }


        public override List<Description> GetDescription(List<MediaItem> mList)
        {
            List<Description> descList = new List<Description>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                foreach (int descId in mList.Where(x => x.DescriptionId != null).Select(x => x.DescriptionId.Value).Distinct())
                {
                    Description description = new Description();
                    description.Value = com.ExecuteScalar<string>("SELECT VALUE FROM DESCRIPTION WHERE ID=" + descId);
                    description.Id = descId;
                    description.MediaItemList = new List<MediaItem>();
                    descList.Add(description);
                }
            }

            foreach (MediaItem mItem in mList)
            {
                if (mItem.DescriptionId != null)
                {
                    Description description = descList.First(x => x.Id.Value == mItem.DescriptionId.Value);
                    description.MediaItemList.Add(mItem);
                    mItem.Description = description.Value;
                }
            }

            return descList;
        }

        public override List<Attachment> GetAttachment(List<MediaItem> mediaItemList)
        {
            List<Attachment> attachmentList = new List<Attachment>();

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.Common.DbDataReader reader = com.ExecuteReader(
                    "SELECT MEDIAFILES_FK, ATTACHMENTS_FK, PATH FROM ATTACHMENTS INNER JOIN ATTACHED ON ATTACHED.ATTACHMENTS_FK=ATTACHMENTS.ID WHERE MEDIAFILES_FK IN ("
                    + ConcatMediaItemIDs(mediaItemList) + ") ORDER BY PATH");

                while (reader.Read())
                {
                    Attachment attachment = attachmentList.FirstOrDefault(x => x.Id == (int)(long)reader["ATTACHMENTS_FK"]);

                    if (attachment == null)
                    {
                        attachment = new Attachment(new List<MediaItem>(), (string)reader["PATH"]);
                        attachment.Id = (int)(long)reader["ATTACHMENTS_FK"];
                        attachmentList.Add(attachment);
                    }

                    attachment.MediaItemList.Add(mediaItemList.First(x => x.Id == (int)(long)reader["MEDIAFILES_FK"]));
                }

                reader.Close();
            }

            return attachmentList;
        }

        public override void SetAttachment(List<Attachment> attachmentList)
        {
            if (attachmentList.Count == 0)
                return;

            foreach (Attachment attachment in attachmentList)
            {
                using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
                {
                    if (attachment.Type == AttachmentType.ATTACHED)
                    {
                        trans.SetParameter("@PATH", attachment.FullName.Trim());
                        if (attachment.Id == null)
                        {
                            attachment.Id = (int?)(long?)trans.ExecuteScalar("SELECT ID FROM ATTACHMENTS WHERE LOWER(PATH)=LOWER(@PATH)");

                            if (attachment.Id == null)
                            {
                                trans.ExecuteNonQuery("INSERT INTO ATTACHMENTS (PATH) VALUES(@PATH)");
                                attachment.Id = (int)trans.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");
                            }

                            foreach (MediaItem mItem in attachment.MediaItemList)
                            {
                                long count = trans.ExecuteScalar<long>("SELECT COUNT(*) FROM ATTACHED WHERE ATTACHMENTS_FK=" + attachment.Id + " AND MEDIAFILES_FK=" + mItem.Id);

                                if (count == 0)
                                {
                                    trans.ExecuteNonQuery("INSERT INTO ATTACHED (ATTACHMENTS_FK, MEDIAFILES_FK) VALUES(" + attachment.Id + "," + mItem.Id + ")");
                                }
                            }
                        }
                        else
                        {
                            trans.ExecuteNonQuery("UPDATE ATTACHMENTS SET PATH=@PATH WHERE ID=" + attachment.Id);
                        }
                    }
                    else if (attachment.Type == AttachmentType.DETACHE
                        && attachment.Id != null
                        && attachment.MediaItemList != null
                        && attachment.MediaItemList.Count > 0)
                    {
                        trans.ExecuteNonQuery("DELETE FROM ATTACHED WHERE MEDIAFILES_FK IN ("
                            + ConcatMediaItemIDs(attachment.MediaItemList) + ") AND ATTACHMENTS_FK=" + attachment.Id);

                        attachment.IsReferenced = (0 != trans.ExecuteScalar<long>("SELECT COUNT(*) FROM ATTACHED WHERE ATTACHMENTS_FK=" + attachment.Id));
                    }
                }
            }
        }

        public override void SetDescription(Description description)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                if (description.Command == DescriptionCommand.DELETE_DESCRIPTION)
                {
                    if (description.Id == null)
                        return;

                    trans.ExecuteNonQuery("DELETE FROM DESCRIPTION WHERE ID=" + description.Id);
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET DESCRIPTION_FK=NULL WHERE DESCRIPTION_FK=" + description.Id);

                    foreach (MediaItem mItem in description.MediaItemList)
                    {
                        mItem.DescriptionId = null;
                        mItem.Description = "";
                    }

                    return;
                }
                else if (description.Command == DescriptionCommand.SET_AND_CREATE_DESCRIPTION)
                {
                    if (description.Value == null || description.Value.Trim().Length == 0)
                        return;

                    trans.SetParameter("@VALUE", description.Value.Trim());
                    description.Id = (int?)(long?)trans.ExecuteScalar("SELECT ID FROM DESCRIPTION WHERE VALUE=@VALUE");

                    if (description.Id == null)
                    {
                        trans.ExecuteNonQuery("INSERT INTO DESCRIPTION (VALUE) VALUES(@VALUE)");
                        description.Id = (int)trans.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");
                    }
                }
                else if (description.Command == DescriptionCommand.REMOVE_FROM_MEDIAITEMS)
                {
                    description.Id = null;
                }

                if ((description.Command == DescriptionCommand.SET_AND_CREATE_DESCRIPTION
                || description.Command == DescriptionCommand.SET_EXISTING_DESCRIPTION
                || description.Command == DescriptionCommand.REMOVE_FROM_MEDIAITEMS) && description.MediaItemList.Count > 0)
                {
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET DESCRIPTION_FK="
                    + (description.Id == null ? "NULL" : description.Id.ToString())
                    + " WHERE ID IN (-1, " + ConcatMediaItemIDs(description.MediaItemList) + ")");

                    foreach (MediaItem mItem in description.MediaItemList)
                    {
                        mItem.DescriptionId = description.Id;
                        mItem.Description = description.Value;
                    }
                }
            }
        }

        public override Dictionary<MediaItem, XmlNode> CreateMediaItemFromXml(List<XmlNode> nodeList, Folder folder, List<Category> categoryImportList)
        {
            Dictionary<MediaItem, XmlNode> resultList = new Dictionary<MediaItem, XmlNode>();
            Dictionary<int, XmlNode> resultListIntern = new Dictionary<int, XmlNode>();

            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                foreach (XmlNode node in nodeList)
                {
                    int? id = CreateMediaItemFromXmlTrans(trans, node, folder, categoryImportList);

                    if (id != null && !resultListIntern.ContainsKey(id.Value))
                    {
                        resultListIntern.Add(id.Value, node);
                    }
                }
            }

            List<MediaItem> list = LoadMediaItems("FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK "
              + " WHERE MEDIAFILES.ID IN (" + String.Join(",", resultListIntern.Keys) + ")", "", resultListIntern.Count);


            foreach (MediaItem mItem in list)
            {
                resultList.Add(mItem, resultListIntern[mItem.Id]);
            }

            return resultList;
        }


        private int? CreateMediaItemFromXmlTrans(MediaBrowser4.DB.ITransaction trans, XmlNode node, Folder folder, List<Category> categoryImportList)
        {
            if (folder.Id < 0)
                CreateFolder(folder, trans);

            int? id = null;

            trans.SetParameter("@name", node.Attributes["name"].Value, DbType.String);
            trans.SetParameter("@md5", node.Attributes["md5"].Value, DbType.String);
            trans.SetParameter("@folderId", folder.Id, DbType.Int32);

            if (0 != trans.ExecuteScalar<long>(@"SELECT COUNT(FILENAME) FROM MEDIAFILES WHERE FOLDERS_FK=@folderId AND FILENAME=@name"))
                return null;

            long cntDublicates = trans.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5");
            bool isDublicate = false;
            if (cntDublicates != 0)
            {
                isDublicate = true;
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE = 1 WHERE ID IN (SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5)");

                if (cntDublicates == 1)
                {
                    int dubId = (int)trans.ExecuteScalar<long>("SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5"); ;
                    if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(dubId))
                        MediaBrowserContext.GlobalMediaItemCache[dubId].IsMd5Dublicate = true;
                }
            }

            int defaultVariationId = Convert.ToInt32(node.Attributes["variation"].Value);

            trans.SetParameter("@length", Convert.ToInt64(node.Attributes["length"].Value), DbType.Int64);
            trans.SetParameter("@viewed", Convert.ToInt32(node.Attributes["viewed"].Value), DbType.Int32);
            trans.SetParameter("@sortname", node.Attributes["sortname"].Value, DbType.String);
            trans.SetParameter("@type", node.Attributes["type"].Value, DbType.String);
            trans.SetParameter("@creationdate", XmlConvert.ToDateTime(node.Attributes["creationdate"].Value, "o"), DbType.DateTime);
            trans.SetParameter("@insertdate", XmlConvert.ToDateTime(node.Attributes["insertdate"].Value, "o"), DbType.DateTime);
            trans.SetParameter("@editdate", XmlConvert.ToDateTime(node.Attributes["editdate"].Value, "o"), DbType.DateTime);
            trans.SetParameter("@mediadate", XmlConvert.ToDateTime(node.Attributes["mediadate"].Value, "o"), DbType.DateTime);
            trans.SetParameter("@orientation", Convert.ToInt32(node.Attributes["orientation"].Value), DbType.Int32);
            trans.SetParameter("@duration", XmlConvert.ToDouble(node.Attributes["duration"].Value), DbType.Double);
            trans.SetParameter("@frames", Convert.ToInt32(node.Attributes["frames"].Value), DbType.Int32);
            trans.SetParameter("@priority", Convert.ToInt32(node.Attributes["priority"].Value), DbType.Int32);
            trans.SetParameter("@width", Convert.ToInt32(node.Attributes["width"].Value), DbType.Int32);
            trans.SetParameter("@height", Convert.ToInt32(node.Attributes["height"].Value), DbType.Int32);

            if (MediaBrowserContext.AllRoles.Any(x => x.Id == Convert.ToInt32(node.Attributes["roleid"].Value)))
            {
                trans.SetParameter("@roleid", Convert.ToInt32(node.Attributes["roleid"].Value), DbType.Int32);
            }
            else
            {
                trans.SetParameter("@roleid", MediaBrowserContext.AllRoles.First(x => x.Name == "PUBLIC").Id, DbType.Int32);
            }

            trans.SetParameter("@isDublicate", isDublicate, DbType.Boolean);

            trans.ExecuteNonQuery(@"INSERT INTO MEDIAFILES(
                                                   SORTORDER, VIEWED, TYPE, FRAMES, ORIENTATION, DURATION, 
                                                       PRIORITY, WIDTH, HEIGHT, LENGTH, FILENAME, MD5VALUE, FOLDERS_FK,
                                                       ISDELETED, CREATIONDATE, MEDIADATE, EDITDATE, INSERTDATE, ROLES_FK, HISTORYVERSION, ISDUBLICATE) 
                                                   VALUES (@sortname, @viewed, @type, @frames, @orientation, @duration, 
                                                           @priority, @width, @height, @length, @name, @md5,
                                                           @folderId, 0, @creationdate, @mediadate, @editdate, @insertdate, @roleid, 0, @isDublicate)");

            id = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL");
            trans.SetParameter("@id", id.Value, DbType.Int32);

            foreach (XmlNode metadataNode in node["metadata"].ChildNodes)
            {
                string metadataKey = metadataNode.Attributes["key"].Value;
                string metadataType = metadataNode.Attributes["type"].Value;
                string metadataGroup = metadataNode.Attributes["group"].Value;

                trans.SetParameter("@metadataKey", metadataKey, DbType.String);
                trans.SetParameter("@metadataType", metadataType, DbType.String);
                trans.SetParameter("@metadataGroup", metadataGroup, DbType.String);
                trans.SetParameter("@metadataValue", metadataNode.InnerText, DbType.String);

                MetaData metaData = MetaDataNames.FirstOrDefault(x =>
                           x.Name == metadataKey
                        && x.Type == metadataType
                        && x.GroupName == metadataGroup);

                if (metaData.Id <= 0)
                {
                    trans.ExecuteNonQuery("INSERT INTO METADATANAME(NAME, TYPE, GROUPNAME) VALUES(@metadataKey, @metadataType, @metadataGroup)");

                    metaData = new MetaData()
                    {
                        Id = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL"),
                        Name = metadataKey,
                        Type = metadataType,
                        GroupName = metadataGroup,
                        IsVisible = true
                    };

                    MetaDataNames.Add(metaData);
                }

                trans.SetParameter("@metadataId", metaData.Id, DbType.Int32);
                trans.ExecuteNonQuery(@"INSERT INTO METADATA(MEDIAFILES_FK, METANAME_FK, VALUE) VALUES(@id, @metadataId, @metadataValue)");
            }

            foreach (XmlNode variationNode in node["variations"].ChildNodes)
            {
                trans.SetParameter("@variationPosition", Convert.ToInt32(variationNode.Attributes["position"].Value), DbType.Int32);
                trans.SetParameter("@variationName", variationNode.Attributes["name"].Value, DbType.String);

                trans.ExecuteNonQuery("INSERT INTO VARIATIONS(MEDIAFILES_FK, NAME, POSITION) VALUES (@id, @variationName, @variationPosition)");

                int variationId = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL");
                trans.SetParameter("@variationId", variationId, DbType.Int32);

                if (defaultVariationId == Convert.ToInt32(variationNode.Attributes["id"].Value))
                    trans.SetParameter("@currentVariation", variationId, DbType.Int32);

                using (MediaBrowser4.DB.ITransaction transThumb = new MediaBrowser4.DB.SQLite.Transaction(
                     new MediaBrowser4.DB.SQLite.SimpleConnection(
                    DBAdministration.GetThumbNailDBPath(dbPath))
                    ))
                {
                    transThumb.SetParameter("@thumb", Convert.FromBase64String(variationNode["thumbnail"].InnerText), System.Data.DbType.Binary);
                    transThumb.SetParameter("@variationId", variationId, DbType.Int32);
                    transThumb.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK=@variationId");
                    transThumb.ExecuteNonQuery("INSERT INTO THUMBS(VARIATIONS_FK, THUMB) VALUES (@variationId, @thumb)");
                }

                foreach (XmlNode layerNode in variationNode["layers"].ChildNodes)
                {
                    trans.SetParameter("@layerPosition", Convert.ToInt32(layerNode.Attributes["position"].Value), DbType.Int32);
                    trans.SetParameter("@layerEdit", layerNode.Attributes["edit"].Value, DbType.String);
                    trans.SetParameter("@layerAction", layerNode.Attributes["action"].Value, DbType.String);

                    trans.ExecuteNonQuery("INSERT INTO LAYERS(VARIATIONS_FK, POSITION, EDIT, ACTION) VALUES (@variationId, @layerPosition, @layerEdit, @layerAction)");
                }

                if (variationNode.Attributes["categoryids"] != null && variationNode.Attributes["categoryids"].Value.Trim().Length > 0)
                {
                    foreach (string catId in variationNode.Attributes["categoryids"].Value.Trim().Split(','))
                    {
                        Category categorize = categoryImportList.FirstOrDefault(x => x.XmlId.ToString() == catId);
                        if (categorize != null)
                        {
                            trans.ExecuteNonQuery("INSERT OR IGNORE INTO CATEGORIZE (CATEGORY_FK, VARIATIONS_FK) VALUES (" + categorize.Id + ", @variationId)");
                        }
                    }
                }
            }

            trans.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=@currentVariation WHERE ID=@id");

            return id;
        }

        private static List<MetaData> metaDataList;
        public static List<MetaData> MetaDataNames
        {
            get
            {
                if (metaDataList == null)
                {
                    metaDataList = new List<MetaData>();
                    using (MediaBrowser4.DB.ICommandHelper command = MediaBrowserContext.MainDBProvider.MBCommand)
                    {
                        using (IDataReader reader = command.ExecuteReader("SELECT ID, NAME, TYPE, GROUPNAME, IsVisible FROM METADATANAME"))
                        {
                            while (reader.Read())
                            {
                                metaDataList.Add(new MetaData()
                                {
                                    Id = (int)reader.GetInt64(0),
                                    Name = reader.GetString(1).Trim(),
                                    Type = reader.GetString(2).Trim(),
                                    GroupName = reader.GetString(3).Trim(),
                                    IsVisible = (bool)reader[4]
                                });
                            }
                        }
                    }
                }
                return metaDataList;
            }
        }

        private static void CreateFolder(Folder folder, MediaBrowser4.DB.ITransaction trans)
        {
            trans.SetParameter("@foldername", MediaBrowserContext.MapPathRootReverse(FilesAndFolders.CleanPath(folder.FullPath)), DbType.String);
            object id = trans.ExecuteScalar(
                "SELECT ID FROM FOLDERS WHERE LOWER(FOLDERNAME)=LOWER(@foldername)");

            if (id == null || id == DBNull.Value)
            {
                trans.ExecuteNonQuery("INSERT INTO FOLDERS(FOLDERNAME) VALUES(@foldername)");
                folder.Id = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL");
            }
            else
            {
                folder.Id = Convert.ToInt32(id);
            }
        }

        public override bool SetVariationDefault(MediaItem mItem, Variation variation)
        {
            if (variation != null)
                mItem.VariationId = variation.Id;

            if (mItem.VariationId == mItem.VariationIdDefault)
                return false;

            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=" + mItem.VariationId + " WHERE ID=" + mItem.Id);
                mItem.VariationIdDefault = mItem.VariationId;
            }

            using (MediaBrowser4.DB.ITransaction trans2 = new MediaBrowser4.DB.SQLite.Transaction(
            new MediaBrowser4.DB.SQLite.SimpleConnection(
                DBAdministration.GetThumbNailDBPath(dbPath))
                ))
            {
                mItem.ThumbJpegData = trans2.ExecuteScalar<byte[]>("SELECT THUMB FROM THUMBS WHERE VARIATIONS_FK=" + mItem.VariationId);
            }

            return true;
        }

        public override bool RemoveVariation(MediaItem mItem)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                if (mItem.VariationId == trans.ExecuteScalar<long>("SELECT CURRENTVARIATION FROM MEDIAFILES WHERE ID=" + mItem.Id))
                    return false;

                trans.ExecuteNonQuery("DELETE FROM VARIATIONS WHERE ID=" + mItem.VariationId);

                int cnt = 0;
                foreach (System.Data.DataRow row in trans.GetDataTable("SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id + " ORDER BY POSITION").Rows)
                {
                    cnt++;
                    trans.ExecuteNonQuery("UPDATE VARIATIONS SET POSITION=" + cnt + " WHERE ID=" + row[0]);
                }
            }

            using (MediaBrowser4.DB.ITransaction trans2 = new MediaBrowser4.DB.SQLite.Transaction(
               new MediaBrowser4.DB.SQLite.SimpleConnection(
                   DBAdministration.GetThumbNailDBPath(dbPath))
                   ))
            {
                trans2.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK=" + mItem.VariationId);
            }

            return true;
        }

        public override bool RemoveVariation(MediaItem mItem, string name)
        {
            List<string> idList = new List<string>();
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                long currentVarId = trans.ExecuteScalar<long>("SELECT CURRENTVARIATION FROM MEDIAFILES WHERE ID=" + mItem.Id);

                trans.SetParameter("@NAME", name.Trim());
                foreach (System.Data.DataRow row in trans.GetDataTable("SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id + " AND NAME=@NAME").Rows)
                {
                    idList.Add(row[0].ToString());
                    if (currentVarId == (long)row[0])
                        return false;
                }

                trans.ExecuteNonQuery("DELETE FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id + " AND NAME=@NAME");

                int cnt = 0;
                foreach (System.Data.DataRow row in trans.GetDataTable("SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id + " ORDER BY POSITION").Rows)
                {
                    cnt++;
                    trans.ExecuteNonQuery("UPDATE VARIATIONS SET POSITION=" + cnt + " WHERE ID=" + row[0]);
                }
            }

            if (idList.Count > 0)
            {
                using (MediaBrowser4.DB.ITransaction trans2 = new MediaBrowser4.DB.SQLite.Transaction(
                   new MediaBrowser4.DB.SQLite.SimpleConnection(
                       DBAdministration.GetThumbNailDBPath(dbPath))
                       ))
                {
                    trans2.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK IN (" + String.Join(",", idList) + ")");
                }
            }

            return true;
        }

        public override Variation SetNewVariation(MediaItem mItem, string name, bool setDefault, string layerAction, string layerEdit, byte[] thumbData)
        {
            Variation variation = null;
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                long newPos = trans.ExecuteScalar<long>("SELECT MAX(POSITION)+1 FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id);
                trans.SetParameter("@NAME", name.Trim());
                trans.ExecuteNonQuery("INSERT INTO VARIATIONS (MEDIAFILES_FK, NAME, POSITION) VALUES (" + mItem.Id + ",@NAME," + newPos + ")");
                long varId = trans.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");

                if (layerAction == null)
                    foreach (System.Data.DataRow row in trans.GetDataTable("SELECT POSITION, EDIT, ACTION FROM LAYERS WHERE VARIATIONS_FK=" + mItem.VariationId).Rows)
                    {
                        trans.SetParameter("@ACTION", row["ACTION"].ToString());
                        trans.ExecuteNonQuery("INSERT INTO LAYERS (VARIATIONS_FK, POSITION, EDIT, ACTION) VALUES("
                            + varId + "," + row["POSITION"] + ",'" + row["EDIT"] + "', @ACTION)");
                    }

                foreach (System.Data.DataRow row in trans.GetDataTable("SELECT CATEGORY_FK FROM CATEGORIZE WHERE VARIATIONS_FK=" + mItem.VariationId).Rows)
                {
                    trans.ExecuteNonQuery("INSERT INTO CATEGORIZE (CATEGORY_FK, VARIATIONS_FK) VALUES(" + row["CATEGORY_FK"] + "," + varId + ")");
                }

                variation = new Variation() { Name = name, Id = (int)varId, Position = (int)newPos, MediaItemId = mItem.Id };

                if (setDefault)
                {
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=" + variation.Id + " WHERE ID=" + mItem.Id);
                    mItem.VariationIdDefault = variation.Id;
                }

                if (layerAction != null && layerAction.Length > 0 && layerEdit != null)
                {
                    trans.SetParameter("@action", layerAction);
                    trans.SetParameter("@edit", layerEdit);
                    trans.ExecuteNonQuery("INSERT INTO LAYERS (ACTION, EDIT, POSITION, VARIATIONS_FK) VALUES (@action, @edit, 0, " + variation.Id + ")");
                }
            }

            using (MediaBrowser4.DB.ITransaction trans2 = new MediaBrowser4.DB.SQLite.Transaction(
              new MediaBrowser4.DB.SQLite.SimpleConnection(
                  DBAdministration.GetThumbNailDBPath(dbPath))
                  ))
            {
                byte[] imageData = thumbData;


                if (imageData == null)
                    imageData = trans2.ExecuteScalar<byte[]>("SELECT THUMB FROM THUMBS WHERE VARIATIONS_FK=" + mItem.VariationId);

                if (imageData != null)
                {
                    trans2.SetParameter("@Thumb", imageData, System.Data.DbType.Binary);
                    trans2.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK=" + variation.Id);
                    trans2.ExecuteNonQuery("INSERT INTO THUMBS(THUMB, VARIATIONS_FK) VALUES(@Thumb, " + variation.Id + ")");
                }
            }

            if (layerAction == null)
                mItem.ChangeVariation(variation);

            return variation;
        }

        public override List<Layer> GetLayers(Variation variation)
        {
            return this.GetLayers(variation.Id);
        }

        public override List<MediaBrowser4.Objects.Layer> GetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            return this.GetLayers(mItem.VariationId);
        }

        private List<MediaBrowser4.Objects.Layer> GetLayers(int variationID)
        {
            List<MediaBrowser4.Objects.Layer> lIst = new List<MediaBrowser4.Objects.Layer>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table
                        = com.GetDataTable("SELECT ACTION, EDIT, POSITION FROM LAYERS WHERE VARIATIONS_FK=" + variationID + " ORDER BY POSITION");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    lIst.Add(
                        new MediaBrowser4.Objects.Layer(row["EDIT"].ToString(), row["ACTION"].ToString(), (int)(long)row["POSITION"])
                        );
                }
            }
            return lIst;
        }

        public override byte[] GetThumbJpegData(int variationId)
        {
            using (MediaBrowser4.DB.ICommandHelper com = new CommandHelper(
             new MediaBrowser4.DB.SQLite.SimpleConnection(
                   DBAdministration.GetThumbNailDBPath(dbPath))))
            {
                return com.ExecuteScalar<byte[]>("SELECT THUMB FROM THUMBS WHERE VARIATIONS_FK=" + variationId);
            }
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetDublicates(List<MediaBrowser4.Objects.MediaItem> mList, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
        {
            List<MediaBrowser4.Objects.MediaItem> returnlist = new List<MediaBrowser4.Objects.MediaItem>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                foreach (MediaBrowser4.Objects.MediaItem mItem in mList)
                {
                    long cnt = 0;
                    switch (criteria)
                    {
                        case MediaItem.DublicateCriteria.CONTAINS:
                            cnt = com.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME) LIKE '%" + mItem.Filename.ToLower() + "%'");
                            break;

                        case MediaItem.DublicateCriteria.FILENAME:
                            cnt = com.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME)='" + mItem.Filename.ToLower() + "'");
                            break;

                        case MediaItem.DublicateCriteria.CHECKSUM:
                            cnt = com.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE='" + mItem.Md5Value + "'");
                            break;
                    }

                    if (cnt > 1)
                    {
                        returnlist.Add(mItem);
                    }
                }
            }

            return returnlist;
        }

        public override bool IsDublicate(MediaBrowser4.Objects.MediaItem mItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                switch (criteria)
                {
                    case MediaItem.DublicateCriteria.FILENAME:
                        return 0 != com.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME)='" + mItem.Filename.ToLower() + "' AND MEDIAFILES.ID<>"
                            + mItem.Id);

                    case MediaItem.DublicateCriteria.CONTAINS:
                        return 0 != com.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME) LIKE '%" + mItem.Filename.ToLower() + "%' AND MEDIAFILES.ID<>"
                            + mItem.Id);

                    default:
                        return false;
                }

            }
        }

        public override Dictionary<int, string> GetDublicates(MediaBrowser4.Objects.MediaItem mediaItem, MediaBrowser4.Objects.MediaItem.DublicateCriteria criteria)
        {
            Dictionary<int, string> dublicateList = new Dictionary<int, string>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                string sql;

                switch (criteria)
                {
                    case MediaItem.DublicateCriteria.FILENAME:
                        sql = "SELECT MEDIAFILES.ID AS MFID, FILENAME, FOLDERNAME FROM MEDIAFILES, FOLDERS WHERE " +
                        "LOWER(FILENAME)='" + mediaItem.Filename.ToLower() + "' AND MEDIAFILES.ID<>" + mediaItem.Id +
                        " AND FOLDERS_FK = FOLDERS.ID ";
                        break;

                    case MediaItem.DublicateCriteria.CONTAINS:
                        sql = "SELECT MEDIAFILES.ID AS MFID, FILENAME, FOLDERNAME FROM MEDIAFILES, FOLDERS WHERE " +
                        "LOWER(FILENAME) LIKE'%" + mediaItem.Filename.ToLower() + "%' AND MEDIAFILES.ID<>" + mediaItem.Id +
                        " AND FOLDERS_FK = FOLDERS.ID ";
                        break;

                    default:
                        sql = "SELECT MEDIAFILES.ID AS MFID, FILENAME, FOLDERNAME FROM MEDIAFILES, FOLDERS WHERE " +
                        "MD5VALUE='" + mediaItem.Md5Value + "' AND MEDIAFILES.ID<>" + mediaItem.Id +
                        " AND FOLDERS_FK = FOLDERS.ID ";
                        break;
                }

                System.Data.DataTable table = com.GetDataTable(sql);

                foreach (System.Data.DataRow row in table.Rows)
                {
                    dublicateList.Add((int)(long)row["MFID"], MediaBrowserContext.MapPathRoot(row["FOLDERNAME"].ToString()) + "\\" + row["FILENAME"]);
                }
            }

            return dublicateList;
        }


        private MediaBrowser4.Objects.Category GetCategoryFromDataRow(System.Data.DataRow row)
        {
            MediaBrowser4.Objects.Category category = new MediaBrowser4.Objects.Category();
            category.Id = (int)(long)row["ID"];
            category.name = row["NAME"].ToString();
            category.Description = row["DESCRIPTION"].ToString();
            category.Sortname = row["SORTORDER"].ToString();
            category.Guid = row["GUID"].ToString();
            category.FullPath = row["FULLPATH"].ToString();
            category.ParentId = (int)(long)row["PARENT"];
            category.IsUnique = (bool)row["ISUNIQUE"];
            category.IsDate = (bool)row["ISDATE"];
            category.IsLocation = (bool)row["ISLOCATION"];
            category.ItemCount = row["ItemCount"] == DBNull.Value ? 0 : (int)(long)row["ItemCount"];


            if (row["Date"] != DBNull.Value)
            {
                category.Date = (DateTime)row["Date"];
            }
            else
            {
                category.Date = DateTime.Now;
            }

            if (row["LATITUDE"] != DBNull.Value)
            {
                category.Latitude = row["LATITUDE"] is decimal ? (decimal)row["LATITUDE"] : (decimal)(double)row["LATITUDE"];
            }

            if (row["LONGITUDE"] != DBNull.Value)
            {
                category.Longitude = row["LONGITUDE"] is decimal ? (decimal)row["LONGITUDE"] : (decimal)(double)row["LONGITUDE"];
            }


            return category;
        }

        public override List<Category> GetCategories(int variationId)
        {
            List<Category> categoryList = new List<Category>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT ID, NAME, PARENT, DESCRIPTION, SORTORDER, GUID, DATE, ISUNIQUE, ISDATE, ISLOCATION, LATITUDE, LONGITUDE, FULLPATH, items.ItemCount FROM CATEGORY LEFT JOIN "
                    + "(select count(c.VARIATIONS_FK) as ItemCount, CATEGORY_FK from categorize c inner join mediafiles ON mediafiles.CURRENTVARIATION = c.VARIATIONS_FK group by c.CATEGORY_FK) items on items.category_fk = ID INNER JOIN CATEGORIZE ON CATEGORIZE.CATEGORY_FK=CATEGORY.ID WHERE VARIATIONS_FK=" + variationId);

                foreach (System.Data.DataRow row in table.Rows)
                {
                    categoryList.Add(GetCategoryFromDataRow(row));
                }
            }

            return categoryList;
        }

        public override CategoryTree GetCategoryTree()
        {
            CategoryCollection categories = new CategoryCollection();
            Dictionary<int, MediaBrowser4.Objects.Category> categoryDic = new Dictionary<int, MediaBrowser4.Objects.Category>();

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT ID, NAME, PARENT, DESCRIPTION, SORTORDER, GUID, DATE, ISUNIQUE, ISDATE, ISLOCATION, LATITUDE, LONGITUDE, FULLPATH, items.ItemCount FROM CATEGORY LEFT JOIN "
                    + "(select count(c.VARIATIONS_FK) as ItemCount, CATEGORY_FK from categorize c inner join mediafiles ON mediafiles.CURRENTVARIATION = c.VARIATIONS_FK group by c.CATEGORY_FK) items on items.category_fk = ID ORDER BY LOWER(SORTORDER) ASC");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    MediaBrowser4.Objects.Category category = GetCategoryFromDataRow(row);
                    categoryDic.Add(category.Id, category);
                }
            }

            foreach (MediaBrowser4.Objects.Category cat in categoryDic.Values)
            {
                if (cat.ParentId <= 0)
                {
                    categories.Add(cat);
                }
                else
                {
                    cat.Parent = categoryDic[cat.ParentId];
                }
            }

            CategoryTree categoryTree = new CategoryTree(categories, categoryDic);

            return categoryTree;
        }

        public override void UpdateMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                trans.SetParameter("@DURATION", mItem.Duration, DbType.Double);
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET DURATION=@DURATION WHERE ID=" + mItem.Id);
            }
        }

        public override void RemoveFolder(Folder folder)
        {
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                List<string> md5Values = new List<string>();
                using (IDataReader reader = trans.ExecuteReader("SELECT ID, MD5VALUE FROM MEDIAFILES WHERE FOLDERS_FK=" + folder.Id))
                {
                    while (reader.Read())
                    {
                        MediaBrowserContext.GlobalMediaItemCache.Remove(reader.GetInt32(0));
                        md5Values.Add(reader.GetString(1).Trim());
                    }
                }

                trans.ExecuteNonQuery("DELETE FROM FOLDERS WHERE ID=" + folder.Id);

                foreach (string md5 in md5Values)
                {
                    if (trans.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE " +
                            "MD5VALUE='" + md5 + "'") == 1)
                    {
                        int id = (int)trans.ExecuteScalar<long>("SELECT ID FROM MEDIAFILES WHERE " +
                            "MD5VALUE='" + md5 + "'");

                        trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0 WHERE ID=" + id);

                        if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(id))
                            MediaBrowserContext.GlobalMediaItemCache[id].IsMd5Dublicate = false;
                    }
                }
            }

            folder.SetVirtual();

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("ATTACH '" + DBAdministration.GetThumbNailDBPath(dbPath) + "' AS thmb;");
                com.ExecuteNonQuery("DELETE FROM thmb.THUMBS WHERE VARIATIONS_FK IN (SELECT VARIATIONS.ID FROM VARIATIONS, MEDIAFILES WHERE VARIATIONS.MEDIAFILES_FK=MEDIAFILES.ID AND MEDIAFILES.FOLDERS_FK=" + folder.Id + ")");
            }
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingFiles(string sortString, int limtRequest)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND MEDIAFILES.ID IN (-1,");

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.Common.DbDataReader reader = com.ExecuteReader(
                    "SELECT MEDIAFILES.ID, FOLDERNAME || '\\' || FILENAME FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK");

                while (reader.Read())
                {
                    if (!System.IO.File.Exists(reader[1].ToString()))
                    {
                        sb.Append(MediaBrowserContext.MapPathRoot(reader[0].ToString()) + ",");
                    }
                }

                reader.Close();

            }

            return LoadMediaItems(sb.ToString().TrimEnd(',') + ") AND HISTORYVERSION=0 ", sortString, limtRequest);
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsWithMissingThumbs(string sortString, int limtRequest)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND MEDIAFILES.ID IN (-1,");
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("ATTACH '" + DBAdministration.GetThumbNailDBPath(dbPath) + "' AS thmb;");

                int result = com.ExecuteNonQuery("DELETE FROM thmb.THUMBS WHERE VARIATIONS_FK IN "
                    + "(SELECT b FROM (SELECT COUNT(VARIATIONS_FK) a, VARIATIONS_FK b "
                    + "FROM thmb.THUMBS GROUP BY VARIATIONS_FK) WHERE a<>1)");

                System.Data.DataTable table
                    = com.GetDataTable("SELECT MEDIAFILES.ID FROM VARIATIONS, MEDIAFILES WHERE "
                        + "MEDIAFILES.CURRENTVARIATION=VARIATIONS.ID AND VARIATIONS.ID NOT IN (SELECT VARIATIONS_FK FROM THUMBS)");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    sb.Append(row[0] + ",");
                }
            }

            return LoadMediaItems(sb.ToString().TrimEnd(',') + ") AND HISTORYVERSION=0 ", sortString, limtRequest);
        }

        public override List<MediaBrowser4.Objects.Folder> GetFolderlist()
        {
            List<MediaBrowser4.Objects.Folder> folderList = new List<MediaBrowser4.Objects.Folder>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT ID, FOLDERNAME, mediaitems.ItemCount FROM FOLDERS LEFT OUTER JOIN (select count(folders_fk) as ItemCount, folders_fk from mediafiles group by folders_fk) as mediaitems on mediaitems.folders_fk = FOLDERS.ID ORDER BY LOWER(FOLDERNAME)");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    MediaBrowser4.Objects.Folder folder = new MediaBrowser4.Objects.Folder(
                        (int)(long)row["ID"], MediaBrowserContext.MapPathRoot(row["FOLDERNAME"].ToString()), row["ItemCount"] == DBNull.Value ? 0 : (int)(long)row["ItemCount"]);

                    folderList.Add(folder);
                }

            }
            return folderList;
        }

        public override void CopyToFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, MediaBrowser4.Objects.Folder folder)
        {
            if (mediaItemList.Count == 0)
                return;

            List<Folder> oldFolders = new List<Folder>();
            foreach (MediaItem mItem in mediaItemList)
            {
                Folder oldFolder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mItem.FolderId);

                if (oldFolder != null)
                {
                    oldFolder.ItemCount--;

                    if (!oldFolders.Contains(oldFolder))
                        oldFolders.Add(oldFolder);
                }

                mItem.FolderId = folder.Id;
                mItem.Foldername = folder.FullPath;
                mItem.FileObject = null;
                folder.ItemCount++;
            }

            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET FOLDERS_FK="
                    + folder.Id
                 + " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");
            }

            Folder parent = folder;
            while (parent != null)
            {
                parent.UpdateItemInfo();
                parent = parent.Parent;
            }

            foreach (Folder oldFolder in oldFolders)
            {
                parent = oldFolder;
                while (parent != null)
                {
                    parent.UpdateItemInfo();
                    parent = parent.Parent;
                }
            }
        }

        public override void SetLayersForMediaItems(List<MediaItem> mItemList)
        {
            if (mItemList.Count == 0)
                return;

            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                foreach (MediaItem mItem in mItemList)
                {
                    SetLayersForMediaItem(mItem, trans);
                    mItem.IsThumbJpegDataOutdated = true;
                }
            }

            MediaBrowser4.DB.ITransaction trans2 = new MediaBrowser4.DB.SQLite.Transaction(
              new MediaBrowser4.DB.SQLite.SimpleConnection(DBAdministration.GetThumbNailDBPath(dbPath)));

            trans2.ExecuteNonQuery("UPDATE THUMBS SET ISOUTDATED=1 WHERE VARIATIONS_FK IN ("
                + String.Join(",", mItemList.Select(x => x.VariationId)) + ")");

            trans2.Dispose();
        }

        public override void SetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                SetLayersForMediaItem(mItem, trans);
            }
        }

        private void SetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem, MediaBrowser4.DB.ITransaction trans)
        {
            trans.ExecuteNonQuery("DELETE FROM LAYERS WHERE VARIATIONS_FK = " + mItem.VariationId);
            foreach (MediaBrowser4.Objects.Layer layer in mItem.Layers)
            {
                trans.SetParameter("@action", layer.action);
                trans.SetParameter("@edit", layer.edit);
                trans.ExecuteNonQuery("INSERT INTO LAYERS (ACTION, EDIT, POSITION, VARIATIONS_FK) VALUES (@action, @edit, "
                    + layer.position + ", " + mItem.VariationId + ")");
            }
        }

        private void GetCategoryDownNodes(MediaBrowser4.Objects.Category parentNode, List<MediaBrowser4.Objects.Category> nodeList)
        {
            foreach (MediaBrowser4.Objects.Category node in parentNode.Children)
            {
                nodeList.Add(node);
                GetCategoryDownNodes(node, nodeList);
            }
        }

        public override void UnCategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList)
        {
            CategorizeMediaItems(mList, cList, true);
        }

        public override void CategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList, List<MediaBrowser4.Objects.Category> cList)
        {
            CategorizeMediaItems(mList, cList, false);
        }

        private void CategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList,
            List<MediaBrowser4.Objects.Category> categoryChangeList, bool removeCategory)
        {
            if (categoryChangeList.Count == 0 || mList.Count == 0)
            {
                return;
            }

            Dictionary<Category, List<Category>> categoryUniqueTrees = null;
            if (!removeCategory)
            {
                //Den Unique-Baum erstellen                
                foreach (Category cat in categoryChangeList)
                {
                    if (cat.IsUnique)
                    {
                        //findet den höchsgelegenen Knoten der noch Uniqie ist
                        Category parent = cat.Parent;
                        Category uniqueRoot = cat;
                        while (parent != null && parent.IsUnique)
                        {
                            uniqueRoot = parent;
                            parent = parent.Parent;
                        }

                        List<Category> uniqueTree = uniqueRoot.AllChildrenRecursive();

                        if (uniqueTree.Count > 1)
                        {
                            if (categoryUniqueTrees == null)
                                categoryUniqueTrees = new Dictionary<Category, List<Category>>();

                            categoryUniqueTrees.Add(cat, uniqueTree);
                        }
                    }
                }

                if (categoryUniqueTrees != null)
                {
                    foreach (List<Category> catUniqueList in categoryUniqueTrees.Values)
                    {
                        int cnt = 0;
                        foreach (Category cat in categoryChangeList)
                        {
                            if (catUniqueList.Contains(cat))
                            {
                                cnt++;

                                if (cnt > 1)
                                {
                                    throw new Exception("Sie versuchen mehr als eine Kategorie in einem als 'Eindeutig' markierten Teilbaum zu erstellen.");
                                }
                            }
                        }
                    }
                }
            }

            //Entferne alle höhergelegenen unnötigen Kategorien aus der Liste
            List<Category> categoryUpNodes = new List<Category>();
            foreach (Category cat in categoryChangeList)
            {
                if (categoryUpNodes.Contains(cat))
                    continue;

                Category parent = cat.Parent;

                while (parent != null)
                {
                    if (!categoryUpNodes.Contains(parent))
                        categoryUpNodes.Add(parent);

                    parent = parent.Parent;
                }
            }

            foreach (Category cat in categoryUpNodes)
            {
                categoryChangeList.Remove(cat);
            }

            string categoryUpNodesIDs = ConcatCategoryIds(categoryUpNodes);
            string newCategoriesListIDs = ConcatCategoryIds(categoryChangeList);
            string variationIDs = ConcatVariationIDs(mList);

            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                //alle neuen plus höhergelegenen Knoten pauschal löschen
                trans.ExecuteNonQuery("DELETE FROM CATEGORIZE WHERE CATEGORY_FK IN ("
                    + newCategoriesListIDs + (categoryUpNodesIDs.Length > 0 ? "," + categoryUpNodesIDs : "")
                    + ") AND VARIATIONS_FK IN ("
                    + variationIDs + ")");

                if (!removeCategory)
                {
                    foreach (MediaBrowser4.Objects.MediaItem mItem in mList)
                    {
                        foreach (MediaBrowser4.Objects.Category cItem in categoryChangeList)
                        {
                            if (trans.ExecuteScalar<long>("select count(*) from CATEGORIZE where CATEGORY_FK = " + cItem.Id + " AND VARIATIONS_FK = " + mItem.VariationId) == 0)
                            {
                                //alle neuen Categorien einfügen
                                trans.ExecuteNonQuery("INSERT INTO CATEGORIZE (CATEGORY_FK,VARIATIONS_FK) VALUES ("
                                    + cItem.Id + "," + mItem.VariationId + ")");
                            }
                        }
                    }
                }

                List<Category> clearCats = new List<Category>();
                foreach (MediaItem mItem in mList)
                {
                    //Kategorien aktualisieren
                    mItem.Categories.Clear();
                    using (IDataReader reader = trans.ExecuteReader("SELECT CATEGORY_FK FROM CATEGORIZE WHERE "
                     + "CATEGORIZE.VARIATIONS_FK=" + mItem.VariationId))
                    {
                        while (reader.Read())
                        {
                            Category cat = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById((int)(long)reader[0]);
                            if (cat != null && !cat.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                                mItem.Categories.Add(cat);
                        }
                    }

                    if (removeCategory)
                        continue;

                    //Aufräumen 
                    List<Category> categoryExistingDownNodes = new List<Category>();
                    foreach (Category cat in mItem.Categories)
                    {
                        if (!categoryChangeList.Contains(cat))
                        {
                            Category parent = cat.Parent;
                            while (parent != null)
                            {
                                if (categoryChangeList.Contains(parent))
                                    categoryExistingDownNodes.Add(cat);

                                parent = parent.Parent;
                            }
                        }
                    }

                    if (categoryExistingDownNodes.Count > 0)
                    {
                        foreach (Category cat in categoryExistingDownNodes)
                        {
                            mItem.Categories.Remove(cat);
                        }

                        string removeCategoriesListIDs = ConcatCategoryIds(categoryExistingDownNodes);
                        trans.ExecuteNonQuery("DELETE FROM CATEGORIZE WHERE CATEGORY_FK IN (" + removeCategoriesListIDs + ") AND VARIATIONS_FK=" + mItem.VariationId);
                    }

                    if (categoryUniqueTrees != null)
                    {
                        List<Category> categoryUniqueRemoveList = new List<Category>();
                        foreach (List<Category> catUniqueList in categoryUniqueTrees.Values)
                        {
                            foreach (Category cat in mItem.Categories)
                            {
                                if (catUniqueList.Contains(cat) && !categoryChangeList.Contains(cat))
                                {
                                    categoryUniqueRemoveList.Add(cat);
                                }
                            }
                        }

                        if (categoryUniqueRemoveList.Count > 0)
                        {
                            trans.ExecuteNonQuery("DELETE FROM CATEGORIZE WHERE CATEGORY_FK IN (" + ConcatCategoryIds(categoryUniqueRemoveList) + ") AND VARIATIONS_FK=" + mItem.VariationId);
                        }

                        foreach (Category cat in categoryUniqueRemoveList)
                            mItem.Categories.Remove(cat);
                    }
                }
            }

            List<Category> catList = categoryChangeList.Where(x => !x.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName)).ToList();

            if (catList.Count > 0)
            {

                foreach (MediaItem mItem in mList)
                    mItem.InvokeCategoriesChanged();

                MediaBrowserContext.InvokeCategoriesChanged(mList, catList, removeCategory);
            }

            foreach (Category cat in categoryChangeList)
            {
                if (removeCategory)
                    cat.ItemCount -= mList.Count;
                else
                    cat.ItemCount += mList.Count;

                Category parent = cat;
                while (parent != null)
                {
                    parent.UpdateItemInfo();
                    parent = parent.Parent;
                }
            }
        }

        private static string ConcatFolderIds(List<Folder> folderList)
        {
            StringBuilder sb = new StringBuilder();
            foreach (MediaBrowser4.Objects.Folder fItem in folderList)
            {
                sb.Append(fItem.Id + ",");
            }
            return sb.ToString().TrimEnd(',');
        }

        private static string ConcatCategoryIds(List<Category> categoryList)
        {
            StringBuilder sb = new StringBuilder();
            foreach (MediaBrowser4.Objects.Category cItem in categoryList)
            {
                sb.Append(cItem.Id + ",");
            }
            return sb.ToString().TrimEnd(',');
        }

        private static string ConcatMediaItemIDs(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            StringBuilder sb = new StringBuilder();
            foreach (MediaBrowser4.Objects.MediaItem mItem in mediaItemList)
            {
                sb.Append(mItem.Id + ",");
            }
            return sb.ToString().TrimEnd(',');
        }

        private static string ConcatVariationIDs(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            StringBuilder sb = new StringBuilder();
            foreach (MediaBrowser4.Objects.MediaItem mItem in mediaItemList)
            {
                sb.Append(mItem.VariationId + ",");
            }
            return sb.ToString().TrimEnd(',');
        }


        public override List<string> GetCategoryIdentifiersFromMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            List<string> catIDs = new List<string>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT CATEGORY_FK FROM CATEGORIZE WHERE VARIATIONS_FK=" + mItem.VariationId);

                foreach (System.Data.DataRow row in table.Rows)
                {
                    catIDs.Add(row[0].ToString());
                }
            }
            return catIDs;
        }

        public override Dictionary<MediaBrowser4.Objects.Category, int> GetCategoriesFromMediaItems(List<MediaItem> mItemList)
        {
            Dictionary<MediaBrowser4.Objects.Category, int> catDict = new Dictionary<Category, int>();

            if (mItemList == null || mItemList.Count == 0)
                return catDict;

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                using (DbDataReader reader = com.ExecuteReader(
                    "SELECT CATEGORY.ID, COUNT(CATEGORY.ID) AS CNT FROM CATEGORY INNER JOIN CATEGORIZE ON CATEGORIZE.CATEGORY_FK=CATEGORY.ID WHERE "
                    + "CATEGORIZE.VARIATIONS_FK IN (" + String.Join(",", mItemList.Select(x => x.VariationId)) + ") GROUP BY CATEGORY.ID"))
                {
                    while (reader.Read())
                    {
                        Category cat = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById((int)(long)reader["ID"]);
                        if (cat != null && !cat.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                        {
                            catDict.Add(cat, (int)(long)reader["CNT"]);
                        }
                    }
                }
            }

            return catDict;
        }

        public override List<MetaData> GetMetaDataFromMediaItems(List<MediaItem> mItemList)
        {
            List<MetaData> metaData = new List<MetaData>();

            using (MediaBrowser4.DB.ICommandHelper com = MediaBrowserContext.MainDBProvider.MBCommand)
            {
                System.Data.DataTable table
                    = com.GetDataTable("SELECT DISTINCT TYPE, GROUPNAME, NAME, VALUE, ISVISIBLE FROM METADATANAME INNER JOIN  "
                    + "METADATA ON METADATANAME.ID = METADATA.METANAME_FK WHERE MEDIAFILES_FK IN (" + String.Join(",", mItemList.Select(x => x.Id)) + ") ORDER BY TYPE, GROUPNAME, NAME, VALUE");

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

            return metaData;
        }




        public override List<MediaBrowser4.Objects.Role> GetRoleList()
        {
            List<MediaBrowser4.Objects.Role> list = new List<MediaBrowser4.Objects.Role>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table = com.GetDataTable("SELECT ID, ROLE, PASSWORD, DESCRIPTION FROM ROLES ORDER BY ROLE");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    list.Add(new MediaBrowser4.Objects.Role(
                        (int)(long)row["ID"],
                        row["ROLE"].ToString(),
                        row["PASSWORD"].ToString(),
                        row["DESCRIPTION"].ToString()));
                }
            }
            return list;
        }

        private void AddMediaItemsToList(DbDataReader reader, ref List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            int colMediaId = reader.GetOrdinal("MF_ID");
            int colType = reader.GetOrdinal("TYPE");
            int colFilename = reader.GetOrdinal("FILENAME");
            int colSortorder = reader.GetOrdinal("SORTORDER");
            int colVariation = reader.GetOrdinal("CURRENTVARIATION");
            int colLength = reader.GetOrdinal("LENGTH");
            int colFoldername = reader.GetOrdinal("FOLDERNAME");
            int colFolderId = reader.GetOrdinal("FOLDERS_FK");
            int colDescriptionId = reader.GetOrdinal("DESCRIPTION_FK");

            int colHeight = reader.GetOrdinal("HEIGHT");
            int colWidth = reader.GetOrdinal("WIDTH");
            int colDuration = reader.GetOrdinal("DURATION");
            int colFrames = reader.GetOrdinal("FRAMES");
            int colPriority = reader.GetOrdinal("PRIORITY");
            int colMediaDate = reader.GetOrdinal("MEDIADATE");
            int colInsertDate = reader.GetOrdinal("INSERTDATE");
            int colCreationDate = reader.GetOrdinal("CREATIONDATE");
            int colEditDate = reader.GetOrdinal("EDITDATE");
            int colMd5Value = reader.GetOrdinal("MD5VALUE");
            int colIsBookmarked = reader.GetOrdinal("ISBOOKMARKED");
            int colIsMd5Dublicate = reader.GetOrdinal("ISDUBLICATE");
            int colIsDeleted = reader.GetOrdinal("ISDELETED");
            int colOrientation = reader.GetOrdinal("ORIENTATION");
            int colViewed = reader.GetOrdinal("VIEWED");
            int colRoleId = reader.GetOrdinal("ROLES_FK");

            List<MediaItem> deleteList = null;
            Dictionary<int, MediaItem> newItems = new Dictionary<int, MediaItem>();
            while (reader.Read())
            {
                if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(reader.GetInt32(colMediaId)))
                {
                    MediaItem mItem = MediaBrowserContext.GlobalMediaItemCache[reader.GetInt32(colMediaId)];

                    if (CheckMissingBehavior(ref deleteList, mItem))
                        continue;

                    mediaItemList.Add(mItem);
                }
                else
                {
                    MediaItem mItem = MediaItem.GetMediaItemFromDBType(reader.GetString(colType));

                    mItem.Id = reader.GetInt32(colMediaId);
                    mItem.Filename = reader.GetString(colFilename);
                    mItem.Sortorder = reader.GetString(colSortorder);
                    mItem.VariationId = reader.GetInt32(colVariation);
                    mItem.VariationIdDefault = mItem.VariationId;
                    mItem.FileLength = reader.GetInt64(colLength);
                    mItem.Foldername = MediaBrowserContext.MapPathRoot(reader.GetString(colFoldername));
                    mItem.FolderId = reader.GetInt32(colFolderId);
                    mItem.DescriptionId = reader[colDescriptionId] == DBNull.Value ? (int?)null : (int?)reader.GetInt32(colDescriptionId);
                    mItem.Height = reader.GetInt32(colHeight);
                    mItem.Width = reader.GetInt32(colWidth);
                    mItem.Duration = reader.GetDouble(colDuration);
                    mItem.Frames = reader.GetInt32(colFrames);
                    mItem.Fps = ((double)mItem.Frames / mItem.Duration);
                    mItem.Priority = reader.GetInt32(colPriority);
                    mItem.MediaDate = reader.GetDateTime(colMediaDate);
                    mItem.InsertDate = reader.GetDateTime(colInsertDate);
                    mItem.CreationDate = reader.GetDateTime(colCreationDate);
                    mItem.LastWriteDate = reader.GetDateTime(colEditDate);
                    mItem.Md5Value = reader.GetString(colMd5Value);
                    mItem.IsBookmarked = (bool)reader[colIsBookmarked];
                    mItem.IsMd5Dublicate = (bool)reader[colIsMd5Dublicate];
                    mItem.IsDeleted = (bool)reader[colIsDeleted];
                    mItem.SetOrientationByNumber(reader.GetInt32(colOrientation));
                    mItem.Viewed = reader.GetInt32(colViewed);
                    mItem.RoleId = reader.GetInt32(colRoleId);
                    mItem.IsThumbJpegDataOutdated = true;

                    if (CheckMissingBehavior(ref deleteList, mItem))
                        continue;

                    MediaBrowserContext.GlobalMediaItemCache.Add(mItem.Id, mItem);
                    mediaItemList.Add(mItem);
                    newItems.Add(mItem.VariationId, mItem);
                }
            }

            if (deleteList != null)
            {
                foreach (MediaItem m in deleteList)
                    RemoveAndRecycle(m);
            }

            if (newItems.Count > 0)
            {
                using (MediaBrowser4.DB.ICommandHelper com = new CommandHelper(
                         new MediaBrowser4.DB.SQLite.SimpleConnection(
                               DBAdministration.GetThumbNailDBPath(dbPath))))
                {
                    using (DbDataReader readerNew = com.ExecuteReader("SELECT THUMB, ISOUTDATED, VARIATIONS_FK FROM THUMBS WHERE VARIATIONS_FK IN ("
                        + String.Join(",", newItems.Keys) + ")", CommandType.Text))
                    {
                        while (readerNew.Read())
                        {
                            MediaItem mItem = newItems[(int)(long)readerNew[2]];
                            mItem.ThumbJpegData = (byte[])readerNew[0];
                            mItem.IsThumbJpegDataOutdated = (bool)readerNew[1];
                        }
                    }
                }
            }
        }

        private static bool CheckMissingBehavior(ref List<MediaItem> deleteList, MediaItem mItem)
        {
            bool result = false;
            if (MediaBrowserContext.MissingFileBehavior != MediaBrowserContext.MissingFileBehaviorType.SHOW)
            {
                if (!mItem.FileObject.Exists || mItem.IsDeleted)
                {
                    if (MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE)
                    {
                        deleteList = deleteList ?? new List<MediaItem>();
                        deleteList.Add(mItem);
                        result = true;
                    }
                    else
                    {
                        result = !mItem.FileObject.Exists;
                    }
                }
            }
            return result;
        }

        string sqlDefaultHead = " MEDIAFILES.ID AS MF_ID, ORIENTATION, FOLDERS_FK, FILENAME, MEDIAFILES.SORTORDER, DESCRIPTION_FK, FRAMES, ROLES_FK, ISBOOKMARKED, PRIORITY, FOLDERNAME, MEDIADATE, "
                                + "CREATIONDATE, EDITDATE, WIDTH, HEIGHT, DURATION, ISDUBLICATE, VIEWED, MEDIAFILES.TYPE, LENGTH, MD5VALUE, CURRENTVARIATION, ISDELETED, INSERTDATE ";

        private List<MediaBrowser4.Objects.MediaItem> LoadMediaItems(string sql, string sortString, int limtRequest)
        {
            return LoadMediaItems(sql, null, sortString, limtRequest, null);
        }

        private List<MediaBrowser4.Objects.MediaItem> LoadMediaItems(MediaItemSqlRequest mediaItemSqlRequest)
        {
            return LoadMediaItems(mediaItemSqlRequest.Sql, mediaItemSqlRequest.ParameterList, mediaItemSqlRequest.SortString, mediaItemSqlRequest.LimitRequest, mediaItemSqlRequest);
        }

        private List<MediaBrowser4.Objects.MediaItem> LoadMediaItems(string sql, List<SQLiteParameter> parameterList, string sortString, int limtRequest, MediaItemRequest mediaItemRequest)
        {
            long requestTicket = DateTime.Now.Ticks;
            List<MediaBrowser4.Objects.MediaItem> mediaItemList = new List<MediaBrowser4.Objects.MediaItem>();

            if (MediaBrowserContext.ConnectedRoles == null || MediaBrowserContext.ConnectedRoles.Count == 0)
                return mediaItemList;

            if (sql.Length > 0)
            {
                try
                {
                    using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
                    {
                        if (parameterList != null)
                        {
                            foreach (SQLiteParameter param in parameterList)
                            {
                                com.SetParameter(param);
                            }
                        }

                        using (DbDataReader reader = com.ExecuteReader("SELECT " + sqlDefaultHead
                                + sql + " AND ROLES_FK IN (" + String.Join(",", MediaBrowserContext.ConnectedRoles.Select(x => x.Id)) + ") GROUP BY MEDIAFILES.ID "
                                + sortString + " LIMIT " + limtRequest))
                        {
                            AddMediaItemsToList(reader, ref mediaItemList);

                            if (mediaItemRequest != null)
                                mediaItemRequest.MediaItemSqlRequest = new MediaItemSqlRequest(sql, parameterList, sortString, limtRequest);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }

                if (requestArchiv.ContainsKey(requestTicket))
                    requestArchiv.Remove(requestTicket);
                requestArchiv.Add(requestTicket, sql);
                MediaBrowserContext.lastRequestTicket = requestTicket;
            }
            return mediaItemList;
        }

        public override void GetThumbsForAllMediaItems(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            StringBuilder sb = new StringBuilder();
            Dictionary<int, MediaItem> mDic = new Dictionary<int, MediaItem>();
            foreach (MediaBrowser4.Objects.MediaItem mItem in mediaItemList)
            {
                if (mItem.ThumbJpegData == null)
                {
                    sb.Append(mItem.VariationId + ",");
                    mDic.Add(mItem.VariationId, mItem);
                }
            }

            if (sb.Length > 0)
            {
                MediaBrowser4.DB.ICommandHelper command;
                System.Data.DataTable table;

                if (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0)
                {
                    command = new MediaBrowser4.DB.SQLite.CommandHelper(
                        new MediaBrowser4.DB.SQLite.SimpleConnection(
                            DBAdministration.GetThumbNailDBPath(dbPath))
                            );

                    table = command.GetDataTable("SELECT VARIATIONS_FK AS VID, THUMB FROM THUMBS WHERE VARIATIONS_FK IN (" + sb.ToString().TrimEnd(',') + ")");
                }
                else
                {
                    command = this.MBCommand;
                    table = command.GetDataTable("SELECT ID AS VID, THUMB FROM VARIATIONS WHERE ID IN (" + sb.ToString().TrimEnd(',') + ")");
                }

                foreach (System.Data.DataRow row in table.Rows)
                {
                    if (mDic.ContainsKey((int)(long)row["VID"])
                        && row["THUMB"] != null && row["THUMB"] != DBNull.Value)
                    {
                        mDic[(int)(long)row["VID"]].ThumbJpegData = (byte[])row["THUMB"];
                    }
                }
                command.Dispose();
            }
        }

        public override void SaveDBProperties(Dictionary<string, string> propertyTable)
        {
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                trans.ExecuteNonQuery("DELETE FROM DBPROPERTIES");

                foreach (KeyValuePair<string, string> kv in propertyTable)
                {
                    trans.SetParameter("@KEY", kv.Key);
                    trans.SetParameter("@VALUE", kv.Value);
                    trans.ExecuteNonQuery("INSERT INTO DBPROPERTIES (KEY, VALUE) VALUES (@KEY, @VALUE)");
                }
            }
        }

        public override void SetBookmark(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            mediaItemList = mediaItemList.ToList();
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISBOOKMARKED=" + (set ? "1" : "0") + " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");

                foreach (MediaBrowser4.Objects.MediaItem mItem in mediaItemList)
                {
                    mItem.IsBookmarked = set;

                    if (set)
                    {
                        if (!MediaBrowserContext.BookmarkedSingleton.Contains(mItem))
                            MediaBrowserContext.BookmarkedSingleton.Add(mItem);
                    }
                    else
                    {
                        if (MediaBrowserContext.BookmarkedSingleton.Contains(mItem))
                            MediaBrowserContext.BookmarkedSingleton.Remove(mItem);
                    }
                }
            }

        }

        public override void SetDeleted(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDELETED=" + (set ? "1" : "0") + " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");

                foreach (MediaBrowser4.Objects.MediaItem mItem in mediaItemList.ToArray())
                {
                    mItem.IsDeleted = set;
                    if (set)
                    {
                        if (!MediaBrowserContext.DeletedSingleton.Contains(mItem))
                            MediaBrowserContext.DeletedSingleton.Add(mItem);
                    }
                    else
                    {
                        if (MediaBrowserContext.DeletedSingleton.Contains(mItem))
                            MediaBrowserContext.DeletedSingleton.Remove(mItem);
                    }

                }
            }
        }

        public override void UpdateDublicate(MediaBrowser4.Objects.MediaItem mediaItem)
        {
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                if (trans.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE " +
                       "MD5VALUE='" + mediaItem.Md5Value + "'") <= 1)
                {
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0 WHERE ID=" + mediaItem.Id);
                    mediaItem.IsMd5Dublicate = false;
                }
            }
        }

        public override void SetTrashFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                trans.SetParameter("@NOWDATE", DateTime.Now, System.Data.DbType.DateTime);
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET DELETEDATE=" + (set ? "@NOWDATE" : "NULL") + ", ISDELETED=" + (set ? "1" : "0") + " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");
            }
        }

        public override void AdjustMediaDate(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                foreach (MediaBrowser4.Objects.MediaItem mItem in mediaItemList)
                {
                    trans.SetParameter("@MEDIADATE", mItem.MediaDate, System.Data.DbType.DateTime);
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET MEDIADATE=@MEDIADATE WHERE ID=" + mItem.Id);
                }
            }
        }

        public override MediaBrowser4.Objects.Category SetCategoryByPath(string catPath, string catSortPath)
        {
            catPath = catPath.Replace('/', '\\').Replace("\\\\", "\\").TrimEnd('\\');
            catSortPath = catSortPath.Replace('/', '\\').Replace("\\\\", "\\").TrimEnd('\\');

            if (catPath == null || catPath.Length == 0 || catSortPath == null || catSortPath.Length == 0
                || catPath.Split('\\').Length != catSortPath.Split('\\').Length)
                return null;

            List<MediaBrowser4.Objects.Category> categoryTree = new List<MediaBrowser4.Objects.Category>();
            MediaBrowser4.Objects.Category parentCategory = null;
            MediaBrowser4.Objects.Category category = null;

            string[] catStrings = catPath.Split('\\');
            string[] catSortStrings = catSortPath.Split('\\');
            DateTime? date = null;

            int year, day, month;

            if (catSortStrings.Length >= 3 && Int32.TryParse(catSortStrings[catSortStrings.Length - 1], out day))
            {
                if (Int32.TryParse(catSortStrings[catSortStrings.Length - 2], out month))
                {
                    if (Int32.TryParse(catSortStrings[catSortStrings.Length - 3], out year))
                    {
                        date = new DateTime(year, month, day);
                    }
                }
            }

            CategoryCollection catCollection = MediaBrowserContext.CategoryTreeSingelton.Children;

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                for (int i = 0; i < catSortStrings.Length; i++)
                {
                    category = null;
                    foreach (Category cat in catCollection)
                    {
                        if (cat.Name.ToLower().Trim() == catStrings[i].ToLower().Trim())
                        {
                            category = cat;
                            catCollection = category.Children;
                            break;
                        }
                    }

                    if (category == null)
                    {
                        category = new MediaBrowser4.Objects.Category();
                        category.IsDate = true;
                        category.name = catStrings[i];
                        category.Description = "";
                        category.Sortname = catSortStrings[i];

                        if (date != null)
                        {
                            if (i == (catSortStrings.Length - 1))
                            {
                                category.Date = date.Value;
                            }
                            else if (i == (catSortStrings.Length - 2))
                            {
                                category.Date = new DateTime(date.Value.Year, date.Value.Month, 1);
                            }
                            else if (i == (catSortStrings.Length - 3))
                            {
                                category.Date = new DateTime(date.Value.Year, 1, 1);
                            }
                        }

                        if (parentCategory != null)
                        {
                            category.Parent = parentCategory;
                            category.IsUnique = parentCategory.IsUnique;
                            category.IsLocation = parentCategory.IsLocation;
                        }

                        SetCategory(category);
                    }

                    parentCategory = category;
                }
            }

            return category;
        }

        public override int CountItemsInFolder(Folder folder)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                return (int)com.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE FOLDERS_FK=" + folder.Id);
            }
        }

        public override void SetFolder(Folder folder)
        {
            if (string.IsNullOrWhiteSpace(folder.Name))
            {
                throw new Exception("Es muss ein Name vergeben werden.");
            }

            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("ATTACH '" + DBAdministration.GetThumbNailDBPath(dbPath) + "' AS thmb;");

                com.SetParameter("@FOLDERNAME", MediaBrowserContext.MapPathRootReverse(FilesAndFolders.CleanPath(folder.FullPath)), DbType.String);
                if (folder.Id == 0)
                {
                    com.ExecuteNonQuery("INSERT OR IGNORE INTO FOLDERS(FOLDERNAME) VALUES(@foldername)");
                    folder.Id = (int)com.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");
                }
                else
                {
                    string oldName = null;
                    if (folder.Id < 0)
                    {
                        oldName = folder.oldName;
                    }
                    else
                    {
                        oldName = MediaBrowserContext.MapPathRoot(com.ExecuteScalar<string>("SELECT FOLDERNAME FROM FOLDERS WHERE ID=" + folder.Id));
                    }
                    oldName = MediaBrowser4.Utilities.FilesAndFolders.CleanPath(oldName);
                    string newName = MediaBrowser4.Utilities.FilesAndFolders.CleanPath(folder.FullPath);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("BEGIN TRANSACTION;");
                    sb.AppendLine("UPDATE FOLDERS SET FOLDERNAME='" + MediaBrowserContext.MapPathRootReverse(newName) + "' WHERE ID=" + folder.Id + ";");

                    List<int> renamedFolders = new List<int>();
                    renamedFolders.Add(folder.Id);
                    using (DbDataReader reader = com.ExecuteReader("SELECT ID, FOLDERNAME FROM FOLDERS WHERE FOLDERNAME LIKE '" + MediaBrowserContext.MapPathRootReverse(oldName) + "\\%'"))
                    {
                        while (reader.Read())
                        {
                            string newFolder = newName + MediaBrowserContext.MapPathRoot(reader["FOLDERNAME"].ToString().Substring(oldName.Length));
                            sb.AppendLine("UPDATE FOLDERS SET FOLDERNAME='" + MediaBrowserContext.MapPathRoot(newFolder) + "' WHERE ID=" + reader["ID"] + ";");
                            renamedFolders.Add(reader.GetInt32(0));
                        }
                    }
                    sb.AppendLine("COMMIT TRANSACTION;");
                    com.ExecuteNonQuery(sb.ToString());

                    foreach (MediaItem mItem in MediaBrowserContext.GlobalMediaItemCache.Values)
                    {
                        if (renamedFolders.Contains(Convert.ToInt32(mItem.FolderId)))
                        {
                            mItem.FileObject = null;
                            mItem.Foldername = MediaBrowserContext.FolderTreeSingelton.GetFolderById(Convert.ToInt32(mItem.FolderId)).FullPath;
                        }
                    }
                }
            }
        }

        public override void SetCategory(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                throw new Exception("Es muss ein Name vergeben werden.");
            }

            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                if (category.Sortname == null || category.Sortname.Trim().Length == 0)
                {
                    category.Sortname = category.Name;
                }

                trans.SetParameter("@name", category.Name.Trim(), System.Data.DbType.String);
                trans.SetParameter("@sort", category.Sortname.Trim(), System.Data.DbType.String);
                trans.SetParameter("@desc", category.Description == null ? (object)DBNull.Value : category.Description.Trim(), System.Data.DbType.String);
                trans.SetParameter("@fullpath", category.FullPath.Trim(), System.Data.DbType.String);
                trans.SetParameter("@date", category.Date, System.Data.DbType.DateTime);

                trans.SetParameter("@LATITUDE", category.Latitude, System.Data.DbType.Double);
                trans.SetParameter("@LONGITUDE", category.Longitude, System.Data.DbType.Double);

                long nameExists = trans.ExecuteScalar<long>("SELECT COUNT (*) FROM CATEGORY WHERE LOWER(NAME)=LOWER(@name) AND PARENT="
                    + (category.ParentId <= 0 ? 0 : category.ParentId) + (category.Id <= 0 ? "" : " AND ID<>" + category.Id));

                if (nameExists > 0)
                {
                    throw new Exception("Der Name ist auf dieser Ebene schon vergeben: " + category.Name);
                }

                if (category.Id <= 0)
                {
                    trans.ExecuteNonQuery("INSERT INTO CATEGORY(SORTORDER, NAME, DESCRIPTION, PARENT, GUID, DATE, ISUNIQUE, LATITUDE, LONGITUDE, ISDATE, ISLOCATION, FULLPATH) VALUES(" +
                        "@sort, @name, @desc,"
                        + (category.ParentId <= 0 ? 0 : category.ParentId)
                        + ", '" + System.Guid.NewGuid().ToString("N") + "', @date, " + (category.IsUnique ? "1" : "0") + ", @LATITUDE, @LONGITUDE, " + (category.IsDate ? "1" : "0") + ", " + (category.IsLocation ? "1" : "0") + ", @fullpath)");

                    category.Id = (int)trans.ExecuteScalar<long>("SELECT last_insert_rowid() FROM DUAL");

                    if (!CategoryCollection.SuppressNotification)
                    {
                        MediaBrowserContext.CategoryTreeSingelton.Add(category);
                        if (category.Parent == null)
                        {
                            MediaBrowserContext.CategoryTreeSingelton.Children.Add(category);
                        }
                        else
                        {
                            category.Parent.Children.Add(category);
                        }
                    }
                }
                else
                {
                    trans.ExecuteNonQuery("UPDATE CATEGORY SET "
                        + "SORTORDER=@sort, NAME=@name, DESCRIPTION=@desc, FULLPATH=@fullpath, ISDATE=" + (category.IsDate ? "1" : "0") + ", ISLOCATION=" + (category.IsLocation ? "1" : "0") + ", LATITUDE=@LATITUDE, LONGITUDE=@LONGITUDE, DATE=@date, PARENT="
                        + (category.ParentId <= 0 ? 0 : category.ParentId) + ", ISUNIQUE=" + (category.IsUnique ? "1" : "0")
                        + " WHERE ID=" + category.Id);

                    if (category.IsUniqueChanged)
                    {
                        category.IsUniqueChanged = false;
                        List<Category> catList = category.AllChildrenRecursive();
                        if (catList.Count > 1)
                        {
                            foreach (Category catUnique in catList)
                            {
                                catUnique.IsUnique = category.IsUnique;
                            }

                            trans.ExecuteNonQuery("UPDATE CATEGORY SET ISUNIQUE=" + (category.IsUnique ? "1" : "0") + " WHERE ID IN (" + ConcatCategoryIds(catList) + ")");
                        }
                    }

                    if (category.IsLocationChanged)
                    {
                        category.IsLocationChanged = false;
                        List<Category> catList = category.AllChildrenRecursive();
                        if (catList.Count > 1)
                        {
                            foreach (Category cat in catList)
                            {
                                cat.IsLocation = category.IsLocation;
                            }

                            trans.ExecuteNonQuery("UPDATE CATEGORY SET ISLOCATION=" + (category.IsLocation ? "1" : "0") + " WHERE ID IN (" + ConcatCategoryIds(catList) + ")");
                        }
                    }

                    if (category.IsDateChanged)
                    {
                        category.IsDateChanged = false;
                        List<Category> catList = category.AllChildrenRecursive();
                        if (catList.Count > 1)
                        {
                            foreach (Category cat in catList)
                            {
                                cat.IsDate = category.IsDate;
                            }

                            trans.ExecuteNonQuery("UPDATE CATEGORY SET ISDATE=" + (category.IsDate ? "1" : "0") + " WHERE ID IN (" + ConcatCategoryIds(catList) + ")");
                        }
                    }
                }
            }
        }

        public override bool RemoveCategory(MediaBrowser4.Objects.Category category)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                if (trans.ExecuteScalar<long>("SELECT COUNT(ID) FROM CATEGORY WHERE PARENT = " + category.Id) == 0)
                {
                    trans.ExecuteScalar<long>("DELETE FROM CATEGORY WHERE ID = " + category.Id);
                    return true;
                }
            }

            return false;
        }

        public override void SetThumbForMediaItem(System.Drawing.Bitmap bmp, MediaBrowser4.Objects.MediaItem mItem)
        {
            if (bmp == null)
                return;

            byte[] newThumb = MediaProcessing.ResizeImage.GetThumbnail(bmp, MediaBrowserContext.ThumbnailSize, MediaBrowserContext.ThumbnailJPEGQuality);

            if (mItem.VariationId == mItem.VariationIdDefault)
                mItem.ThumbJpegData = newThumb;

            MediaBrowser4.DB.ITransaction trans2 = new MediaBrowser4.DB.SQLite.Transaction(
                 new MediaBrowser4.DB.SQLite.SimpleConnection(
                     DBAdministration.GetThumbNailDBPath(dbPath))
                     );

            trans2.SetParameter("@Thumb", newThumb, System.Data.DbType.Binary);

            if (trans2.ExecuteScalar<long>("SELECT COUNT(VARIATIONS_FK) FROM THUMBS WHERE VARIATIONS_FK="
                    + mItem.VariationId) > 0)
            {
                trans2.ExecuteNonQuery("UPDATE THUMBS SET THUMB=@Thumb, ISOUTDATED=0 WHERE VARIATIONS_FK="
                    + mItem.VariationId);
            }
            else
            {
                trans2.ExecuteNonQuery("INSERT INTO THUMBS(THUMB, VARIATIONS_FK) VALUES(@Thumb, " + mItem.VariationId + ")");
            }
            mItem.IsThumbJpegDataOutdated = false;
            trans2.Dispose();

            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET WIDTH = "
                    + mItem.Width + ", HEIGHT = " + mItem.Height + " WHERE ID = " + mItem.Id);
            }

            if (this.OnThumbUpdate != null && mItem.VariationId == mItem.VariationIdDefault)
            {
                this.OnThumbUpdate(this, new MediaItemNewThumbArgs(bmp, mItem));
            }
        }

        public override List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromSearchToken(MediaBrowser4.Objects.SearchToken searchToken, bool storeView, string sortString, int limtRequest)
        {
            return GetMediaItemsFromSearchToken(searchToken, storeView, sortString, limtRequest, null);
        }

        private List<MediaBrowser4.Objects.MediaItem> GetMediaItemsFromSearchToken(MediaBrowser4.Objects.SearchToken searchToken, bool storeView, string sortString, int limtRequest, MediaItemRequest mediaItemRequest)
        {
            SearchTokenSql searchTokenSql = new SearchTokenSql(searchToken);
            List<MediaItem> mediaItemList = new List<MediaItem>();

            if (searchTokenSql.IsValid)
            {
                using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
                {
                    List<System.Data.SQLite.SQLiteParameter> parameterList = null;

                    foreach (System.Data.SQLite.SQLiteParameter param in searchTokenSql.ParameterList)
                    {
                        com.SetParameter(param);

                        if (mediaItemRequest != null)
                        {
                            parameterList = new List<SQLiteParameter>();
                            parameterList.Add(param);
                        }
                    }

                    string sql = "FROM VARIATIONS INNER JOIN MEDIAFILES ON VARIATIONS.ID=MEDIAFILES.CURRENTVARIATION "
                        + "INNER JOIN FOLDERS ON FOLDERS.ID=FOLDERS_FK "
                        + searchTokenSql.JoinSql
                        + "WHERE " + searchTokenSql.WhereSql + " AND HISTORYVERSION=0 ";

                    using (DbDataReader reader = com.ExecuteReader("SELECT" + sqlDefaultHead
                            + sql
                            + "AND ROLES_FK IN (" + String.Join(",", MediaBrowserContext.ConnectedRoles.Select(x => x.Id)) + ") "
                            + "GROUP BY MEDIAFILES.ID " + sortString + " LIMIT " + limtRequest))
                    {
                        AddMediaItemsToList(reader, ref mediaItemList);
                    }

                    if (mediaItemRequest != null)
                        mediaItemRequest.MediaItemSqlRequest = new MediaItemSqlRequest(sql, parameterList, sortString, limtRequest);
                }
            }

            return mediaItemList;
        }

        public override void Rotate90(MediaItem mItem)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.ExecuteNonQuery("UPDATE MEDIAFILES SET ORIENTATION=" + (int)mItem.Orientation + " WHERE ID=" + mItem.Id);
            }
        }

        public override SortedDictionary<DateTime, Tuple<double, double>> GetMeteorologyData(DateTime start, DateTime stop)
        {
            SortedDictionary<DateTime, Tuple<double, double>> data = new SortedDictionary<DateTime, Tuple<double, double>>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                com.SetParameter("@start", start, DbType.DateTime);
                com.SetParameter("@stop", stop, DbType.DateTime);
                using (DbDataReader reader = com.ExecuteReader("SELECT LOGTIME, TEMPERATURE, HUMIDITY FROM DATALOGGER_METEOROLOGY WHERE LOGTIME BETWEEN @start AND @stop"))
                {
                    while (reader.Read())
                    {
                        data.Add(reader.GetDateTime(0), Tuple.Create(reader.GetDouble(1), reader.GetDouble(2)));
                    }
                }
            }

            return data;
        }

        public override void InsertMeteorologyData(SortedDictionary<DateTime, Tuple<double, double>> data)
        {
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                using (DbDataReader reader = com.ExecuteReader("SELECT LOGTIME FROM DATALOGGER_METEOROLOGY"))
                {
                    while (reader.Read())
                    {
                        data.Remove(reader.GetDateTime(0));
                    }
                }
            }

            using (MediaBrowser4.DB.ITransaction trans = MediaBrowserContext.MainDBProvider.MBTransaction)
            {
                foreach (KeyValuePair<DateTime, Tuple<double, double>> value in data)
                {
                    trans.SetParameter("@LOGTIME", value.Key, DbType.DateTime);
                    trans.SetParameter("@TEMPERATURE", value.Value.Item1, DbType.Double);
                    trans.SetParameter("@HUMIDITY", value.Value.Item2, DbType.Double);
                    trans.ExecuteNonQuery("INSERT INTO DATALOGGER_METEOROLOGY (LOGTIME, TEMPERATURE, HUMIDITY) VALUES (@LOGTIME, @TEMPERATURE, @HUMIDITY);");
                }
            }
        }

        public override List<string> GetMetadataKeyList()
        {
            List<string> list = new List<string>();
            using (MediaBrowser4.DB.ICommandHelper com = this.MBCommand)
            {
                System.Data.DataTable table = com.GetDataTable("SELECT DISTINCT NAME FROM METADATANAME ORDER BY NAME");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    list.Add(row["NAME"].ToString());
                }
            }
            return list;
        }

        public override string ToString()
        {
            return "SQLite DB: " + dbPath;
        }

        public override bool EditVariation(List<Variation> variationList)
        {
            using (MediaBrowser4.DB.ITransaction trans = this.MBTransaction)
            {
                foreach (Variation variation in variationList.Where(x => x.Name.Trim().Length > 0 && x.Id > 0))
                {
                    trans.SetParameter("@NAME", variation.Name.Trim());
                    trans.ExecuteNonQuery("UPDATE VARIATIONS SET NAME=@NAME WHERE ID=" + variation.Id);
                }
            }
            return true;
        }
    }
}
