using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Data.SQLite;
using System.Linq;

using MediaBrowser4;
using MediaBrowser4.Objects;
using System.IO;
using System.Data.Common;
using MediaBrowser4.Utilities;

namespace MediaBrowser4.DB.SQLite
{
    internal class SQLiteInsert : MediaBrowser4.DB.InsertItems
    {
        internal event EventHandler<MediaItemCallbackArgs> OnInsert;

        List<MediaItem> newMediaItems = new List<MediaItem>();

        internal SQLiteInsert(string connectionString)
            : base(connectionString)
        {

        }

        int insertCount = 0;
        protected override void InsertInDB()
        {
            int maxValue = insertQueque.Count;
            MediaBrowser4.DB.ITransaction trans = null;
            MediaBrowser4.DB.ITransaction transThumb = null;
            MediaBrowser4.MediaBrowserContext.abortInsert = false;
            DateTime transStart = DateTime.Now;
            MediaItem mItem = null;
            bool folderAdded = false;

            string historyVersion;

            while (insertQueque.Count > 0)
            {
                mItem = insertQueque.Dequeue();
                historyVersion = null;

                if (trans == null)
                {
                    trans = new MediaBrowser4.DB.SQLite.Transaction(
                        new MediaBrowser4.DB.SQLite.SimpleConnection(connectionString));

                    if (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0)
                    {
                        transThumb = new MediaBrowser4.DB.SQLite.Transaction(
                            new MediaBrowser4.DB.SQLite.SimpleConnection(DBAdministration.GetThumbNailDBPath(connectionString)));
                    }

                    transStart = DateTime.Now;
                }

                //Ordner finden
                trans.SetParameter("@foldername", MediaBrowser4.Utilities.FilesAndFolders.CleanPath(mItem.FileObject.DirectoryName), DbType.String);
                object id = trans.ExecuteScalar(
                    "SELECT ID FROM FOLDERS WHERE LOWER(FOLDERNAME)=LOWER(@foldername)");

                //existiert ein Eintrag zu diesem File in dem Ordner mit gleicher Größe wird schon hier übersprungen 
                //ebenso werden zu kleine Dateien übersprungen
                trans.SetParameter("@filename", mItem.Filename, DbType.String);
                if (id != null && id != DBNull.Value && (mItem.FileObject.Length < 100 || 1 == trans.ExecuteScalar<long>("SELECT count(*) FROM MEDIAFILES WHERE "
                      + " FOLDERS_FK=" + id + " AND FILENAME=@filename AND LENGTH=" + mItem.FileObject.Length
                      + " AND HISTORYVERSION=0 LIMIT 1")))
                {
                    continue;
                }

                Utilities.Crypto.GetMD5Value(mItem, MediaBrowserContext.CheckSumMaxLength);

                if (String.IsNullOrWhiteSpace(mItem.Md5Value) || mItem.FileObject.Length < 100)
                    continue;

                //Ordner erstellen
                if (id == null || id == DBNull.Value)
                {
                    trans.ExecuteNonQuery("INSERT INTO FOLDERS(FOLDERNAME) VALUES(@foldername)");
                    mItem.FolderId = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL");
                    folderAdded = true;
                }
                else
                {
                    mItem.FolderId = Convert.ToInt32(id);
                }

                //existiert ein Eintrag zu diesem File für den das File nicht vorhanden ist
                string oldFoderName = null;
                string oldFileName = null;
                long oldMediaId = 0;
                long oldFolderId = 0;

                trans.SetParameter("@filename", mItem.Filename, DbType.String);
                using (DbDataReader reader = trans.ExecuteReader("SELECT FILENAME, MEDIAFILES.ID AS MediaId, FOLDERS.ID AS FolderId, FOLDERNAME FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID=MEDIAFILES.FOLDERS_FK WHERE MD5VALUE='"
                            + mItem.Md5Value + "' AND LENGTH=" + mItem.FileObject.Length + " AND HISTORYVERSION=0 LIMIT 1"))
                {
                    if (reader.Read())
                    {
                        oldFoderName = (string)reader["FOLDERNAME"];
                        oldFileName = (string)reader["FILENAME"];
                        oldFolderId = (long)reader["FolderId"];
                        oldMediaId = (long)reader["MediaId"];
                    }
                }


                if (oldFoderName != null && !File.Exists(Path.Combine(oldFoderName.EndsWith(":") ? oldFoderName + "\\" : oldFoderName, oldFileName)))
                {
                    mItem.Id = (int)oldMediaId;
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET FILENAME=@filename, FOLDERS_FK=" + mItem.FolderId + " WHERE ID=" + oldMediaId);
                    MediaBrowserContext.GlobalMediaItemCache.Remove(mItem.Id);

                    continue;
                }

                //existiert ein Eintrag dieses Item an diesem Ort schon  
                trans.SetParameter("@filename", mItem.Filename, DbType.String);
                object obj = trans.ExecuteScalar("SELECT ID FROM MEDIAFILES WHERE FOLDERS_FK="
                 + mItem.FolderId + " AND LOWER(FILENAME)=LOWER(@filename) AND HISTORYVERSION=0");

                if (obj != null && obj != DBNull.Value)
                {
                    historyVersion = obj.ToString();

                    if (trans.ExecuteScalar<long>("SELECT LENGTH FROM MEDIAFILES WHERE ID=" +
                            historyVersion) == mItem.FileObject.Length)
                    {
                        //File ist identisch
                        continue;
                    }
                    else
                    {
                        //Version eins hochsetzen
                        trans.ExecuteNonQuery("UPDATE MEDIAFILES SET HISTORYVERSION = (HISTORYVERSION + 1) " +
                            "WHERE FOLDERS_FK=" + mItem.FolderId + " AND LOWER(FILENAME)=LOWER(@filename)");
                    }
                }

                //Basisdaten holen
                try
                {
                    mItem.GetThumbnail();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, mItem.FileObject.FullName);
                }

                if (mItem.MediaDate == DateTime.MinValue)
                    Utilities.DateAndTime.GetMediaDate(mItem, MediaBrowserContext.MediaDateDefaultFormatString);

                mItem.CreationDate = mItem.FileObject.CreationTime;
                mItem.LastWriteDate = mItem.FileObject.LastWriteTime;
                trans.SetParameter("@sortname", mItem.Sortorder, DbType.String);
                trans.SetParameter("@duration", mItem.Duration, DbType.Double);
                trans.SetParameter("@CREATIONDATE", mItem.CreationDate, System.Data.DbType.DateTime);
                trans.SetParameter("@MEDIADATE", mItem.MediaDate, System.Data.DbType.DateTime);
                trans.SetParameter("@EDITDATE", mItem.LastWriteDate, System.Data.DbType.DateTime);
                trans.SetParameter("@INSERTDATE", transStart, System.Data.DbType.DateTime);


                mItem.RoleId = MediaBrowser4.MediaBrowserContext.DefaultRoleID;
                long cntDublicates = trans.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE='" + mItem.Md5Value + "'");
                if (cntDublicates == 0)
                {
                    mItem.IsMd5Dublicate = false;
                }
                else
                {
                    mItem.IsMd5Dublicate = true;
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE = 1 WHERE ID IN (SELECT ID FROM MEDIAFILES WHERE MD5VALUE='"
                        + mItem.Md5Value + "')");

                    if (cntDublicates == 1)
                    {
                        int dubId = (int)trans.ExecuteScalar<long>("SELECT ID FROM MEDIAFILES WHERE MD5VALUE='" + mItem.Md5Value + "'"); ;
                        if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(dubId))
                            MediaBrowserContext.GlobalMediaItemCache[dubId].IsMd5Dublicate = true;
                    }
                }

