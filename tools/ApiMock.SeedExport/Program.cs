using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiMock.Shared;
using MySqlConnector;

namespace ApiMock.SeedExport
{
    internal static class Program
    {
        private const long RecompressThreshold = 10L * 1024L * 1024L;

        private static readonly int[] DefaultContentIds =
        {
            164345, 157858, 162930, 163084, 162940, 152334, 104725, 64722
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        public static int Main(string[] args)
        {
            try
            {
                return Run(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Seed-Export fehlgeschlagen: " + ex.Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static async Task<int> Run(string[] args)
        {
            string outDir = Path.GetFullPath(Arg(args, "--out", Path.Combine("tools", "ApiMock", "data")));
            string dbHost = Arg(args, "--db-host", "192.168.2.129");
            int dbPort = IntArg(args, "--db-port", 3306);
            string dbName = Arg(args, "--db-name", "mediabrowser-new");
            string dbUser = Arg(args, "--db-user", "mediabrowser-new");
            string dbPassword = ReadSecret(args, "--db-password-file", "MEDIABROWSER_DB_PASSWORD");
            string apiBaseUrl = Arg(args, "--api-base-url", "http://192.168.2.129:8282");
            string apiKey = ReadSecret(args, "--api-key-file", Arg(args, "--api-key-env", "MEDIABROWSER_API_KEY"));
            string ffmpeg = Arg(args, "--ffmpeg", "ffmpeg");
            string ffprobe = Arg(args, "--ffprobe", "ffprobe");
            string contentCache = Arg(args, "--content-cache",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MediaBrowserWpf", "content-cache", dbName));
            int browseLimit = IntArg(args, "--browse-limit", 80);
            int geoLimit = IntArg(args, "--geo-limit", 120);
            bool includeContent = !HasFlag(args, "--no-content");

            if (String.IsNullOrWhiteSpace(dbPassword))
                throw new InvalidOperationException("DB-Passwort fehlt. MEDIABROWSER_DB_PASSWORD setzen oder --db-password-file angeben.");

            Console.WriteLine("Lese MariaDB " + dbHost + ":" + dbPort + "/" + dbName + " (read-only) ...");

            string connectionString =
                "Server=" + dbHost + ";Port=" + dbPort + ";Database=" + dbName + ";User ID=" + dbUser +
                ";Password=" + dbPassword + ";Connection Timeout=20;DefaultCommandTimeout=300;AllowUserVariables=true";

            List<int> selectedIds;
            SeedRoot seed = new SeedRoot();
            seed.GeneratedUtc = DateTime.UtcNow;
            seedRootDataPath = outDir;

            await using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                selectedIds = SelectMediaIds(connection, browseLimit, geoLimit);
                if (selectedIds.Count == 0)
                    throw new InvalidOperationException("Es wurden keine Medien gefunden.");

                Console.WriteLine("Ausgewählte Medien: " + selectedIds.Count);

                seed.Catalog = new CatalogInfoSeed
                {
                    Valid = true,
                    Guid = "mock-catalog-" + Guid.NewGuid().ToString("N"),
                    DbName = "Mock-Katalog",
                    User = "mock-user",
                    Host = "mock-host",
                    Message = null
                };

                seed.Folders = ReadFolders(connection, selectedIds);
                seed.Categories = ReadCategories(connection);
                seed.Roles = ReadRoles(connection);
                seed.Properties = ReadProperties(connection);
                seed.LastFolders = ReadLastFolders(connection);

                List<MediaSeed> media = ReadMedia(connection, selectedIds);
                List<VariationSeed> variations = ReadVariations(connection, selectedIds);
                ReadLayers(connection, variations);
                Dictionary<int, List<int>> variationCategories = ReadVariationCategories(connection, variations);
                Dictionary<int, List<MetaDataSeed>> metadata = ReadMetadata(connection, selectedIds);
                seed.Descriptions = ReadDescriptions(connection, selectedIds);
                seed.GpsPoints = ReadGpsPoints(connection);
                seed.GpsFiles = ReadGpsFiles(connection);
                seed.Meteorology = ReadMeteorology(connection);
                seed.Duplicates = ReadDuplicates(connection, selectedIds);

                foreach (MediaSeed item in media)
                {
                    item.Metadata = metadata.TryGetValue(item.Id, out List<MetaDataSeed> values)
                        ? values
                        : new List<MetaDataSeed>();
                }

                foreach (VariationSeed variation in variations)
                {
                    if (variationCategories.TryGetValue(variation.Id, out List<int> categoryIds))
                        variation.CategoryIds = categoryIds;
                }

                foreach (MediaSeed item in media)
                {
                    VariationSeed defaultVariation = variations
                        .Where(v => v.MediaItemId == item.Id)
                        .OrderBy(v => v.Position)
                        .ThenBy(v => v.Id)
                        .FirstOrDefault();

                    if (defaultVariation != null)
                    {
                        if (item.VariationId <= 0)
                            item.VariationId = defaultVariation.Id;
                        if (item.DefaultVariationId <= 0)
                            item.DefaultVariationId = defaultVariation.Id;
                        if (item.CurrentVariationId == null)
                            item.CurrentVariationId = defaultVariation.Id;

                        item.CategoryIds = new List<int>(defaultVariation.CategoryIds);
                    }

                    if (item.DescriptionId.HasValue)
                    {
                        DescriptionSeed description = seed.Descriptions.FirstOrDefault(d => d.Id == item.DescriptionId.Value);
                        if (description != null)
                            item.Description = description.Value;
                    }
                }

                AddDemoVariations(media, variations);
                AddDemoCategories(seed, media, variations);

                seed.MediaItems = media;
                seed.Variations = variations;

                Dictionary<int, int> categoryCounts = new Dictionary<int, int>();
                foreach (MediaSeed item in media)
                {
                    foreach (int categoryId in item.CategoryIds.Distinct())
                    {
                        categoryCounts.TryGetValue(categoryId, out int count);
                        categoryCounts[categoryId] = count + 1;
                    }
                }

                foreach (CategorySeed category in seed.Categories)
                {
                    category.ItemCount = categoryCounts.TryGetValue(category.Id, out int count) ? count : 0;
                }

                foreach (FolderSeed folder in seed.Folders)
                {
                    folder.ItemCount = media.Count(m =>
                        String.Equals(m.Foldername, folder.Foldername, StringComparison.OrdinalIgnoreCase));
                }

                if (includeContent)
                {
                    await PrepareContentAsync(seed, apiBaseUrl, apiKey, contentCache, ffmpeg, ffprobe);
                }
                else
                {
                    Console.WriteLine("Content-Export übersprungen.");
                }
            }

            PrepareThumbsAndPreviews(seed, outDir, ffmpeg, ffprobe, apiKey, apiBaseUrl);

            Directory.CreateDirectory(outDir);
            string seedPath = Path.Combine(outDir, "seed.json");
            File.WriteAllText(seedPath, JsonSerializer.Serialize(seed, JsonOptions), new UTF8Encoding(false));

            long contentBytes = Directory.Exists(Path.Combine(outDir, "content"))
                ? Directory.GetFiles(Path.Combine(outDir, "content"), "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
                : 0;

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("ApiMock Seed");
            summary.AppendLine("GeneratedUtc: " + seed.GeneratedUtc.ToString("u", CultureInfo.InvariantCulture));
            summary.AppendLine("MediaItems: " + seed.MediaItems.Count);
            summary.AppendLine("Variations: " + seed.Variations.Count);
            summary.AppendLine("Categories: " + seed.Categories.Count);
            summary.AppendLine("Folders: " + seed.Folders.Count);
            summary.AppendLine("ContentBytes: " + contentBytes);
            summary.AppendLine("Seed: " + seedPath);
            File.WriteAllText(Path.Combine(outDir, "seed-summary.txt"), summary.ToString(), new UTF8Encoding(false));

            Console.WriteLine("Seed geschrieben: " + seedPath);
            Console.WriteLine("Medien: " + seed.MediaItems.Count + ", Variationen: " + seed.Variations.Count +
                              ", Content: " + contentBytes + " Bytes");
            return 0;
        }

        private static List<int> SelectMediaIds(MySqlConnection connection, int browseLimit, int geoLimit)
        {
            string sql = @"
SELECT ID FROM (
    SELECT m.ID, ROW_NUMBER() OVER (PARTITION BY m.FOLDERS_FK ORDER BY m.MEDIADATE DESC, m.ID DESC) AS rn
    FROM MEDIAFILES m
    WHERE m.ISDELETED = 0 AND m.CURRENTVARIATION IS NOT NULL
) ranked
WHERE ranked.rn <= 2
ORDER BY ranked.ID DESC
LIMIT " + Math.Max(1, browseLimit);

            List<int> ids = Query(connection, sql, reader => Convert.ToInt32(reader.GetValue(0)));

            string geoSql = @"
SELECT ID
FROM MEDIAFILES
WHERE HISTORYVERSION = 0
  AND ISDELETED = 0
  AND CURRENTVARIATION IS NOT NULL
  AND LATITUDE IS NOT NULL
  AND LONGITUDE IS NOT NULL
  AND NOT (LATITUDE = 0 AND LONGITUDE = 0)
ORDER BY RAND(42)
LIMIT " + Math.Max(1, geoLimit);

            ids.AddRange(Query(connection, geoSql, reader => Convert.ToInt32(reader.GetValue(0))));

            foreach (int id in DefaultContentIds)
                ids.Add(id);

            return ids.Distinct().ToList();
        }

        private static List<FolderSeed> ReadFolders(MySqlConnection connection, List<int> mediaIds)
        {
            string sql = @"
SELECT f.ID, f.FOLDERNAME
FROM FOLDERS f
WHERE f.ID IN (
    SELECT DISTINCT m.FOLDERS_FK FROM MEDIAFILES m WHERE m.ID IN (" + JoinIds(mediaIds) + @")
)
ORDER BY f.FOLDERNAME";

            return Query(connection, sql, reader => new FolderSeed
            {
                Id = Convert.ToInt32(reader["ID"]),
                Foldername = reader["FOLDERNAME"] as string
            });
        }

        private static List<CategorySeed> ReadCategories(MySqlConnection connection)
        {
            string sql = @"
SELECT ID, PARENT, NAME, SORTORDER, DESCRIPTION, ISUNIQUE, ISLOCATION, ISDATE,
       LATITUDE, LONGITUDE, DATE, FULLPATH, GUID
FROM CATEGORY
ORDER BY ID";

            return Query(connection, sql, reader => new CategorySeed
            {
                Id = Convert.ToInt32(reader["ID"]),
                ParentId = reader["PARENT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PARENT"]),
                Name = reader["NAME"] as string,
                Sortname = reader["SORTORDER"] as string,
                Description = reader["DESCRIPTION"] as string,
                IsUnique = Convert.ToBoolean(reader["ISUNIQUE"]),
                IsLocation = Convert.ToBoolean(reader["ISLOCATION"]),
                IsDate = Convert.ToBoolean(reader["ISDATE"]),
                Latitude = reader["LATITUDE"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["LATITUDE"]),
                Longitude = reader["LONGITUDE"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["LONGITUDE"]),
                Date = reader["DATE"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["DATE"]),
                FullPath = reader["FULLPATH"] as string,
                Guid = reader["GUID"] as string
            });
        }

        private static List<RoleSeed> ReadRoles(MySqlConnection connection)
        {
            return Query(connection, "SELECT ID, ROLE, PASSWORD, DESCRIPTION FROM ROLES ORDER BY ID",
                reader => new RoleSeed
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Name = reader["ROLE"] as string,
                    Password = reader["PASSWORD"] as string,
                    Description = reader["DESCRIPTION"] as string
                });
        }

        private static Dictionary<string, string> ReadProperties(MySqlConnection connection)
        {
            List<KeyValuePair<string, string>> rows = Query(connection,
                "SELECT `KEY`, `VALUE` FROM DBPROPERTIES ORDER BY `KEY`",
                reader => new KeyValuePair<string, string>(
                    reader["KEY"] as string,
                    reader["VALUE"] as string));

            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> row in rows)
            {
                if (row.Key != null)
                    result[row.Key] = row.Value;
            }

            return result;
        }

        private static Dictionary<string, DateTime> ReadLastFolders(MySqlConnection connection)
        {
            List<KeyValuePair<string, DateTime>> rows = Query(connection, @"
SELECT f.FOLDERNAME, MAX(m.INSERTDATE) AS LASTDATE
FROM MEDIAFILES m
JOIN FOLDERS f ON f.ID = m.FOLDERS_FK
GROUP BY f.FOLDERNAME
ORDER BY LASTDATE DESC
LIMIT 10",
                reader => new KeyValuePair<string, DateTime>(
                    reader["FOLDERNAME"] as string,
                    Convert.ToDateTime(reader["LASTDATE"])));

            Dictionary<string, DateTime> result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, DateTime> row in rows)
            {
                if (row.Key != null)
                    result[row.Key] = row.Value;
            }

            return result;
        }

        private static List<MediaSeed> ReadMedia(MySqlConnection connection, List<int> mediaIds)
        {
            string sql = @"
SELECT m.ID, m.FILENAME, f.FOLDERNAME, m.SORTORDER, m.MD5VALUE, m.LENGTH, m.VIEWED,
       m.DURATION, m.FRAMES, m.ORIENTATION, m.TYPE, m.ISBOOKMARKED, m.ISDELETED,
       m.ISDUBLICATE, m.PRIORITY, m.ROLES_FK, m.CURRENTVARIATION, m.DESCRIPTION_FK,
       m.CREATIONDATE, m.MEDIADATE, m.EDITDATE, m.INSERTDATE, m.LONGITUDE, m.LATITUDE,
       m.WIDTH, m.HEIGHT
FROM MEDIAFILES m
JOIN FOLDERS f ON f.ID = m.FOLDERS_FK
WHERE m.ID IN (" + JoinIds(mediaIds) + @")
ORDER BY m.ID";

            return Query(connection, sql, reader => new MediaSeed
            {
                Id = Convert.ToInt32(reader["ID"]),
                Filename = reader["FILENAME"] as string,
                Foldername = reader["FOLDERNAME"] as string,
                Sortorder = reader["SORTORDER"] as string,
                Md5Value = reader["MD5VALUE"] as string,
                FileLength = reader["LENGTH"] == DBNull.Value ? 0 : Convert.ToInt64(reader["LENGTH"]),
                Viewed = reader["VIEWED"] == DBNull.Value ? 0 : Convert.ToInt32(reader["VIEWED"]),
                Duration = reader["DURATION"] == DBNull.Value ? 0 : Convert.ToDouble(reader["DURATION"]),
                Frames = reader["FRAMES"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FRAMES"]),
                Orientation = reader["ORIENTATION"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ORIENTATION"]),
                Type = reader["TYPE"] as string,
                IsBookmarked = Convert.ToBoolean(reader["ISBOOKMARKED"]),
                IsDeleted = Convert.ToBoolean(reader["ISDELETED"]),
                IsDuplicate = Convert.ToBoolean(reader["ISDUBLICATE"]),
                Priority = reader["PRIORITY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PRIORITY"]),
                RoleId = Convert.ToInt32(reader["ROLES_FK"]),
                CurrentVariationId = reader["CURRENTVARIATION"] == DBNull.Value
                    ? (int?)null
                    : Convert.ToInt32(reader["CURRENTVARIATION"]),
                DescriptionId = reader["DESCRIPTION_FK"] == DBNull.Value
                    ? (int?)null
                    : Convert.ToInt32(reader["DESCRIPTION_FK"]),
                CreationDate = Convert.ToDateTime(reader["CREATIONDATE"]),
                MediaDate = Convert.ToDateTime(reader["MEDIADATE"]),
                EditDate = Convert.ToDateTime(reader["EDITDATE"]),
                InsertDate = Convert.ToDateTime(reader["INSERTDATE"]),
                Longitude = reader["LONGITUDE"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["LONGITUDE"]),
                Latitude = reader["LATITUDE"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["LATITUDE"]),
                Width = reader["WIDTH"] == DBNull.Value ? 0 : Convert.ToInt32(reader["WIDTH"]),
                Height = reader["HEIGHT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["HEIGHT"]),
                VariationId = reader["CURRENTVARIATION"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["CURRENTVARIATION"]),
                DefaultVariationId = reader["CURRENTVARIATION"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["CURRENTVARIATION"])
            });
        }

        private static List<VariationSeed> ReadVariations(MySqlConnection connection, List<int> mediaIds)
        {
            string sql = @"
SELECT v.ID, v.MEDIAFILES_FK, v.POSITION, v.NAME, v.DESCRIPTION, v.THUMB,
       t.THUMB AS MAIN_THUMB, t.ISOUTDATED, t.EXTENSION, t.WIDTH, t.HEIGHT
FROM VARIATIONS v
LEFT JOIN THUMBS t ON t.VARIATIONS_FK = v.ID AND t.PREVIEW_WIDTH = 0
WHERE v.MEDIAFILES_FK IN (" + JoinIds(mediaIds) + @")
ORDER BY v.MEDIAFILES_FK, v.POSITION, v.ID";

            Dictionary<int, VariationSeed> result = new Dictionary<int, VariationSeed>();
            using MySqlCommand command = new MySqlCommand(sql, connection);
            command.CommandTimeout = 300;

            using MySqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = Convert.ToInt32(reader["ID"]);
                if (result.ContainsKey(id))
                    continue;

                byte[] thumb = reader["MAIN_THUMB"] == DBNull.Value
                    ? (reader["THUMB"] == DBNull.Value ? null : (byte[])reader["THUMB"])
                    : (byte[])reader["MAIN_THUMB"];

                result[id] = new VariationSeed
                {
                    Id = id,
                    MediaItemId = Convert.ToInt32(reader["MEDIAFILES_FK"]),
                    Position = Convert.ToInt32(reader["POSITION"]),
                    Name = String.IsNullOrWhiteSpace(reader["NAME"] as string) ? "Default" : reader["NAME"] as string,
                    Description = reader["DESCRIPTION"] as string,
                    IsOutdated = reader["ISOUTDATED"] != DBNull.Value && Convert.ToBoolean(reader["ISOUTDATED"]),
                    ThumbFile = null,
                    IsSynthetic = false,
                    ThumbBytes = thumb
                };
            }

            return result.Values.OrderBy(v => v.MediaItemId).ThenBy(v => v.Position).ThenBy(v => v.Id).ToList();
        }

        private static void ReadLayers(MySqlConnection connection, List<VariationSeed> variations)
        {
            if (variations.Count == 0)
                return;

            string sql = @"
SELECT VARIATIONS_FK, POSITION, EDIT, ACTION
FROM LAYERS
WHERE VARIATIONS_FK IN (" + JoinIds(variations.Select(v => v.Id)) + @")
ORDER BY VARIATIONS_FK, POSITION";

            Dictionary<int, VariationSeed> byId = variations.ToDictionary(v => v.Id);
            foreach (LayerRow row in Query(connection, sql, reader => new LayerRow
            {
                VariationId = Convert.ToInt32(reader["VARIATIONS_FK"]),
                Position = Convert.ToInt32(reader["POSITION"]),
                Edit = reader["EDIT"] as string,
                Action = reader["ACTION"] as string
            }))
            {
                if (byId.TryGetValue(row.VariationId, out VariationSeed variation))
                {
                    variation.Layers.Add(new LayerSeed
                    {
                        Position = row.Position,
                        Edit = row.Edit,
                        Action = row.Action
                    });
                }
            }
        }

        private static Dictionary<int, List<int>> ReadVariationCategories(MySqlConnection connection,
            List<VariationSeed> variations)
        {
            Dictionary<int, List<int>> result = new Dictionary<int, List<int>>();
            if (variations.Count == 0)
                return result;

            string sql = @"
SELECT VARIATIONS_FK, CATEGORY_FK
FROM CATEGORIZE
WHERE VARIATIONS_FK IN (" + JoinIds(variations.Select(v => v.Id)) + @")
ORDER BY VARIATIONS_FK, CATEGORY_FK";

            foreach (CategoryLinkRow row in Query(connection, sql, reader => new CategoryLinkRow
            {
                VariationId = Convert.ToInt32(reader["VARIATIONS_FK"]),
                CategoryId = Convert.ToInt32(reader["CATEGORY_FK"])
            }))
            {
                if (!result.TryGetValue(row.VariationId, out List<int> list))
                {
                    list = new List<int>();
                    result[row.VariationId] = list;
                }

                if (!list.Contains(row.CategoryId))
                    list.Add(row.CategoryId);
            }

            return result;
        }

        private static Dictionary<int, List<MetaDataSeed>> ReadMetadata(MySqlConnection connection,
            List<int> mediaIds)
        {
            string sql = @"
SELECT m.MEDIAFILES_FK, n.ID, n.NAME, n.GROUPNAME, n.TYPE, n.ISVISIBLE, m.VALUE
FROM METADATA m
JOIN METADATANAME n ON n.ID = m.METANAME_FK
WHERE m.MEDIAFILES_FK IN (" + JoinIds(mediaIds) + @")
ORDER BY m.MEDIAFILES_FK, n.GROUPNAME, n.NAME";

            Dictionary<int, List<MetaDataSeed>> result = new Dictionary<int, List<MetaDataSeed>>();
            foreach (MetaDataRow row in Query(connection, sql, reader => new MetaDataRow
            {
                MediaItemId = Convert.ToInt32(reader["MEDIAFILES_FK"]),
                Id = Convert.ToInt32(reader["ID"]),
                Name = reader["NAME"] as string,
                GroupName = reader["GROUPNAME"] as string,
                Type = reader["TYPE"] as string,
                IsVisible = Convert.ToBoolean(reader["ISVISIBLE"]),
                Value = reader["VALUE"] == DBNull.Value ? null : reader["VALUE"] as string
            }))
            {
                if (!result.TryGetValue(row.MediaItemId, out List<MetaDataSeed> list))
                {
                    list = new List<MetaDataSeed>();
                    result[row.MediaItemId] = list;
                }

                if (list.Count >= 120)
                    continue;

                list.Add(new MetaDataSeed
                {
                    Id = row.Id,
                    Name = row.Name,
                    GroupName = row.GroupName,
                    Type = row.Type,
                    IsVisible = row.IsVisible,
                    Value = row.Value,
                    IsNull = row.Value == null
                });
            }

            return result;
        }

        private static List<DescriptionSeed> ReadDescriptions(MySqlConnection connection, List<int> mediaIds)
        {
            string sql = @"
SELECT d.ID, d.VALUE, m.ID AS MEDIA_ID
FROM DESCRIPTION d
JOIN MEDIAFILES m ON m.DESCRIPTION_FK = d.ID
WHERE m.ID IN (" + JoinIds(mediaIds) + @")
ORDER BY d.ID";

            Dictionary<int, DescriptionSeed> result = new Dictionary<int, DescriptionSeed>();
            foreach (DescriptionRow row in Query(connection, sql, reader => new DescriptionRow
            {
                Id = Convert.ToInt32(reader["ID"]),
                Value = reader["VALUE"] as string,
                MediaItemId = Convert.ToInt32(reader["MEDIA_ID"])
            }))
            {
                if (!result.TryGetValue(row.Id, out DescriptionSeed description))
                {
                    description = new DescriptionSeed
                    {
                        Id = row.Id,
                        Value = row.Value,
                        Command = null
                    };
                    result[row.Id] = description;
                }

                description.MediaItemIds.Add(row.MediaItemId);
            }

            return result.Values.ToList();
        }

        private static List<GeoPointSeed> ReadGpsPoints(MySqlConnection connection)
        {
            string sql = @"
SELECT LONGITUDE, LATITUDE, ALTITUDE, LOCALTIME, UTCTIME, FILESIZE, FILETIME
FROM DATALOGGER_GPS
ORDER BY LOCALTIME DESC
LIMIT 300";

            List<GeoPointSeed> points = Query(connection, sql, reader => new GeoPointSeed
            {
                Longitude = Convert.ToDouble(reader["LONGITUDE"]),
                Latitude = Convert.ToDouble(reader["LATITUDE"]),
                Altitude = reader["ALTITUDE"] == DBNull.Value ? 0 : Convert.ToDouble(reader["ALTITUDE"]),
                LocalTime = Convert.ToDateTime(reader["LOCALTIME"]),
                UtcTime = Convert.ToDateTime(reader["UTCTIME"]),
                GpsFileSize = Convert.ToInt64(reader["FILESIZE"]),
                GpsFileTime = Convert.ToDateTime(reader["FILETIME"])
            });

            points.Reverse();
            return points;
        }

        private static List<GpsFileSeed> ReadGpsFiles(MySqlConnection connection)
        {
            return Query(connection, @"
SELECT FILESIZE, FILETIME
FROM DATALOGGER_GPS
GROUP BY FILESIZE, FILETIME
ORDER BY FILETIME DESC
LIMIT 20",
                reader => new GpsFileSeed
                {
                    Filesize = Convert.ToInt64(reader["FILESIZE"]),
                    FileTime = Convert.ToDateTime(reader["FILETIME"])
                });
        }

        private static List<MeteorologySeed> ReadMeteorology(MySqlConnection connection)
        {
            string sql = @"
SELECT LOGTIME, TEMPERATURE, HUMIDITY
FROM DATALOGGER_METEOROLOGY
ORDER BY LOGTIME DESC
LIMIT 300";

            List<MeteorologySeed> result = Query(connection, sql, reader => new MeteorologySeed
            {
                LogTime = Convert.ToDateTime(reader["LOGTIME"]),
                Temperature = Convert.ToDouble(reader["TEMPERATURE"]),
                Humidity = Convert.ToDouble(reader["HUMIDITY"])
            });

            result.Reverse();
            return result;
        }

        private static List<DuplicateSeed> ReadDuplicates(MySqlConnection connection, List<int> mediaIds)
        {
            string sql = "SELECT ID, MD5VALUE FROM MEDIAFILES WHERE ID IN (" + JoinIds(mediaIds) +
                         ") AND MD5VALUE IS NOT NULL AND MD5VALUE <> '' ORDER BY MD5VALUE, ID";

            List<(int Id, string Value)> rows = Query(connection, sql, reader =>
                (Convert.ToInt32(reader["ID"]), reader["MD5VALUE"] as string));

            return rows.GroupBy(r => r.Value, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Select(r => new DuplicateSeed { MediaItemId = r.Id, Value = r.Value }))
                .ToList();
        }

        private static void AddDemoVariations(List<MediaSeed> media, List<VariationSeed> variations)
        {
            int nextId = Math.Max(900000, variations.Count == 0 ? 900000 : variations.Max(v => v.Id) + 1);
            HashSet<int> usedIds = new HashSet<int>(variations.Select(v => v.Id));

            foreach (MediaSeed item in media)
            {
                if (!DefaultContentIds.Contains(item.Id))
                    continue;

                VariationSeed defaultVariation = variations
                    .Where(v => v.MediaItemId == item.Id)
                    .OrderBy(v => v.Position)
                    .ThenBy(v => v.Id)
                    .FirstOrDefault();

                if (defaultVariation == null)
                {
                    defaultVariation = new VariationSeed
                    {
                        Id = nextId++,
                        MediaItemId = item.Id,
                        Position = 1,
                        Name = "Default",
                        IsSynthetic = true,
                        CategoryIds = item.CategoryIds
                    };
                    variations.Add(defaultVariation);
                    usedIds.Add(defaultVariation.Id);
                }

                List<int> categoryIds = new List<int>(defaultVariation.CategoryIds);

                if (item.Id == 164345)
                {
                    item.IsPano = true;
                    if (!defaultVariation.Layers.Any(l => String.Equals(l.Edit, "PANO", StringComparison.OrdinalIgnoreCase)))
                    {
                        defaultVariation.Layers.Add(new LayerSeed
                        {
                            Position = defaultVariation.Layers.Count,
                            Edit = "PANO",
                            Action = ""
                        });
                    }
                }

                List<(string Name, LayerSeed[] Layers)> specs = item.Id == 162930
                    ? new List<(string, LayerSeed[])>
                    {
                        ("Ausschnitt", new[] { new LayerSeed { Position = 0, Edit = "TRIM", Action = "2.000 7.000" } }),
                        ("Zuschnitt", new[] { new LayerSeed { Position = 0, Edit = "CROP", Action = "8.000 8.000 8.000 8.000" } }),
                        ("Drehen", new[] { new LayerSeed { Position = 0, Edit = "ROT", Action = "5.0" } }),
                        ("Zuschnitt + Drehen", new[]
                        {
                            new LayerSeed { Position = 0, Edit = "ROT", Action = "5.0" },
                            new LayerSeed { Position = 1, Edit = "CROP", Action = "8.000 8.000 8.000 8.000" }
                        })
                    }
                    : new List<(string, LayerSeed[])>
                    {
                        ("Zuschnitt", new[] { new LayerSeed { Position = 0, Edit = "CROP", Action = "8.000 8.000 8.000 8.000" } }),
                        ("Drehen", new[] { new LayerSeed { Position = 0, Edit = "ROT", Action = "5.0" } }),
                        ("Zuschnitt + Drehen", new[]
                        {
                            new LayerSeed { Position = 0, Edit = "ROT", Action = "5.0" },
                            new LayerSeed { Position = 1, Edit = "CROP", Action = "8.000 8.000 8.000 8.000" }
                        })
                    };

                foreach ((string name, LayerSeed[] layers) in specs)
                {
                    if (variations.Any(v => v.MediaItemId == item.Id &&
                                            String.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    VariationSeed variation = new VariationSeed
                    {
                        Id = nextId++,
                        MediaItemId = item.Id,
                        Position = variations.Count(v => v.MediaItemId == item.Id) + 1,
                        Name = name,
                        IsSynthetic = true,
                        Layers = layers.ToList(),
                        CategoryIds = categoryIds
                    };

                    while (usedIds.Contains(variation.Id))
                        variation.Id = nextId++;

                    usedIds.Add(variation.Id);
                    variations.Add(variation);
                }

                item.VariationId = defaultVariation.Id;
                item.DefaultVariationId = defaultVariation.Id;
                item.CurrentVariationId = defaultVariation.Id;
            }
        }

        private static byte[] TryGetThumbBytes(VariationSeed variation)
        {
            return variation.ThumbBytes;
        }

        private static async Task PrepareContentAsync(SeedRoot seed, string apiBaseUrl, string apiKey,
            string contentCache, string ffmpeg, string ffprobe)
        {
            string workDir = Path.Combine(Path.GetTempPath(), "ApiMockSeed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            try
            {
                using HttpClient http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);

                if (!String.IsNullOrWhiteSpace(apiKey))
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                foreach (MediaSeed item in seed.MediaItems)
                {
                    if (!DefaultContentIds.Contains(item.Id))
                        continue;

                    byte[] content = null;
                    string contentType = null;

                    if (!String.IsNullOrWhiteSpace(apiKey))
                    {
                        try
                        {
                            string url = apiBaseUrl.TrimEnd('/') + "/api/v1/mediafiles/" + item.Id + "/content";
                            using HttpResponseMessage response = await http.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                content = await response.Content.ReadAsByteArrayAsync();
                                contentType = response.Content.Headers.ContentType?.MediaType;
                            }
                            else
                            {
                                Console.WriteLine("Content " + item.Id + " über API: " + (int)response.StatusCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Content " + item.Id + " über API fehlgeschlagen: " + ex.Message);
                        }
                    }

                    if (content == null || content.Length == 0)
                    {
                        string cachePath = FindCacheFile(contentCache, item.Id);
                        if (cachePath != null)
                        {
                            content = await File.ReadAllBytesAsync(cachePath);
                            contentType = GuessContentType(cachePath);
                        }
                    }

                    if (content == null || content.Length == 0)
                    {
                        Console.WriteLine("Kein Original für " + item.Id + " " + item.Filename);
                        continue;
                    }

                    string extension = Path.GetExtension(item.Filename);
                    if (String.IsNullOrWhiteSpace(extension))
                        extension = contentType == "video/mp4" ? ".mp4" : ".jpg";

                    string sourcePath = Path.Combine(workDir, item.Id + "_source" + extension);
                    await File.WriteAllBytesAsync(sourcePath, content);

                    string outputPath = sourcePath;
                    if (content.LongLength > RecompressThreshold)
                    {
                        outputPath = Path.Combine(workDir, item.Id + "_recompressed" + extension);
                        bool isPano = item.IsPano || (item.Width > 0 && item.Height > 0 &&
                                                      Math.Abs((double)item.Width / item.Height - 2.0) < 0.1);

                        if (String.Equals(item.Type, "dsh", StringComparison.OrdinalIgnoreCase))
                        {
                            RunProcess(ffmpeg, new[]
                            {
                                "-y", "-i", sourcePath,
                                "-vf", "scale=-2:720",
                                "-c:v", "libx264", "-crf", "28", "-preset", "fast",
                                "-c:a", "aac", "-b:a", "128k",
                                "-movflags", "+faststart",
                                outputPath
                            });
                        }
                        else
                        {
                            string scale = isPano ? "scale='min(8192,iw)':-2" : "scale='min(4096,iw)':-2";
                            RunProcess(ffmpeg, new[]
                            {
                                "-y", "-i", sourcePath,
                                "-vf", scale,
                                "-q:v", "3",
                                outputPath
                            });
                        }

                        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                        {
                            Console.WriteLine("Recompress fehlgeschlagen, verwende Original: " + item.Id);
                            outputPath = sourcePath;
                        }
                    }

                    string safeName = SafeFileName(item.Filename);
                    string contentDir = Path.Combine(seedRootDataPath, "content");
                    Directory.CreateDirectory(contentDir);
                    string targetPath = Path.Combine(contentDir, item.Id + "_" + safeName);
                    File.Copy(outputPath, targetPath, true);

                    FileInfo info = new FileInfo(targetPath);
                    item.ContentFile = "content/" + Path.GetFileName(targetPath);
                    item.ContentType = GuessContentType(targetPath) ?? contentType ?? "application/octet-stream";
                    item.FileLength = info.Length;

                    MediaInfo mediaInfo = ProbeMedia(ffprobe, targetPath);
                    if (mediaInfo.Width > 0 && mediaInfo.Height > 0)
                    {
                        item.Width = mediaInfo.Width;
                        item.Height = mediaInfo.Height;
                    }

                    if (mediaInfo.Duration > 0 && String.Equals(item.Type, "dsh", StringComparison.OrdinalIgnoreCase))
                        item.Duration = mediaInfo.Duration;

                    Console.WriteLine("Content " + item.Id + ": " + info.Length + " Bytes, " +
                                      item.Width + "x" + item.Height);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(workDir, true);
                }
                catch
                {
                }
            }
        }

        private static string seedRootDataPath = Path.Combine("tools", "ApiMock", "data");

        private static void PrepareThumbsAndPreviews(SeedRoot seed, string outDir, string ffmpeg,
            string ffprobe, string apiKey, string apiBaseUrl)
        {
            seedRootDataPath = outDir;
            string thumbsDir = Path.Combine(outDir, "thumbs");
            string previewsDir = Path.Combine(outDir, "previews");
            Directory.CreateDirectory(thumbsDir);
            Directory.CreateDirectory(previewsDir);

            foreach (MediaSeed item in seed.MediaItems)
            {
                List<VariationSeed> itemVariations = seed.Variations
                    .Where(v => v.MediaItemId == item.Id)
                    .OrderBy(v => v.Position)
                    .ThenBy(v => v.Id)
                    .ToList();

                VariationSeed defaultVariation = itemVariations.FirstOrDefault();

                if (!String.IsNullOrWhiteSpace(item.ContentFile))
                {
                    string contentPath = Path.Combine(outDir, item.ContentFile.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(contentPath))
                    {
                        string previewPath = Path.Combine(previewsDir, item.Id + ".jpg");
                        if (String.Equals(item.Type, "dsh", StringComparison.OrdinalIgnoreCase))
                        {
                            double seek = item.Duration > 1.5 ? 1.0 : 0.0;
                            RunProcess(ffmpeg, new[]
                            {
                                "-y", "-ss", seek.ToString("0.###", CultureInfo.InvariantCulture),
                                "-i", contentPath,
                                "-frames:v", "1",
                                "-vf", "scale='min(1920,iw)':-2",
                                "-q:v", "3",
                                previewPath
                            });
                        }
                        else
                        {
                            RunProcess(ffmpeg, new[]
                            {
                                "-y", "-i", contentPath,
                                "-vf", "scale='min(1920,iw)':-2",
                                "-q:v", "3",
                                previewPath
                            });
                        }

                        if (File.Exists(previewPath) && new FileInfo(previewPath).Length > 0)
                        {
                            MediaInfo previewInfo = ProbeMedia(ffprobe, previewPath);
                            item.PreviewFile = "previews/" + Path.GetFileName(previewPath);
                            item.PreviewWidth = previewInfo.Width;
                            item.PreviewHeight = previewInfo.Height;
                        }
                    }
                }

                foreach (VariationSeed variation in itemVariations)
                {
                    byte[] thumbBytes = TryGetThumbBytes(variation);
                    if (thumbBytes != null && thumbBytes.Length > 0)
                    {
                        string thumbPath = Path.Combine(thumbsDir, variation.Id + ".jpg");
                        File.WriteAllBytes(thumbPath, thumbBytes);
                        variation.ThumbFile = "thumbs/" + Path.GetFileName(thumbPath);
                    }
                }

                if (defaultVariation != null && !String.IsNullOrWhiteSpace(defaultVariation.ThumbFile))
                    item.ThumbFile = defaultVariation.ThumbFile;

                foreach (VariationSeed variation in itemVariations)
                {
                    if (!String.IsNullOrWhiteSpace(variation.ThumbFile))
                        continue;

                    string generated = GenerateVariationThumb(seed, outDir, item, variation, ffmpeg);
                    if (!String.IsNullOrWhiteSpace(generated))
                    {
                        variation.ThumbFile = generated;
                        if (defaultVariation != null && variation.Id == defaultVariation.Id)
                            item.ThumbFile = generated;
                    }
                }

                if (String.IsNullOrWhiteSpace(item.PreviewFile) && !String.IsNullOrWhiteSpace(item.ThumbFile))
                {
                    item.PreviewFile = item.ThumbFile;
                    item.PreviewWidth = 0;
                    item.PreviewHeight = 0;
                }
            }

            foreach (VariationSeed variation in seed.Variations)
            {
                if (String.IsNullOrWhiteSpace(variation.ThumbFile))
                {
                    MediaSeed item = seed.MediaItems.FirstOrDefault(m => m.Id == variation.MediaItemId);
                    if (item != null && !String.IsNullOrWhiteSpace(item.ThumbFile))
                        variation.ThumbFile = item.ThumbFile;
                }
            }
        }

        private static void AddDemoCategories(SeedRoot seed, List<MediaSeed> media,
            List<VariationSeed> variations)
        {
            List<MediaSeed> demoItems = media.Where(m => DefaultContentIds.Contains(m.Id)).ToList();
            if (demoItems.Count == 0)
                return;

            const int rootId = 910000;
            const int instaId = 910001;
            const int imageId = 910002;
            const int videoId = 910003;

            seed.Categories.Add(new CategorySeed
            {
                Id = rootId,
                ParentId = 0,
                Name = "Mock-Demo",
                Sortname = "00000000",
                Description = "Lokaler API-Mock",
                FullPath = "\\Mock-Demo",
                Guid = Guid.NewGuid().ToString("N")
            });

            seed.Categories.Add(new CategorySeed
            {
                Id = instaId,
                ParentId = rootId,
                Name = "Insta360",
                Sortname = "Insta360",
                Description = "360-Grad-Demo",
                FullPath = "\\Mock-Demo\\Insta360",
                Guid = Guid.NewGuid().ToString("N")
            });

            seed.Categories.Add(new CategorySeed
            {
                Id = imageId,
                ParentId = rootId,
                Name = "Bild",
                Sortname = "Bild",
                Description = "Bild-Demo",
                FullPath = "\\Mock-Demo\\Bild",
                Guid = Guid.NewGuid().ToString("N")
            });

            seed.Categories.Add(new CategorySeed
            {
                Id = videoId,
                ParentId = rootId,
                Name = "Video",
                Sortname = "Video",
                Description = "Video-Demo",
                FullPath = "\\Mock-Demo\\Video",
                Guid = Guid.NewGuid().ToString("N")
            });

            foreach (MediaSeed item in demoItems)
            {
                int typeId = item.Id == 164345
                    ? instaId
                    : (String.Equals(item.Type, "dsh", StringComparison.OrdinalIgnoreCase) ? videoId : imageId);

                item.CategoryIds.Add(rootId);
                item.CategoryIds.Add(typeId);

                foreach (VariationSeed variation in variations.Where(v => v.MediaItemId == item.Id))
                {
                    if (!variation.CategoryIds.Contains(rootId))
                        variation.CategoryIds.Add(rootId);
                    if (!variation.CategoryIds.Contains(typeId))
                        variation.CategoryIds.Add(typeId);
                }
            }
        }

        private static string GenerateVariationThumb(SeedRoot seed, string outDir, MediaSeed item,
            VariationSeed variation, string ffmpeg)
        {
            string source = null;
            string tempFrame = null;

            try
            {
                if (!String.IsNullOrWhiteSpace(item.ContentFile))
                {
                    source = Path.Combine(outDir, item.ContentFile.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(source))
                        source = null;
                }

                if (source == null && !String.IsNullOrWhiteSpace(item.PreviewFile))
                {
                    source = Path.Combine(outDir, item.PreviewFile.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(source))
                        source = null;
                }

                if (source == null)
                    return null;

                if (String.Equals(item.Type, "dsh", StringComparison.OrdinalIgnoreCase))
                {
                    double seek = GetTrimStart(variation.Layers);
                    tempFrame = Path.Combine(Path.GetTempPath(), "ApiMockFrame-" + Guid.NewGuid().ToString("N") + ".jpg");
                    RunProcess(ffmpeg, new[]
                    {
                        "-y", "-ss", seek.ToString("0.###", CultureInfo.InvariantCulture),
                        "-i", source, "-frames:v", "1", "-q:v", "3", tempFrame
                    });

                    if (!File.Exists(tempFrame) || new FileInfo(tempFrame).Length == 0)
                        return null;

                    source = tempFrame;
                }

                string filter = BuildThumbFilter(variation.Layers);
                if (String.IsNullOrWhiteSpace(filter))
                    filter = "scale='min(300,iw)':-2";
                else
                    filter += ",scale='min(300,iw)':-2";

                string thumbPath = Path.Combine(outDir, "thumbs", variation.Id + ".jpg");
                Directory.CreateDirectory(Path.GetDirectoryName(thumbPath));

                RunProcess(ffmpeg, new[]
                {
                    "-y", "-i", source,
                    "-vf", filter,
                    "-frames:v", "1",
                    "-q:v", "3",
                    thumbPath
                });

                if (File.Exists(thumbPath) && new FileInfo(thumbPath).Length > 0)
                    return "thumbs/" + Path.GetFileName(thumbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Thumbnail für Variation " + variation.Id + " fehlgeschlagen: " + ex.Message);
            }
            finally
            {
                if (tempFrame != null)
                {
                    try
                    {
                        File.Delete(tempFrame);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static string BuildThumbFilter(List<LayerSeed> layers)
        {
            List<string> filters = new List<string>();

            foreach (LayerSeed layer in layers.OrderBy(l => l.Position))
            {
                if (String.Equals(layer.Edit, "CROP", StringComparison.OrdinalIgnoreCase) && layer.Action != null)
                {
                    double[] values = layer.Action.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => Double.Parse(v, CultureInfo.InvariantCulture))
                        .ToArray();

                    if (values.Length == 4)
                    {
                        double left = values[0] / 100.0;
                        double top = values[1] / 100.0;
                        double right = values[2] / 100.0;
                        double bottom = values[3] / 100.0;

                        filters.Add("crop=iw*(1-" + F(left) + "-" + F(right) + "):ih*(1-" + F(top) + "-" +
                                    F(bottom) + "):iw*" + F(left) + ":ih*" + F(top));
                    }
                }
                else if (String.Equals(layer.Edit, "ROT", StringComparison.OrdinalIgnoreCase) &&
                         layer.Action != null &&
                         Double.TryParse(layer.Action, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle))
                {
                    string radians = "(" + F(angle) + "*PI/180)";
                    filters.Add("rotate=" + radians + ":ow=rotw(" + radians + "):oh=roth(" + radians + "):c=black");
                }
            }

            return String.Join(",", filters);
        }

        private static double GetTrimStart(List<LayerSeed> layers)
        {
            LayerSeed trim = layers.FirstOrDefault(l =>
                String.Equals(l.Edit, "TRIM", StringComparison.OrdinalIgnoreCase));
            if (trim?.Action == null)
                return 0;

            string[] parts = trim.Action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 &&
                Double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double start))
                return Math.Max(0, start);

            return 0;
        }

        private static string FindCacheFile(string contentCache, int mediaItemId)
        {
            if (String.IsNullOrWhiteSpace(contentCache))
                return null;

            string folder = Path.Combine(contentCache, mediaItemId.ToString(CultureInfo.InvariantCulture));
            if (!Directory.Exists(folder))
                return null;

            return Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
        }

        private static MediaInfo ProbeMedia(string ffprobe, string path)
        {
            try
            {
                ProcessResult result = RunProcess(ffprobe, new[]
                {
                    "-v", "error",
                    "-select_streams", "v:0",
                    "-show_entries", "stream=width,height,duration",
                    "-of", "csv=p=0",
                    path
                });

                if (result.ExitCode != 0 || String.IsNullOrWhiteSpace(result.Output))
                    return new MediaInfo();

                string[] parts = result.Output.Trim().Split(',');
                MediaInfo info = new MediaInfo();
                if (parts.Length > 0 && Int32.TryParse(parts[0], out int width))
                    info.Width = width;
                if (parts.Length > 1 && Int32.TryParse(parts[1], out int height))
                    info.Height = height;
                if (parts.Length > 2 &&
                    Double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double duration))
                    info.Duration = duration;

                return info;
            }
            catch
            {
                return new MediaInfo();
            }
        }

        private static ProcessResult RunProcess(string fileName, IEnumerable<string> arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
                info.ArgumentList.Add(argument);

            using Process process = Process.Start(info);
            if (process == null)
                throw new InvalidOperationException("Prozess konnte nicht gestartet werden: " + fileName);

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0 && !String.IsNullOrWhiteSpace(error))
                Console.WriteLine(fileName + ": " + error.Trim());

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };
        }

        private static string GuessContentType(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".webp":
                    return "image/webp";
                case ".mp4":
                    return "video/mp4";
                case ".avi":
                    return "video/x-msvideo";
                case ".mov":
                    return "video/quicktime";
                case ".mkv":
                    return "video/x-matroska";
                default:
                    return null;
            }
        }

        private static string SafeFileName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "content";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value;
        }

        private static string ReadSecret(string[] args, string fileOption, string envName)
        {
            string file = Arg(args, fileOption, null);
            if (!String.IsNullOrWhiteSpace(file))
            {
                if (!File.Exists(file))
                    throw new FileNotFoundException("Passwortdatei nicht gefunden: " + file, file);

                string content = File.ReadAllText(file).Trim();
                if (content.Contains("="))
                {
                    string prefix = envName + "=";
                    string line = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (line != null)
                        return line.Substring(prefix.Length).Trim();
                }

                return content.TrimEnd('\r', '\n');
            }

            return Environment.GetEnvironmentVariable(envName);
        }

        private static string Arg(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        return args[i + 1];
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        private static int IntArg(string[] args, string name, int defaultValue)
        {
            string value = Arg(args, name, null);
            return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : defaultValue;
        }

        private static bool HasFlag(string[] args, string name)
        {
            return args.Any(a => String.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string JoinIds(IEnumerable<int> ids)
        {
            return String.Join(",", ids.Distinct());
        }

        private static string F(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static List<T> Query<T>(MySqlConnection connection, string sql, Func<MySqlDataReader, T> map)
        {
            List<T> result = new List<T>();
            using MySqlCommand command = new MySqlCommand(sql, connection);
            command.CommandTimeout = 300;
            using MySqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
                result.Add(map(reader));
            return result;
        }

        private sealed class MediaInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public double Duration { get; set; }
        }

        private sealed class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }

        private sealed class LayerRow
        {
            public int VariationId { get; set; }
            public int Position { get; set; }
            public string Edit { get; set; }
            public string Action { get; set; }
        }

        private sealed class CategoryLinkRow
        {
            public int VariationId { get; set; }
            public int CategoryId { get; set; }
        }

        private sealed class MetaDataRow
        {
            public int MediaItemId { get; set; }
            public int Id { get; set; }
            public string Name { get; set; }
            public string GroupName { get; set; }
            public string Type { get; set; }
            public bool IsVisible { get; set; }
            public string Value { get; set; }
        }

        private sealed class DescriptionRow
        {
            public int Id { get; set; }
            public string Value { get; set; }
            public int MediaItemId { get; set; }
        }
    }
}
