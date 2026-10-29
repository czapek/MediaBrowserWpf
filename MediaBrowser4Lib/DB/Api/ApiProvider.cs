using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using MediaBrowser4.Objects;
using MediaProcessing.FaceDetection;
using Newtonsoft.Json;

namespace MediaBrowser4.DB.Api
{
    public sealed class ApiProvider : DBProvider
    {
        private readonly ApiConnectionOptions options;
        private readonly SpringApiClient client;
        private readonly ApiInsert insert;
        private readonly object contentCacheSync = new object();

        public ApiProvider(ApiConnectionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            this.options = options;
            this.client = new SpringApiClient(options);
            this.insert = new ApiInsert(options, this.client);
            this.insert.OnInsert += (sender, args) =>
            {
                if (this.OnInsert != null)
                    this.OnInsert(this, args);
            };

            this.Guid = String.Empty;
            this.DBName = String.Empty;
            this.User = String.Empty;
            this.Host = String.Empty;
        }

        public string LastValidationError { get; private set; }

        public string GetLocalContentPath(MediaItem item)
        {
            if (item == null || item.Id <= 0)
                return null;

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MediaBrowserWpf",
                "content-cache",
                SafePathPart(this.DBName),
                item.Id.ToString());

            string extension = Path.GetExtension(item.Filename);
            string fileName = item.Id + "_" + item.LastWriteDate.Ticks
                + (String.IsNullOrWhiteSpace(extension) ? ".bin" : extension);
            string path = Path.Combine(folder, fileName);

            lock (this.contentCacheSync)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                    return path;

                byte[] content = this.client.GetBytes("/mediafiles/" + item.Id + "/content");

                if (content == null || content.Length == 0)
                    return null;

                Directory.CreateDirectory(folder);
                string tempPath = path + ".tmp";

