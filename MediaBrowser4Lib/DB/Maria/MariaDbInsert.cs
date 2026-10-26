using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;

namespace MediaBrowser4.DB.Maria
{
    internal class MariaDbInsert : InsertItems
    {
        internal event EventHandler<MediaItemCallbackArgs> OnInsert;

        private readonly MariaDbConnectionOptions options;
        private readonly List<MediaItem> newMediaItems = new List<MediaItem>();
        private int insertCount;

        internal MariaDbInsert(MariaDbConnectionOptions options)
            : base(options.BuildConnectionString())
        {
            this.options = options;
        }

        protected override void InsertInDB()
        {
            int maxValue = insertQueque.Count;
            ITransaction transaction = null;
            MediaBrowserContext.abortInsert = false;
            DateTime transactionStart = DateTime.Now;
            MediaItem mediaItem = null;
            bool folderAdded = false;

            while (insertQueque.Count > 0)
            {
                mediaItem = insertQueque.Dequeue();
                string historyVersion = null;

                if (transaction == null)
                {
                    transaction = new Transaction(new SimpleConnection(this.options.BuildConnectionString()));
                    transactionStart = DateTime.Now;
                }

                transaction.SetParameter("@foldername",
                    FilesAndFolders.CleanPath(mediaItem.FileObject.DirectoryName), DbType.String);

                object folderIdValue = transaction.ExecuteScalar(
                    "SELECT ID FROM FOLDERS WHERE LOWER(FOLDERNAME)=LOWER(@foldername)");

                transaction.SetParameter("@filename", mediaItem.Filename, DbType.String);

                if (folderIdValue != null && folderIdValue != DBNull.Value
                    && (mediaItem.FileObject.Length < 100
                        || 1 == transaction.ExecuteScalar<long>(
                            "SELECT COUNT(*) FROM MEDIAFILES WHERE FOLDERS_FK=" + folderIdValue +
                            " AND FILENAME=@filename AND LENGTH=" + mediaItem.FileObject.Length +
                            " AND HISTORYVERSION=0")))
                {
                    continue;
                }

                Crypto.GetMD5Value(mediaItem, MediaBrowserContext.CheckSumMaxLength);

                if (String.IsNullOrWhiteSpace(mediaItem.Md5Value) || mediaItem.FileObject.Length < 100)
                {
                    continue;
                }

                if (folderIdValue == null || folderIdValue == DBNull.Value)
                {
                    transaction.ExecuteNonQuery("INSERT INTO FOLDERS (FOLDERNAME) VALUES (@foldername)");
                    mediaItem.FolderId = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                    folderAdded = true;
                }
                else
                {
                    mediaItem.FolderId = Convert.ToInt32(folderIdValue);
                }

                string oldFolderName = null;
                string oldFileName = null;
                long oldMediaId = 0;
                long oldFolderId = 0;

                transaction.SetParameter("@md5", mediaItem.Md5Value, DbType.String);
                transaction.SetParameter("@length", mediaItem.FileObject.Length, DbType.Int64);

                using (DbDataReader reader = transaction.ExecuteReader(
                    "SELECT FILENAME, MEDIAFILES.ID AS MediaId, FOLDERS.ID AS FolderId, FOLDERNAME " +
                    "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID=MEDIAFILES.FOLDERS_FK " +
                    "WHERE MD5VALUE=@md5 AND LENGTH=@length AND HISTORYVERSION=0 LIMIT 1"))
                {
                    if (reader.Read())
                    {
                        oldFolderName = reader["FOLDERNAME"].ToString();
                        oldFileName = reader["FILENAME"].ToString();
                        oldFolderId = Convert.ToInt64(reader["FolderId"]);
                        oldMediaId = Convert.ToInt64(reader["MediaId"]);
                    }
                }

                if (oldFolderName != null
                    && !File.Exists(Path.Combine(
                        oldFolderName.EndsWith(":") ? oldFolderName + "\\" : oldFolderName, oldFileName)))
                {
                    mediaItem.Id = (int)oldMediaId;
                    transaction.SetParameter("@filename", mediaItem.Filename, DbType.String);
                    transaction.SetParameter("@folderId", mediaItem.FolderId, DbType.Int32);
                    transaction.ExecuteNonQuery(
                        "UPDATE MEDIAFILES SET FILENAME=@filename, FOLDERS_FK=@folderId WHERE ID=" + oldMediaId);
                    MediaBrowserContext.GlobalMediaItemCache.Remove(mediaItem.Id);
                    continue;
                }

                transaction.SetParameter("@filename", mediaItem.Filename, DbType.String);
                transaction.SetParameter("@folderId", mediaItem.FolderId, DbType.Int32);

                object existingId = transaction.ExecuteScalar(
                    "SELECT ID FROM MEDIAFILES WHERE FOLDERS_FK=@folderId AND LOWER(FILENAME)=LOWER(@filename) " +
                    "AND HISTORYVERSION=0");

                if (existingId != null && existingId != DBNull.Value)
                {
                    historyVersion = existingId.ToString();

                    if (transaction.ExecuteScalar<long>("SELECT LENGTH FROM MEDIAFILES WHERE ID=" +
                                                        historyVersion) == mediaItem.FileObject.Length)
                    {
                        continue;
                    }

                    transaction.ExecuteNonQuery(
                        "UPDATE MEDIAFILES SET HISTORYVERSION=(HISTORYVERSION + 1) " +
                        "WHERE FOLDERS_FK=@folderId AND LOWER(FILENAME)=LOWER(@filename)");
                }

                try
                {
                    mediaItem.GetThumbnail();
                }
                catch (Exception exception)
                {
                    Log.Exception(exception, mediaItem.FileObject.FullName);
                }

                if (mediaItem.MediaDate == DateTime.MinValue)
                {
                    DateAndTime.GetMediaDate(mediaItem, MediaBrowserContext.MediaDateDefaultFormatString);
                }

                mediaItem.CreationDate = mediaItem.FileObject.CreationTime;
                mediaItem.LastWriteDate = mediaItem.FileObject.LastWriteTime;

                transaction.SetParameter("@sortname", mediaItem.Sortorder, DbType.String);
                transaction.SetParameter("@duration", mediaItem.Duration, DbType.Double);
                transaction.SetParameter("@creationdate", mediaItem.CreationDate, DbType.DateTime);
                transaction.SetParameter("@mediadate", mediaItem.MediaDate, DbType.DateTime);
                transaction.SetParameter("@editdate", mediaItem.LastWriteDate, DbType.DateTime);
                transaction.SetParameter("@insertdate", transactionStart, DbType.DateTime);

                mediaItem.RoleId = MediaBrowserContext.DefaultRoleID;

                long duplicateCount = transaction.ExecuteScalar<long>(
                    "SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5");

                if (duplicateCount == 0)
                {
                    mediaItem.IsMd5Dublicate = false;
                }
                else
                {
                    mediaItem.IsMd5Dublicate = true;
                    transaction.ExecuteNonQuery(
                        "UPDATE MEDIAFILES SET ISDUBLICATE=1 WHERE ID IN " +
                        "(SELECT ID FROM (SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5) AS duplicateIds)");

                    if (duplicateCount == 1)
                    {
                        int duplicateId = transaction.ExecuteScalar<int>(
                            "SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5");

                        if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(duplicateId))
                        {
                            MediaBrowserContext.GlobalMediaItemCache[duplicateId].IsMd5Dublicate = true;
                        }
                    }
                }

                transaction.SetParameter("@sortorder", mediaItem.Sortorder, DbType.String);
                transaction.SetParameter("@type", mediaItem.DBType, DbType.String);
                transaction.SetParameter("@frames", mediaItem.Frames, DbType.Int32);
                transaction.SetParameter("@orientation", (int)mediaItem.Orientation, DbType.Int32);
                transaction.SetParameter("@width", mediaItem.Width, DbType.Int32);
                transaction.SetParameter("@height", mediaItem.Height, DbType.Int32);
                transaction.SetParameter("@viewed", 0, DbType.Int32);
                transaction.SetParameter("@length", mediaItem.FileObject.Length, DbType.Int64);
                transaction.SetParameter("@filename", mediaItem.Filename, DbType.String);
                transaction.SetParameter("@md5", mediaItem.Md5Value, DbType.String);
                transaction.SetParameter("@folderId", mediaItem.FolderId, DbType.Int32);
                transaction.SetParameter("@roleId", mediaItem.RoleId, DbType.Int32);
                transaction.SetParameter("@isDuplicate", mediaItem.IsMd5Dublicate, DbType.Boolean);

                transaction.ExecuteNonQuery(
                    "INSERT INTO MEDIAFILES (SORTORDER, `TYPE`, FRAMES, ORIENTATION, DURATION, PRIORITY, WIDTH, " +
                    "HEIGHT, VIEWED, LENGTH, FILENAME, MD5VALUE, FOLDERS_FK, ISDELETED, CREATIONDATE, MEDIADATE, " +
                    "EDITDATE, INSERTDATE, ROLES_FK, HISTORYVERSION, ISDUBLICATE) VALUES (@sortorder, @type, " +
                    "@frames, @orientation, @duration, 5, @width, @height, @viewed, @length, @filename, @md5, " +
                    "@folderId, 0, @creationdate, @mediadate, @editdate, @insertdate, @roleId, 0, @isDuplicate)");

                mediaItem.Id = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                newMediaItems.Add(mediaItem);

                Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mediaItem.FolderId);

