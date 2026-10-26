using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Device.Location;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using MediaBrowser4;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;

namespace MediaBrowser4.DB.Maria
{
    public class MariaDbProvider : DBProvider
    {
        private readonly MariaDbConnectionOptions options;
        private readonly Dictionary<long, string> requestArchiv = new Dictionary<long, string>();
        private readonly MariaDbInsert insert;

        private const string SqlDefaultHead =
            " MEDIAFILES.ID AS MF_ID, ORIENTATION, FOLDERS_FK, FILENAME, MEDIAFILES.SORTORDER, DESCRIPTION_FK, " +
            "FRAMES, ROLES_FK, ISBOOKMARKED, PRIORITY, FOLDERNAME, MEDIADATE, CREATIONDATE, EDITDATE, WIDTH, HEIGHT, " +
            "DURATION, ISDUBLICATE, VIEWED, MEDIAFILES.TYPE, LENGTH, MD5VALUE, CURRENTVARIATION, ISDELETED, INSERTDATE, " +
            "MEDIAFILES.LONGITUDE, MEDIAFILES.LATITUDE ";

        public MariaDbProvider(MariaDbConnectionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            this.options = options;
            this.DBName = options.Database;
            this.User = options.User;
            this.Host = options.Host;
            this.insert = new MariaDbInsert(options);
            this.insert.OnInsert += (sender, args) =>
            {
                if (this.OnInsert != null)
                {
                    this.OnInsert(this, args);
                }
            };
        }

        public MariaDbConnectionOptions Options
        {
            get { return this.options; }
        }

        public override event EventHandler<MediaItemCallbackArgs> OnInsert;
        public override event EventHandler<MediaItemNewThumbArgs> OnThumbUpdate;

        public string LastValidationError { get; private set; }

        public override string Guid { get; set; }
        public override string DBName { get; set; }
        public override string User { get; set; }
        public override string Host { get; set; }

        public override ICommandHelper MBCommand
        {
            get { return new CommandHelper(new SimpleConnection(this.options.BuildConnectionString())); }
        }

        public override ITransaction MBTransaction
        {
            get { return new Transaction(new SimpleConnection(this.options.BuildConnectionString())); }
        }

        public override bool ValidateDB()
        {
            try
            {
                new MariaDbAdministration(this.options).ValidateSchema();

                using (ICommandHelper command = this.MBCommand)
                {
                    this.GetBasics(command);
                }

                this.LastValidationError = null;
                return true;
            }
            catch (Exception ex)
            {
                this.LastValidationError = ex.Message;
                return false;
            }
        }

        public override void CreateDB()
        {
            this.CreateDB(null);
        }

        public void CreateDB(Action<string> progress)
        {
            new MariaDbAdministration(this.options, progress).CreateDatabaseAndSchema();
        }