                string oldIdString = null;

                trans.ExecuteNonQuery("INSERT INTO MEDIAFILES(SORTORDER, TYPE, FRAMES, ORIENTATION, DURATION, " +
                   "PRIORITY, WIDTH, HEIGHT, VIEWED, LENGTH, FILENAME, MD5VALUE, FOLDERS_FK, " +
                   "ISDELETED, CREATIONDATE, MEDIADATE, EDITDATE, INSERTDATE, ROLES_FK, HISTORYVERSION, ISDUBLICATE "
                   + (oldIdString == null ? "" : ", ID ") + ") VALUES (@sortname," +
                     "'" + mItem.DBType + "'," + mItem.Frames + ","
                     + (int)mItem.Orientation + ",@duration, 5,"
                     + mItem.Width
                     + "," + mItem.Height
                     + ",0," + mItem.FileObject.Length + ",@filename,'" + mItem.Md5Value
                     + "'," + mItem.FolderId + ", 0, @CREATIONDATE, @MEDIADATE, @EDITDATE, @INSERTDATE"
                     + ", " + mItem.RoleId + ", 0, " + (mItem.IsMd5Dublicate ? 1 : 0)
                     + (oldIdString == null ? "" : "," + oldIdString)
                     + ")");

                mItem.Id = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL");