                try
                {
                    File.WriteAllBytes(tempPath, content);

                    if (File.Exists(path))
                        File.Delete(path);

                    File.Move(tempPath, path);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            File.Delete(tempPath);
                        }
                        catch
                        {
                        }
                    }
                }

                return path;
            }
        }

        public ApiImageAnalysisDto GetImageAnalysis(int mediaItemId)
        {
            if (mediaItemId <= 0)
                return null;

            return this.client.GetOrDefault<ApiImageAnalysisDto>(
                "/mediafiles/" + mediaItemId + "/image-analysis");
        }

        private static string SafePathPart(string value)
        {
            string result = String.IsNullOrWhiteSpace(value) ? "default" : value;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                result = result.Replace(invalidChar, '_');

            return result;
        }

        public override event EventHandler<MediaItemCallbackArgs> OnInsert;
        public override event EventHandler<MediaItemNewThumbArgs> OnThumbUpdate;

        public override ITransaction MBTransaction
        {
            get { throw new NotSupportedException("Transaktionen werden vom API-Provider nicht unterstützt."); }
        }

        public override ICommandHelper MBCommand
        {
            get { throw new NotSupportedException("Direkte SQL-Aufrufe werden vom API-Provider nicht unterstützt."); }
        }

        public override string Guid { get; set; }
        public override string DBName { get; set; }
        public override string User { get; set; }
        public override string Host { get; set; }

        public override bool ValidateDB()
        {
            try
            {
                ApiCatalogInfoDto info = this.client.Get<ApiCatalogInfoDto>("/catalog/validate");

                if (info == null || !info.Valid)
                {
                    this.LastValidationError = info == null ? "Die API lieferte keine Kataloginformationen." : info.Message;
                    return false;
                }

                this.Guid = info.Guid;
                this.DBName = info.DbName;
                this.User = info.User;
                this.Host = info.Host;
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
            this.client.Post("/catalog/create");
        }

        public override Dictionary<string, string> GetDBProperties()
        {
            Dictionary<string, string> loaded = this.client.Get<Dictionary<string, string>>("/catalog/properties");
            Dictionary<string, string> result = loaded == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);

            SetDefaultProperty(result, "CheckSumMaxLength", "52428800");
            SetDefaultProperty(result, "CommitInsertSeconds", "5");
            SetDefaultProperty(result, "DefaultRoleID", "1");
            SetDefaultProperty(result, "ThumbnailJPEGQuality", "80");
            SetDefaultProperty(result, "ThumbnailSize", "150");
            SetDefaultProperty(result, "AutoCategorizeDate", "0");
            SetDefaultProperty(result, "MissingFileBehavior", "show");

            return result;
        }

        private static void SetDefaultProperty(Dictionary<string, string> properties, string key,
            string value)
        {
            if (!properties.ContainsKey(key))
                properties[key] = value;
        }

        public override void SaveDBProperties(Dictionary<string, string> propertyTable)
        {
            this.client.Put("/catalog/properties", propertyTable ?? new Dictionary<string, string>());
        }

        public override int ExecuteNonQuery(string sql)
        {
            throw new NotSupportedException("Direkte SQL-Aufrufe werden vom API-Provider nicht unterstützt.");
        }

        public override void AddMediaItemAsync(MediaItem mItem)
        {
            throw new NotSupportedException("Dateiimporte werden im API-Modus nicht unterstützt.");
        }

        public override List<Folder> GetFolderlist()
        {
            List<ApiFolderDto> folders = this.client.Get<List<ApiFolderDto>>("/folders");
            return folders == null
                ? new List<Folder>()
                : folders.Select(x => new Folder(x.Id, x.Foldername, x.ItemCount)).ToList();
        }

        public override void SetFolder(Folder folder)
        {
            if (folder == null)
                return;

            ApiFolderDto result = this.client.Post<ApiFolderDto>("/folders/ensure", new ApiFolderDto
            {
                Id = folder.Id,
                Foldername = folder.FullPath,
                ItemCount = folder.ItemCount
            });

            if (result != null)
            {
                folder.Id = result.Id;
                folder.ItemCount = result.ItemCount;
            }
        }

        public override void RemoveFolder(Folder folder)
        {
            if (folder != null && folder.Id > 0)
                this.client.Delete("/folders/" + folder.Id);
        }

        public override int CountItemsInFolder(Folder folder)
        {
            if (folder == null || folder.Id <= 0)
                return 0;

            return this.client.Get<int>("/folders/" + folder.Id + "/count");
        }

        public override Dictionary<string, DateTime> LastAddedFolders()
        {
            Dictionary<string, DateTime> result =
                this.client.Get<Dictionary<string, DateTime>>("/catalog/last-folders");

            return result ?? new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        public override CategoryTree GetCategoryTree()
        {
            List<ApiCategoryDto> categories = this.client.Get<List<ApiCategoryDto>>("/categories/tree")
                ?? new List<ApiCategoryDto>();
            Dictionary<int, Category> dictionary = new Dictionary<int, Category>();
            List<Category> mapped = new List<Category>();

            foreach (ApiCategoryDto dto in categories)
            {
                Category category = ToCategory(dto);
                dictionary[category.Id] = category;
                mapped.Add(category);
            }

            foreach (Category category in mapped)
            {
                Category parent;

                if (category.ParentId > 0 && dictionary.TryGetValue(category.ParentId, out parent))
                    category.Parent = parent;
            }

            CategoryCollection roots = new CategoryCollection(
                mapped.Where(x => x.ParentId <= 0 || !dictionary.ContainsKey(x.ParentId)));
            CategoryTree tree = new CategoryTree(roots, dictionary);

            return tree;
        }

        public override List<Category> GetCategories(int variationId)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "variationId", variationId.ToString() }
            };

            List<ApiCategoryDto> categories = this.client.Get<List<ApiCategoryDto>>("/categories", query);
            return categories == null ? new List<Category>() : categories.Select(ToCategory).ToList();
        }

        public override void SetCategory(Category category)
        {
            if (category == null)
                return;

            ApiCategoryDto result = this.client.Post<ApiCategoryDto>("/categories", ToCategoryDto(category));

            if (result != null)
                CopyCategory(result, category);
        }

        public override Category SetCategoryByPath(string catPath, string catSortPath)
        {
            ApiCategoryDto result = this.client.Post<ApiCategoryDto>("/categories/by-path", new ApiPathDto
            {
                Path = catPath,
                SortPath = catSortPath
            });

            return result == null ? null : ToCategory(result);
        }

        public override bool RemoveCategory(Category category)
        {
            if (category == null || category.Id <= 0)
                return false;

            this.client.Delete("/categories/" + category.Id);
            return true;
        }

        public override List<Category> GetCategoriesDiaryGeoData(double longitute, double width,
            double latitude, double height)
        {
            return this.GetGeoCategories("diary", longitute, width, latitude, height);
        }

        public override List<Category> GetCategoriesLocationGeoData(double longitute, double width,
            double latitude, double height)
        {
            return this.GetGeoCategories("location", longitute, width, latitude, height);
        }

        private List<Category> GetGeoCategories(string kind, double longitude, double width,
            double latitude, double height)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "kind", kind },
                { "longitude", longitude.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "width", width.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "latitude", latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "height", height.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            };

            List<ApiCategoryDto> categories = this.client.Get<List<ApiCategoryDto>>("/categories/geo", query);
            return categories == null ? new List<Category>() : categories.Select(ToCategory).ToList();
        }

        public override Dictionary<Category, int> GetCategoriesFromMediaItems(List<MediaItem> mItemList)
        {
            Dictionary<Category, int> result = new Dictionary<Category, int>();

            if (mItemList == null || mItemList.Count == 0)
                return result;

            List<int> variationIds = mItemList.Select(x => x.VariationId).Where(x => x > 0).Distinct().ToList();
            List<ApiCategoryCountDto> counts =
                this.client.Post<List<ApiCategoryCountDto>>("/categories/from-media", variationIds);

            if (counts != null)
            {
                foreach (ApiCategoryCountDto count in counts)
                {
                    if (count != null && count.Category != null)
                        result[ToCategory(count.Category)] = count.Count;
                }
            }

            return result;
        }

        public override List<string> GetCategoryIdentifiersFromMediaItem(MediaItem mItem)
        {
            if (mItem == null || mItem.VariationId <= 0)
                return new List<string>();

            List<int> identifiers = this.client.Get<List<int>>(
                "/categories/identifiers/" + mItem.VariationId);

            return identifiers == null ? new List<string>() : identifiers.Select(x => x.ToString()).ToList();
        }

        public override List<Role> GetRoleList()
        {
            List<ApiRoleDto> roles = this.client.Get<List<ApiRoleDto>>("/roles");
            return roles == null
                ? new List<Role>()
                : roles.Select(x => new Role(x.Id, x.Name, x.Password, x.Description)).ToList();
        }

        public override List<MediaItem> GetMediaItems(MediaItemRequest request)
        {
            if (request == null)
                return new List<MediaItem>();

            if (request is MediaItemObservableCollectionRequest)
                return ((MediaItemObservableCollectionRequest)request).MediaItemList;

            if (request is MediaItemSqlRequest)
                return this.ExecuteSqlQuery((MediaItemSqlRequest)request);

            if (request is MediaItemSearchRequest)
            {
                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = "SEARCH",
                    SearchToken = ToSearchToken(((MediaItemSearchRequest)request).SearchToken),
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemFolderRequest)
            {
                MediaItemFolderRequest folderRequest = (MediaItemFolderRequest)request;

                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = "FOLDERS",
                    FolderIds = folderRequest.FoldersComplete.Where(x => x.Id > 0).Select(x => x.Id).ToList(),
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemCategoryRequest)
            {
                MediaItemCategoryRequest categoryRequest = (MediaItemCategoryRequest)request;
                bool missing = categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_DATE
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_LOCATION
                    || categoryRequest.CategoryRequestType == MediaItemCategoryRequestType.NO_OTHER;

                bool recursive = request.RequestType != MediaItemRequestType.SINGLE;
                int categoryCount = categoryRequest.Categories.Count(x => x.Id > 0);

                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = missing ? "MISSING_CATEGORIES" : "CATEGORIES",
                    CategoryRequestType = categoryRequest.CategoryRequestType.ToString(),
                    CategoryIds = GetCategoryIds(categoryRequest.Categories, recursive),
                    IsIntersection = request.RequestType == MediaItemRequestType.INTERSECT
                        && categoryCount > 1 && !recursive,
                    IsRecursive = recursive,
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemFilesRequest)
            {
                MediaItemFilesRequest filesRequest = (MediaItemFilesRequest)request;

                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = filesRequest.FilesRequestType == MediaItemFilesRequestType.FilesNotExist
                        ? "MISSING_FILES"
                        : (filesRequest.FilesRequestType == MediaItemFilesRequestType.FilesFromChecksum
                            ? "FILES_FROM_CHECKSUM"
                            : "FILES_FROM_LIST"),
                    FileNames = filesRequest.FileList ?? new List<string>(),
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemSortRequest)
            {
                MediaItemSortRequest sortRequest = (MediaItemSortRequest)request;

                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = "SORT",
                    MediaItemIds = sortRequest.MediaItemList == null
                        ? new List<int>()
                        : sortRequest.MediaItemList.Select(x => x.Id).ToList(),
                    SortFields = sortRequest.SortTypeList
                        .Select(x => x.Item1 + ":" + x.Item2).ToList(),
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemDublicatesRequest)
            {
                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = "DUPLICATES",
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemRequestGeoData)
            {
                MediaItemRequestGeoData geoRequest = (MediaItemRequestGeoData)request;

                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = "GEO",
                    Longitude = geoRequest.Longitute,
                    Latitude = geoRequest.Latitude,
                    Width = geoRequest.Width,
                    Height = geoRequest.Height,
                    Limit = GetLimit(request)
                });
            }

            if (request is MediaItemVirtualRequest)
            {
                return this.QueryMedia(new ApiMediaQueryDto
                {
                    Mode = "VIRTUAL",
                    FileNames = ((MediaItemVirtualRequest)request).FileList ?? new List<string>(),
                    Limit = GetLimit(request)
                });
            }

            throw new NotSupportedException("Nicht unterstützter MediaItemRequest: " + request.GetType().Name);
        }

        public override List<MediaItem> GetMediaItemsFromFolders(List<Folder> folderList,
            string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "FOLDERS",
                FolderIds = folderList == null
                    ? new List<int>()
                    : folderList.Where(x => x.Id > 0).Select(x => x.Id).ToList(),
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override List<MediaItem> GetMediaItemsWithMissingFiles(string sortString, int limtRequest)
        {
            return new List<MediaItem>();
        }

        public override List<MediaItem> GetMediaItemsWithMissingThumbs(string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "MISSING_THUMBS",
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override List<MediaItem> GetMediaItemsFromSearchToken(SearchToken searchToken,
            bool storeView, string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "SEARCH",
                SearchToken = ToSearchToken(searchToken),
                StoreView = storeView,
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override List<MediaItem> GetMediaItemsFromCategories(List<Category> categoryList,
            bool isIntersection, bool isRecursive, string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "CATEGORIES",
                CategoryIds = GetCategoryIds(categoryList, isRecursive),
                IsIntersection = isIntersection
                    && categoryList != null && categoryList.Count(x => x.Id > 0) > 1
                    && !isRecursive,
                IsRecursive = isRecursive,
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override List<MediaItem> GetMediaItemsFromTrashfolder(string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "TRASH",
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override List<MediaItem> GetMediaItemsFromBookmarks(string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "BOOKMARKS",
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override List<MediaItem> GetMediaItemsGeoData(double longitute, double width,
            double latitude, double height, int limitRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "GEO",
                Longitude = longitute,
                Width = width,
                Latitude = latitude,
                Height = height,
                Limit = limitRequest
            });
        }

        public override List<MediaItem> GetMediaItemsFromChecksum(string checksum)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "CHECKSUM",
                Checksum = checksum,
                Limit = 500
            });
        }

        public override MediaItem GetMediaItemFromFile(string file)
        {
            List<MediaItem> result = this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "FILE_PATH",
                FilePath = file,
                Limit = 1
            });

            return result.Count == 1 ? result[0] : null;
        }

        public override List<MediaItem> GetMediaItemsFromFileName(string filename)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "FILENAME",
                FileNames = new List<string> { filename },
                Limit = 500
            });
        }

        public override List<MediaItem> GetMediaItemsForRequestTicket(long requestTicket,
            string sortString, int limtRequest)
        {
            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "REQUEST",
                RequestTicket = requestTicket,
                SortString = sortString,
                Limit = limtRequest
            });
        }

        public override void GetThumbsForAllMediaItems(List<MediaItem> mediaItemList)
        {
            if (mediaItemList == null || mediaItemList.Count == 0)
                return;

            List<int> variationIds = mediaItemList
                .Where(x => x.VariationId > 0)
                .Select(x => x.VariationId)
                .Distinct()
                .ToList();

            if (variationIds.Count == 0)
                return;

            List<ApiThumbDto> thumbs = this.client.Post<List<ApiThumbDto>>("/mediafiles/thumbs", variationIds);

            if (thumbs == null)
                return;

            Dictionary<int, ApiThumbDto> byVariation = thumbs
                .Where(x => x != null)
                .GroupBy(x => x.VariationId)
                .ToDictionary(x => x.Key, x => x.First());

            foreach (MediaItem item in mediaItemList)
            {
                ApiThumbDto thumb;

                if (byVariation.TryGetValue(item.VariationId, out thumb))
                {
                    item.ThumbJpegData = thumb.Thumb;
                    item.IsThumbJpegDataOutdated = thumb.IsOutdated;
                }
            }
        }

        public override List<Variation> GetVariations(MediaItem mItem, bool getThumbdata)
        {
            if (mItem == null || mItem.Id <= 0)
                return new List<Variation>();

            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "includeThumbs", getThumbdata ? "true" : "false" }
            };

            List<ApiVariationDto> variations = this.client.Get<List<ApiVariationDto>>(
                "/mediafiles/" + mItem.Id + "/variations", query);

            return variations == null
                ? new List<Variation>()
                : variations.Select(ToVariation).ToList();
        }

        public override Variation SetNewVariation(MediaItem mItem, string name, bool setDefault,
            string layerAction, string layerEdit, byte[] thumbData)
        {
            if (mItem == null || mItem.Id <= 0)
                return null;

            ApiVariationDto result = this.client.Post<ApiVariationDto>(
                "/mediafiles/" + mItem.Id + "/variations", new ApiVariationCreateDto
                {
                    Name = name,
                    SetDefault = setDefault,
                    LayerAction = layerAction,
                    LayerEdit = layerEdit,
                    Thumb = thumbData
                });

            if (result == null)
                return null;

            Variation variation = ToVariation(result);

            if (setDefault)
            {
                mItem.VariationId = variation.Id;
                mItem.VariationIdDefault = variation.Id;
            }

            return variation;
        }

        public override bool RemoveVariation(MediaItem mItem)
        {
            if (mItem == null || mItem.VariationId <= 0)
                return false;

            this.client.Delete("/variations/" + mItem.VariationId);
            return true;
        }

        public override bool RemoveVariation(MediaItem mItem, string name)
        {
            if (mItem == null || mItem.Id <= 0 || String.IsNullOrWhiteSpace(name))
                return false;

            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "name", name }
            };

            this.client.Delete("/mediafiles/" + mItem.Id + "/variations", query);
            return true;
        }

        public override bool SetVariationDefault(MediaItem mItem, Variation variation)
        {
            if (mItem == null || variation == null || variation.Id <= 0)
                return false;

            this.client.Post("/variations/" + variation.Id + "/default");
            mItem.VariationId = variation.Id;
            mItem.VariationIdDefault = variation.Id;
            return true;
        }

        public override bool EditVariation(List<Variation> variationList)
        {
            if (variationList == null)
                return false;

            foreach (Variation variation in variationList)
            {
                if (variation != null && variation.Id > 0)
                    this.client.Put("/variations/" + variation.Id, ToVariationDto(variation));
            }

            return true;
        }

        public override void ReplaceVariations(Dictionary<MediaItem, List<Variation>> mediaItemDic)
        {
            if (mediaItemDic == null)
                return;

            foreach (KeyValuePair<MediaItem, List<Variation>> entry in mediaItemDic)
            {
                if (entry.Key == null || entry.Key.Id <= 0)
                    continue;

                List<ApiVariationDto> result = this.client.Post<List<ApiVariationDto>>(
                    "/mediafiles/" + entry.Key.Id + "/variations/replace",
                    (entry.Value ?? new List<Variation>()).Select(ToVariationDto).ToList());

                if (result != null && result.Count > 0)
                {
                    entry.Key.VariationId = result[0].Id;
                    entry.Key.VariationIdDefault = result.FirstOrDefault(x => x.Id == entry.Key.VariationId) != null
                        ? entry.Key.VariationId
                        : result[0].Id;
                }
            }
        }

        public override List<Layer> GetLayers(Variation variation)
        {
            if (variation == null || variation.Id <= 0)
                return new List<Layer>();

            return this.GetLayers(variation.Id);
        }

        public override List<Layer> GetLayersForMediaItem(MediaItem mItem)
        {
            return mItem == null || mItem.VariationId <= 0
                ? new List<Layer>()
                : this.GetLayers(mItem.VariationId);
        }

        private List<Layer> GetLayers(int variationId)
        {
            List<ApiLayerDto> layers = this.client.Get<List<ApiLayerDto>>(
                "/variations/" + variationId + "/layers");

            return layers == null
                ? new List<Layer>()
                : layers.Select(x => new Layer(x.Edit, x.Action, x.Position)).ToList();
        }

        public override void SetLayersForMediaItem(MediaItem mItem)
        {
            if (mItem == null || mItem.VariationId <= 0)
                return;

            this.client.Put("/variations/" + mItem.VariationId + "/layers",
                (mItem.Layers ?? new List<Layer>()).Select(ToLayerDto).ToList());
        }

        public override void SetLayersForMediaItems(List<MediaItem> mItemList)
        {
            if (mItemList == null)
                return;

            foreach (MediaItem item in mItemList)
                this.SetLayersForMediaItem(item);
        }

        public override byte[] GetThumbJpegData(int variationId)
        {
            if (variationId <= 0)
                return null;

            return this.client.GetBytes("/variations/" + variationId + "/thumb", ThumbQuery(0, 0));
        }

        public override void SetThumbForMediaItem(Bitmap bmp, MediaItem mItem)
        {
            if (bmp == null || mItem == null || mItem.VariationId <= 0)
                return;

            byte[] thumbnail = MediaProcessing.ResizeImage.GetThumbnail(bmp,
                MediaBrowserContext.ThumbnailSize, MediaBrowserContext.ThumbnailJPEGQuality);

            this.client.Put("/variations/" + mItem.VariationId + "/thumb", new ApiThumbUploadDto
            {
                Thumb = thumbnail,
                Extension = "jpg",
                Width = bmp.Width,
                Height = bmp.Height,
                IsOutdated = false
            });

            mItem.ThumbJpegData = thumbnail;
            mItem.IsThumbJpegDataOutdated = false;

            if (this.OnThumbUpdate != null && mItem.VariationId == mItem.VariationIdDefault)
                this.OnThumbUpdate(this, new MediaItemNewThumbArgs(bmp, mItem));
        }

        public override PreviewObject GetImagePreviewDB(int variationId, System.Drawing.Size size)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "width", size.Width.ToString() },
                { "height", size.Height.ToString() }
            };

            ApiPreviewDto preview = this.client.GetOrDefault<ApiPreviewDto>(
                "/previews/" + variationId, query);

            return preview == null
                ? null
                : new PreviewObject
                {
                    Binary = preview.Binary,
                    Width = preview.Width,
                    Height = preview.Height,
                    Extension = preview.Extension,
                    TempPath = preview.TempPath
                };
        }

        public override Faces GetFaceDetectionPreviewDB(int variationId, System.Drawing.Size size)
        {
            return new Faces
            {
                Width = 0,
                Height = 0,
                Facelist = new List<Rectangle>()
            };
        }

        public override Dictionary<int, int> GetAllFaces(System.Drawing.Size size, double min,
            double max, int countMin, int countMax, List<MediaItem> itemList)
        {
            return new Dictionary<int, int>();
        }

        public override bool IsImageInPreviewDB(int variationId, System.Drawing.Size size)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "variationId", variationId.ToString() },
                { "width", size.Width.ToString() },
                { "height", size.Height.ToString() }
            };

            return this.client.Get<bool>("/previews/exists", query);
        }

        public override void DeleteFromPreviewDB(List<MediaItem> fmediaItemList, System.Drawing.Size size)
        {
            throw new NotSupportedException("Preview-Dateien werden im API-Modus nicht verwaltet.");
        }

        public override void WriteToPreviewDB(List<string> fileList, System.Drawing.Size size)
        {
            throw new NotSupportedException("Preview-Dateien werden im API-Modus nicht verwaltet.");
        }

        public override List<int> GetPreviewDBVariationIdList(System.Drawing.Size size)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "width", size.Width.ToString() },
                { "height", size.Height.ToString() }
            };

            return this.client.Get<List<int>>("/previews/variation-ids", query) ?? new List<int>();
        }

        public override List<MediaItem> GetDublicates(List<MediaItem> mList,
            MediaItem.DublicateCriteria criteria)
        {
            if (mList == null || mList.Count == 0)
                return new List<MediaItem>();

            return this.QueryMedia(new ApiMediaQueryDto
            {
                Mode = "DUPLICATES",
                Criteria = criteria.ToString(),
                MediaItemIds = mList.Select(x => x.Id).ToList(),
                Limit = 500
            });
        }

        public override Dictionary<int, string> GetDublicates(MediaItem mediaItem,
            MediaItem.DublicateCriteria criteria)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();

            if (mediaItem == null || mediaItem.Id <= 0)
                return result;

            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "criteria", criteria.ToString() }
            };

            List<ApiDuplicateDto> duplicates = this.client.Get<List<ApiDuplicateDto>>(
                "/duplicates/" + mediaItem.Id, query);

            if (duplicates != null)
            {
                foreach (ApiDuplicateDto duplicate in duplicates)
                {
                    if (duplicate != null)
                        result[duplicate.MediaItemId] = duplicate.Value;
                }
            }

            return result;
        }

        public override bool IsDublicate(MediaItem mItem, MediaItem.DublicateCriteria criteria)
        {
            if (mItem == null || mItem.Id <= 0)
                return false;

            return this.client.Post<bool>("/duplicates/check", new ApiMediaActionDto
            {
                Action = "CHECK",
                MediaItemIds = new List<int> { mItem.Id },
                Criteria = criteria.ToString()
            });
        }

        public override int CanFoundByChecksum(string md5)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "checksum", md5 }
            };

            return this.client.Get<int>("/mediafiles/checksum-count", query);
        }

        public override void UpdateDublicate(MediaItem mediaItem)
        {
            if (mediaItem == null)
                return;

            this.PostAction(new ApiMediaActionDto
            {
                Action = "UPDATE_DUPLICATE",
                MediaItemIds = new List<int> { mediaItem.Id }
            });
        }

        public override List<MediaItemRequest> GetUserDefinedRequests()
        {
            List<ApiUserRequestDto> requests = this.client.Get<List<ApiUserRequestDto>>("/requests");
            List<MediaItemRequest> result = new List<MediaItemRequest>();

            if (requests == null)
                return result;

            foreach (ApiUserRequestDto request in requests)
            {
                if (request == null)
                    continue;

                MediaItemRequest itemRequest;

                if (String.Equals(request.Kind, "SQL", StringComparison.OrdinalIgnoreCase))
                {
                    itemRequest = new MediaItemSqlRequest(request.Payload, null, String.Empty, request.Limit);
                }
                else
                {
                    SearchToken token = FromSearchToken(
                        JsonConvert.DeserializeObject<ApiSearchTokenDto>(request.Payload ?? "{}"));
                    itemRequest = new MediaItemSearchRequest(token, 1);
                }

                itemRequest.UserDefinedId = request.Id;
                itemRequest.UserDefinedName = request.Name;
                itemRequest.LimitRequest = request.Limit > 0 ? request.Limit : MediaBrowserContext.LimitRequest;
                result.Add(itemRequest);
            }

            return result;
        }

        public override bool SaveUserDefinedRequest(MediaItemRequest request)
        {
            if (request == null)
                return false;

            ApiUserRequestDto dto;
            MediaItemSearchRequest searchRequest = request as MediaItemSearchRequest;
            MediaItemSqlRequest sqlRequest = request as MediaItemSqlRequest;

            if (searchRequest != null)
            {
                dto = new ApiUserRequestDto
                {
                    Id = searchRequest.UserDefinedId,
                    Name = searchRequest.UserDefinedName,
                    Kind = "SEARCH",
                    Limit = searchRequest.LimitRequest,
                    Payload = JsonConvert.SerializeObject(ToSearchToken(searchRequest.SearchToken))
                };
            }
            else if (sqlRequest != null)
            {
                dto = new ApiUserRequestDto
                {
                    Id = sqlRequest.UserDefinedId,
                    Name = sqlRequest.UserDefinedName,
                    Kind = "SQL",
                    Limit = sqlRequest.LimitRequest,
                    Payload = sqlRequest.Sql
                };
            }
            else
            {
                return false;
            }

            ApiUserRequestDto result = this.client.Post<ApiUserRequestDto>("/requests", dto);

            if (result != null)
                request.UserDefinedId = result.Id;

            return result != null;
        }

        public override void DeleteUserDefinedRequest(MediaItemRequest request)
        {
            if (request != null && request.UserDefinedId > 0)
                this.client.Delete("/requests/" + request.UserDefinedId);
        }

        public override string GetSQLFromRequestTicket(long ticket)
        {
            ApiTextDto result = this.client.Get<ApiTextDto>("/requests/" + ticket + "/sql");
            return result == null ? null : result.Value;
        }

        public override void SetSQLForRequestTicket(long ticket, string sql)
        {
            this.client.Put("/requests/" + ticket + "/sql", new ApiTextDto { Value = sql });
        }

        public override MediaItemSqlRequest GetSqlRequest(List<MediaItem> mediaItemList)
        {
            if (mediaItemList == null || mediaItemList.Count == 0)
                return null;

            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "mediaItemIds", String.Join(",", mediaItemList.Select(x => x.Id).Distinct()) }
            };

            ApiSqlQueryDto result = this.client.Get<ApiSqlQueryDto>("/mediafiles/sql-request", query);

            return result == null
                ? null
                : new MediaItemSqlRequest(
                    result.Sql,
                    result.Parameters == null
                        ? new List<SqlParameterInfo>()
                        : result.Parameters.Select(x => new SqlParameterInfo(x.Name, x.Value)).ToList(),
                    result.SortString,
                    result.Limit);
        }

        public override List<MetaData> GetMetaDataFromMediaItems(List<MediaItem> mItemList)
        {
            if (mItemList == null || mItemList.Count == 0)
                return new List<MetaData>();

            List<ApiMetaDataDto> metadata = this.client.Post<List<ApiMetaDataDto>>(
                "/metadata/query", mItemList.Select(x => x.Id).Distinct().ToList());

            return metadata == null
                ? new List<MetaData>()
                : metadata.Where(x => x != null).Select(ToMetaData).ToList();
        }

        public override List<string> GetMetadataKeyList()
        {
            return this.client.Get<List<string>>("/metadata/keys") ?? new List<string>();
        }

        public override List<Description> GetDescription(List<MediaItem> mList)
        {
            if (mList == null || mList.Count == 0)
                return new List<Description>();

            List<ApiDescriptionDto> descriptions = this.client.Post<List<ApiDescriptionDto>>(
                "/descriptions/query", mList.Select(x => x.Id).Distinct().ToList());

            if (descriptions == null)
                return new List<Description>();

            Dictionary<int, MediaItem> items = mList.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());

            return descriptions.Where(x => x != null).Select(x =>
            {
                Description description = new Description
                {
                    Id = x.Id,
                    Value = x.Value,
                    Command = ParseDescriptionCommand(x.Command),
                    MediaItemList = x.MediaItemIds == null
                        ? new List<MediaItem>()
                        : x.MediaItemIds.Where(items.ContainsKey).Select(id => items[id]).ToList()
                };

                return description;
            }).ToList();
        }

        public override string GetDescription(MediaItem mItem)
        {
            if (mItem == null || mItem.Id <= 0)
                return null;

            ApiTextDto result = this.client.GetOrDefault<ApiTextDto>("/mediafiles/" + mItem.Id + "/description");
            return result == null ? null : result.Value;
        }

        public override void SetDescription(Description desc)
        {
            if (desc == null)
                return;

            this.client.Post("/descriptions", new ApiDescriptionDto
            {
                Id = desc.Id,
                Value = desc.Value,
                Command = desc.Command.ToString(),
                MediaItemIds = desc.MediaItemList == null
                    ? new List<int>()
                    : desc.MediaItemList.Select(x => x.Id).ToList()
            });
        }

        public override List<Attachment> GetAttachment(List<MediaItem> mediaItemList)
        {
            if (mediaItemList == null || mediaItemList.Count == 0)
                return new List<Attachment>();

            List<ApiAttachmentDto> attachments = this.client.Post<List<ApiAttachmentDto>>(
                "/attachments/query", mediaItemList.Select(x => x.Id).Distinct().ToList());

            if (attachments == null)
                return new List<Attachment>();

            Dictionary<int, MediaItem> items = mediaItemList.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
            List<Attachment> result = new List<Attachment>();

            foreach (ApiAttachmentDto dto in attachments.Where(x => x != null))
            {
                List<MediaItem> attachedItems = dto.MediaItemIds == null
                    ? new List<MediaItem>()
                    : dto.MediaItemIds.Where(items.ContainsKey).Select(id => items[id]).ToList();

                Attachment attachment;

                if (attachedItems.Count == 1 && String.Equals(dto.Type, "STICKY", StringComparison.OrdinalIgnoreCase))
                    attachment = new Attachment(attachedItems[0], dto.FullName);
                else
                    attachment = new Attachment(attachedItems, dto.FullName);

                attachment.Id = dto.Id;
                attachment.IsReferenced = dto.IsReferenced;
                attachment.IsShared = dto.IsShared;

                if (String.Equals(dto.Type, "DETACHE", StringComparison.OrdinalIgnoreCase))
                    attachment.Detach();

                result.Add(attachment);
            }

            return result;
        }

        public override void SetAttachment(List<Attachment> attachmentList)
        {
            throw new NotSupportedException("Datei-Anhänge werden im API-Modus nicht verändert.");
        }

        public override Dictionary<MediaItem, XmlNode> CreateMediaItemFromXml(List<XmlNode> nodeList,
            Folder folder, List<Category> categoryImportList)
        {
            Dictionary<MediaItem, XmlNode> result = new Dictionary<MediaItem, XmlNode>();

            if (nodeList == null || nodeList.Count == 0)
                return result;

            ApiXmlImportRequestDto request = new ApiXmlImportRequestDto
            {
                FolderId = folder == null ? 0 : folder.Id,
                CategoryIds = categoryImportList == null
                    ? new List<int>()
                    : categoryImportList.Where(x => x.Id > 0).Select(x => x.Id).ToList(),
                Nodes = nodeList.Select((node, index) => new ApiXmlNodeDto
                {
                    SourceId = index.ToString(),
                    Xml = node.OuterXml
                }).ToList()
            };

            List<ApiMediaItemDto> items = this.client.Post<List<ApiMediaItemDto>>(
                "/mediafiles/from-xml", request);

            if (items == null)
                return result;

            for (int i = 0; i < Math.Min(nodeList.Count, items.Count); i++)
            {
                if (items[i] != null)
                    result[ToMediaItem(items[i])] = nodeList[i];
            }

            return result;
        }

        public override void SetRole(List<MediaItem> mList, int roleId)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "SET_ROLE",
                MediaItemIds = Ids(mList),
                RoleId = roleId
            });

            if (mList != null)
            {
                foreach (MediaItem item in mList)
                    item.RoleId = roleId;
            }
        }

        public override void SetPriority(List<MediaItem> mList, int priority)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "SET_PRIORITY",
                MediaItemIds = Ids(mList),
                Priority = priority
            });

            if (mList != null)
            {
                foreach (MediaItem item in mList)
                    item.Priority = priority;
            }
        }

        public override void SetBookmark(List<MediaItem> mediaItemList, bool set)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "SET_BOOKMARK",
                MediaItemIds = Ids(mediaItemList),
                Flag = set
            });

            if (mediaItemList != null)
            {
                foreach (MediaItem item in mediaItemList)
                    item.IsBookmarked = set;
            }
        }

        public override void SetDeleted(List<MediaItem> mediaItemList, bool set)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "SET_DELETED",
                MediaItemIds = Ids(mediaItemList),
                Flag = set
            });

            if (mediaItemList != null)
            {
                foreach (MediaItem item in mediaItemList)
                    item.IsDeleted = set;
            }
        }

        public override void SetTrashFolder(List<MediaItem> mediaItemList, bool set)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "SET_TRASH",
                MediaItemIds = Ids(mediaItemList),
                Flag = set
            });
        }

        public override void SetGeodata(List<MediaItem> mediaItemList)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "SET_GEODATA",
                MediaItemIds = Ids(mediaItemList)
            });
        }

        public override void AdjustMediaDate(List<MediaItem> mediaItemList)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "ADJUST_MEDIA_DATE",
                MediaItemIds = Ids(mediaItemList)
            });
        }

        public override void AddViewTime(MediaItem mediaItem, int seconds)
        {
            if (mediaItem == null || seconds == 0)
                return;

            this.PostAction(new ApiMediaActionDto
            {
                Action = "ADD_VIEW_TIME",
                MediaItemIds = new List<int> { mediaItem.Id },
                Seconds = seconds
            });
        }

        public override void RenameMediaItem(MediaItem mediaItem, string newName)
        {
            throw new NotSupportedException("Dateien werden im API-Modus nicht umbenannt.");
        }

        public override void CopyToFolder(List<MediaItem> mediaItemList, Folder folder)
        {
            throw new NotSupportedException("Dateien werden im API-Modus nicht kopiert.");
        }

        public override Exception RemoveAndRecycle(MediaItem mItem)
        {
            return new NotSupportedException("Dateien werden im API-Modus nicht gelöscht oder recycelt.");
        }

        public override void RemoveFromDB(List<MediaItem> mediaItemList)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "REMOVE_FROM_DB",
                MediaItemIds = Ids(mediaItemList)
            });
        }

        public override void Rotate90(MediaItem mItem)
        {
            if (mItem == null)
                return;

            this.PostAction(new ApiMediaActionDto
            {
                Action = "ROTATE_90",
                MediaItemIds = new List<int> { mItem.Id },
                VariationIds = new List<int> { mItem.VariationId }
            });
        }

        public override void CategorizeMediaItems(List<MediaItem> mList, List<Category> cList)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "CATEGORIZE",
                MediaItemIds = Ids(mList),
                VariationIds = mList == null ? new List<int>() : mList.Select(x => x.VariationId).ToList(),
                Categories = cList == null ? new List<ApiCategoryDto>() : cList.Select(ToCategoryDto).ToList()
            });
        }

        public override void UnCategorizeMediaItems(List<MediaItem> mList, List<Category> cList)
        {
            this.PostAction(new ApiMediaActionDto
            {
                Action = "UNCATEGORIZE",
                MediaItemIds = Ids(mList),
                VariationIds = mList == null ? new List<int>() : mList.Select(x => x.VariationId).ToList(),
                Categories = cList == null ? new List<ApiCategoryDto>() : cList.Select(ToCategoryDto).ToList()
            });
        }

        public override void UpdateMediaItem(MediaItem mItem)
        {
            if (mItem == null || mItem.Id <= 0)
                return;

            this.client.Put("/mediafiles/" + mItem.Id, ToMediaItemDto(mItem));
        }

        public override void CleanDB()
        {
            this.client.Post("/maintenance/clean");
        }

        public override int DeleteGpsFile(DateTime fileTime)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "fileTime", fileTime.ToString("o") }
            };

            this.client.Delete("/gps/files", query);
            return 1;
        }

        public override void InsertGpsPoints(List<GeoPoint> gpsList)
        {
            if (gpsList == null || gpsList.Count == 0)
                return;

            this.client.Post("/gps/points", gpsList.Select(x => new ApiGeoPointDto
            {
                Longitude = x.Longitude,
                Latitude = x.Latitude,
                Altitude = x.Altitude,
                LocalTime = x.LocalTime,
                UtcTime = x.UtcTime,
                GpsFileSize = x.GpsFile == null ? 0 : x.GpsFile.Filesize,
                GpsFileTime = x.GpsFile == null ? DateTime.MinValue : x.GpsFile.FileTime,
                DistanceMeter = x.DistanceMeter,
                Acceleration = x.Acceleration
            }).ToList());
        }

        public override GeoPoint GetGpsNearest(DateTime date)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "date", date.ToString("o") }
            };

            ApiGeoPointDto result = this.client.GetOrDefault<ApiGeoPointDto>("/gps/nearest", query);
            return result == null ? null : ToGeoPoint(result);
        }

        public override List<GpsFile> GetGpsFileList()
        {
            List<ApiGpsFileDto> files = this.client.Get<List<ApiGpsFileDto>>("/gps/files");
            return files == null
                ? new List<GpsFile>()
                : files.Select(x => new GpsFile { Filesize = x.Filesize, FileTime = x.FileTime }).ToList();
        }

        public override List<GeoPoint> GetGpsList(DateTime from, DateTime to)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "from", from.ToString("o") },
                { "to", to.ToString("o") }
            };

            List<ApiGeoPointDto> points = this.client.Get<List<ApiGeoPointDto>>("/gps", query);
            return points == null ? new List<GeoPoint>() : points.Select(ToGeoPoint).ToList();
        }

        public override void InsertMeteorologyData(
            SortedDictionary<DateTime, Tuple<double, double>> data)
        {
            if (data == null || data.Count == 0)
                return;

            this.client.Post("/meteorology", data.Select(x => new ApiMeteorologyDto
            {
                LogTime = x.Key,
                Temperature = x.Value.Item1,
                Humidity = x.Value.Item2
            }).ToList());
        }

        public override SortedDictionary<DateTime, Tuple<double, double>> GetMeteorologyData(
            DateTime start, DateTime stop)
        {
            IDictionary<string, string> query = new Dictionary<string, string>
            {
                { "from", start.ToString("o") },
                { "to", stop.ToString("o") }
            };

            List<ApiMeteorologyDto> data = this.client.Get<List<ApiMeteorologyDto>>("/meteorology", query);
            SortedDictionary<DateTime, Tuple<double, double>> result =
                new SortedDictionary<DateTime, Tuple<double, double>>();

            if (data != null)
            {
                foreach (ApiMeteorologyDto entry in data.Where(x => x != null))
                    result[entry.LogTime] = Tuple.Create(entry.Temperature, entry.Humidity);
            }

            return result;
        }

        private List<MediaItem> ExecuteSqlQuery(MediaItemSqlRequest request)
        {
            ApiSqlQueryDto query = new ApiSqlQueryDto
            {
                Sql = request.Sql,
                SortString = request.SortString,
                Limit = request.LimitRequest,
                Parameters = request.ParameterList == null
                    ? new List<ApiSqlParameterDto>()
                    : request.ParameterList.Select(x => new ApiSqlParameterDto
                    {
                        Name = x.ParameterName,
                        Value = x.Value
                    }).ToList()
            };

            List<ApiMediaItemDto> items =
                this.client.Post<List<ApiMediaItemDto>>("/mediafiles/sql-query", query);

            return items == null ? new List<MediaItem>() : items.Where(x => x != null).Select(ToMediaItem).ToList();
        }

        private List<MediaItem> QueryMedia(ApiMediaQueryDto query)
        {
            if (query.Limit <= 0)
                query.Limit = MediaBrowserContext.LimitRequest;

            List<ApiMediaItemDto> items =
                this.client.Post<List<ApiMediaItemDto>>("/mediafiles/query", query);

            return items == null ? new List<MediaItem>() : items.Where(x => x != null).Select(ToMediaItem).ToList();
        }

        private void PostAction(ApiMediaActionDto action)
        {
            this.client.Post("/mediafiles/actions", action);
        }

        private static int GetLimit(MediaItemRequest request)
        {
            return request.LimitRequest > 0 ? request.LimitRequest : MediaBrowserContext.LimitRequest;
        }

        private static List<int> GetCategoryIds(IEnumerable<Category> categories, bool recursive)
        {
            if (categories == null)
                return new List<int>();

            List<int> result = new List<int>();

            foreach (Category category in categories)
            {
                if (category == null || category.Id <= 0)
                    continue;

                if (recursive)
                {
                    foreach (Category child in category.AllChildrenRecursive())
                    {
                        if (child != null && child.Id > 0)
                            result.Add(child.Id);
                    }
                }
                else
                {
                    result.Add(category.Id);
                }
            }

            return result.Distinct().ToList();
        }

        private static List<int> Ids(List<MediaItem> items)
        {
            return items == null ? new List<int>() : items.Select(x => x.Id).ToList();
        }

        private static IDictionary<string, string> ThumbQuery(int width, int height)
        {
            return new Dictionary<string, string>
            {
                { "width", width.ToString() },
                { "height", height.ToString() }
            };
        }

        private static MediaItem ToMediaItem(ApiMediaItemDto dto)
        {
            if (dto == null)
                return null;

            MediaItem item = String.Equals(dto.Type, "rgb", StringComparison.OrdinalIgnoreCase)
                ? (MediaItem)new MediaItemBitmap()
                : new MediaItemVideo();

            item.Id = dto.Id;
            item.Filename = dto.Filename;
            item.Foldername = dto.Foldername;
            item.Sortorder = dto.Sortorder;
            item.Md5Value = dto.Md5Value;
            item.FileLength = dto.FileLength;
            item.Width = dto.Width;
            item.Height = dto.Height;
            item.Viewed = dto.Viewed;
            item.Duration = dto.Duration;
            item.Frames = dto.Frames;
            item.Orientation = (MediaItem.MediaOrientation)dto.Orientation;
            item.IsBookmarked = dto.IsBookmarked;
            item.IsDeleted = dto.IsDeleted;
            item.IsFileNotFound = dto.IsFileNotFound;
            item.IsMd5Dublicate = dto.IsDuplicate;
            item.Priority = dto.Priority;
            item.RoleId = dto.RoleId;
            item.VariationId = dto.VariationId;
            item.VariationIdDefault = dto.DefaultVariationId > 0 ? dto.DefaultVariationId : dto.VariationId;
            item.DescriptionId = dto.DescriptionId;
            item.Description = dto.Description;
            item.CreationDate = dto.CreationDate;
            item.LastWriteDate = dto.EditDate;
            item.MediaDate = dto.MediaDate;
            item.InsertDate = dto.InsertDate;
            item.Longitude = dto.Longitude;
            item.Latitude = dto.Latitude;
            item.ThumbnailSize = MediaBrowserContext.ThumbnailSize;
            item.IsThumbJpegDataOutdated = dto.IsThumbOutdated;

            if (dto.Thumb != null)
                item.ThumbJpegData = dto.Thumb;

            if (dto.Metadata != null)
                item.MetaData = ToMetaDataList(dto.Metadata);

            return item;
        }

        private static MetaDataList ToMetaDataList(IEnumerable<ApiMetaDataDto> metadata)
        {
            MetaDataList result = new MetaDataList();

            foreach (ApiMetaDataDto entry in metadata)
            {
                if (entry != null)
                    result.Add(ToMetaData(entry));
            }

            return result;
        }

        private static ApiMediaItemDto ToMediaItemDto(MediaItem item)
        {
            return new ApiMediaItemDto
            {
                Id = item.Id,
                Filename = item.Filename,
                Foldername = item.Foldername,
                Sortorder = item.Sortorder,
                Md5Value = item.Md5Value,
                FileLength = item.FileLength,
                Width = item.Width,
                Height = item.Height,
                Viewed = item.Viewed,
                Duration = item.Duration,
                Frames = item.Frames,
                Orientation = (int)item.Orientation,
                Type = item.DBType,
                IsBookmarked = item.IsBookmarked,
                IsDeleted = item.IsDeleted,
                IsDuplicate = item.IsMd5Dublicate,
                IsFileNotFound = item.IsFileNotFound,
                Priority = item.Priority,
                RoleId = item.RoleId,
                VariationId = item.VariationId,
                DefaultVariationId = item.VariationIdDefault,
                CurrentVariationId = item.VariationId,
                DescriptionId = item.DescriptionId,
                Description = item.Description,
                CreationDate = item.CreationDate,
                MediaDate = item.MediaDate,
                EditDate = item.LastWriteDate,
                InsertDate = item.InsertDate,
                Longitude = item.Longitude,
                Latitude = item.Latitude
            };
        }

        private static Category ToCategory(ApiCategoryDto dto)
        {
            return new Category
            {
                Id = dto.Id,
                ParentId = dto.ParentId,
                Name = dto.Name,
                Sortname = dto.Sortname,
                Description = dto.Description,
                FullPath = dto.FullPath,
                Guid = dto.Guid,
                IsDate = dto.IsDate,
                IsLocation = dto.IsLocation,
                IsUnique = dto.IsUnique,
                Date = dto.Date ?? DateTime.MinValue,
                Longitude = dto.Longitude,
                Latitude = dto.Latitude,
                ItemCount = dto.ItemCount
            };
        }

        private static ApiCategoryDto ToCategoryDto(Category category)
        {
            return new ApiCategoryDto
            {
                Id = category.Id,
                ParentId = category.ParentId,
                Name = category.Name,
                Sortname = category.Sortname,
                Description = category.Description,
                FullPath = category.FullPath,
                Guid = category.Guid,
                IsDate = category.IsDate,
                IsLocation = category.IsLocation,
                IsUnique = category.IsUnique,
                Date = category.Date == DateTime.MinValue ? (DateTime?)null : category.Date,
                Longitude = category.Longitude,
                Latitude = category.Latitude,
                ItemCount = category.ItemCount
            };
        }

        private static void CopyCategory(ApiCategoryDto dto, Category category)
        {
            category.Id = dto.Id;
            category.ParentId = dto.ParentId;
            category.Name = dto.Name;
            category.Sortname = dto.Sortname;
            category.Description = dto.Description;
            category.FullPath = dto.FullPath;
            category.Guid = dto.Guid;
            category.IsDate = dto.IsDate;
            category.IsLocation = dto.IsLocation;
            category.IsUnique = dto.IsUnique;
            category.Date = dto.Date ?? DateTime.MinValue;
            category.Longitude = dto.Longitude;
            category.Latitude = dto.Latitude;
            category.ItemCount = dto.ItemCount;
        }

        private static Variation ToVariation(ApiVariationDto dto)
        {
            return new Variation
            {
                Id = dto.Id,
                MediaItemId = dto.MediaItemId,
                Position = dto.Position,
                Name = String.IsNullOrWhiteSpace(dto.Name) ? "Default" : dto.Name,
                Description = dto.Description,
                IsOutdated = dto.IsOutdated,
                ThumbJpegData = dto.Thumb,
                Layers = dto.Layers == null
                    ? null
                    : dto.Layers.Select(x => new Layer(x.Edit, x.Action, x.Position)).ToList()
            };
        }

        private static ApiVariationDto ToVariationDto(Variation variation)
        {
            return new ApiVariationDto
            {
                Id = variation.Id,
                MediaItemId = variation.MediaItemId,
                Position = variation.Position,
                Name = variation.Name,
                Description = variation.Description,
                IsOutdated = variation.IsOutdated,
                Thumb = variation.ThumbJpegData,
                Layers = variation.Layers == null
                    ? new List<ApiLayerDto>()
                    : variation.Layers.Select(ToLayerDto).ToList()
            };
        }

        private static ApiLayerDto ToLayerDto(Layer layer)
        {
            return new ApiLayerDto
            {
                Edit = layer.Edit,
                Action = layer.Action,
                Position = layer.Position
            };
        }

        private static ApiMetaDataDto ToMetaDataDto(MetaData metadata)
        {
            return new ApiMetaDataDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                GroupName = metadata.GroupName,
                Value = metadata.Value,
                Type = metadata.Type,
                IsVisible = metadata.IsVisible,
                IsNull = metadata.Null
            };
        }

        private static MetaData ToMetaData(ApiMetaDataDto dto)
        {
            MetaData metadata = new MetaData(
                dto.Name ?? String.Empty,
                dto.GroupName ?? String.Empty,
                dto.Value ?? String.Empty,
                dto.Type ?? String.Empty,
                dto.IsVisible);
            metadata.Id = dto.Id;
            metadata.Null = dto.IsNull;
            return metadata;
        }

        private static ApiSearchTokenDto ToSearchToken(SearchToken token)
        {
            if (token == null)
                return null;

            return new ApiSearchTokenDto
            {
                SearchText1 = token.SearchText1,
                SearchText1Not = token.SearchText1Not,
                SearchText1Description = token.SearchText1Description,
                SearchText1Category = token.SearchText1Category,
                SearchText1Filename = token.SearchText1Filename,
                SearchText1Md5 = token.SearchText1Md5,
                SearchText1Folder = token.SearchText1Folder,
                SearchText2 = token.SearchText2,
                SearchText2Not = token.SearchText2Not,
                SearchText2Description = token.SearchText2Description,
                SearchText2Category = token.SearchText2Category,
                SearchText2Filename = token.SearchText2Filename,
                SearchText2Md5 = token.SearchText2Md5,
                SearchText2Folder = token.SearchText2Folder,
                MetaDataKey = token.MetaDataKey,
                MetaDataValue = token.MetaDataValue,
                DateFromEnabled = token.DateFromEnabled,
                DateFrom = token.DateFrom,
                DateToEnabled = token.DateToEnabled,
                DateTo = token.DateTo,
                LengthFrom = token.LengthFrom,
                LengthTo = token.LengthTo,
                DurationFrom = token.DurationFrom,
                DurationTo = token.DurationTo,
                PriorityFrom = token.PriorityFrom,
                PriorityTo = token.PriorityTo,
                DimensionFrom = token.DimensionFrom,
                DimensionTo = token.DimensionTo,
                RelationType = token.Relation == null ? "ALL" : token.Relation.RelationType.ToString(),
                MediaTypeRgb = token.MediaTypeRgb,
                MediaTypeDirectShow = token.MediaTypeDirectShow
            };
        }

        private static SearchToken FromSearchToken(ApiSearchTokenDto dto)
        {
            SearchToken token = new SearchToken();

            if (dto == null)
                return token;

            token.SearchText1 = dto.SearchText1 ?? String.Empty;
            token.SearchText1Not = dto.SearchText1Not;
            token.SearchText1Description = dto.SearchText1Description;
            token.SearchText1Category = dto.SearchText1Category;
            token.SearchText1Filename = dto.SearchText1Filename;
            token.SearchText1Md5 = dto.SearchText1Md5;
            token.SearchText1Folder = dto.SearchText1Folder;
            token.SearchText2 = dto.SearchText2 ?? String.Empty;
            token.SearchText2Not = dto.SearchText2Not;
            token.SearchText2Description = dto.SearchText2Description;
            token.SearchText2Category = dto.SearchText2Category;
            token.SearchText2Filename = dto.SearchText2Filename;
            token.SearchText2Md5 = dto.SearchText2Md5;
            token.SearchText2Folder = dto.SearchText2Folder;
            token.MetaDataKey = dto.MetaDataKey ?? String.Empty;
            token.MetaDataValue = dto.MetaDataValue ?? String.Empty;
            token.DateFromEnabled = dto.DateFromEnabled;
            token.DateFrom = dto.DateFrom;
            token.DateToEnabled = dto.DateToEnabled;
            token.DateTo = dto.DateTo;
            token.LengthFrom = dto.LengthFrom;
            token.LengthTo = dto.LengthTo;
            token.DurationFrom = dto.DurationFrom;
            token.DurationTo = dto.DurationTo;
            token.PriorityFrom = dto.PriorityFrom;
            token.PriorityTo = dto.PriorityTo;
            token.DimensionFrom = dto.DimensionFrom;
            token.DimensionTo = dto.DimensionTo;
            token.MediaTypeRgb = dto.MediaTypeRgb;
            token.MediaTypeDirectShow = dto.MediaTypeDirectShow;
            token.Relation = new SearchTokenRelation
            {
                RelationType = ParseRelationType(dto.RelationType)
            };

            return token;
        }

        private static SearchTokenRelationType ParseRelationType(string value)
        {
            SearchTokenRelationType relationType;

            return Enum.TryParse(value, true, out relationType)
                ? relationType
                : SearchTokenRelationType.ALL;
        }

        private static DescriptionCommand ParseDescriptionCommand(string value)
        {
            DescriptionCommand command;

            return Enum.TryParse(value, true, out command)
                ? command
                : DescriptionCommand.SET_AND_CREATE_DESCRIPTION;
        }

        private static GeoPoint ToGeoPoint(ApiGeoPointDto dto)
        {
            return new GeoPoint
            {
                Longitude = dto.Longitude,
                Latitude = dto.Latitude,
                Altitude = dto.Altitude,
                LocalTime = dto.LocalTime,
                UtcTime = dto.UtcTime,
                DistanceMeter = dto.DistanceMeter,
                Acceleration = dto.Acceleration,
                GpsFile = new GpsFile
                {
                    Filesize = dto.GpsFileSize,
                    FileTime = dto.GpsFileTime
                }
            };
        }
    }
}