        public override int ExecuteNonQuery(string sql)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                return command.ExecuteNonQuery(sql);
            }
        }

        private void GetBasics(ICommandHelper command)
        {
            object value = command.ExecuteScalar("SELECT `VALUE` FROM DBPROPERTIES WHERE `KEY` = 'GUID'");

            if (value != null && value != DBNull.Value)
            {
                this.Guid = value.ToString();
            }

        }

        // Generated provider surface placeholders. Replace each body in phases 2-6.

        public override int DeleteGpsFile(DateTime fileTime)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@fileTime", fileTime, DbType.DateTime);
                return command.ExecuteNonQuery("DELETE FROM DATALOGGER_GPS WHERE FILETIME = @fileTime");
            }
        }

        public override void InsertGpsPoints(List<GeoPoint> gpsList)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (GeoPoint geoPoint in gpsList)
                {
                    transaction.SetParameter("@FileDate", geoPoint.GpsFile.FileTime, DbType.DateTime);
                    transaction.SetParameter("@Filesize", geoPoint.GpsFile.Filesize, DbType.UInt64);
                    transaction.SetParameter("@Date", geoPoint.LocalTime, DbType.DateTime);
                    transaction.SetParameter("@UtcDate", geoPoint.UtcTime, DbType.DateTime);
                    transaction.SetParameter("@Lon", geoPoint.Longitude, DbType.Decimal);
                    transaction.SetParameter("@Lat", geoPoint.Latitude, DbType.Decimal);
                    transaction.SetParameter("@Alt", geoPoint.Altitude, DbType.Decimal);

                    transaction.ExecuteNonQuery(
                        "INSERT INTO DATALOGGER_GPS (FILETIME, FILESIZE, UTCTIME, `LOCALTIME`, LONGITUDE, LATITUDE, ALTITUDE) " +
                        "VALUES (@FileDate, @Filesize, @UtcDate, @Date, @Lon, @Lat, @Alt)");
                }
            }
        }

        public override GeoPoint GetGpsNearest(DateTime date)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@from", date.AddDays(-1), DbType.DateTime);
                command.SetParameter("@to", date, DbType.DateTime);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT FILETIME, FILESIZE, `LOCALTIME`, LONGITUDE, LATITUDE, ALTITUDE FROM DATALOGGER_GPS " +
                    "WHERE `LOCALTIME` BETWEEN @from AND @to ORDER BY `LOCALTIME` DESC LIMIT 1"))
                {
                    if (reader.Read())
                    {
                        return new GeoPoint
                        {
                            LocalTime = reader.GetDateTime(2),
                            Longitude = reader.GetDouble(3),
                            Latitude = reader.GetDouble(4),
                            Altitude = reader.GetDouble(5),
                            GpsFile = new GpsFile { FileTime = reader.GetDateTime(0), Filesize = reader.GetInt64(1) }
                        };
                    }
                }
            }

            return null;
        }

        public override List<GpsFile> GetGpsFileList()
        {
            List<GpsFile> fileList = new List<GpsFile>();

            using (ICommandHelper command = this.MBCommand)
            using (DbDataReader reader = command.ExecuteReader("SELECT DISTINCT FILETIME, FILESIZE FROM DATALOGGER_GPS"))
            {
                while (reader.Read())
                {
                    fileList.Add(new GpsFile
                    {
                        FileTime = reader.GetDateTime(0),
                        Filesize = reader.GetInt64(1)
                    });
                }
            }

            return fileList;
        }

        public override List<GeoPoint> GetGpsList(DateTime from, DateTime to)
        {
            List<GeoPoint> gpsList = new List<GeoPoint>();

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@from", from, DbType.DateTime);
                command.SetParameter("@to", to, DbType.DateTime);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT FILETIME, FILESIZE, `LOCALTIME`, LONGITUDE, LATITUDE, ALTITUDE, UTCTIME FROM DATALOGGER_GPS " +
                    "WHERE `LOCALTIME` BETWEEN @from AND @to ORDER BY `LOCALTIME`"))
                {
                    GeoPoint pointLast = null;

                    while (reader.Read())
                    {
                        GeoPoint point = new GeoPoint
                        {
                            LocalTime = reader.GetDateTime(2),
                            Longitude = reader.GetDouble(3),
                            Latitude = reader.GetDouble(4),
                            Altitude = reader.GetDouble(5),
                            UtcTime = reader.GetDateTime(6),
                            GpsFile = new GpsFile { FileTime = reader.GetDateTime(0), Filesize = reader.GetInt64(1) },
                            Predecessor = pointLast
                        };

                        if (point.Altitude == 0 && point.Predecessor != null)
                        {
                            point.Altitude = point.Predecessor.Altitude;
                        }

                        gpsList.Add(point);
                        pointLast = point;
                    }
                }
            }

            return gpsList;
        }

        public override List<Category> GetCategories(int variationId)
        {
            List<Category> categoryList = new List<Category>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT ID, NAME, PARENT, DESCRIPTION, SORTORDER, GUID, `DATE`, ISUNIQUE, ISDATE, ISLOCATION, " +
                    "LATITUDE, LONGITUDE, FULLPATH, items.ItemCount FROM CATEGORY LEFT JOIN " +
                    "(SELECT COUNT(c.VARIATIONS_FK) AS ItemCount, CATEGORY_FK FROM CATEGORIZE c " +
                    "INNER JOIN MEDIAFILES ON MEDIAFILES.CURRENTVARIATION = c.VARIATIONS_FK GROUP BY c.CATEGORY_FK) items " +
                    "ON items.CATEGORY_FK = ID INNER JOIN CATEGORIZE ON CATEGORIZE.CATEGORY_FK = CATEGORY.ID " +
                    "WHERE CATEGORIZE.VARIATIONS_FK = " + variationId);

                foreach (DataRow row in table.Rows)
                {
                    categoryList.Add(GetCategoryFromDataRow(row));
                }
            }

            return categoryList;
        }

        private static Category GetCategoryFromDataRow(DataRow row)
        {
            Category category = new Category
            {
                Id = Convert.ToInt32(row["ID"]),
                name = row["NAME"].ToString(),
                Description = row["DESCRIPTION"].ToString(),
                Sortname = row["SORTORDER"].ToString(),
                Guid = row["GUID"].ToString(),
                FullPath = row["FULLPATH"].ToString(),
                ParentId = row["PARENT"] == DBNull.Value ? 0 : Convert.ToInt32(row["PARENT"]),
                IsUnique = Convert.ToBoolean(row["ISUNIQUE"]),
                IsDate = Convert.ToBoolean(row["ISDATE"]),
                IsLocation = Convert.ToBoolean(row["ISLOCATION"]),
                ItemCount = row["ItemCount"] == DBNull.Value ? 0 : Convert.ToInt32(row["ItemCount"]),
                Date = row["DATE"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(row["DATE"])
            };

            if (row["LATITUDE"] != DBNull.Value)
            {
                category.Latitude = Convert.ToDouble(row["LATITUDE"]);
            }

            if (row["LONGITUDE"] != DBNull.Value)
            {
                category.Longitude = Convert.ToDouble(row["LONGITUDE"]);
            }

            return category;
        }

        public override List<MediaItemRequest> GetUserDefinedRequests()
        {
            List<MediaItemRequest> requestList = new List<MediaItemRequest>();

            using (ICommandHelper command = this.MBCommand)
            using (DbDataReader reader = command.ExecuteReader("SELECT ID, REQUEST FROM USERDEFINEDREQUESTS"))
            {
                while (reader.Read())
                {
                    long id = reader.GetInt64(0);
                    byte[] binaryData = (byte[])reader[1];

                    using (MemoryStream stream = new MemoryStream(binaryData))
                    {
                        MediaItemRequest request = (MediaItemRequest)new BinaryFormatter().Deserialize(stream);

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
            if (request == null || request.UserDefinedId == 0)
            {
                return;
            }

            using (ICommandHelper command = this.MBCommand)
            {
                command.ExecuteNonQuery("DELETE FROM USERDEFINEDREQUESTS WHERE ID=" + request.UserDefinedId);
            }
        }

        public override bool SaveUserDefinedRequest(MediaItemRequest request)
        {
            if (request is MediaItemObservableCollectionRequest || request is MediaItemVirtualRequest)
            {
                return false;
            }

            byte[] binaryData;

            using (MemoryStream stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, request);
                binaryData = stream.ToArray();
            }

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@binaryData", binaryData, DbType.Binary);

                if (request.UserDefinedId == 0
                    || command.ExecuteScalar<long>("SELECT COUNT(*) FROM USERDEFINEDREQUESTS WHERE ID=" + request.UserDefinedId) == 0)
                {
                    command.ExecuteNonQuery("INSERT INTO USERDEFINEDREQUESTS (REQUEST) VALUES(@binaryData)");
                    request.UserDefinedId = (int)command.ExecuteScalar<long>("SELECT LAST_INSERT_ID()");
                    return true;
                }

                command.ExecuteNonQuery("UPDATE USERDEFINEDREQUESTS SET REQUEST=@binaryData WHERE ID=" + request.UserDefinedId);
                return false;
            }
        }

        public override int CanFoundByChecksum(string md5)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@md5", md5);
                return (int)command.ExecuteScalar<long>("SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5 LIMIT 1");
            }
        }

        public override List<Description> GetDescription(List<MediaItem> mList)
        {
            List<Description> descriptionList = new List<Description>();

            using (ICommandHelper command = this.MBCommand)
            {
                foreach (int descriptionId in mList.Where(x => x.DescriptionId != null)
                    .Select(x => x.DescriptionId.Value).Distinct())
                {
                    descriptionList.Add(new Description
                    {
                        Value = command.ExecuteScalar<string>("SELECT `VALUE` FROM DESCRIPTION WHERE ID=" + descriptionId),
                        Id = descriptionId,
                        MediaItemList = new List<MediaItem>()
                    });
                }
            }

            foreach (MediaItem mediaItem in mList)
            {
                if (mediaItem.DescriptionId != null)
                {
                    Description description = descriptionList.First(x => x.Id.Value == mediaItem.DescriptionId.Value);
                    description.MediaItemList.Add(mediaItem);
                    mediaItem.Description = description.Value;
                }
            }

            return descriptionList;
        }

        public override string GetDescription(MediaItem mItem)
        {
            if (mItem.DescriptionId == null)
            {
                return null;
            }

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@id", mItem.DescriptionId.Value, DbType.Int32);
                mItem.Description = command.ExecuteScalar<string>("SELECT `VALUE` FROM DESCRIPTION WHERE ID=@id");
                return mItem.Description;
            }
        }

        public override Dictionary<MediaItem, XmlNode> CreateMediaItemFromXml(List<XmlNode> nodeList, Folder folder,
            List<Category> categoryImportList)
        {
            Dictionary<MediaItem, XmlNode> result = new Dictionary<MediaItem, XmlNode>();
            Dictionary<int, XmlNode> nodesById = new Dictionary<int, XmlNode>();

            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (XmlNode node in nodeList)
                {
                    int? id = CreateMediaItemFromXmlTransaction(transaction, node, folder, categoryImportList);

                    if (id != null && !nodesById.ContainsKey(id.Value))
                    {
                        nodesById.Add(id.Value, node);
                    }
                }
            }

            if (nodesById.Count == 0)
            {
                return result;
            }

            List<MediaItem> mediaItems = LoadMediaItems(
                "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK WHERE MEDIAFILES.ID IN (" +
                String.Join(",", nodesById.Keys) + ")", "", nodesById.Count);

            foreach (MediaItem mediaItem in mediaItems)
            {
                result.Add(mediaItem, nodesById[mediaItem.Id]);
            }

            return result;
        }

        private int? CreateMediaItemFromXmlTransaction(ITransaction transaction, XmlNode node, Folder folder,
            List<Category> categoryImportList)
        {
            if (folder.Id < 0)
            {
                CreateFolder(folder, transaction);
            }

            transaction.SetParameter("@name", node.Attributes["name"].Value, DbType.String);
            transaction.SetParameter("@md5", node.Attributes["md5"].Value, DbType.String);
            transaction.SetParameter("@folderId", folder.Id, DbType.Int32);

            if (transaction.ExecuteScalar<long>(
                    "SELECT COUNT(FILENAME) FROM MEDIAFILES WHERE FOLDERS_FK=@folderId AND FILENAME=@name") != 0)
            {
                return null;
            }

            long duplicateCount = transaction.ExecuteScalar<long>(
                "SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5");
            bool isDuplicate = duplicateCount != 0;

            if (isDuplicate)
            {
                transaction.ExecuteNonQuery(
                    "UPDATE MEDIAFILES SET ISDUBLICATE=1 WHERE ID IN (SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5)");

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

            int xmlDefaultVariationId = Convert.ToInt32(node.Attributes["variation"].Value);

            transaction.SetParameter("@length", Convert.ToInt64(node.Attributes["length"].Value), DbType.Int64);
            transaction.SetParameter("@viewed", Convert.ToInt32(node.Attributes["viewed"].Value), DbType.Int32);
            transaction.SetParameter("@sortname", node.Attributes["sortname"].Value, DbType.String);
            transaction.SetParameter("@type", node.Attributes["type"].Value, DbType.String);
            transaction.SetParameter("@creationdate",
                XmlConvert.ToDateTime(node.Attributes["creationdate"].Value, XmlDateTimeSerializationMode.RoundtripKind),
                DbType.DateTime);
            transaction.SetParameter("@insertdate",
                XmlConvert.ToDateTime(node.Attributes["insertdate"].Value, XmlDateTimeSerializationMode.RoundtripKind),
                DbType.DateTime);
            transaction.SetParameter("@editdate",
                XmlConvert.ToDateTime(node.Attributes["editdate"].Value, XmlDateTimeSerializationMode.RoundtripKind),
                DbType.DateTime);
            transaction.SetParameter("@mediadate",
                XmlConvert.ToDateTime(node.Attributes["mediadate"].Value, XmlDateTimeSerializationMode.RoundtripKind),
                DbType.DateTime);
            transaction.SetParameter("@orientation", Convert.ToInt32(node.Attributes["orientation"].Value),
                DbType.Int32);
            transaction.SetParameter("@duration", XmlConvert.ToDouble(node.Attributes["duration"].Value),
                DbType.Double);
            transaction.SetParameter("@frames", Convert.ToInt32(node.Attributes["frames"].Value), DbType.Int32);
            transaction.SetParameter("@priority", Convert.ToInt32(node.Attributes["priority"].Value), DbType.Int32);
            transaction.SetParameter("@width", Convert.ToInt32(node.Attributes["width"].Value), DbType.Int32);
            transaction.SetParameter("@height", Convert.ToInt32(node.Attributes["height"].Value), DbType.Int32);

            int roleId = Convert.ToInt32(node.Attributes["roleid"].Value);

            if (!MediaBrowserContext.AllRoles.Any(x => x.Id == roleId))
            {
                roleId = MediaBrowserContext.AllRoles.First(x => x.Name == "PUBLIC").Id;
            }

            transaction.SetParameter("@roleid", roleId, DbType.Int32);
            transaction.SetParameter("@isDublicate", isDuplicate, DbType.Boolean);

            transaction.ExecuteNonQuery(
                "INSERT INTO MEDIAFILES (SORTORDER, VIEWED, `TYPE`, FRAMES, ORIENTATION, DURATION, PRIORITY, WIDTH, " +
                "HEIGHT, LENGTH, FILENAME, MD5VALUE, FOLDERS_FK, ISDELETED, CREATIONDATE, MEDIADATE, EDITDATE, " +
                "INSERTDATE, ROLES_FK, HISTORYVERSION, ISDUBLICATE) VALUES (@sortname, @viewed, @type, @frames, " +
                "@orientation, @duration, @priority, @width, @height, @length, @name, @md5, @folderId, 0, " +
                "@creationdate, @mediadate, @editdate, @insertdate, @roleid, 0, @isDublicate)");

            int mediaItemId = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
            transaction.SetParameter("@id", mediaItemId, DbType.Int32);

            XmlNode metadataNode = node["metadata"];

            if (metadataNode != null)
            {
                foreach (XmlNode metadata in metadataNode.ChildNodes)
                {
                    string key = metadata.Attributes["key"].Value;
                    string type = metadata.Attributes["type"].Value;
                    string group = metadata.Attributes["group"].Value;

                    transaction.SetParameter("@metadataKey", key, DbType.String);
                    transaction.SetParameter("@metadataType", type, DbType.String);
                    transaction.SetParameter("@metadataGroup", group, DbType.String);

                    object metadataId = transaction.ExecuteScalar(
                        "SELECT ID FROM METADATANAME WHERE NAME=@metadataKey AND `TYPE`=@metadataType " +
                        "AND GROUPNAME=@metadataGroup");

                    if (metadataId == null || metadataId == DBNull.Value)
                    {
                        transaction.ExecuteNonQuery(
                            "INSERT INTO METADATANAME (NAME, `TYPE`, GROUPNAME) " +
                            "VALUES (@metadataKey, @metadataType, @metadataGroup)");
                        metadataId = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                    }

                    transaction.SetParameter("@metadataId", Convert.ToInt32(metadataId), DbType.Int32);
                    transaction.SetParameter("@metadataValue", metadata.InnerText, DbType.String);
                    transaction.ExecuteNonQuery(
                        "INSERT INTO METADATA (MEDIAFILES_FK, METANAME_FK, `VALUE`) " +
                        "VALUES (@id, @metadataId, @metadataValue)");
                }
            }

            int currentVariationId = 0;
            int lastVariationId = 0;
            XmlNode variationsNode = node["variations"];

            if (variationsNode != null)
            {
                foreach (XmlNode variationNode in variationsNode.ChildNodes)
                {
                    transaction.SetParameter("@variationPosition",
                        Convert.ToInt32(variationNode.Attributes["position"].Value), DbType.Int32);
                    transaction.SetParameter("@variationName", variationNode.Attributes["name"].Value, DbType.String);

                    transaction.ExecuteNonQuery(
                        "INSERT INTO VARIATIONS (MEDIAFILES_FK, NAME, `POSITION`) " +
                        "VALUES (@id, @variationName, @variationPosition)");

                    int variationId = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                    lastVariationId = variationId;
                    transaction.SetParameter("@variationId", variationId, DbType.Int32);

                    if (xmlDefaultVariationId == Convert.ToInt32(variationNode.Attributes["id"].Value))
                    {
                        currentVariationId = variationId;
                    }

                    XmlNode thumbnailNode = variationNode["thumbnail"];

                    if (thumbnailNode != null && !String.IsNullOrWhiteSpace(thumbnailNode.InnerText))
                    {
                        transaction.SetParameter("@thumb", Convert.FromBase64String(thumbnailNode.InnerText),
                            DbType.Binary);
                        transaction.ExecuteNonQuery(
                            "INSERT INTO THUMBS (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, THUMB) " +
                            "VALUES (@variationId, 0, 0, @thumb)");
                    }

                    XmlNode layersNode = variationNode["layers"];

                    if (layersNode != null)
                    {
                        foreach (XmlNode layerNode in layersNode.ChildNodes)
                        {
                            transaction.SetParameter("@layerPosition",
                                Convert.ToInt32(layerNode.Attributes["position"].Value), DbType.Int32);
                            transaction.SetParameter("@layerEdit", layerNode.Attributes["edit"].Value,
                                DbType.String);
                            transaction.SetParameter("@layerAction", layerNode.Attributes["action"].Value,
                                DbType.String);

                            transaction.ExecuteNonQuery(
                                "INSERT INTO LAYERS (VARIATIONS_FK, `POSITION`, EDIT, ACTION) " +
                                "VALUES (@variationId, @layerPosition, @layerEdit, @layerAction)");
                        }
                    }

                    if (variationNode.Attributes["categoryids"] == null
                        || String.IsNullOrWhiteSpace(variationNode.Attributes["categoryids"].Value))
                    {
                        continue;
                    }

                    foreach (string categoryXmlId in variationNode.Attributes["categoryids"].Value.Trim()
                                 .Split(','))
                    {
                        Category category = categoryImportList.FirstOrDefault(x =>
                            x.XmlId.ToString() == categoryXmlId);

                        if (category != null)
                        {
                            transaction.ExecuteNonQuery(
                                "INSERT IGNORE INTO CATEGORIZE (CATEGORY_FK, VARIATIONS_FK) " +
                                "VALUES (" + category.Id + ", @variationId)");
                        }
                    }
                }
            }

            if (currentVariationId == 0)
            {
                currentVariationId = lastVariationId;
            }

            transaction.SetParameter("@currentVariation", currentVariationId, DbType.Int32);
            transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=@currentVariation WHERE ID=@id");

            return mediaItemId;
        }

        private static void CreateFolder(Folder folder, ITransaction transaction)
        {
            transaction.SetParameter("@foldername",
                MediaBrowserContext.MapPathRootReverse(FilesAndFolders.CleanPath(folder.FullPath)), DbType.String);

            object id = transaction.ExecuteScalar(
                "SELECT ID FROM FOLDERS WHERE LOWER(FOLDERNAME)=LOWER(@foldername)");

            if (id == null || id == DBNull.Value)
            {
                transaction.ExecuteNonQuery("INSERT INTO FOLDERS (FOLDERNAME) VALUES (@foldername)");
                folder.Id = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
            }
            else
            {
                folder.Id = Convert.ToInt32(id);
            }
        }

        public override void SetDescription(Description description)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                if (description.Command == DescriptionCommand.DELETE_DESCRIPTION)
                {
                    if (description.Id == null)
                    {
                        return;
                    }

                    transaction.ExecuteNonQuery("DELETE FROM DESCRIPTION WHERE ID=" + description.Id);
                    transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET DESCRIPTION_FK=NULL WHERE DESCRIPTION_FK=" +
                                                description.Id);

                    foreach (MediaItem mediaItem in description.MediaItemList)
                    {
                        mediaItem.DescriptionId = null;
                        mediaItem.Description = "";
                    }

                    return;
                }

                if (description.Command == DescriptionCommand.SET_AND_CREATE_DESCRIPTION)
                {
                    if (String.IsNullOrWhiteSpace(description.Value))
                    {
                        return;
                    }

                    transaction.SetParameter("@VALUE", description.Value.Trim(), DbType.String);
                    object id = transaction.ExecuteScalar("SELECT ID FROM DESCRIPTION WHERE `VALUE`=@VALUE");

                    description.Id = id == null || id == DBNull.Value ? (int?)null : Convert.ToInt32(id);

                    if (description.Id == null)
                    {
                        transaction.ExecuteNonQuery("INSERT INTO DESCRIPTION (`VALUE`) VALUES (@VALUE)");
                        description.Id = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                    }
                }
                else if (description.Command == DescriptionCommand.REMOVE_FROM_MEDIAITEMS)
                {
                    description.Id = null;
                }

                if ((description.Command == DescriptionCommand.SET_AND_CREATE_DESCRIPTION
                     || description.Command == DescriptionCommand.SET_EXISTING_DESCRIPTION
                     || description.Command == DescriptionCommand.REMOVE_FROM_MEDIAITEMS)
                    && description.MediaItemList.Count > 0)
                {
                    transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET DESCRIPTION_FK=" +
                                                (description.Id == null
                                                    ? "NULL"
                                                    : description.Id.ToString()) +
                                                " WHERE ID IN (-1, " +
                                                ConcatMediaItemIDs(description.MediaItemList) + ")");

                    foreach (MediaItem mediaItem in description.MediaItemList)
                    {
                        mediaItem.DescriptionId = description.Id;
                        mediaItem.Description = description.Value;
                    }
                }
            }
        }

        public override void SetAttachment(List<Attachment> attachmentList)
        {
            if (attachmentList.Count == 0)
            {
                return;
            }

            foreach (Attachment attachment in attachmentList)
            {
                using (ITransaction transaction = this.MBTransaction)
                {
                    if (attachment.Type == AttachmentType.ATTACHED)
                    {
                        transaction.SetParameter("@PATH", attachment.FullName.Trim(), DbType.String);

                        if (attachment.Id == null)
                        {
                            object id = transaction.ExecuteScalar(
                                "SELECT ID FROM ATTACHMENTS WHERE LOWER(PATH)=LOWER(@PATH)");

                            attachment.Id = id == null || id == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(id);

                            if (attachment.Id == null)
                            {
                                transaction.ExecuteNonQuery("INSERT INTO ATTACHMENTS (PATH) VALUES (@PATH)");
                                attachment.Id = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
                            }

                            foreach (MediaItem mediaItem in attachment.MediaItemList)
                            {
                                long count = transaction.ExecuteScalar<long>(
                                    "SELECT COUNT(*) FROM ATTACHED WHERE ATTACHMENTS_FK=" + attachment.Id +
                                    " AND MEDIAFILES_FK=" + mediaItem.Id);

                                if (count == 0)
                                {
                                    transaction.ExecuteNonQuery(
                                        "INSERT INTO ATTACHED (ATTACHMENTS_FK, MEDIAFILES_FK) VALUES (" +
                                        attachment.Id + "," + mediaItem.Id + ")");
                                }
                            }
                        }
                        else
                        {
                            transaction.ExecuteNonQuery("UPDATE ATTACHMENTS SET PATH=@PATH WHERE ID=" +
                                                        attachment.Id);
                        }
                    }
                    else if (attachment.Type == AttachmentType.DETACHE
                             && attachment.Id != null
                             && attachment.MediaItemList != null
                             && attachment.MediaItemList.Count > 0)
                    {
                        transaction.ExecuteNonQuery("DELETE FROM ATTACHED WHERE MEDIAFILES_FK IN (" +
                                                    ConcatMediaItemIDs(attachment.MediaItemList) +
                                                    ") AND ATTACHMENTS_FK=" + attachment.Id);

                        attachment.IsReferenced = 0 != transaction.ExecuteScalar<long>(
                            "SELECT COUNT(*) FROM ATTACHED WHERE ATTACHMENTS_FK=" + attachment.Id);
                    }
                }
            }
        }

        public override List<Attachment> GetAttachment(List<MediaItem> mediaItemList)
        {
            List<Attachment> attachmentList = new List<Attachment>();

            if (mediaItemList == null || mediaItemList.Count == 0)
            {
                return attachmentList;
            }

            using (ICommandHelper command = this.MBCommand)
            using (DbDataReader reader = command.ExecuteReader(
                "SELECT MEDIAFILES_FK, ATTACHMENTS_FK, PATH FROM ATTACHMENTS " +
                "INNER JOIN ATTACHED ON ATTACHED.ATTACHMENTS_FK=ATTACHMENTS.ID " +
                "WHERE MEDIAFILES_FK IN (" + ConcatMediaItemIDs(mediaItemList) + ") ORDER BY PATH"))
            {
                while (reader.Read())
                {
                    int attachmentId = Convert.ToInt32(reader["ATTACHMENTS_FK"]);
                    Attachment attachment = attachmentList.FirstOrDefault(x => x.Id == attachmentId);

                    if (attachment == null)
                    {
                        attachment = new Attachment(new List<MediaItem>(), (string)reader["PATH"]);
                        attachment.Id = attachmentId;
                        attachmentList.Add(attachment);
                    }

                    attachment.MediaItemList.Add(
                        mediaItemList.First(x => x.Id == Convert.ToInt32(reader["MEDIAFILES_FK"])));
                }
            }

            return attachmentList;
        }

        public override MediaItemSqlRequest GetSqlRequest(List<MediaItem> mediaItemList)
        {
            if (mediaItemList == null || mediaItemList.Count == 0)
            {
                return null;
            }

            return new MediaItemSqlRequest(
                "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK WHERE MEDIAFILES.ID IN ("
                + String.Join(",", mediaItemList.Select(x => x.Id)) + ") AND HISTORYVERSION = 0",
                new List<SqlParameterInfo>(), String.Empty, MediaBrowserContext.LimitRequest);
        }

        public override bool IsDublicate(MediaItem mItem, MediaItem.DublicateCriteria criteria)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                switch (criteria)
                {
                    case MediaItem.DublicateCriteria.FILENAME:
                        command.SetParameter("@filename", mItem.Filename.ToLower(), DbType.String);
                        return command.ExecuteScalar<long>(
                            "SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME)=@filename AND ID<>" + mItem.Id) != 0;

                    case MediaItem.DublicateCriteria.CONTAINS:
                        command.SetParameter("@filename", "%" + mItem.Filename.ToLower() + "%", DbType.String);
                        return command.ExecuteScalar<long>(
                            "SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME) LIKE @filename AND ID<>" + mItem.Id) != 0;

                    default:
                        return false;
                }
            }
        }

        public override PreviewObject GetImagePreviewDB(int variationId, System.Drawing.Size size)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@variationId", variationId, DbType.Int32);
                command.SetParameter("@width", size.Width, DbType.Int32);
                command.SetParameter("@height", size.Height, DbType.Int32);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT THUMB, EXTENSION FROM THUMBS WHERE VARIATIONS_FK=@variationId " +
                    "AND PREVIEW_WIDTH=@width AND PREVIEW_HEIGHT=@height"))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    PreviewObject previewObject = new PreviewObject
                    {
                        Extension = reader.GetString(1)
                    };

                    previewObject.TempPath = Path.Combine(MediaBrowserContext.DBTempFolder,
                        variationId + "." + previewObject.Extension.Replace("agif", "gif"));

                    byte[] binary = (byte[])reader[0];

                    if (previewObject.Extension == "jpg" || previewObject.Extension == "agif")
                    {
                        previewObject.Binary = binary;
                    }
                    else if (!File.Exists(previewObject.TempPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(previewObject.TempPath));
                        File.WriteAllBytes(previewObject.TempPath, binary);
                    }

                    return previewObject;
                }
            }
        }

        public override MediaProcessing.FaceDetection.Faces GetFaceDetectionPreviewDB(int variationId,
            System.Drawing.Size size)
        {
            MediaProcessing.FaceDetection.Faces faces = new MediaProcessing.FaceDetection.Faces
            {
                Facelist = new List<System.Drawing.Rectangle>()
            };

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@variationId", variationId, DbType.Int32);
                command.SetParameter("@width", size.Width, DbType.Int32);
                command.SetParameter("@height", size.Height, DbType.Int32);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT FACES.X, FACES.Y, FACES.WIDTH, FACES.HEIGHT, THUMBS.WIDTH, THUMBS.HEIGHT " +
                    "FROM FACES INNER JOIN THUMBS ON FACES.VARIATIONS_FK=THUMBS.VARIATIONS_FK " +
                    "AND FACES.PREVIEW_WIDTH=THUMBS.PREVIEW_WIDTH AND FACES.PREVIEW_HEIGHT=THUMBS.PREVIEW_HEIGHT " +
                    "WHERE FACES.VARIATIONS_FK=@variationId AND THUMBS.PREVIEW_WIDTH=@width " +
                    "AND THUMBS.PREVIEW_HEIGHT=@height"))
                {
                    while (reader.Read())
                    {
                        faces.Facelist.Add(new System.Drawing.Rectangle(
                            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));

                        faces.Width = ReadInt32(reader, 4);
                        faces.Height = ReadInt32(reader, 5);
                    }
                }
            }

            return faces;
        }

        public override Dictionary<int, int> GetAllFaces(System.Drawing.Size size, double min, double max,
            int countMin, int countMax, List<MediaItem> itemList)
        {
            Dictionary<int, int> faceList = new Dictionary<int, int>();
            string restriction = itemList != null && itemList.Count > 0
                ? " AND VARIATIONS_FK IN (" + String.Join(",", itemList.Select(x => x.VariationId)) + ")"
                : String.Empty;

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@min", min, DbType.Double);
                command.SetParameter("@max", max, DbType.Double);
                command.SetParameter("@countMin", countMin, DbType.Int32);
                command.SetParameter("@countMax", countMax, DbType.Int32);
                command.SetParameter("@width", size.Width, DbType.Int32);
                command.SetParameter("@height", size.Height, DbType.Int32);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT VARIATIONS_FK, COUNT(VARIATIONS_FK) FROM FACES " +
                    "WHERE REL_SIZE >= @min AND REL_SIZE <= @max AND PREVIEW_WIDTH=@width " +
                    "AND PREVIEW_HEIGHT=@height" + restriction +
                    " GROUP BY VARIATIONS_FK HAVING COUNT(VARIATIONS_FK) >= @countMin " +
                    "AND COUNT(VARIATIONS_FK) <= @countMax"))
                {
                    while (reader.Read())
                    {
                        faceList.Add(ReadInt32(reader, 0), ReadInt32(reader, 1));
                    }
                }
            }

            return faceList;
        }

        public override bool IsImageInPreviewDB(int variationId, System.Drawing.Size size)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@variationId", variationId, DbType.Int32);
                command.SetParameter("@width", size.Width, DbType.Int32);
                command.SetParameter("@height", size.Height, DbType.Int32);

                return command.ExecuteScalar<long>(
                    "SELECT COUNT(VARIATIONS_FK) FROM THUMBS WHERE VARIATIONS_FK=@variationId " +
                    "AND PREVIEW_WIDTH=@width AND PREVIEW_HEIGHT=@height") > 0;
            }
        }

        public override void DeleteFromPreviewDB(List<MediaItem> mediaItemList, System.Drawing.Size size)
        {
            if (mediaItemList == null || mediaItemList.Count == 0)
            {
                return;
            }

            List<int> variationIds = new List<int>();

            using (ICommandHelper command = this.MBCommand)
            {
                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK IN (" +
                    String.Join(",", mediaItemList.Select(x => x.Id)) + ")"))
                {
                    while (reader.Read())
                    {
                        variationIds.Add(ReadInt32(reader, 0));
                    }
                }

                if (variationIds.Count > 0)
                {
                    command.SetParameter("@width", size.Width, DbType.Int32);
                    command.SetParameter("@height", size.Height, DbType.Int32);
                    command.ExecuteNonQuery(
                        "DELETE FROM FACES WHERE PREVIEW_WIDTH=@width AND PREVIEW_HEIGHT=@height " +
                        "AND VARIATIONS_FK IN (" + String.Join(",", variationIds) + ")");
                    command.ExecuteNonQuery(
                        "DELETE FROM THUMBS WHERE PREVIEW_WIDTH=@width AND PREVIEW_HEIGHT=@height " +
                        "AND VARIATIONS_FK IN (" + String.Join(",", variationIds) + ")");
                }
            }
        }

        public override void WriteToPreviewDB(List<string> fileList, System.Drawing.Size size)
        {
            int count = 0;

            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (string file in fileList)
                {
                    string filename = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                    int variationId;

                    if (!Int32.TryParse(filename, out variationId) || String.IsNullOrWhiteSpace(extension))
                    {
                        continue;
                    }

                    byte[] thumbnail = File.ReadAllBytes(file);
                    byte[] faceData = File.Exists(file + ".fcd") ? File.ReadAllBytes(file + ".fcd") : null;

                    transaction.SetParameter("@variationId", variationId, DbType.Int32);
                    transaction.SetParameter("@width", size.Width, DbType.Int32);
                    transaction.SetParameter("@height", size.Height, DbType.Int32);
                    transaction.SetParameter("@thumb", thumbnail, DbType.Binary);
                    transaction.SetParameter("@extension", extension, DbType.String);

                    double imageWidth = 0;
                    double imageHeight = 0;

                    try
                    {
                        System.Windows.Media.Imaging.BitmapDecoder decoder =
                            System.Windows.Media.Imaging.BitmapDecoder.Create(new Uri(file),
                                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                                System.Windows.Media.Imaging.BitmapCacheOption.None);
                        imageWidth = decoder.Frames[0].Width;
                        imageHeight = decoder.Frames[0].Height;
                    }
                    catch
                    {
                    }

                    transaction.ExecuteNonQuery(
                        "DELETE FROM FACES WHERE VARIATIONS_FK=@variationId AND PREVIEW_WIDTH=@width " +
                        "AND PREVIEW_HEIGHT=@height");
                    transaction.ExecuteNonQuery(
                        "DELETE FROM THUMBS WHERE VARIATIONS_FK=@variationId AND PREVIEW_WIDTH=@width " +
                        "AND PREVIEW_HEIGHT=@height");

                    transaction.SetParameter("@imageWidth", imageWidth > 0 ? (object)imageWidth : DBNull.Value,
                        DbType.Double);
                    transaction.SetParameter("@imageHeight", imageHeight > 0 ? (object)imageHeight : DBNull.Value,
                        DbType.Double);

                    transaction.ExecuteNonQuery(
                        "INSERT INTO THUMBS (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, THUMB, EXTENSION, WIDTH, HEIGHT) " +
                        "VALUES (@variationId, @width, @height, @thumb, @extension, @imageWidth, @imageHeight)");

                    if (faceData != null && imageWidth > 0 && imageHeight > 0)
                    {
                        MediaProcessing.FaceDetection.Faces faces;

                        using (MemoryStream stream = new MemoryStream(faceData))
                        {
                            faces = (MediaProcessing.FaceDetection.Faces)new BinaryFormatter().Deserialize(stream);
                        }

                        foreach (System.Drawing.Rectangle rectangle in faces.Facelist)
                        {
                            double relativeSize = 100.0 * rectangle.Width * rectangle.Height / (imageWidth * imageHeight);
                            transaction.SetParameter("@relativeSize", relativeSize, DbType.Double);

                            transaction.ExecuteNonQuery(
                                "INSERT INTO FACES (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, X, Y, WIDTH, HEIGHT, REL_SIZE) " +
                                "VALUES (@variationId, @width, @height, " + rectangle.X + ", " + rectangle.Y + ", " +
                                rectangle.Width + ", " + rectangle.Height + ", @relativeSize)");
                        }
                    }

                    count++;

                    if (this.OnInsert != null)
                    {
                        this.OnInsert(this, new MediaItemCallbackArgs(count, fileList.Count, null));
                    }
                }
            }

            if (this.OnInsert != null)
            {
                this.OnInsert(this, new MediaItemCallbackArgs(count, fileList.Count, null, true, false));
            }
        }

        public override List<int> GetPreviewDBVariationIdList(System.Drawing.Size size)
        {
            List<int> variationIds = new List<int>();

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@width", size.Width, DbType.Int32);
                command.SetParameter("@height", size.Height, DbType.Int32);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT VARIATIONS_FK FROM THUMBS WHERE PREVIEW_WIDTH=@width AND PREVIEW_HEIGHT=@height"))
                {
                    while (reader.Read())
                    {
                        variationIds.Add(ReadInt32(reader, 0));
                    }
                }
            }

            return variationIds;
        }

        public override void CleanDB()
        {
            using (ICommandHelper command = this.MBCommand)
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    DeleteWhereIdIn(command,
                        "SELECT ID FROM CATEGORY WHERE ISDATE=1 AND ID NOT IN (SELECT CATEGORY_FK FROM CATEGORIZE) " +
                        "AND ID NOT IN (SELECT PARENT FROM CATEGORY)",
                        "DELETE FROM CATEGORY WHERE ID IN (");
                }

                DeleteWhereIdIn(command,
                    "SELECT ID FROM FOLDERS WHERE ID NOT IN (SELECT FOLDERS_FK FROM MEDIAFILES)",
                    "DELETE FROM FOLDERS WHERE ID IN (");
                DeleteWhereIdIn(command,
                    "SELECT ID FROM ATTACHMENTS WHERE ID NOT IN (SELECT DISTINCT ATTACHMENTS_FK FROM ATTACHED)",
                    "DELETE FROM ATTACHMENTS WHERE ID IN (");
                DeleteWhereIdIn(command,
                    "SELECT ID FROM DESCRIPTION WHERE ID NOT IN " +
                    "(SELECT DESCRIPTION_FK FROM MEDIAFILES WHERE DESCRIPTION_FK IS NOT NULL)",
                    "DELETE FROM DESCRIPTION WHERE ID IN (");

                command.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE FOLDERS_FK NOT IN (SELECT ID FROM FOLDERS)");
                command.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE HISTORYVERSION > 0");
                command.ExecuteNonQuery("DELETE FROM THUMBS WHERE VARIATIONS_FK NOT IN (SELECT ID FROM VARIATIONS)");
                command.ExecuteNonQuery("DELETE FROM FACES WHERE VARIATIONS_FK NOT IN (SELECT ID FROM VARIATIONS)");
                command.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0");
                command.ExecuteNonQuery(
                    "UPDATE MEDIAFILES SET ISDUBLICATE=1 WHERE MD5VALUE IN " +
                    "(SELECT MD5VALUE FROM (SELECT MD5VALUE FROM MEDIAFILES GROUP BY MD5VALUE " +
                    "HAVING COUNT(MD5VALUE) > 1) AS duplicates)");
            }

            using (ICommandHelper command = this.MBCommand)
            {
                foreach (string table in new[]
                         {
                             "MEDIAFILES", "VARIATIONS", "THUMBS", "FACES", "CATEGORY", "CATEGORIZE", "FOLDERS",
                             "METADATA", "METADATANAME"
                         })
                {
                    command.ExecuteNonQuery("ANALYZE TABLE " + table);
                }
            }
        }

        private static void DeleteWhereIdIn(ICommandHelper command, string selectSql, string deleteSqlPrefix)
        {
            List<long> ids = new List<long>();

            foreach (DataRow row in command.GetDataTable(selectSql).Rows)
            {
                ids.Add(Convert.ToInt64(row[0]));
            }

            if (ids.Count > 0)
            {
                command.ExecuteNonQuery(deleteSqlPrefix + String.Join(",", ids) + ")");
            }
        }

        public override void SetRole(List<MediaBrowser4.Objects.MediaItem> mList, int roleId)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.ExecuteNonQuery("UPDATE MEDIAFILES SET ROLES_FK=" + roleId + " WHERE ID IN (" +
                                        ConcatMediaItemIDs(mList) + ")");
            }

            foreach (MediaItem mediaItem in mList)
            {
                mediaItem.RoleId = roleId;
            }
        }

        public override void SetPriority(List<MediaBrowser4.Objects.MediaItem> mList, int priority)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.ExecuteNonQuery("UPDATE MEDIAFILES SET PRIORITY=" + priority + " WHERE ID IN (" +
                                        ConcatMediaItemIDs(mList) + ")");
            }

            foreach (MediaItem mediaItem in mList)
            {
                mediaItem.Priority = priority;
            }
        }

        public override bool RemoveVariation(MediaBrowser4.Objects.MediaItem mItem)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                if (mItem.VariationId == transaction.ExecuteScalar<long>(
                    "SELECT CURRENTVARIATION FROM MEDIAFILES WHERE ID=" + mItem.Id))
                {
                    return false;
                }

                transaction.ExecuteNonQuery("DELETE FROM VARIATIONS WHERE ID=" + mItem.VariationId);
                ReorderVariations(transaction, mItem.Id);
            }

            return true;
        }

        public override bool RemoveVariation(MediaBrowser4.Objects.MediaItem mItem, string name)
        {
            List<int> variationIds = new List<int>();

            using (ITransaction transaction = this.MBTransaction)
            {
                long currentVariationId = transaction.ExecuteScalar<long>(
                    "SELECT CURRENTVARIATION FROM MEDIAFILES WHERE ID=" + mItem.Id);

                transaction.SetParameter("@NAME", name.Trim(), DbType.String);

                using (DbDataReader reader = transaction.ExecuteReader(
                    "SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id + " AND NAME=@NAME"))
                {
                    while (reader.Read())
                    {
                        int variationId = ReadInt32(reader, 0);

                        if (currentVariationId == variationId)
                        {
                            return false;
                        }

                        variationIds.Add(variationId);
                    }
                }

                transaction.ExecuteNonQuery(
                    "DELETE FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id + " AND NAME=@NAME");
                ReorderVariations(transaction, mItem.Id);
            }

            return true;
        }

        private static void ReorderVariations(ITransaction transaction, int mediaItemId)
        {
            List<int> ids = new List<int>();

            using (DbDataReader reader = transaction.ExecuteReader(
                "SELECT ID FROM VARIATIONS WHERE MEDIAFILES_FK=" + mediaItemId + " ORDER BY `POSITION`, NAME"))
            {
                while (reader.Read())
                {
                    ids.Add(ReadInt32(reader, 0));
                }
            }

            int position = 0;

            foreach (int id in ids)
            {
                position++;
                transaction.ExecuteNonQuery("UPDATE VARIATIONS SET `POSITION`=" + position + " WHERE ID=" + id);
            }
        }

        public override void RenameMediaItem(MediaItem mediaItem, string newName)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@FILENAME", newName.Trim(), DbType.String);
                command.ExecuteNonQuery(
                    "UPDATE MEDIAFILES SET FILENAME=@FILENAME, SORTORDER=@FILENAME WHERE ID=" + mediaItem.Id);
            }

            mediaItem.Filename = newName.Trim();
            mediaItem.Sortorder = mediaItem.Filename;
            mediaItem.FileObject = new FileInfo(Path.Combine(mediaItem.Foldername, newName.Trim()));
        }

        public override bool SetVariationDefault(MediaItem mItem, Variation variation)
        {
            if (variation != null)
            {
                mItem.VariationId = variation.Id;
            }

            if (mItem.VariationId == mItem.VariationIdDefault)
            {
                return false;
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=" + mItem.VariationId +
                                            " WHERE ID=" + mItem.Id);
                mItem.VariationIdDefault = mItem.VariationId;
            }

            mItem.ThumbJpegData = this.GetThumbJpegData(mItem.VariationId);

            return true;
        }

        public override bool EditVariation(List<Variation> variationList)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (Variation variation in variationList)
                {
                    transaction.SetParameter("@NAME", variation.Name.Trim(), DbType.String);
                    transaction.ExecuteNonQuery("UPDATE VARIATIONS SET NAME=@NAME, `POSITION`=" +
                                                variation.Position + " WHERE ID=" + variation.Id);
                }
            }

            return true;
        }

        public override Exception RemoveAndRecycle(MediaItem mItem)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                try
                {
                    transaction.SetParameter("@md5", mItem.Md5Value, DbType.String);
                    transaction.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE ID=" + mItem.Id);
                    UpdateDublicates(mItem, transaction);

                    if (mItem.FileObject != null && mItem.FileObject.Exists)
                    {
                        MediaBrowser4.Utilities.RecycleBin.Recycle(mItem.FullName, false);

                        foreach (Attachment attachment in FilesAndFolders.GetAttachments(mItem))
                        {
                            if (!attachment.IsShared)
                            {
                                MediaBrowser4.Utilities.RecycleBin.Recycle(attachment.FullName, false);
                            }
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
                catch (Exception exception)
                {
                    transaction.Rollback();
                    return exception;
                }
            }

            MediaBrowserContext.GlobalMediaItemCache.Remove(mItem);

            return null;
        }

        private static void UpdateDublicates(MediaItem mediaItem, ITransaction transaction)
        {
            transaction.SetParameter("@md5", mediaItem.Md5Value, DbType.String);

            if (transaction.ExecuteScalar<long>(
                    "SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5") == 1)
            {
                int id = transaction.ExecuteScalar<int>("SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5");
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0 WHERE ID=" + id);

                if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(id))
                {
                    MediaBrowserContext.GlobalMediaItemCache[id].IsMd5Dublicate = false;
                }
            }
        }

        public override void AddMediaItemAsync(MediaBrowser4.Objects.MediaItem mItems)
        {
            this.insert.AddMediaItem(mItems);
        }

        public override void Rotate90(MediaItem mItem)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.ExecuteNonQuery("UPDATE MEDIAFILES SET ORIENTATION=" + (int)mItem.Orientation +
                                        " WHERE ID=" + mItem.Id);
            }
        }

        public override Variation SetNewVariation(MediaBrowser4.Objects.MediaItem mItem, string name, bool setDefault,
            string layerAction, string layerEdit, byte[] thumbData)
        {
            Variation variation = null;

            using (ITransaction transaction = this.MBTransaction)
            {
                long position = transaction.ExecuteScalar<long>(
                    "SELECT COALESCE(MAX(`POSITION`), 0) + 1 FROM VARIATIONS WHERE MEDIAFILES_FK=" + mItem.Id);

                transaction.SetParameter("@NAME", (name ?? String.Empty).Trim(), DbType.String);
                transaction.ExecuteNonQuery("INSERT INTO VARIATIONS (MEDIAFILES_FK, NAME, `POSITION`) VALUES (" +
                                            mItem.Id + ", @NAME, " + position + ")");

                long variationId = transaction.ExecuteScalar<long>("SELECT LAST_INSERT_ID()");

                if (layerAction == null && mItem.VariationId > 0)
                {
                    DataTable layerTable = transaction.GetDataTable(
                        "SELECT `POSITION`, EDIT, ACTION FROM LAYERS WHERE VARIATIONS_FK=" + mItem.VariationId);

                    foreach (DataRow row in layerTable.Rows)
                    {
                        transaction.SetParameter("@ACTION", row["ACTION"].ToString(), DbType.String);
                        transaction.SetParameter("@EDIT", row["EDIT"].ToString(), DbType.String);
                        transaction.ExecuteNonQuery(
                            "INSERT INTO LAYERS (VARIATIONS_FK, `POSITION`, EDIT, ACTION) VALUES(" +
                            variationId + ", " + Convert.ToInt32(row["POSITION"]) + ", @EDIT, @ACTION)");
                    }

                    transaction.ExecuteNonQuery(
                        "INSERT INTO CATEGORIZE (CATEGORY_FK, VARIATIONS_FK) SELECT CATEGORY_FK, " + variationId +
                        " FROM CATEGORIZE WHERE VARIATIONS_FK=" + mItem.VariationId);
                }

                variation = new Variation
                {
                    Name = name,
                    Id = (int)variationId,
                    Position = (int)position,
                    MediaItemId = mItem.Id
                };

                if (setDefault)
                {
                    transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET CURRENTVARIATION=" + variationId +
                                                " WHERE ID=" + mItem.Id);
                    mItem.VariationIdDefault = variation.Id;
                }

                if (!String.IsNullOrEmpty(layerAction) && layerEdit != null)
                {
                    transaction.SetParameter("@action", layerAction, DbType.String);
                    transaction.SetParameter("@edit", layerEdit, DbType.String);
                    transaction.ExecuteNonQuery(
                        "INSERT INTO LAYERS (ACTION, EDIT, `POSITION`, VARIATIONS_FK) " +
                        "VALUES (@action, @edit, 0, " + variationId + ")");
                }

                byte[] imageData = thumbData;

                if (imageData == null && mItem.VariationId > 0)
                {
                    imageData = GetThumbJpegData(transaction, mItem.VariationId);
                }

                if (imageData != null)
                {
                    transaction.SetParameter("@thumb", imageData, DbType.Binary);
                    transaction.ExecuteNonQuery(
                        "INSERT INTO THUMBS (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, THUMB) " +
                        "VALUES (" + variationId + ", 0, 0, @thumb)");
                }
            }

            if (layerAction == null)
            {
                mItem.ChangeVariation(variation);
            }

            return variation;
        }

        public override List<MetaData> GetMetaDataFromMediaItems(List<MediaItem> mItemList)
        {
            List<MetaData> metaDataList = new List<MetaData>();

            if (mItemList == null || mItemList.Count == 0)
            {
                return metaDataList;
            }

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT DISTINCT `TYPE`, GROUPNAME, NAME, `VALUE`, ISVISIBLE FROM METADATANAME " +
                    "INNER JOIN METADATA ON METADATANAME.ID = METADATA.METANAME_FK " +
                    "WHERE MEDIAFILES_FK IN (" + String.Join(",", mItemList.Select(x => x.Id)) + ") " +
                    "ORDER BY `TYPE`, GROUPNAME, NAME, `VALUE`");

                foreach (DataRow row in table.Rows)
                {
                    metaDataList.Add(new MetaData(row["NAME"].ToString(), row["GROUPNAME"].ToString(),
                        row["VALUE"].ToString(), row["TYPE"].ToString(), Convert.ToBoolean(row["ISVISIBLE"])));
                }
            }

            return metaDataList;
        }

        public override List<MediaBrowser4.Objects.Variation> GetVariations(MediaBrowser4.Objects.MediaItem mItem,
            bool getThumbdata)
        {
            List<Variation> variations = new List<Variation>();

            using (ICommandHelper command = this.MBCommand)
            {
                string sql = "SELECT v.ID, v.`POSITION`, v.NAME, t.THUMB FROM VARIATIONS v " +
                             "LEFT JOIN THUMBS t ON t.VARIATIONS_FK=v.ID AND t.PREVIEW_WIDTH=0 AND t.PREVIEW_HEIGHT=0 " +
                             "WHERE v.MEDIAFILES_FK=" + mItem.Id + " ORDER BY v.`POSITION`, v.NAME";

                using (DbDataReader reader = command.ExecuteReader(sql))
                {
                    while (reader.Read())
                    {
                        Variation variation = new Variation
                        {
                            Id = ReadInt32(reader, 0),
                            Position = ReadInt32(reader, 1),
                            Name = reader.IsDBNull(2) || String.IsNullOrWhiteSpace(reader.GetString(2))
                                ? "Default"
                                : reader.GetString(2),
                            MediaItemId = mItem.Id
                        };

                        if (getThumbdata && !reader.IsDBNull(3))
                        {
                            variation.ThumbJpegData = (byte[])reader[3];
                        }

                        variations.Add(variation);
                    }
                }
            }

            return variations;
        }

        public override List<MediaItem> GetMediaItems(MediaItemRequest request)
        {
            if (request is MediaItemFilesRequest)
            {
                return GetMediaItemsFromFileRequest((MediaItemFilesRequest)request);
            }

            if (request is MediaItemFolderRequest)
            {
                MediaItemFolderRequest folderRequest = (MediaItemFolderRequest)request;
                return GetMediaItemsFromFolders(new List<Folder>(folderRequest.FoldersComplete), "",
                    request.LimitRequest == 0 ? MediaBrowserContext.LimitRequest : request.LimitRequest, folderRequest);
            }

            if (request is MediaItemCategoryRequest)
            {
                MediaItemCategoryRequest categoryRequest = (MediaItemCategoryRequest)request;

                if (categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_DATE
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_OTHER
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_LOCATION
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY)
                {
                    return GetMediaItemsFromMissingCategories(categoryRequest);
                }

                return GetMediaItemsFromCategories(new List<Category>(categoryRequest.Categories),
                    request.RequestType == MediaItemRequestType.INTERSECT,
                    request.RequestType != MediaItemRequestType.SINGLE, "",
                    request.LimitRequest == 0 ? MediaBrowserContext.LimitRequest : request.LimitRequest,
                    categoryRequest);
            }

            if (request is MediaItemSearchRequest)
            {
                return GetMediaItemsFromSearchToken(((MediaItemSearchRequest)request).SearchToken, false, "",
                    request.LimitRequest == 0 ? MediaBrowserContext.LimitRequest : request.LimitRequest, request);
            }

            if (request is MediaItemSortRequest)
            {
                return GetMediaItemsFromSortRequest((MediaItemSortRequest)request);
            }

            if (request is MediaItemDublicatesRequest)
            {
                return GetDublicates((MediaItemDublicatesRequest)request);
            }

            if (request is MediaItemSqlRequest)
            {
                return LoadMediaItems((MediaItemSqlRequest)request);
            }

            if (request is MediaItemRequestGeoData)
            {
                MediaItemRequestGeoData geoRequest = (MediaItemRequestGeoData)request;
                return GetMediaItemsGeoData(geoRequest.Longitute, geoRequest.Width, geoRequest.Latitude,
                    geoRequest.Height, geoRequest.LimitRequest);
            }

            throw new Exception("Not Implemented: " + request);
        }

        private List<MediaItem> GetMediaItemsFromMissingCategories(MediaItemCategoryRequest request)
        {
            string sqlCategory;

            if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_DATE)
            {
                sqlCategory = "ISDATE=1";
            }
            else if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_LOCATION)
            {
                sqlCategory = "ISLOCATION=1";
            }
            else if (request.CategoryRequestType == MediaItemCategoryRequestType.NO_OTHER)
            {
                sqlCategory = "ISLOCATION<>1 AND ISDATE<>1";
            }
            else
            {
                sqlCategory = String.Empty;
            }

            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems(
                "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                searchTokenSql.JoinSql +
                " WHERE MEDIAFILES.ID NOT IN " +
                "(SELECT VARIATIONS.MEDIAFILES_FK FROM VARIATIONS " +
                "INNER JOIN MEDIAFILES ON VARIATIONS.MEDIAFILES_FK = MEDIAFILES.ID " +
                "INNER JOIN CATEGORIZE ON CATEGORIZE.VARIATIONS_FK = VARIATIONS.ID" +
                (request.CategoryRequestType != MediaItemCategoryRequestType.NO_CATEGORY
                    ? " WHERE CATEGORIZE.CATEGORY_FK IN (SELECT ID FROM CATEGORY WHERE " + sqlCategory + ")"
                    : "") +
                ") " + (searchTokenSql.IsValid ? "AND " + searchTokenSql.WhereSql : "") +
                " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, "", request.LimitRequest, request);
        }

        private List<MediaItem> GetMediaItemsFromFolders(List<Folder> folderList, string sortString,
            int limitRequest, MediaItemFolderRequest request)
        {
            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems(
                "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                searchTokenSql.JoinSql +
                " WHERE FOLDERS.ID IN (" +
                String.Join(", ", folderList.Where(x => x.Id >= 0).Select(x => x.Id)) + ") " +
                (searchTokenSql.IsValid ? "AND " + searchTokenSql.WhereSql : "") +
                " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, sortString, limitRequest, request);
        }

        private List<MediaItem> GetDublicates(MediaItemDublicatesRequest request)
        {
            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems(
                "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                searchTokenSql.JoinSql +
                " WHERE MEDIAFILES.MD5VALUE IN (SELECT MD5VALUE FROM MEDIAFILES GROUP BY MD5VALUE HAVING COUNT(MD5VALUE) > 1) " +
                (searchTokenSql.IsValid ? "AND " + searchTokenSql.WhereSql : "") +
                " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, "", request.LimitRequest, request);
        }

        private List<MediaItem> GetMediaItemsFromFileRequest(MediaItemFilesRequest request)
        {
            if (request.FilesRequestType == MediaItemFilesRequestType.FilesNotExist)
            {
                List<int> itemIds = new List<int>();
                SearchTokenSql searchTokenSql = new SearchTokenSql(request);

                using (ICommandHelper command = this.MBCommand)
                {
                    if (searchTokenSql.ParameterList != null)
                    {
                        foreach (SqlParameterInfo parameter in searchTokenSql.ParameterList)
                        {
                            command.SetParameter(parameter.ParameterName, parameter.Value, parameter.EffectiveDbType);
                        }
                    }

                    using (DbDataReader reader = command.ExecuteReader(
                        "SELECT MEDIAFILES.ID, FILENAME, FOLDERNAME FROM FOLDERS " +
                        "INNER JOIN MEDIAFILES ON FOLDERS.ID=MEDIAFILES.FOLDERS_FK " +
                        searchTokenSql.JoinSql +
                        (searchTokenSql.IsValid ? " WHERE " + searchTokenSql.WhereSql : "") + " AND HISTORYVERSION=0 "))
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

                if (itemIds.Count == 0)
                {
                    return new List<MediaItem>();
                }

                return LoadMediaItems(
                    "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                    " WHERE MEDIAFILES.ID IN (" + String.Join(",", itemIds) + ") " +
                    " AND HISTORYVERSION=0 ", searchTokenSql.ParameterList, "", request.LimitRequest, request);
            }

            if (request.FilesRequestType == MediaItemFilesRequestType.FilesFromList)
            {
                List<MediaItem> mediaItems = new List<MediaItem>();
                foreach (string line in request.FileList)
                {
                    mediaItems.AddRange(GetMediaItemsFromFileName(line));
                }

                return mediaItems;
            }

            if (request.FilesRequestType == MediaItemFilesRequestType.FilesFromChecksum)
            {
                List<MediaItem> mediaItems = new List<MediaItem>();
                foreach (string line in request.FileList)
                {
                    mediaItems.AddRange(GetMediaItemsFromChecksum(line));
                }

                return mediaItems;
            }

            return null;
        }

        public override List<MediaItem> GetMediaItemsFromFolders(List<Folder> folderList, string sortString,
            int limitRequest)
        {
            return GetMediaItemsFromFolders(folderList, sortString, limitRequest, null);
        }

        public override List<MediaItem> GetMediaItemsWithMissingFiles(string sortString, int limitRequest)
        {
            List<int> missingIds = new List<int>();

            using (ICommandHelper command = this.MBCommand)
            using (DbDataReader reader = command.ExecuteReader(
                "SELECT MEDIAFILES.ID, FOLDERNAME, FILENAME FROM MEDIAFILES, FOLDERS " +
                "WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK"))
            {
                while (reader.Read())
                {
                    string fullName = MediaBrowserContext.MapPathRoot(reader.GetString(1)) + "\\" + reader.GetString(2);

                    if (!File.Exists(fullName))
                    {
                        missingIds.Add(reader.GetInt32(0));
                    }
                }
            }

            if (missingIds.Count == 0)
            {
                return new List<MediaItem>();
            }

            return LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND MEDIAFILES.ID IN ("
                + String.Join(",", missingIds) + ") AND HISTORYVERSION=0 ", sortString, limitRequest);
        }

        public override List<MediaItem> GetMediaItemsWithMissingThumbs(string sortString, int limitRequest)
        {
            List<int> missingIds = new List<int>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT MEDIAFILES.ID FROM VARIATIONS, MEDIAFILES WHERE MEDIAFILES.CURRENTVARIATION=VARIATIONS.ID " +
                    "AND VARIATIONS.ID NOT IN (SELECT VARIATIONS_FK FROM THUMBS WHERE PREVIEW_WIDTH=0 AND PREVIEW_HEIGHT=0)");

                foreach (DataRow row in table.Rows)
                {
                    missingIds.Add(Convert.ToInt32(row[0]));
                }
            }

            if (missingIds.Count == 0)
            {
                return new List<MediaItem>();
            }

            return LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND MEDIAFILES.ID IN ("
                + String.Join(",", missingIds) + ") AND HISTORYVERSION=0 ", sortString, limitRequest);
        }

        public override List<MediaItem> GetMediaItemsFromSearchToken(SearchToken searchToken, bool storeView,
            string sortString, int limitRequest)
        {
            return GetMediaItemsFromSearchToken(searchToken, storeView, sortString, limitRequest, null);
        }

        private List<MediaItem> GetMediaItemsFromSearchToken(SearchToken searchToken, bool storeView,
            string sortString, int limitRequest, MediaItemRequest mediaItemRequest)
        {
            SearchTokenSql searchTokenSql = new SearchTokenSql(searchToken);
            List<MediaItem> mediaItemList = new List<MediaItem>();

            if (!searchTokenSql.IsValid)
            {
                return mediaItemList;
            }

            using (ICommandHelper command = this.MBCommand)
            {
                List<SqlParameterInfo> parameterList = null;

                foreach (SqlParameterInfo parameter in searchTokenSql.ParameterList)
                {
                    command.SetParameter(parameter.ParameterName, parameter.Value, parameter.EffectiveDbType);

                    if (mediaItemRequest != null)
                    {
                        if (parameterList == null)
                        {
                            parameterList = new List<SqlParameterInfo>();
                        }

                        parameterList.Add(parameter);
                    }
                }

                string sql = "FROM VARIATIONS INNER JOIN MEDIAFILES ON VARIATIONS.ID=MEDIAFILES.CURRENTVARIATION " +
                    "INNER JOIN FOLDERS ON FOLDERS.ID=FOLDERS_FK " + searchTokenSql.JoinSql +
                    "WHERE " + searchTokenSql.WhereSql + " AND HISTORYVERSION=0 ";

                using (DbDataReader reader = command.ExecuteReader("SELECT" + SqlDefaultHead + sql +
                    "AND ROLES_FK IN (" + String.Join(",", MediaBrowserContext.ConnectedRoles.Select(x => x.Id)) + ") " +
                    "GROUP BY MEDIAFILES.ID " + sortString + " LIMIT " + limitRequest))
                {
                    AddMediaItemsToList(reader, ref mediaItemList);
                }

                if (mediaItemRequest != null)
                {
                    mediaItemRequest.MediaItemSqlRequest =
                        new MediaItemSqlRequest(sql, parameterList, sortString, limitRequest);
                }
            }

            return mediaItemList;
        }

        public override void GetThumbsForAllMediaItems(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            List<int> missingIds = new List<int>();
            Dictionary<int, MediaItem> mediaItems = new Dictionary<int, MediaItem>();

            foreach (MediaItem mediaItem in mediaItemList)
            {
                if (mediaItem.ThumbJpegData == null && !mediaItems.ContainsKey(mediaItem.VariationId))
                {
                    mediaItems.Add(mediaItem.VariationId, mediaItem);
                    missingIds.Add(mediaItem.VariationId);
                }
            }

            if (missingIds.Count == 0)
            {
                return;
            }

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT VARIATIONS_FK AS VID, THUMB FROM THUMBS WHERE PREVIEW_WIDTH=0 AND PREVIEW_HEIGHT=0 " +
                    "AND VARIATIONS_FK IN (" + String.Join(",", missingIds) + ")");

                foreach (DataRow row in table.Rows)
                {
                    int variationId = Convert.ToInt32(row["VID"]);

                    if (row["THUMB"] != DBNull.Value && mediaItems.ContainsKey(variationId))
                    {
                        mediaItems[variationId].ThumbJpegData = (byte[])row["THUMB"];
                    }
                }
            }
        }

        public override List<Folder> GetFolderlist()
        {
            List<Folder> folderList = new List<Folder>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT ID, FOLDERNAME, mediaitems.ItemCount FROM FOLDERS " +
                    "LEFT OUTER JOIN (SELECT COUNT(FOLDERS_FK) AS ItemCount, FOLDERS_FK FROM MEDIAFILES GROUP BY FOLDERS_FK) mediaitems " +
                    "ON mediaitems.FOLDERS_FK = FOLDERS.ID ORDER BY LOWER(FOLDERNAME)");

                foreach (DataRow row in table.Rows)
                {
                    Folder folder = new Folder
                    {
                        Id = Convert.ToInt32(row["ID"]),
                        FullPath = MediaBrowserContext.MapPathRoot(row["FOLDERNAME"].ToString())
                    };

                    if (row["ItemCount"] != DBNull.Value)
                    {
                        folder.ItemCount = Convert.ToInt32(row["ItemCount"]);
                    }

                    folderList.Add(folder);
                }
            }

            return folderList;
        }

        public override void CopyToFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList,
            MediaBrowser4.Objects.Folder folder)
        {
            if (mediaItemList.Count == 0)
            {
                return;
            }

            HashSet<Folder> oldFolders = new HashSet<Folder>();

            foreach (MediaItem mediaItem in mediaItemList)
            {
                Folder oldFolder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mediaItem.FolderId);

                if (oldFolder != null)
                {
                    oldFolder.ItemCount--;
                    oldFolders.Add(oldFolder);
                }

                mediaItem.FolderId = folder.Id;
                mediaItem.Foldername = folder.FullPath;
                mediaItem.FileObject = null;
                folder.ItemCount++;
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET FOLDERS_FK=" + folder.Id +
                                            " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");
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

        public override Dictionary<int, string> GetDublicates(MediaItem mediaItem, MediaItem.DublicateCriteria criteria)
        {
            Dictionary<int, string> duplicateList = new Dictionary<int, string>();

            using (ICommandHelper command = this.MBCommand)
            {
                switch (criteria)
                {
                    case MediaItem.DublicateCriteria.FILENAME:
                        command.SetParameter("@filename", mediaItem.Filename.ToLower(), DbType.String);
                        command.SetParameter("@id", mediaItem.Id, DbType.Int32);
                        using (DbDataReader reader = command.ExecuteReader(
                            "SELECT MEDIAFILES.ID AS MFID, FILENAME, FOLDERNAME FROM MEDIAFILES, FOLDERS " +
                            "WHERE LOWER(FILENAME)=@filename AND MEDIAFILES.ID<>@id AND FOLDERS_FK=FOLDERS.ID"))
                        {
                            while (reader.Read())
                            {
                                duplicateList.Add(Convert.ToInt32(reader["MFID"]),
                                    MediaBrowserContext.MapPathRoot(reader["FOLDERNAME"].ToString()) + "\\" + reader["FILENAME"]);
                            }
                        }
                        break;

                    case MediaItem.DublicateCriteria.CONTAINS:
                        command.SetParameter("@filename", "%" + mediaItem.Filename.ToLower() + "%", DbType.String);
                        command.SetParameter("@id", mediaItem.Id, DbType.Int32);
                        using (DbDataReader reader = command.ExecuteReader(
                            "SELECT MEDIAFILES.ID AS MFID, FILENAME, FOLDERNAME FROM MEDIAFILES, FOLDERS " +
                            "WHERE LOWER(FILENAME) LIKE @filename AND MEDIAFILES.ID<>@id AND FOLDERS_FK=FOLDERS.ID"))
                        {
                            while (reader.Read())
                            {
                                duplicateList.Add(Convert.ToInt32(reader["MFID"]),
                                    MediaBrowserContext.MapPathRoot(reader["FOLDERNAME"].ToString()) + "\\" + reader["FILENAME"]);
                            }
                        }
                        break;

                    default:
                        command.SetParameter("@md5", mediaItem.Md5Value, DbType.String);
                        command.SetParameter("@id", mediaItem.Id, DbType.Int32);
                        using (DbDataReader reader = command.ExecuteReader(
                            "SELECT MEDIAFILES.ID AS MFID, FILENAME, FOLDERNAME FROM MEDIAFILES, FOLDERS " +
                            "WHERE MD5VALUE=@md5 AND MEDIAFILES.ID<>@id AND FOLDERS_FK=FOLDERS.ID"))
                        {
                            while (reader.Read())
                            {
                                duplicateList.Add(Convert.ToInt32(reader["MFID"]),
                                    MediaBrowserContext.MapPathRoot(reader["FOLDERNAME"].ToString()) + "\\" + reader["FILENAME"]);
                            }
                        }
                        break;
                }
            }

            return duplicateList;
        }

        public override List<MediaItem> GetDublicates(List<MediaItem> mList, MediaItem.DublicateCriteria criteria)
        {
            List<MediaItem> returnList = new List<MediaItem>();

            using (ICommandHelper command = this.MBCommand)
            {
                foreach (MediaItem mediaItem in mList)
                {
                    switch (criteria)
                    {
                        case MediaItem.DublicateCriteria.CONTAINS:
                            command.SetParameter("@filename", "%" + mediaItem.Filename.ToLower() + "%", DbType.String);
                            break;

                        case MediaItem.DublicateCriteria.FILENAME:
                            command.SetParameter("@filename", mediaItem.Filename.ToLower(), DbType.String);
                            break;

                        default:
                            command.SetParameter("@md5", mediaItem.Md5Value, DbType.String);
                            break;
                    }

                    string sql = criteria == MediaItem.DublicateCriteria.CHECKSUM
                        ? "SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5"
                        : criteria == MediaItem.DublicateCriteria.FILENAME
                            ? "SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME)=@filename"
                            : "SELECT COUNT(ID) FROM MEDIAFILES WHERE LOWER(FILENAME) LIKE @filename";

                    if (command.ExecuteScalar<long>(sql) > 1)
                    {
                        returnList.Add(mediaItem);
                    }
                }
            }

            return returnList;
        }

        public override List<Role> GetRoleList()
        {
            List<Role> roleList = new List<Role>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT ID, `ROLE`, `PASSWORD`, DESCRIPTION FROM ROLES ORDER BY `ROLE`");

                foreach (DataRow row in table.Rows)
                {
                    roleList.Add(new Role(Convert.ToInt32(row["ID"]), row["ROLE"].ToString(),
                        row["PASSWORD"] == DBNull.Value ? null : row["PASSWORD"].ToString(),
                        row["DESCRIPTION"] == DBNull.Value ? null : row["DESCRIPTION"].ToString()));
                }
            }

            return roleList;
        }

        public override CategoryTree GetCategoryTree()
        {
            CategoryCollection categories = new CategoryCollection();
            Dictionary<int, Category> categoryDictionary = new Dictionary<int, Category>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT ID, NAME, PARENT, DESCRIPTION, SORTORDER, GUID, `DATE`, ISUNIQUE, ISDATE, ISLOCATION, " +
                    "LATITUDE, LONGITUDE, FULLPATH, items.ItemCount FROM CATEGORY LEFT JOIN " +
                    "(SELECT COUNT(c.VARIATIONS_FK) AS ItemCount, CATEGORY_FK FROM CATEGORIZE c " +
                    "INNER JOIN MEDIAFILES ON MEDIAFILES.CURRENTVARIATION = c.VARIATIONS_FK GROUP BY c.CATEGORY_FK) items " +
                    "ON items.CATEGORY_FK = ID ORDER BY LOWER(SORTORDER) ASC");

                foreach (DataRow row in table.Rows)
                {
                    Category category = GetCategoryFromDataRow(row);
                    categoryDictionary.Add(category.Id, category);
                }
            }

            foreach (Category category in categoryDictionary.Values)
            {
                if (category.ParentId <= 0)
                {
                    categories.Add(category);
                }
                else
                {
                    category.Parent = categoryDictionary[category.ParentId];
                }
            }

            return new CategoryTree(categories, categoryDictionary);
        }

        public override Dictionary<string, DateTime> LastAddedFolders()
        {
            Dictionary<string, DateTime> folders = new Dictionary<string, DateTime>();

            using (ICommandHelper command = this.MBCommand)
            using (DbDataReader reader = command.ExecuteReader(
                "SELECT FOLDERS.FOLDERNAME, MAX(MEDIAFILES.MEDIADATE) AS LASTDATE FROM FOLDERS, MEDIAFILES " +
                "WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK GROUP BY FOLDERS.FOLDERNAME HAVING COUNT(*) > 0 " +
                "ORDER BY LASTDATE DESC LIMIT 10"))
            {
                while (reader.Read())
                {
                    folders.Add(MediaBrowserContext.MapPathRoot(reader.GetString(0)), reader.GetDateTime(1));
                }
            }

            return folders;
        }

        public override List<Layer> GetLayers(Variation variation)
        {
            return GetLayers(variation.Id);
        }

        private List<Layer> GetLayers(int variationId)
        {
            List<Layer> layers = new List<Layer>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT ACTION, EDIT, `POSITION` FROM LAYERS WHERE VARIATIONS_FK=" + variationId +
                    " ORDER BY `POSITION`");

                foreach (DataRow row in table.Rows)
                {
                    layers.Add(new Layer(row["EDIT"].ToString(), row["ACTION"].ToString(),
                        Convert.ToInt32(row["POSITION"])));
                }
            }

            return layers;
        }

        public override byte[] GetThumbJpegData(int variationId)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@variationId", variationId, DbType.Int32);
                return command.ExecuteScalar<byte[]>(
                    "SELECT THUMB FROM THUMBS WHERE VARIATIONS_FK=@variationId " +
                    "AND PREVIEW_WIDTH=0 AND PREVIEW_HEIGHT=0");
            }
        }

        private static byte[] GetThumbJpegData(ITransaction transaction, int variationId)
        {
            using (DbDataReader reader = transaction.ExecuteReader(
                "SELECT THUMB FROM THUMBS WHERE VARIATIONS_FK=" + variationId +
                " AND PREVIEW_WIDTH=0 AND PREVIEW_HEIGHT=0"))
            {
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    return (byte[])reader[0];
                }
            }

            return null;
        }

        private static void GetGeoBounds(double longitude, double width, double latitude, double height,
            out double longitudeMin, out double longitudeMax, out double latitudeMin, out double latitudeMax)
        {
            GeoCoordinate start = new GeoCoordinate(latitude, longitude);
            GeoCoordinate end = new GeoCoordinate(latitude + height, longitude + width);

            if (start.GetDistanceTo(end) > 10000)
            {
                width = width / 2;
                height = height / 2;
            }

            longitudeMin = longitude - width;
            longitudeMax = longitude + width;
            latitudeMin = latitude - height;
            latitudeMax = latitude + height;

            if (longitudeMin > longitudeMax)
            {
                double swap = longitudeMin;
                longitudeMin = longitudeMax;
                longitudeMax = swap;
            }

            if (latitudeMin > latitudeMax)
            {
                double swap = latitudeMin;
                latitudeMin = latitudeMax;
                latitudeMax = swap;
            }
        }

        public override List<Category> GetCategoriesLocationGeoData(double longitude, double width, double latitude, double height)
        {
            List<Category> categories = new List<Category>();
            double longitudeMin, longitudeMax, latitudeMin, latitudeMax;
            GetGeoBounds(longitude, width, latitude, height, out longitudeMin, out longitudeMax, out latitudeMin, out latitudeMax);

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@lo1", longitudeMin, DbType.Double);
                command.SetParameter("@lo2", longitudeMax, DbType.Double);
                command.SetParameter("@la1", latitudeMin, DbType.Double);
                command.SetParameter("@la2", latitudeMax, DbType.Double);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT ID FROM CATEGORY c WHERE c.ISLOCATION = 1 " +
                    "AND (LONGITUDE BETWEEN @lo1 AND @lo2) AND (LATITUDE BETWEEN @la1 AND @la2) ORDER BY FULLPATH"))
                {
                    while (reader.Read())
                    {
                        Category category = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(Convert.ToInt32(reader["ID"]));

                        if (category != null && !category.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                        {
                            categories.Add(category);
                        }
                    }
                }
            }

            return categories;
        }

        public override List<Category> GetCategoriesDiaryGeoData(double longitude, double width, double latitude, double height)
        {
            List<Category> categories = new List<Category>();
            double longitudeMin, longitudeMax, latitudeMin, latitudeMax;
            GetGeoBounds(longitude, width, latitude, height, out longitudeMin, out longitudeMax, out latitudeMin, out latitudeMax);

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@lo1", longitudeMin, DbType.Double);
                command.SetParameter("@lo2", longitudeMax, DbType.Double);
                command.SetParameter("@la1", latitudeMin, DbType.Double);
                command.SetParameter("@la2", latitudeMax, DbType.Double);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT DISTINCT c.ID FROM CATEGORY c INNER JOIN CATEGORIZE z ON z.CATEGORY_FK=c.ID " +
                    "WHERE c.ISDATE = 1 AND c.FULLPATH LIKE 'Tagebuch%' " +
                    "AND DATE(c.`DATE`) IN (SELECT DISTINCT DATE(`LOCALTIME`) FROM DATALOGGER_GPS " +
                    "WHERE (LONGITUDE BETWEEN @lo1 AND @lo2) AND (LATITUDE BETWEEN @la1 AND @la2)) ORDER BY c.`DATE`"))
                {
                    while (reader.Read())
                    {
                        Category category = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(Convert.ToInt32(reader["ID"]));

                        if (category != null && !category.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                        {
                            categories.Add(category);
                        }
                    }
                }
            }

            return categories;
        }

        public override List<MediaItem> GetMediaItemsGeoData(double longitude, double width, double latitude, double height, int limitRequest)
        {
            double longitudeMin, longitudeMax, latitudeMin, latitudeMax;
            GetGeoBounds(longitude, width, latitude, height, out longitudeMin, out longitudeMax, out latitudeMin, out latitudeMax);

            List<Category> categoryList = GetCategoriesLocationGeoData(longitude, width, latitude, height);
            List<MediaItem> categoryItems = GetMediaItemsFromCategories(categoryList, false, true, String.Empty, limitRequest, null);

            string sql = "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND HISTORYVERSION=0 " +
                "AND (LONGITUDE BETWEEN " + longitudeMin.ToString(CultureInfo.InvariantCulture) +
                " AND " + longitudeMax.ToString(CultureInfo.InvariantCulture) + ") " +
                "AND (LATITUDE BETWEEN " + latitudeMin.ToString(CultureInfo.InvariantCulture) +
                " AND " + latitudeMax.ToString(CultureInfo.InvariantCulture) + ")";

            List<MediaItem> geoItems = LoadMediaItems(sql, null, String.Empty, limitRequest, null);
            categoryItems.RemoveAll(x => geoItems.Contains(x));

            return geoItems.Union(categoryItems).Distinct().OrderBy(x => x.MediaDate).ToList();
        }

        public override Dictionary<Category, int> GetCategoriesFromMediaItems(List<MediaItem> mItemList)
        {
            Dictionary<Category, int> categoryDictionary = new Dictionary<Category, int>();

            if (mItemList == null || mItemList.Count == 0)
            {
                return categoryDictionary;
            }

            using (ICommandHelper command = this.MBCommand)
            using (DbDataReader reader = command.ExecuteReader(
                "SELECT CATEGORY.ID, COUNT(CATEGORY.ID) AS CNT FROM CATEGORY " +
                "INNER JOIN CATEGORIZE ON CATEGORIZE.CATEGORY_FK=CATEGORY.ID " +
                "WHERE CATEGORIZE.VARIATIONS_FK IN (" + String.Join(",", mItemList.Select(x => x.VariationId)) + ") " +
                "GROUP BY CATEGORY.ID"))
            {
                while (reader.Read())
                {
                    Category category = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(Convert.ToInt32(reader["ID"]));

                    if (category != null && !category.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                    {
                        categoryDictionary.Add(category, Convert.ToInt32(reader["CNT"]));
                    }
                }
            }

            return categoryDictionary;
        }

        public override List<MediaItem> GetMediaItemsFromCategories(List<Category> categoryList,
            bool isIntersection, bool isRecursive, string sortString, int limitRequest)
        {
            return GetMediaItemsFromCategories(categoryList, isIntersection, isRecursive, sortString, limitRequest, null);
        }

        private List<MediaItem> GetMediaItemsFromCategories(List<Category> categoryList,
            bool isIntersection, bool isRecursive, string sortString, int limitRequest, MediaItemCategoryRequest request)
        {
            if (categoryList == null || categoryList.Count == 0)
            {
                return new List<MediaItem>();
            }

            Dictionary<Category, List<Category>> categoryUniqueTrees = new Dictionary<Category, List<Category>>();
            Dictionary<Category, List<Category>> uniqueNodes = new Dictionary<Category, List<Category>>();

            if (isIntersection)
            {
                foreach (Category category in categoryList)
                {
                    if (category.IsUnique && !categoryUniqueTrees.ContainsKey(category.UniqueRoot))
                    {
                        categoryUniqueTrees.Add(category.UniqueRoot, category.UniqueRoot.AllChildrenRecursive());
                    }
                }

                foreach (Category category in categoryList)
                {
                    bool isUnion = false;

                    foreach (KeyValuePair<Category, List<Category>> pair in categoryUniqueTrees)
                    {
                        if (pair.Value.Contains(category))
                        {
                            if (!uniqueNodes.ContainsKey(pair.Key))
                            {
                                uniqueNodes[pair.Key] = new List<Category>();
                            }

                            uniqueNodes[pair.Key].Add(category);
                            isUnion = true;
                            break;
                        }
                    }

                    if (!isUnion)
                    {
                        uniqueNodes[category] = null;
                    }
                }
            }
            else
            {
                foreach (Category category in categoryList)
                {
                    uniqueNodes[category] = null;
                }
            }

            List<string> sqlList = new List<string>();

            foreach (KeyValuePair<Category, List<Category>> pair in uniqueNodes)
            {
                if (pair.Value == null)
                {
                    sqlList.Add("SELECT VARIATIONS_FK FROM CATEGORIZE WHERE CATEGORIZE.CATEGORY_FK IN ("
                        + (isRecursive ? ConcatCategoryIds(pair.Key.AllChildrenRecursive()) : pair.Key.Id.ToString()) + ")");
                }
                else
                {
                    StringBuilder union = new StringBuilder();

                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        union.AppendLine("SELECT VARIATIONS_FK FROM CATEGORIZE WHERE CATEGORIZE.CATEGORY_FK IN ("
                            + (isRecursive
                                ? ConcatCategoryIds(pair.Value[i].AllChildrenRecursive())
                                : pair.Value[i].Id.ToString()) + ")");

                        if (i < pair.Value.Count - 1)
                        {
                            union.AppendLine("UNION");
                        }
                    }

                    sqlList.Add(union.ToString());
                }
            }

            StringBuilder allVariations = new StringBuilder();

            for (int i = 0; i < sqlList.Count; i++)
            {
                allVariations.AppendLine("SELECT VARIATIONS_FK FROM (" + sqlList[i] + ") AS variation_set_" + i);

                if (i < sqlList.Count - 1)
                {
                    allVariations.AppendLine(isIntersection ? "INTERSECT" : "UNION");
                }
            }

            string allVariationsSqlString = "(SELECT VARIATIONS_FK FROM (" + allVariations + ") AS variation_result)";

            SearchTokenSql searchTokenSql = new SearchTokenSql(request);

            return LoadMediaItems(
                ", VARIATIONS.ID AS VARIATIONSID FROM VARIATIONS " +
                "INNER JOIN MEDIAFILES ON VARIATIONS.MEDIAFILES_FK=MEDIAFILES.ID " +
                "INNER JOIN FOLDERS ON FOLDERS.ID=MEDIAFILES.FOLDERS_FK " +
                "INNER JOIN CATEGORIZE ON CATEGORIZE.VARIATIONS_FK=VARIATIONS.ID " +
                searchTokenSql.JoinSqlCategory +
                "WHERE " + (request != null && request.Header.EndsWith(" (!)") ? "MEDIAFILES.ISBOOKMARKED = 0 AND " : String.Empty) +
                "VARIATIONS.ID IN " + allVariationsSqlString +
                (searchTokenSql.IsValid ? " AND " + searchTokenSql.WhereSql : "") +
                " AND HISTORYVERSION=0", searchTokenSql.ParameterList, sortString, limitRequest, request);
        }

        private List<MediaItem> GetMediaItemsFromSortRequest(MediaItemSortRequest sortRequest)
        {
            List<string> sortList = new List<string>();

            if (sortRequest.ShuffleType == MediaItemRequestShuffleType.NONE)
            {
                foreach (Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection> sortTuple in sortRequest.SortTypeList)
                {
                    sortList.Add("VIEWED" + (sortTuple.Item2 == MediaItemRequestSortDirection.DESCENDING ? " DESC" : ""));
                }
            }
            else
            {
                sortList.Add("RAND()");
            }

            SearchTokenSql searchTokenSql = new SearchTokenSql(sortRequest);

            string mediaItemListSql = String.Empty;

            if (sortRequest.MediaItemList != null && sortRequest.MediaItemList.Count > 0)
            {
                mediaItemListSql = "MEDIAFILES.ID IN (" + ConcatMediaItemIDs(sortRequest.MediaItemList) + ") AND ";
            }

            return LoadMediaItems(
                "FROM MEDIAFILES INNER JOIN FOLDERS ON FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                searchTokenSql.JoinSql +
                " WHERE " + mediaItemListSql +
                (searchTokenSql.IsValid ? searchTokenSql.WhereSql + " AND " : "") +
                "HISTORYVERSION=0 ", searchTokenSql.ParameterList,
                "ORDER BY " + String.Join(", ", sortList), sortRequest.LimitRequest, sortRequest);
        }

        public override List<MediaItem> GetMediaItemsFromTrashfolder(string sortString, int limitRequest)
        {
            return LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND HISTORYVERSION=0 " +
                "AND MEDIAFILES.ISDELETED=1 ", sortString, limitRequest);
        }

        public override List<MediaItem> GetMediaItemsFromBookmarks(string sortString, int limitRequest)
        {
            return LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK AND HISTORYVERSION=0 " +
                "AND MEDIAFILES.ISBOOKMARKED=1 ", sortString, limitRequest);
        }

        public override List<MediaItem> GetMediaItemsFromChecksum(string checksum)
        {
            List<SqlParameterInfo> parameterList = new List<SqlParameterInfo>
            {
                new SqlParameterInfo("@MD5VALUE", Path.GetFileName(checksum).ToLower().Trim(), DbType.String)
            };

            return LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                "AND MD5VALUE=LOWER(@MD5VALUE) AND HISTORYVERSION=0 ", parameterList, String.Empty, 1, null);
        }

        public override List<MediaItem> GetMediaItemsFromFileName(string filename)
        {
            List<SqlParameterInfo> parameterList = new List<SqlParameterInfo>
            {
                new SqlParameterInfo("@FILENAME", Path.GetFileName(filename).ToLower().Trim(), DbType.String)
            };

            return LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                "AND FILENAME=LOWER(@FILENAME) AND HISTORYVERSION=0 ", parameterList, String.Empty, 1, null);
        }

        public override MediaItem GetMediaItemFromFile(string filename)
        {
            List<SqlParameterInfo> parameterList = new List<SqlParameterInfo>
            {
                new SqlParameterInfo("@FOLDERNAME",
                    MediaBrowserContext.MapPathRootReverse(Path.GetDirectoryName(filename)), DbType.String),
                new SqlParameterInfo("@FILENAME", Path.GetFileName(filename), DbType.String)
            };

            List<MediaItem> result = LoadMediaItems(
                "FROM MEDIAFILES, FOLDERS WHERE FOLDERS.ID = MEDIAFILES.FOLDERS_FK " +
                "AND FOLDERNAME=@FOLDERNAME AND FILENAME=@FILENAME AND HISTORYVERSION=0 ",
                parameterList, String.Empty, 1, null);

            return result.Count == 1 ? result[0] : null;
        }

        public override void SetBookmark(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            mediaItemList = mediaItemList.ToList();

            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET ISBOOKMARKED=" + (set ? "1" : "0") +
                                            " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");

                foreach (MediaItem mediaItem in mediaItemList)
                {
                    mediaItem.IsBookmarked = set;

                    if (set)
                    {
                        if (!MediaBrowserContext.BookmarkedSingleton.Contains(mediaItem))
                        {
                            MediaBrowserContext.BookmarkedSingleton.Add(mediaItem);
                        }
                    }
                    else if (MediaBrowserContext.BookmarkedSingleton.Contains(mediaItem))
                    {
                        MediaBrowserContext.BookmarkedSingleton.Remove(mediaItem);
                    }
                }
            }
        }

        public override void SetDeleted(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDELETED=" + (set ? "1" : "0") +
                                            " WHERE ID IN(" + ConcatMediaItemIDs(mediaItemList) + ")");

                foreach (MediaItem mediaItem in mediaItemList.ToArray())
                {
                    mediaItem.IsDeleted = set;

                    if (set)
                    {
                        if (!MediaBrowserContext.DeletedSingleton.Contains(mediaItem))
                        {
                            MediaBrowserContext.DeletedSingleton.Add(mediaItem);
                        }
                    }
                    else if (MediaBrowserContext.DeletedSingleton.Contains(mediaItem))
                    {
                        MediaBrowserContext.DeletedSingleton.Remove(mediaItem);
                    }
                }
            }
        }

        public override void UpdateDublicate(MediaBrowser4.Objects.MediaItem mediaItem)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.SetParameter("@md5", mediaItem.Md5Value, DbType.String);

                if (transaction.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5") <= 1)
                {
                    transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0 WHERE ID=" + mediaItem.Id);
                    mediaItem.IsMd5Dublicate = false;
                }
            }
        }

        public override void SetGeodata(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (MediaItem mediaItem in mediaItemList)
                {
                    transaction.SetParameter("@LONGITUDE", mediaItem.Longitude, DbType.Double);
                    transaction.SetParameter("@LATITUDE", mediaItem.Latitude, DbType.Double);
                    transaction.SetParameter("@ID", mediaItem.Id, DbType.Int32);
                    transaction.ExecuteNonQuery(
                        "UPDATE MEDIAFILES SET LONGITUDE=@LONGITUDE, LATITUDE=@LATITUDE WHERE ID=@ID");
                }
            }
        }

        public override void ReplaceVariations(Dictionary<MediaItem, List<Variation>> mediaItemDic)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (KeyValuePair<MediaItem, List<Variation>> pair in mediaItemDic)
                {
                    if (pair.Value == null || pair.Value.Count == 0)
                    {
                        throw new Exception("Variationlist is empty!");
                    }

                    transaction.ExecuteNonQuery("DELETE FROM VARIATIONS WHERE MEDIAFILES_FK=" + pair.Key.Id);

                    long defaultVariationId = 0;
                    long lastVariationId = 0;

                    foreach (Variation newVariation in pair.Value)
                    {
                        transaction.SetParameter("@NAME", newVariation.Name, DbType.String);
                        transaction.ExecuteNonQuery(
                            "INSERT INTO VARIATIONS (MEDIAFILES_FK, NAME) VALUES (" + pair.Key.Id + ", @NAME)");

                        lastVariationId = transaction.ExecuteScalar<long>("SELECT LAST_INSERT_ID()");

                        if (newVariation.Id == pair.Key.VariationId)
                        {
                            defaultVariationId = lastVariationId;
                        }

                        if (newVariation.Layers != null)
                        {
                            foreach (Layer layer in newVariation.Layers)
                            {
                                transaction.SetParameter("@ACTION", layer.Action, DbType.String);
                                transaction.SetParameter("@EDIT", layer.Edit, DbType.String);
                                transaction.ExecuteNonQuery(
                                    "INSERT INTO LAYERS (VARIATIONS_FK, `POSITION`, EDIT, ACTION) VALUES(" +
                                    lastVariationId + ", " + layer.Position + ", @EDIT, @ACTION)");
                            }
                        }

                        if (newVariation.ThumbJpegData != null && newVariation.ThumbJpegData.Length > 0)
                        {
                            transaction.SetParameter("@thumb", newVariation.ThumbJpegData, DbType.Binary);
                            transaction.ExecuteNonQuery(
                                "INSERT INTO THUMBS (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, THUMB) " +
                                "VALUES (" + lastVariationId + ", 0, 0, @thumb)");
                        }
                    }

                    if (defaultVariationId == 0)
                    {
                        defaultVariationId = lastVariationId;
                    }

                    transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET ORIENTATION=" +
                                                (int)pair.Key.Orientation + ", CURRENTVARIATION=" +
                                                defaultVariationId + " WHERE ID=" + pair.Key.Id);
                }
            }
        }

        public override void SetCategory(Category category)
        {
            if (String.IsNullOrWhiteSpace(category.Name))
            {
                throw new Exception("Es muss ein Name vergeben werden.");
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                if (String.IsNullOrWhiteSpace(category.Sortname))
                {
                    category.Sortname = category.Name;
                }

                transaction.SetParameter("@name", category.Name.Trim(), DbType.String);
                transaction.SetParameter("@sort", category.Sortname.Trim(), DbType.String);
                transaction.SetParameter("@desc",
                    category.Description == null ? (object)DBNull.Value : category.Description.Trim(),
                    DbType.String);
                transaction.SetParameter("@fullpath",
                    category.FullPath == null ? (object)DBNull.Value : category.FullPath.Trim(), DbType.String);
                transaction.SetParameter("@date", category.Date, DbType.DateTime);
                transaction.SetParameter("@LATITUDE", category.Latitude, DbType.Double);
                transaction.SetParameter("@LONGITUDE", category.Longitude, DbType.Double);

                long nameExists = transaction.ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM CATEGORY WHERE LOWER(NAME)=LOWER(@name) AND PARENT=" +
                    (category.ParentId <= 0 ? 0 : category.ParentId) +
                    (category.Id <= 0 ? "" : " AND ID<>" + category.Id));

                if (nameExists > 0)
                {
                    throw new Exception("Der Name ist auf dieser Ebene schon vergeben: " + category.Name);
                }

                if (category.Id <= 0)
                {
                    transaction.ExecuteNonQuery(
                        "INSERT INTO CATEGORY (SORTORDER, NAME, DESCRIPTION, PARENT, GUID, `DATE`, ISUNIQUE, " +
                        "LATITUDE, LONGITUDE, ISDATE, ISLOCATION, FULLPATH) VALUES (" +
                        "@sort, @name, @desc, " + (category.ParentId <= 0 ? 0 : category.ParentId) + ", '" +
                        System.Guid.NewGuid().ToString("N") + "', @date, " + (category.IsUnique ? "1" : "0") + ", " +
                        "@LATITUDE, @LONGITUDE, " + (category.IsDate ? "1" : "0") + ", " +
                        (category.IsLocation ? "1" : "0") + ", @fullpath)");

                    category.Id = transaction.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");

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
                    transaction.ExecuteNonQuery(
                        "UPDATE CATEGORY SET SORTORDER=@sort, NAME=@name, DESCRIPTION=@desc, FULLPATH=@fullpath, " +
                        "ISDATE=" + (category.IsDate ? "1" : "0") + ", ISLOCATION=" +
                        (category.IsLocation ? "1" : "0") + ", LATITUDE=@LATITUDE, LONGITUDE=@LONGITUDE, " +
                        "`DATE`=@date, PARENT=" + (category.ParentId <= 0 ? 0 : category.ParentId) +
                        ", ISUNIQUE=" + (category.IsUnique ? "1" : "0") + " WHERE ID=" + category.Id);

                    if (category.IsUniqueChanged)
                    {
                        category.IsUniqueChanged = false;
                        UpdateCategoryFlag(transaction, category, "ISUNIQUE", category.IsUnique);
                    }

                    if (category.IsLocationChanged)
                    {
                        category.IsLocationChanged = false;
                        UpdateCategoryFlag(transaction, category, "ISLOCATION", category.IsLocation);
                    }

                    if (category.IsDateChanged)
                    {
                        category.IsDateChanged = false;
                        UpdateCategoryFlag(transaction, category, "ISDATE", category.IsDate);
                    }
                }
            }
        }

        private static void UpdateCategoryFlag(ITransaction transaction, Category category, string column,
            bool value)
        {
            List<Category> categories = category.AllChildrenRecursive();

            if (categories.Count <= 1)
            {
                return;
            }

            if (column == "ISUNIQUE")
            {
                categories.ForEach(x => x.IsUnique = value);
            }
            else if (column == "ISLOCATION")
            {
                categories.ForEach(x => x.IsLocation = value);
            }
            else
            {
                categories.ForEach(x => x.IsDate = value);
            }

            transaction.ExecuteNonQuery("UPDATE CATEGORY SET " + column + "=" + (value ? "1" : "0") +
                                        " WHERE ID IN (" + ConcatCategoryIds(categories) + ")");
        }

        public override void SetFolder(Folder folder)
        {
            if (String.IsNullOrWhiteSpace(folder.Name))
            {
                throw new Exception("Es muss ein Name vergeben werden.");
            }

            using (ICommandHelper command = this.MBCommand)
            {
                string newName = MediaBrowserContext.MapPathRootReverse(
                    FilesAndFolders.CleanPath(folder.FullPath));

                command.SetParameter("@FOLDERNAME", newName, DbType.String);

                if (folder.Id == 0)
                {
                    command.ExecuteNonQuery("INSERT IGNORE INTO FOLDERS (FOLDERNAME) VALUES (@FOLDERNAME)");
                    folder.Id = command.ExecuteScalar<int>(
                        "SELECT ID FROM FOLDERS WHERE FOLDERNAME=@FOLDERNAME");
                }
                else
                {
                    string oldName;

                    if (folder.Id < 0)
                    {
                        oldName = folder.oldName;
                    }
                    else
                    {
                        command.SetParameter("@folderId", folder.Id, DbType.Int32);
                        oldName = MediaBrowserContext.MapPathRoot(command.ExecuteScalar<string>(
                            "SELECT FOLDERNAME FROM FOLDERS WHERE ID=@folderId"));
                    }

                    oldName = FilesAndFolders.CleanPath(oldName);
                    string newPath = FilesAndFolders.CleanPath(folder.FullPath);

                    command.SetParameter("@folderId", folder.Id, DbType.Int32);
                    command.SetParameter("@newName", newName, DbType.String);
                    command.ExecuteNonQuery("UPDATE FOLDERS SET FOLDERNAME=@newName WHERE ID=@folderId");

                    DataTable table = command.GetDataTable("SELECT ID, FOLDERNAME FROM FOLDERS");
                    List<int> renamedFolders = new List<int> {folder.Id};

                    foreach (DataRow row in table.Rows)
                    {
                        int id = Convert.ToInt32(row["ID"]);

                        if (id == folder.Id)
                        {
                            continue;
                        }

                        string folderName = MediaBrowserContext.MapPathRoot(row["FOLDERNAME"].ToString());

                        if (folderName.StartsWith(oldName + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            string suffix = folderName.Substring(oldName.Length);
                            command.SetParameter("@renamed", MediaBrowserContext.MapPathRootReverse(newPath + suffix),
                                DbType.String);
                            command.SetParameter("@renamedId", id, DbType.Int32);
                            command.ExecuteNonQuery("UPDATE FOLDERS SET FOLDERNAME=@renamed WHERE ID=@renamedId");
                            renamedFolders.Add(id);
                        }
                    }

                    foreach (MediaItem mediaItem in MediaBrowserContext.GlobalMediaItemCache.Values)
                    {
                        if (renamedFolders.Contains(Convert.ToInt32(mediaItem.FolderId)))
                        {
                            mediaItem.FileObject = null;
                            Folder cachedFolder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(
                                Convert.ToInt32(mediaItem.FolderId));

                            if (cachedFolder != null)
                            {
                                mediaItem.Foldername = cachedFolder.FullPath;
                            }
                        }
                    }
                }
            }
        }

        public override MediaBrowser4.Objects.Category SetCategoryByPath(string catPath, string catSortPath)
        {
            catPath = catPath.Replace('/', '\\').Replace("\\\\", "\\").TrimEnd('\\');
            catSortPath = catSortPath.Replace('/', '\\').Replace("\\\\", "\\").TrimEnd('\\');

            if (String.IsNullOrEmpty(catPath) || String.IsNullOrEmpty(catSortPath) ||
                catPath.Split('\\').Length != catSortPath.Split('\\').Length)
            {
                return null;
            }

            Category parentCategory = null;
            Category category = null;

            string[] catStrings = catPath.Split('\\');
            string[] catSortStrings = catSortPath.Split('\\');
            DateTime? date = null;

            if (catSortStrings.Length >= 3
                && Int32.TryParse(catSortStrings[catSortStrings.Length - 1], out int day)
                && Int32.TryParse(catSortStrings[catSortStrings.Length - 2], out int month)
                && Int32.TryParse(catSortStrings[catSortStrings.Length - 3], out int year))
            {
                date = new DateTime(year, month, day);
            }

            CategoryCollection categoryCollection = MediaBrowserContext.CategoryTreeSingelton.Children;

            for (int i = 0; i < catSortStrings.Length; i++)
            {
                category = categoryCollection.FirstOrDefault(x =>
                    x.Name.ToLower().Trim() == catStrings[i].ToLower().Trim());

                if (category == null)
                {
                    category = new Category
                    {
                        IsDate = true,
                        name = catStrings[i],
                        Description = "",
                        Sortname = catSortStrings[i]
                    };

                    if (date != null)
                    {
                        if (i == catSortStrings.Length - 1)
                        {
                            category.Date = date.Value;
                        }
                        else if (i == catSortStrings.Length - 2)
                        {
                            category.Date = new DateTime(date.Value.Year, date.Value.Month, 1);
                        }
                        else if (i == catSortStrings.Length - 3)
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
                    categoryCollection = category.Children;
                }
                else
                {
                    categoryCollection = category.Children;
                }

                parentCategory = category;
            }

            return category;
        }

        public override bool RemoveCategory(MediaBrowser4.Objects.Category category)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                if (transaction.ExecuteScalar<long>("SELECT COUNT(ID) FROM CATEGORY WHERE PARENT=" +
                                                    category.Id) == 0)
                {
                    transaction.ExecuteNonQuery("DELETE FROM CATEGORY WHERE ID=" + category.Id);
                    return true;
                }
            }

            return false;
        }

        public override void RemoveFolder(Folder folder)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                List<string> md5Values = new List<string>();

                using (DbDataReader reader = transaction.ExecuteReader(
                    "SELECT ID, MD5VALUE FROM MEDIAFILES WHERE FOLDERS_FK=" + folder.Id))
                {
                    while (reader.Read())
                    {
                        MediaBrowserContext.GlobalMediaItemCache.Remove(ReadInt32(reader, 0));
                        md5Values.Add(reader.GetString(1).Trim());
                    }
                }

                transaction.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE FOLDERS_FK=" + folder.Id);
                transaction.ExecuteNonQuery("DELETE FROM FOLDERS WHERE ID=" + folder.Id);

                foreach (string md5 in md5Values)
                {
                    transaction.SetParameter("@md5", md5, DbType.String);

                    if (transaction.ExecuteScalar<long>("SELECT COUNT(ID) FROM MEDIAFILES WHERE MD5VALUE=@md5") == 1)
                    {
                        int id = transaction.ExecuteScalar<int>("SELECT ID FROM MEDIAFILES WHERE MD5VALUE=@md5");
                        transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET ISDUBLICATE=0 WHERE ID=" + id);

                        if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(id))
                        {
                            MediaBrowserContext.GlobalMediaItemCache[id].IsMd5Dublicate = false;
                        }
                    }
                }
            }

            folder.SetVirtual();
        }

        public override int CountItemsInFolder(Folder folder)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@folderId", folder.Id, DbType.Int32);
                return Convert.ToInt32(command.ExecuteScalar<long>(
                    "SELECT COUNT(ID) FROM MEDIAFILES WHERE FOLDERS_FK=@folderId"));
            }
        }

        public override void AddViewTime(MediaBrowser4.Objects.MediaItem mediaItem, int seconds)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                command.ExecuteNonQuery("UPDATE MEDIAFILES SET VIEWED=VIEWED+" + seconds +
                                        " WHERE ID=" + mediaItem.Id);
            }

            mediaItem.Viewed += seconds;
        }

        public override void SetTrashFolder(List<MediaBrowser4.Objects.MediaItem> mediaItemList, bool set)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.SetParameter("@NOWDATE", DateTime.Now, DbType.DateTime);
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET DELETEDATE=" + (set ? "@NOWDATE" : "NULL") +
                                            ", ISDELETED=" + (set ? "1" : "0") + " WHERE ID IN(" +
                                            ConcatMediaItemIDs(mediaItemList) + ")");
            }
        }

        public override void AdjustMediaDate(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (MediaItem mediaItem in mediaItemList)
                {
                    transaction.SetParameter("@MEDIADATE", mediaItem.MediaDate, DbType.DateTime);
                    transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET MEDIADATE=@MEDIADATE WHERE ID=" +
                                                mediaItem.Id);
                }
            }
        }

        public override void RemoveFromDB(List<MediaBrowser4.Objects.MediaItem> mediaItemList)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("DELETE FROM MEDIAFILES WHERE ID IN (" +
                                            ConcatMediaItemIDs(mediaItemList) + ")");

                foreach (MediaItem mediaItem in mediaItemList.ToList())
                {
                    MediaBrowserContext.GlobalMediaItemCache.Remove(mediaItem);
                    UpdateDublicates(mediaItem, transaction);

                    Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderById(mediaItem.FolderId);

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

        public override List<MediaItem> GetMediaItemsForRequestTicket(long requestTicket, string sortString, int limitRequest)
        {
            if (!this.requestArchiv.ContainsKey(requestTicket))
            {
                return null;
            }

            string sql = this.requestArchiv[requestTicket];
            this.requestArchiv.Remove(requestTicket);
            return LoadMediaItems(sql, sortString, limitRequest);
        }

        public override List<string> GetCategoryIdentifiersFromMediaItem(MediaItem mItem)
        {
            List<string> categoryIds = new List<string>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT CATEGORY_FK FROM CATEGORIZE WHERE VARIATIONS_FK=" + mItem.VariationId);

                foreach (DataRow row in table.Rows)
                {
                    categoryIds.Add(row[0].ToString());
                }
            }

            return categoryIds;
        }

        public override List<string> GetMetadataKeyList()
        {
            List<string> list = new List<string>();

            using (ICommandHelper command = this.MBCommand)
            {
                DataTable table = command.GetDataTable(
                    "SELECT DISTINCT NAME FROM METADATANAME ORDER BY NAME");

                foreach (DataRow row in table.Rows)
                {
                    list.Add(row["NAME"].ToString());
                }
            }

            return list;
        }

        public override List<MediaBrowser4.Objects.Layer> GetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            return GetLayers(mItem.VariationId);
        }

        public override void SetLayersForMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                SetLayersForMediaItem(mItem, transaction);
            }
        }

        public override void SetLayersForMediaItems(List<MediaItem> mItemList)
        {
            if (mItemList.Count == 0)
            {
                return;
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (MediaItem mItem in mItemList)
                {
                    SetLayersForMediaItem(mItem, transaction);
                    mItem.IsThumbJpegDataOutdated = true;
                }
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.SetParameter("@outdated", true, DbType.Boolean);
                transaction.ExecuteNonQuery("UPDATE THUMBS SET ISOUTDATED=@outdated WHERE VARIATIONS_FK IN (" +
                                            String.Join(",", mItemList.Select(x => x.VariationId)) + ")");
            }
        }

        private static void SetLayersForMediaItem(MediaItem mItem, ITransaction transaction)
        {
            transaction.ExecuteNonQuery("DELETE FROM LAYERS WHERE VARIATIONS_FK=" + mItem.VariationId);

            foreach (Layer layer in mItem.Layers)
            {
                transaction.SetParameter("@action", layer.Action, DbType.String);
                transaction.SetParameter("@edit", layer.Edit, DbType.String);
                transaction.ExecuteNonQuery(
                    "INSERT INTO LAYERS (ACTION, EDIT, `POSITION`, VARIATIONS_FK) " +
                    "VALUES (@action, @edit, " + layer.Position + ", " + mItem.VariationId + ")");
            }
        }

        public override void CategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList,
            List<MediaBrowser4.Objects.Category> cList)
        {
            CategorizeMediaItems(mList, cList, false);
        }

        public override void UnCategorizeMediaItems(List<MediaBrowser4.Objects.MediaItem> mList,
            List<MediaBrowser4.Objects.Category> cList)
        {
            CategorizeMediaItems(mList, cList, true);
        }

        private void CategorizeMediaItems(List<MediaItem> mediaItems,
            List<Category> categoryChangeList, bool removeCategory)
        {
            if (categoryChangeList.Count == 0 || mediaItems.Count == 0)
            {
                return;
            }

            Dictionary<Category, List<Category>> uniqueTrees = null;

            if (!removeCategory)
            {
                foreach (Category category in categoryChangeList)
                {
                    if (!category.IsUnique)
                    {
                        continue;
                    }

                    Category parent = category.Parent;
                    Category uniqueRoot = category;

                    while (parent != null && parent.IsUnique)
                    {
                        uniqueRoot = parent;
                        parent = parent.Parent;
                    }

                    List<Category> uniqueTree = uniqueRoot.AllChildrenRecursive();

                    if (uniqueTree.Count > 1)
                    {
                        if (uniqueTrees == null)
                        {
                            uniqueTrees = new Dictionary<Category, List<Category>>();
                        }

                        uniqueTrees.Add(category, uniqueTree);
                    }
                }

                if (uniqueTrees != null)
                {
                    foreach (List<Category> tree in uniqueTrees.Values)
                    {
                        int count = categoryChangeList.Count(tree.Contains);

                        if (count > 1)
                        {
                            throw new Exception(
                                "Sie versuchen mehr als eine Kategorie in einem als 'Eindeutig' markierten Teilbaum zu erstellen.");
                        }
                    }
                }
            }

            List<Category> categoryUpNodes = new List<Category>();

            foreach (Category category in categoryChangeList)
            {
                Category parent = category.Parent;

                while (parent != null)
                {
                    if (!categoryUpNodes.Contains(parent))
                    {
                        categoryUpNodes.Add(parent);
                    }

                    parent = parent.Parent;
                }
            }

            categoryChangeList.RemoveAll(categoryUpNodes.Contains);

            string upNodeIds = ConcatCategoryIds(categoryUpNodes);
            string changeIds = ConcatCategoryIds(categoryChangeList);
            string variationIds = ConcatVariationIDs(mediaItems);

            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("DELETE FROM CATEGORIZE WHERE CATEGORY_FK IN (" + changeIds +
                                            (upNodeIds.Length > 0 ? "," + upNodeIds : "") +
                                            ") AND VARIATIONS_FK IN (" + variationIds + ")");

                if (!removeCategory)
                {
                    foreach (MediaItem mediaItem in mediaItems)
                    {
                        foreach (Category category in categoryChangeList)
                        {
                            if (transaction.ExecuteScalar<long>(
                                    "SELECT COUNT(*) FROM CATEGORIZE WHERE CATEGORY_FK=" + category.Id +
                                    " AND VARIATIONS_FK=" + mediaItem.VariationId) == 0)
                            {
                                transaction.ExecuteNonQuery(
                                    "INSERT INTO CATEGORIZE (CATEGORY_FK, VARIATIONS_FK) VALUES (" +
                                    category.Id + "," + mediaItem.VariationId + ")");
                            }
                        }
                    }
                }

                foreach (MediaItem mediaItem in mediaItems)
                {
                    List<int> categoryIds = new List<int>();

                    using (DbDataReader reader = transaction.ExecuteReader(
                        "SELECT CATEGORY_FK FROM CATEGORIZE WHERE VARIATIONS_FK=" + mediaItem.VariationId))
                    {
                        while (reader.Read())
                        {
                            categoryIds.Add(ReadInt32(reader, 0));
                        }
                    }

                    mediaItem.Categories.Clear();

                    foreach (int categoryId in categoryIds)
                    {
                        Category category = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(categoryId);

                        if (category != null && !category.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                        {
                            mediaItem.Categories.Add(category);
                        }
                    }

                    if (removeCategory)
                    {
                        continue;
                    }

                    List<Category> existingDownNodes = new List<Category>();

                    foreach (Category category in mediaItem.Categories)
                    {
                        if (categoryChangeList.Contains(category))
                        {
                            continue;
                        }

                        Category parent = category.Parent;

                        while (parent != null)
                        {
                            if (categoryChangeList.Contains(parent))
                            {
                                existingDownNodes.Add(category);
                            }

                            parent = parent.Parent;
                        }
                    }

                    if (existingDownNodes.Count > 0)
                    {
                        foreach (Category category in existingDownNodes)
                        {
                            mediaItem.Categories.Remove(category);
                        }

                        transaction.ExecuteNonQuery("DELETE FROM CATEGORIZE WHERE CATEGORY_FK IN (" +
                                                    ConcatCategoryIds(existingDownNodes) +
                                                    ") AND VARIATIONS_FK=" + mediaItem.VariationId);
                    }

                    if (uniqueTrees != null)
                    {
                        List<Category> uniqueRemoveList = new List<Category>();

                        foreach (List<Category> tree in uniqueTrees.Values)
                        {
                            uniqueRemoveList.AddRange(mediaItem.Categories.Where(category =>
                                tree.Contains(category) && !categoryChangeList.Contains(category)));
                        }

                        if (uniqueRemoveList.Count > 0)
                        {
                            transaction.ExecuteNonQuery("DELETE FROM CATEGORIZE WHERE CATEGORY_FK IN (" +
                                                        ConcatCategoryIds(uniqueRemoveList) +
                                                        ") AND VARIATIONS_FK=" + mediaItem.VariationId);
                        }

                        foreach (Category category in uniqueRemoveList)
                        {
                            mediaItem.Categories.Remove(category);
                        }
                    }
                }
            }

            List<Category> visibleCategories = categoryChangeList
                .Where(x => !x.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName)).ToList();

            if (visibleCategories.Count > 0)
            {
                foreach (MediaItem mediaItem in mediaItems)
                {
                    mediaItem.InvokeCategoriesChanged();
                }

                MediaBrowserContext.InvokeCategoriesChanged(mediaItems, visibleCategories, removeCategory);
            }

            foreach (Category category in categoryChangeList)
            {
                category.ItemCount += removeCategory ? -mediaItems.Count : mediaItems.Count;
                Category parent = category;

                while (parent != null)
                {
                    parent.UpdateItemInfo();
                    parent = parent.Parent;
                }
            }
        }

        public override void SaveDBProperties(Dictionary<string, string> propertyTable)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.ExecuteNonQuery("DELETE FROM DBPROPERTIES");

                foreach (KeyValuePair<string, string> pair in propertyTable)
                {
                    transaction.SetParameter("@KEY", pair.Key, DbType.String);
                    transaction.SetParameter("@VALUE", pair.Value, DbType.String);
                    transaction.ExecuteNonQuery(
                        "INSERT INTO DBPROPERTIES (`KEY`, `VALUE`) VALUES (@KEY, @VALUE)");
                }
            }
        }

        public override void SetThumbForMediaItem(System.Drawing.Bitmap bmp, MediaBrowser4.Objects.MediaItem mItem)
        {
            if (bmp == null)
            {
                return;
            }

            byte[] newThumbnail = MediaProcessing.ResizeImage.GetThumbnail(bmp,
                MediaBrowserContext.ThumbnailSize, MediaBrowserContext.ThumbnailJPEGQuality);

            if (mItem.VariationId == mItem.VariationIdDefault)
            {
                mItem.ThumbJpegData = newThumbnail;
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.SetParameter("@thumb", newThumbnail, DbType.Binary);
                transaction.SetParameter("@variationId", mItem.VariationId, DbType.Int32);
                transaction.ExecuteNonQuery(
                    "DELETE FROM THUMBS WHERE VARIATIONS_FK=@variationId AND PREVIEW_WIDTH=0 AND PREVIEW_HEIGHT=0");
                transaction.ExecuteNonQuery(
                    "INSERT INTO THUMBS (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT, THUMB, ISOUTDATED) " +
                    "VALUES (@variationId, 0, 0, @thumb, 0)");
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET WIDTH=" + mItem.Width +
                                            ", HEIGHT=" + mItem.Height + " WHERE ID=" + mItem.Id);
            }

            mItem.IsThumbJpegDataOutdated = false;

            if (this.OnThumbUpdate != null && mItem.VariationId == mItem.VariationIdDefault)
            {
                this.OnThumbUpdate(this, new MediaItemNewThumbArgs(bmp, mItem));
            }
        }

        public override void UpdateMediaItem(MediaBrowser4.Objects.MediaItem mItem)
        {
            using (ITransaction transaction = this.MBTransaction)
            {
                transaction.SetParameter("@DURATION", mItem.Duration, DbType.Double);
                transaction.ExecuteNonQuery("UPDATE MEDIAFILES SET DURATION=@DURATION WHERE ID=" + mItem.Id);
            }
        }

        public override string GetSQLFromRequestTicket(long ticket)
        {
            return this.requestArchiv.ContainsKey(ticket) ? this.requestArchiv[ticket] : null;
        }

        public override void SetSQLForRequestTicket(long ticket, string sql)
        {
            if (this.requestArchiv.ContainsKey(ticket))
            {
                this.requestArchiv.Remove(ticket);
            }

            this.requestArchiv.Add(ticket, sql);
        }

        public override void InsertMeteorologyData(SortedDictionary<DateTime, Tuple<double, double>> data)
        {
            using (ICommandHelper command = this.MBCommand)
            {
                using (DbDataReader reader = command.ExecuteReader("SELECT LOGTIME FROM DATALOGGER_METEOROLOGY"))
                {
                    while (reader.Read())
                    {
                        data.Remove(reader.GetDateTime(0));
                    }
                }
            }

            using (ITransaction transaction = this.MBTransaction)
            {
                foreach (KeyValuePair<DateTime, Tuple<double, double>> value in data)
                {
                    transaction.SetParameter("@LOGTIME", value.Key, DbType.DateTime);
                    transaction.SetParameter("@TEMPERATURE", value.Value.Item1, DbType.Double);
                    transaction.SetParameter("@HUMIDITY", value.Value.Item2, DbType.Double);
                    transaction.ExecuteNonQuery(
                        "INSERT INTO DATALOGGER_METEOROLOGY (LOGTIME, TEMPERATURE, HUMIDITY) " +
                        "VALUES (@LOGTIME, @TEMPERATURE, @HUMIDITY)");
                }
            }
        }

        public override SortedDictionary<DateTime, Tuple<double, double>> GetMeteorologyData(DateTime start, DateTime stop)
        {
            SortedDictionary<DateTime, Tuple<double, double>> data =
                new SortedDictionary<DateTime, Tuple<double, double>>();

            using (ICommandHelper command = this.MBCommand)
            {
                command.SetParameter("@start", start, DbType.DateTime);
                command.SetParameter("@stop", stop, DbType.DateTime);

                using (DbDataReader reader = command.ExecuteReader(
                    "SELECT LOGTIME, TEMPERATURE, HUMIDITY FROM DATALOGGER_METEOROLOGY " +
                    "WHERE LOGTIME BETWEEN @start AND @stop ORDER BY LOGTIME"))
                {
                    while (reader.Read())
                    {
                        data[reader.GetDateTime(0)] = Tuple.Create(reader.GetDouble(1), reader.GetDouble(2));
                    }
                }
            }

            return data;
        }

        private List<MediaItem> LoadMediaItems(MediaItemSqlRequest request)
        {
            return LoadMediaItems(request.Sql, request.ParameterList, request.SortString,
                request.LimitRequest, request);
        }

        private List<MediaItem> LoadMediaItems(string sql, string sortString, int limitRequest)
        {
            return LoadMediaItems(sql, null, sortString, limitRequest, null);
        }

        private List<MediaItem> LoadMediaItems(string sql, List<SqlParameterInfo> parameterList,
            string sortString, int limitRequest, MediaItemRequest mediaItemRequest)
        {
            long requestTicket = DateTime.Now.Ticks;
            List<MediaItem> mediaItemList = new List<MediaItem>();

            if (MediaBrowserContext.ConnectedRoles == null || MediaBrowserContext.ConnectedRoles.Count == 0)
            {
                return mediaItemList;
            }

            if (String.IsNullOrWhiteSpace(sql))
            {
                return mediaItemList;
            }

            try
            {
                using (ICommandHelper command = this.MBCommand)
                {
                    if (parameterList != null)
                    {
                        foreach (SqlParameterInfo parameter in parameterList)
                        {
                            command.SetParameter(parameter.ParameterName, parameter.Value, parameter.EffectiveDbType);
                        }
                    }

                    using (DbDataReader reader = command.ExecuteReader(
                        "SELECT " + SqlDefaultHead + sql +
                        " AND ROLES_FK IN (" + String.Join(",", MediaBrowserContext.ConnectedRoles.Select(x => x.Id)) + ") " +
                        "GROUP BY MEDIAFILES.ID " + sortString + " LIMIT " + limitRequest))
                    {
                        AddMediaItemsToList(reader, ref mediaItemList);

                        if (mediaItemRequest != null)
                        {
                            mediaItemRequest.MediaItemSqlRequest =
                                new MediaItemSqlRequest(sql, parameterList, sortString, limitRequest);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Exception(exception);
            }

            if (this.requestArchiv.ContainsKey(requestTicket))
            {
                this.requestArchiv.Remove(requestTicket);
            }

            this.requestArchiv.Add(requestTicket, sql);
            MediaBrowserContext.lastRequestTicket = requestTicket;

            return mediaItemList;
        }

        private void AddMediaItemsToList(DbDataReader reader, ref List<MediaItem> mediaItemList)
        {
            int colMediaId = reader.GetOrdinal("MF_ID");
            int colType = reader.GetOrdinal("TYPE");
            int colFilename = reader.GetOrdinal("FILENAME");
            int colSortOrder = reader.GetOrdinal("SORTORDER");
            int colVariation = reader.GetOrdinal("CURRENTVARIATION");
            int colLength = reader.GetOrdinal("LENGTH");
            int colFolderName = reader.GetOrdinal("FOLDERNAME");
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
            int colIsMd5Duplicate = reader.GetOrdinal("ISDUBLICATE");
            int colIsDeleted = reader.GetOrdinal("ISDELETED");
            int colOrientation = reader.GetOrdinal("ORIENTATION");
            int colViewed = reader.GetOrdinal("VIEWED");
            int colRoleId = reader.GetOrdinal("ROLES_FK");
            int colLongitude = reader.GetOrdinal("LONGITUDE");
            int colLatitude = reader.GetOrdinal("LATITUDE");

            List<MediaItem> deleteList = null;
            Dictionary<int, MediaItem> newItems = new Dictionary<int, MediaItem>();

            while (reader.Read())
            {
                int mediaId = ReadInt32(reader, colMediaId);

                if (MediaBrowserContext.GlobalMediaItemCache.ContainsKey(mediaId))
                {
                    MediaItem cachedItem = MediaBrowserContext.GlobalMediaItemCache[mediaId];

                    if (CheckMissingBehavior(ref deleteList, cachedItem))
                    {
                        continue;
                    }

                    mediaItemList.Add(cachedItem);
                    continue;
                }

                MediaItem mediaItem = MediaItem.GetMediaItemFromDBType(ReadString(reader, colType));

                if (mediaItem == null)
                {
                    continue;
                }

                mediaItem.Id = mediaId;
                mediaItem.Filename = ReadString(reader, colFilename);
                mediaItem.Sortorder = ReadString(reader, colSortOrder);
                mediaItem.VariationId = ReadInt32(reader, colVariation);
                mediaItem.VariationIdDefault = mediaItem.VariationId;
                mediaItem.FileLength = ReadInt64(reader, colLength);
                mediaItem.Foldername = MediaBrowserContext.MapPathRoot(ReadString(reader, colFolderName));
                mediaItem.FolderId = ReadInt32(reader, colFolderId);
                mediaItem.DescriptionId = reader.IsDBNull(colDescriptionId)
                    ? (int?)null
                    : ReadInt32(reader, colDescriptionId);
                mediaItem.Height = ReadInt32(reader, colHeight);
                mediaItem.Width = ReadInt32(reader, colWidth);
                mediaItem.Duration = ReadDouble(reader, colDuration);
                mediaItem.Frames = ReadInt32(reader, colFrames);
                mediaItem.Fps = mediaItem.Duration > 0 ? mediaItem.Frames / mediaItem.Duration : 0;
                mediaItem.Priority = ReadInt32(reader, colPriority);
                mediaItem.MediaDate = ReadDateTime(reader, colMediaDate);
                mediaItem.InsertDate = ReadDateTime(reader, colInsertDate);
                mediaItem.CreationDate = ReadDateTime(reader, colCreationDate);
                mediaItem.LastWriteDate = ReadDateTime(reader, colEditDate);
                mediaItem.Md5Value = ReadString(reader, colMd5Value);
                mediaItem.IsBookmarked = ReadBoolean(reader, colIsBookmarked);
                mediaItem.IsMd5Dublicate = ReadBoolean(reader, colIsMd5Duplicate);
                mediaItem.IsDeleted = ReadBoolean(reader, colIsDeleted);
                mediaItem.SetOrientationByNumber(ReadInt32(reader, colOrientation));
                mediaItem.Viewed = ReadInt32(reader, colViewed);
                mediaItem.RoleId = ReadInt32(reader, colRoleId);
                mediaItem.Longitude = ReadNullableDouble(reader, colLongitude);
                mediaItem.Latitude = ReadNullableDouble(reader, colLatitude);
                mediaItem.IsThumbJpegDataOutdated = true;

                if (CheckMissingBehavior(ref deleteList, mediaItem))
                {
                    continue;
                }

                MediaBrowserContext.GlobalMediaItemCache.Add(mediaItem.Id, mediaItem);
                mediaItemList.Add(mediaItem);
                newItems.Add(mediaItem.VariationId, mediaItem);
            }

            if (deleteList != null)
            {
                foreach (MediaItem mediaItem in deleteList)
                {
                    RemoveAndRecycle(mediaItem);
                }
            }

            if (newItems.Count > 0)
            {
                using (ICommandHelper command = this.MBCommand)
                using (DbDataReader thumbReader = command.ExecuteReader(
                    "SELECT THUMB, ISOUTDATED, VARIATIONS_FK FROM THUMBS " +
                    "WHERE PREVIEW_WIDTH=0 AND PREVIEW_HEIGHT=0 AND VARIATIONS_FK IN (" +
                    String.Join(",", newItems.Keys) + ")"))
                {
                    while (thumbReader.Read())
                    {
                        MediaItem mediaItem = newItems[Convert.ToInt32(thumbReader[2])];
                        mediaItem.ThumbJpegData = (byte[])thumbReader[0];
                        mediaItem.IsThumbJpegDataOutdated = Convert.ToBoolean(thumbReader[1]);
                    }
                }
            }
        }

        private static bool CheckMissingBehavior(ref List<MediaItem> deleteList, MediaItem mediaItem)
        {
            if (MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.SHOW)
            {
                return false;
            }

            if (mediaItem.FileObject.Exists && !mediaItem.IsDeleted)
            {
                return false;
            }

            if (MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE)
            {
                deleteList = deleteList ?? new List<MediaItem>();
                deleteList.Add(mediaItem);
                return true;
            }

            return !mediaItem.FileObject.Exists;
        }

        private static int ReadInt32(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static long ReadInt64(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
        }

        private static double ReadDouble(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static double? ReadNullableDouble(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? (double?)null
                : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static string ReadString(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? String.Empty : Convert.ToString(reader.GetValue(ordinal));
        }

        private static bool ReadBoolean(DbDataReader reader, int ordinal)
        {
            return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
        }

        private static DateTime ReadDateTime(DbDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        private static string ConcatCategoryIds(List<Category> categoryList)
        {
            return String.Join(",", categoryList.Select(x => x.Id));
        }

        private static string ConcatMediaItemIDs(List<MediaItem> mediaItemList)
        {
            return String.Join(",", mediaItemList.Select(x => x.Id));
        }

        private static string ConcatVariationIDs(List<MediaItem> mediaItemList)
        {
            return String.Join(",", mediaItemList.Select(x => x.VariationId));
        }
    }
}