                if (folder != null)
                {
                    folder.ItemCount++;

                    while (folder != null)
                    {
                        folder.UpdateItemInfo();
                        folder = folder.Parent;
                    }
                }

                transaction.SetParameter("@variationName", DBNull.Value, DbType.String);
                transaction.ExecuteNonQuery(
                    "INSERT INTO VARIATIONS (MEDIAFILES_FK, NAME) VALUES (" + mediaItem.Id + ", @variationName)");

                mediaItem.VariationId = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                mediaItem.VariationIdDefault = mediaItem.VariationId;

                if (mediaItem.ThumbJpegData != null && mediaItem.ThumbJpegData.Length > 0)
                {
                    transaction.SetParameter("@thumbnail", mediaItem.ThumbJpegData, DbType.Binary);
                    transaction.ExecuteNonQuery(
                        "INSERT INTO THUMBS (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, THUMB) VALUES (" +
                        mediaItem.VariationId + ", 0, 0, @thumbnail)");
                }

                transaction.SetParameter("@variationId", mediaItem.VariationId, DbType.Int64);
                transaction.SetParameter("@mediaId", mediaItem.Id, DbType.Int32);
                transaction.ExecuteNonQuery(
                    "UPDATE MEDIAFILES SET CURRENTVARIATION=@variationId WHERE ID=@mediaId");