                this.newMediaItems.Add(mItem);

                Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mItem.FolderId);

                if (folder != null)
                {
                    folder.ItemCount++;
                    while (folder != null)
                    {
                        folder.UpdateItemInfo();
                        folder = folder.Parent;
                    }
                }

                //Variation
                trans.SetParameter("@VARIATIONNAME", DBNull.Value, System.Data.DbType.String);

                if (MediaBrowser4.MediaBrowserContext.ThumbnailSize == 0)
                {
                    if (mItem.ThumbJpegData == null || mItem.ThumbJpegData.Length == 0)
                        throw new Exception("Could not receive thumbnail!");

                    trans.ExecuteNonQuery("INSERT INTO VARIATIONS(MEDIAFILES_FK, THUMB, NAME) VALUES (" +
                          mItem.Id + ",@THUMBNAIL, @VARIATIONNAME)");
                }
                else
                {
                    trans.ExecuteNonQuery("INSERT INTO VARIATIONS(MEDIAFILES_FK, NAME) VALUES (" +
                          mItem.Id + ", @VARIATIONNAME)");
                }

                mItem.VariationId = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL");
                mItem.VariationIdDefault = mItem.VariationId;

                if (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0
                    && mItem.ThumbJpegData != null && mItem.ThumbJpegData.Length > 0)
                {
                    transThumb.SetParameter("@THUMBNAIL", mItem.ThumbJpegData, System.Data.DbType.Binary);
                    transThumb.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK=" +
                        mItem.VariationId);
                    transThumb.ExecuteNonQuery("INSERT INTO THUMBS(VARIATIONS_FK, THUMB) VALUES (" +
                         mItem.VariationId + ", @THUMBNAIL)");
                }

                trans.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=" + mItem.VariationId + " WHERE ID=" + mItem.Id);

                //copy old description, move category etc
                if (historyVersion != null)
                {
                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET DESCRIPTION_FK=(SELECT DESCRIPTION_FK FROM MEDIAFILES WHERE ID=" +
                        historyVersion + ") WHERE ID=" + mItem.Id);

                    string currentLayerOld = trans.ExecuteScalar("SELECT CURRENTVARIATION FROM MEDIAFILES WHERE ID=" + historyVersion).ToString();

                    trans.ExecuteNonQuery("UPDATE CATEGORIZE SET VARIATIONS_FK=" + mItem.VariationId +
                        " WHERE VARIATIONS_FK=" + currentLayerOld);

                    trans.ExecuteNonQuery("UPDATE EXTRADATAS SET MEDIAFILES_FK=" + mItem.Id +
                        " WHERE MEDIAFILES_FK=" + historyVersion);
                }

                //Metatdata
                if (mItem.MetaData != null)
                {
                    foreach (MetaData mData in mItem.MetaData)
                    {
                        if (mData.Value.Length > 0 && mData.Name.Length > 0 && !mData.Name.StartsWith("Camera State")
                                            && mData.GroupName.Length > 0 && mData.Type.Length > 0 && !mData.Null)
                        {
                            trans.SetParameter("@metaname", mData.Name, DbType.String);
                            trans.SetParameter("@metavalue", mData.Value, DbType.String);
                            trans.SetParameter("@type", mData.Type, DbType.String);
                            trans.SetParameter("@groupName", mData.GroupName, DbType.String);

                            MetaData metaData = SQLiteProvider.MetaDataNames.FirstOrDefault(x =>
                                       x.Name == mData.Name
                                    && x.Type == mData.Type
                                    && x.GroupName == mData.GroupName);

                            if (metaData.Id <= 0)
                            {
                                trans.ExecuteNonQuery("INSERT INTO METADATANAME(NAME, TYPE, GROUPNAME) VALUES(@metaname, @type, @groupName)");

                                metaData = new MetaData()
                                {
                                    Id = (int)(long)trans.ExecuteScalar("SELECT last_insert_rowid() FROM DUAL"),
                                    Name = mData.Name,
                                    Type = mData.Type,
                                    GroupName = mData.GroupName,
                                    IsVisible = true
                                };

                                SQLiteProvider.MetaDataNames.Add(metaData);
                            }

                            trans.SetParameter("@metadataId", metaData.Id, DbType.Int32);
                            trans.ExecuteNonQuery("INSERT INTO METADATA(MEDIAFILES_FK, METANAME_FK, VALUE) VALUES("
                                + mItem.Id + ", @metadataId, @metavalue)");

                            if ((mData.Name == "GPS Latitude" || mData.Name == "GPS Longitude") && !String.IsNullOrWhiteSpace(mData.Value))
                            {
                                double degree = Double.Parse(mData.Value.Split('"')[0]);
                                double minute = Double.Parse(mData.Value.Split('"')[1].Split('\'')[0]);
                                double second = Double.Parse(mData.Value.Split('"')[1].Split('\'')[1]);

                                double gps = degree + (minute * (1.0 / 60.0)) + ((second / 60.0) * (1.0 / 60.0));

                                trans.SetParameter("@gps", gps, DbType.Double);

                                if (mData.Name == "GPS Longitude")
                                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET LONGITUDE=@gps WHERE ID=" + mItem.Id);

                                if (mData.Name == "GPS Latitude")
                                    trans.ExecuteNonQuery("UPDATE MEDIAFILES SET LATITUDE=@gps WHERE ID=" + mItem.Id);
                            }
                        }
                    }
                }

                insertCount++;

                if (this.OnInsert != null)
                {
                    if (maxValue < insertQueque.Count)
                        maxValue = insertQueque.Count + 1;

                    if (insertCount > maxValue)
                        maxValue = insertCount;

                    this.OnInsert(this, new MediaItemCallbackArgs(insertCount, maxValue, mItem, false, folderAdded));
                }


                //Commit
                if ((DateTime.Now - transStart).Seconds > MediaBrowserContext.CommitInsertSeconds)
                {
                    trans = CommitTransaction(trans, transThumb);
                }

                if (MediaBrowser4.MediaBrowserContext.abortInsert)
                {
                    insertQueque.Clear();
                }
            }

            if (trans != null)
            {
                trans = CommitTransaction(trans, transThumb);
            }

            if (this.OnInsert != null)
            {
                this.OnInsert(this, new MediaItemCallbackArgs(insertCount, insertCount, mItem, true, folderAdded));
            }
            insertCount = 0;
        }

        private MediaBrowser4.DB.ITransaction CommitTransaction(ITransaction trans, ITransaction transThumb)
        {
            trans.Dispose();
            trans = null;

            this.newMediaItems.Clear();

            if (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0)
                transThumb.Dispose();
            return trans;
        }
    }
}