                if (historyVersion != null)
                {
                    transaction.ExecuteNonQuery(
                        "UPDATE MEDIAFILES SET DESCRIPTION_FK=(SELECT DESCRIPTION_FK FROM MEDIAFILES WHERE ID=" +
                        historyVersion + ") WHERE ID=" + mediaItem.Id);

                    string oldVariationId = transaction.ExecuteScalar(
                        "SELECT CURRENTVARIATION FROM MEDIAFILES WHERE ID=" + historyVersion).ToString();

                    transaction.ExecuteNonQuery(
                        "UPDATE CATEGORIZE SET VARIATIONS_FK=" + mediaItem.VariationId +
                        " WHERE VARIATIONS_FK=" + oldVariationId);

                    transaction.ExecuteNonQuery(
                        "UPDATE EXTRADATAS SET MEDIAFILES_FK=" + mediaItem.Id +
                        " WHERE MEDIAFILES_FK=" + historyVersion);
                }

                if (mediaItem.MetaData != null)
                {
                    foreach (MetaData metaData in mediaItem.MetaData)
                    {
                        if (String.IsNullOrEmpty(metaData.Value) || String.IsNullOrEmpty(metaData.Name)
                            || metaData.Name.StartsWith("Camera State") || String.IsNullOrEmpty(metaData.GroupName)
                            || String.IsNullOrEmpty(metaData.Type) || metaData.Null)
                        {
                            continue;
                        }

                        int metadataId = ResolveMetaDataId(transaction, metaData);

                        transaction.SetParameter("@mediaId", mediaItem.Id, DbType.Int32);
                        transaction.SetParameter("@metadataId", metadataId, DbType.Int32);
                        transaction.SetParameter("@metadataValue", metaData.Value, DbType.String);
                        transaction.ExecuteNonQuery(
                            "INSERT INTO METADATA (MEDIAFILES_FK, METANAME_FK, `VALUE`) " +
                            "VALUES (@mediaId, @metadataId, @metadataValue)");

                        if ((metaData.Name == "GPS Latitude" || metaData.Name == "GPS Longitude")
                            && !String.IsNullOrWhiteSpace(metaData.Value))
                        {
                            double gps = ParseGps(metaData.Value);
                            transaction.SetParameter("@gps", gps, DbType.Double);

                            if (metaData.Name == "GPS Longitude")
                            {
                                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET LONGITUDE=@gps WHERE ID=" +
                                                            mediaItem.Id);
                            }

                            if (metaData.Name == "GPS Latitude")
                            {
                                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET LATITUDE=@gps WHERE ID=" +
                                                            mediaItem.Id);
                            }
                        }
                    }
                }

                insertCount++;

                if (this.OnInsert != null)
                {
                    if (maxValue < insertQueque.Count)
                    {
                        maxValue = insertQueque.Count + 1;
                    }

                    if (insertCount > maxValue)
                    {
                        maxValue = insertCount;
                    }

                    this.OnInsert(this, new MediaItemCallbackArgs(insertCount, maxValue, mediaItem, false, folderAdded));
                }

                if ((DateTime.Now - transactionStart).Seconds > MediaBrowserContext.CommitInsertSeconds)
                {
                    transaction.Dispose();
                    transaction = null;
                }

                if (MediaBrowserContext.abortInsert)
                {
                    insertQueque.Clear();
                }
            }

            if (transaction != null)
            {
                transaction.Dispose();
            }

            if (this.OnInsert != null)
            {
                this.OnInsert(this, new MediaItemCallbackArgs(insertCount, insertCount, mediaItem, true, folderAdded));
            }

            insertCount = 0;
        }

        private static int ResolveMetaDataId(ITransaction transaction, MetaData metaData)
        {
            transaction.SetParameter("@name", metaData.Name, DbType.String);
            transaction.SetParameter("@type", metaData.Type, DbType.String);
            transaction.SetParameter("@group", metaData.GroupName, DbType.String);

            object id = transaction.ExecuteScalar(
                "SELECT ID FROM METADATANAME WHERE NAME=@name AND `TYPE`=@type AND GROUPNAME=@group");

            if (id != null && id != DBNull.Value)
            {
                return Convert.ToInt32(id);
            }

            transaction.ExecuteNonQuery(
                "INSERT INTO METADATANAME (NAME, `TYPE`, GROUPNAME, ISVISIBLE) VALUES (@name, @type, @group, 1)");

            return transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
        }

        private static double ParseGps(string value)
        {
            string[] parts = value.Split('"');

            double degree = Double.Parse(parts[0], CultureInfo.InvariantCulture);
            double minute = Double.Parse(parts[1].Split('\'')[0], CultureInfo.InvariantCulture);
            double second = Double.Parse(parts[1].Split('\'')[1], CultureInfo.InvariantCulture);

            return degree + minute * (1.0 / 60.0) + second / 60.0 / 60.0;
        }
    }
}
