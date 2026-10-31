using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ApiMock.Shared;
using MediaBrowser4.DB.Api;

namespace ApiMock
{
    public sealed class MockStore
    {
        private readonly object sync = new object();
        private readonly Dictionary<int, MediaSeed> mediaById = new Dictionary<int, MediaSeed>();
        private readonly Dictionary<int, VariationSeed> variationById = new Dictionary<int, VariationSeed>();
        private readonly Dictionary<int, CategorySeed> categoryById = new Dictionary<int, CategorySeed>();
        private readonly Dictionary<int, FolderSeed> folderById = new Dictionary<int, FolderSeed>();
        private readonly Dictionary<int, RoleSeed> roleById = new Dictionary<int, RoleSeed>();
        private readonly Dictionary<string, byte[]> dataFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, byte[]> thumbOverrides = new Dictionary<int, byte[]>();
        private readonly string dataDirectory;
        private int nextCategoryId = 900000;
        private int nextVariationId = 900000;
        private int nextDescriptionId = 900000;
        private int nextRequestId = 1;

        public SeedRoot Seed { get; private set; }

        private MockStore(SeedRoot seed, Dictionary<string, byte[]> files, string dataDirectory)
        {
            this.Seed = seed;
            this.dataDirectory = dataDirectory;

            foreach (KeyValuePair<string, byte[]> entry in files)
                this.dataFiles[entry.Key.Replace('\\', '/')] = entry.Value;

            RebuildIndexes();
        }

        public static MockStore Load(string[] args)
        {
            string dataDirectory = GetArg(args, "--data", null);
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            string seedJson;

            if (!String.IsNullOrWhiteSpace(dataDirectory))
            {
                dataDirectory = Path.GetFullPath(dataDirectory);
                string seedPath = Path.Combine(dataDirectory, "seed.json");
                if (!File.Exists(seedPath))
                    throw new FileNotFoundException("seed.json nicht gefunden: " + seedPath, seedPath);

                seedJson = File.ReadAllText(seedPath);

                foreach (string file in Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dataDirectory, file).Replace('\\', '/');
                    files[relative] = File.ReadAllBytes(file);
                }
            }
            else
            {
                Assembly assembly = typeof(MockStore).Assembly;
                foreach (string resourceName in assembly.GetManifestResourceNames())
                {
                    string normalized = resourceName.Replace('\\', '/');
                    int index = normalized.IndexOf("data/", StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                        continue;

                    string relative = normalized.Substring(index + "data/".Length);
                    using Stream stream = assembly.GetManifestResourceStream(resourceName);
                    using MemoryStream memory = new MemoryStream();
                    stream.CopyTo(memory);
                    files[relative] = memory.ToArray();
                }

                if (!files.TryGetValue("seed.json", out byte[] seedBytes))
                    throw new InvalidOperationException("Kein eingebettetes data/seed.json gefunden. Mit --data <dir> starten oder Seed neu bauen.");

                seedJson = System.Text.Encoding.UTF8.GetString(seedBytes);
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            SeedRoot seed = JsonSerializer.Deserialize<SeedRoot>(seedJson, options);
            if (seed == null)
                throw new InvalidOperationException("seed.json konnte nicht gelesen werden.");

            return new MockStore(seed, files, dataDirectory);
        }

        private void RebuildIndexes()
        {
            mediaById.Clear();
            variationById.Clear();
            categoryById.Clear();
            folderById.Clear();
            roleById.Clear();

            foreach (MediaSeed item in Seed.MediaItems)
                mediaById[item.Id] = item;
            foreach (VariationSeed variation in Seed.Variations)
                variationById[variation.Id] = variation;
            foreach (CategorySeed category in Seed.Categories)
                categoryById[category.Id] = category;
            foreach (FolderSeed folder in Seed.Folders)
                folderById[folder.Id] = folder;
            foreach (RoleSeed role in Seed.Roles)
                roleById[role.Id] = role;

            nextCategoryId = Math.Max(900000, Seed.Categories.Count == 0 ? 900000 : Seed.Categories.Max(c => c.Id) + 1);
            nextVariationId = Math.Max(900000, Seed.Variations.Count == 0 ? 900000 : Seed.Variations.Max(v => v.Id) + 1);
            nextDescriptionId = Math.Max(900000, Seed.Descriptions.Count == 0 ? 900000 : Seed.Descriptions.Max(d => d.Id) + 1);
            nextRequestId = Math.Max(1, Seed.Requests.Count == 0 ? 1 : Seed.Requests.Max(r => r.Id) + 1);
        }

        public ApiCatalogInfoDto GetCatalogInfo()
        {
            return new ApiCatalogInfoDto
            {
                Valid = true,
                Guid = Seed.Catalog.Guid,
                DbName = Seed.Catalog.DbName,
                User = Seed.Catalog.User,
                Host = Seed.Catalog.Host,
                Message = Seed.Catalog.Message
            };
        }

        public Dictionary<string, string> GetProperties()
        {
            lock (sync)
                return new Dictionary<string, string>(Seed.Properties, StringComparer.OrdinalIgnoreCase);
        }

        public void SaveProperties(Dictionary<string, string> properties)
        {
            lock (sync)
            {
                Seed.Properties = properties == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
            }
        }

        public Dictionary<string, DateTime> GetLastFolders()
        {
            lock (sync)
                return new Dictionary<string, DateTime>(Seed.LastFolders, StringComparer.OrdinalIgnoreCase);
        }

        public List<ApiFolderDto> GetFolders()
        {
            lock (sync)
                return Seed.Folders
                    .OrderBy(f => f.Foldername, StringComparer.OrdinalIgnoreCase)
                    .Select(ToFolderDto)
                    .ToList();
        }

        public ApiFolderDto EnsureFolder(ApiFolderDto dto)
        {
            lock (sync)
            {
                FolderSeed folder = Seed.Folders.FirstOrDefault(f =>
                    String.Equals(f.Foldername, dto.Foldername, StringComparison.OrdinalIgnoreCase));

                if (folder == null)
                {
                    folder = new FolderSeed
                    {
                        Id = folderById.Count == 0 ? 1 : folderById.Keys.Max() + 1,
                        Foldername = dto.Foldername
                    };
                    Seed.Folders.Add(folder);
                    folderById[folder.Id] = folder;
                }

                folder.ItemCount = Seed.MediaItems.Count(m =>
                    String.Equals(m.Foldername, folder.Foldername, StringComparison.OrdinalIgnoreCase));

                return ToFolderDto(folder);
            }
        }

        public void RemoveFolder(int id)
        {
            lock (sync)
            {
                FolderSeed folder;
                if (!folderById.TryGetValue(id, out folder))
                    return;

                Seed.Folders.Remove(folder);
                folderById.Remove(id);

                List<MediaSeed> items = Seed.MediaItems
                    .Where(m => String.Equals(m.Foldername, folder.Foldername, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (MediaSeed item in items)
                    RemoveMedia(item.Id);
            }
        }

        public int CountFolderItems(int id)
        {
            lock (sync)
            {
                FolderSeed folder;
                if (!folderById.TryGetValue(id, out folder))
                    return 0;

                return Seed.MediaItems.Count(m =>
                    String.Equals(m.Foldername, folder.Foldername, StringComparison.OrdinalIgnoreCase));
            }
        }

        public List<ApiCategoryDto> GetCategoryTree()
        {
            lock (sync)
                return Seed.Categories
                    .OrderBy(c => c.FullPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.Id)
                    .Select(ToCategoryDto)
                    .ToList();
        }

        public List<ApiCategoryDto> GetCategoriesForVariation(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation))
                    return new List<ApiCategoryDto>();

                return variation.CategoryIds
                    .Where(categoryById.ContainsKey)
                    .Select(id => ToCategoryDto(categoryById[id]))
                    .ToList();
            }
        }

        public ApiCategoryDto SaveCategory(ApiCategoryDto dto)
        {
            lock (sync)
            {
                CategorySeed category;
                if (dto.Id > 0 && categoryById.TryGetValue(dto.Id, out category))
                {
                    CopyCategory(dto, category);
                }
                else
                {
                    category = new CategorySeed
                    {
                        Id = nextCategoryId++,
                        Guid = String.IsNullOrWhiteSpace(dto.Guid) ? Guid.NewGuid().ToString("N") : dto.Guid
                    };
                    CopyCategory(dto, category);
                    Seed.Categories.Add(category);
                    categoryById[category.Id] = category;
                }

                return ToCategoryDto(category);
            }
        }

        public ApiCategoryDto SaveCategoryByPath(string path, string sortPath)
        {
            lock (sync)
            {
                if (String.IsNullOrWhiteSpace(path))
                    return null;

                string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                CategorySeed parent = null;
                CategorySeed current = null;
                string fullPath = "";

                foreach (string part in parts)
                {
                    fullPath += "\\" + part;
                    current = Seed.Categories.FirstOrDefault(c =>
                        String.Equals(c.FullPath, fullPath, StringComparison.OrdinalIgnoreCase) &&
                        (parent == null || c.ParentId == parent.Id));

                    if (current == null)
                    {
                        current = new CategorySeed
                        {
                            Id = nextCategoryId++,
                            Name = part,
                            Sortname = part,
                            FullPath = fullPath,
                            ParentId = parent?.Id ?? 0,
                            Guid = Guid.NewGuid().ToString("N"),
                            IsDate = false,
                            IsLocation = false,
                            IsUnique = false
                        };

                        Seed.Categories.Add(current);
                        categoryById[current.Id] = current;
                    }

                    parent = current;
                }

                if (current != null && !String.IsNullOrWhiteSpace(sortPath))
                    current.Sortname = sortPath;

                return current == null ? null : ToCategoryDto(current);
            }
        }

        public void RemoveCategory(int id)
        {
            lock (sync)
            {
                CategorySeed category;
                if (!categoryById.TryGetValue(id, out category))
                    return;

                Seed.Categories.Remove(category);
                categoryById.Remove(id);

                foreach (MediaSeed item in Seed.MediaItems)
                    item.CategoryIds.Remove(id);
                foreach (VariationSeed variation in Seed.Variations)
                    variation.CategoryIds.Remove(id);
            }
        }

        public List<ApiCategoryDto> GetGeoCategories(string kind, double longitude, double width,
            double latitude, double height)
        {
            lock (sync)
            {
                IEnumerable<CategorySeed> categories = Seed.Categories;

                if (String.Equals(kind, "diary", StringComparison.OrdinalIgnoreCase))
                    categories = categories.Where(c => c.IsDate);
                else if (String.Equals(kind, "location", StringComparison.OrdinalIgnoreCase))
                    categories = categories.Where(c => c.IsLocation);

                if (width > 0 && height > 0)
                {
                    categories = categories.Where(c =>
                        c.Longitude.HasValue && c.Latitude.HasValue &&
                        c.Longitude.Value >= longitude && c.Longitude.Value <= longitude + width &&
                        c.Latitude.Value >= latitude && c.Latitude.Value <= latitude + height);
                }

                return categories.Select(ToCategoryDto).ToList();
            }
        }

        public List<ApiCategoryCountDto> GetCategoriesFromMedia(List<int> variationIds)
        {
            lock (sync)
            {
                Dictionary<int, int> counts = new Dictionary<int, int>();
                foreach (int variationId in (variationIds ?? new List<int>()).Distinct())
                {
                    VariationSeed variation;
                    if (!variationById.TryGetValue(variationId, out variation))
                        continue;

                    foreach (int categoryId in variation.CategoryIds.Distinct())
                    {
                        counts.TryGetValue(categoryId, out int count);
                        counts[categoryId] = count + 1;
                    }
                }

                return counts
                    .Where(entry => categoryById.ContainsKey(entry.Key))
                    .Select(entry => new ApiCategoryCountDto
                    {
                        Category = ToCategoryDto(categoryById[entry.Key]),
                        Count = entry.Value
                    })
                    .ToList();
            }
        }

        public List<int> GetCategoryIdentifiers(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                return variationById.TryGetValue(variationId, out variation)
                    ? variation.CategoryIds.Distinct().ToList()
                    : new List<int>();
            }
        }

        public List<ApiRoleDto> GetRoles()
        {
            lock (sync)
                return Seed.Roles.Select(r => new ApiRoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Password = r.Password,
                    Description = r.Description
                }).ToList();
        }

        public List<ApiMediaItemDto> QueryMedia(ApiMediaQueryDto query)
        {
            lock (sync)
            {
                IEnumerable<MediaSeed> items = Seed.MediaItems;
                string mode = (query.Mode ?? String.Empty).ToUpperInvariant();

                switch (mode)
                {
                    case "FOLDERS":
                        items = FilterFolders(items, query.FolderIds);
                        break;
                    case "CATEGORIES":
                        items = FilterCategories(items, query.CategoryIds, query.IsRecursive, query.IsIntersection);
                        break;
                    case "MISSING_CATEGORIES":
                        items = FilterMissingCategories(items, query.CategoryRequestType);
                        break;
                    case "FILES_FROM_LIST":
                    case "VIRTUAL":
                        items = FilterFiles(items, query.FileNames);
                        break;
                    case "FILES_FROM_CHECKSUM":
                        items = FilterChecksums(items, query.FileNames, query.Checksum);
                        break;
                    case "MISSING_FILES":
                        items = items.Where(i => i.IsFileNotFound);
                        break;
                    case "MISSING_THUMBS":
                        items = items.Where(i => String.IsNullOrWhiteSpace(i.ThumbFile));
                        break;
                    case "DUPLICATES":
                        items = FilterDuplicates(items);
                        break;
                    case "GEO":
                        items = FilterGeo(items, query);
                        break;
                    case "SEARCH":
                        items = FilterSearch(items, query.SearchToken);
                        break;
                    case "SORT":
                        items = FilterIds(items, query.MediaItemIds);
                        break;
                    case "REQUEST":
                        break;
                    default:
                        break;
                }

                items = ApplySort(items, query.SortString, query.SortFields);

                int limit = query.Limit > 0 ? query.Limit : items.Count();
                return items.Take(limit).Select(ToMediaDto).ToList();
            }
        }

        public ApiMediaItemDto GetMedia(int id)
        {
            lock (sync)
                return mediaById.TryGetValue(id, out MediaSeed item) ? ToMediaDto(item) : null;
        }

        public void UpdateMedia(int id, ApiMediaItemDto dto)
        {
            lock (sync)
            {
                MediaSeed item;
                if (!mediaById.TryGetValue(id, out item) || dto == null)
                    return;

                if (dto.Filename != null)
                {
                    item.Filename = dto.Filename;
                    item.Sortorder = dto.Filename;
                }
                if (dto.Priority != 0)
                    item.Priority = dto.Priority;
                if (dto.RoleId > 0)
                    item.RoleId = dto.RoleId;
                item.IsBookmarked = dto.IsBookmarked;
                item.IsDeleted = dto.IsDeleted;
                item.Description = dto.Description;
                item.MediaDate = dto.MediaDate == default ? item.MediaDate : dto.MediaDate;
                item.EditDate = dto.EditDate == default ? item.EditDate : dto.EditDate;
                item.Longitude = dto.Longitude;
                item.Latitude = dto.Latitude;

                if (dto.VariationId > 0)
                {
                    item.VariationId = dto.VariationId;
                    item.CurrentVariationId = dto.VariationId;
                }
                if (dto.DefaultVariationId > 0)
                    item.DefaultVariationId = dto.DefaultVariationId;
            }
        }

        public void ApplyAction(ApiMediaActionDto action)
        {
            if (action == null)
                return;

            lock (sync)
            {
                List<MediaSeed> items = (action.MediaItemIds ?? new List<int>())
                    .Distinct()
                    .Where(mediaById.ContainsKey)
                    .Select(id => mediaById[id])
                    .ToList();

                switch ((action.Action ?? String.Empty).ToUpperInvariant())
                {
                    case "SET_ROLE":
                        foreach (MediaSeed item in items)
                            item.RoleId = action.RoleId ?? item.RoleId;
                        break;
                    case "SET_PRIORITY":
                        foreach (MediaSeed item in items)
                            item.Priority = action.Priority ?? item.Priority;
                        break;
                    case "SET_BOOKMARK":
                        foreach (MediaSeed item in items)
                            item.IsBookmarked = action.Flag ?? item.IsBookmarked;
                        break;
                    case "SET_DELETED":
                        foreach (MediaSeed item in items)
                            item.IsDeleted = action.Flag ?? item.IsDeleted;
                        break;
                    case "SET_TRASH":
                        foreach (MediaSeed item in items)
                            item.IsDeleted = action.Flag ?? item.IsDeleted;
                        break;
                    case "SET_GEODATA":
                    case "ADJUST_MEDIA_DATE":
                        break;
                    case "ADD_VIEW_TIME":
                        foreach (MediaSeed item in items)
                            item.Viewed = item.Viewed + 1;
                        break;
                    case "REMOVE_FROM_DB":
                        foreach (MediaSeed item in items)
                            RemoveMedia(item.Id);
                        break;
                    case "ROTATE_90":
                        foreach (MediaSeed item in items)
                            item.Orientation = (item.Orientation + 1) % 4;
                        break;
                    case "UPDATE_DUPLICATE":
                        break;
                    case "CATEGORIZE":
                        ApplyCategorize(items, action.Categories, true);
                        break;
                    case "UNCATEGORIZE":
                        ApplyCategorize(items, action.Categories, false);
                        break;
                }
            }
        }

        public byte[] GetContentBytes(int mediaItemId)
        {
            lock (sync)
            {
                MediaSeed item;
                if (!mediaById.TryGetValue(mediaItemId, out item) || String.IsNullOrWhiteSpace(item.ContentFile))
                    return null;
                return GetDataFile(item.ContentFile);
            }
        }

        public string GetContentType(int mediaItemId)
        {
            lock (sync)
            {
                MediaSeed item;
                return mediaById.TryGetValue(mediaItemId, out item)
                    ? (String.IsNullOrWhiteSpace(item.ContentType) ? "application/octet-stream" : item.ContentType)
                    : null;
            }
        }

        public List<ApiThumbDto> GetThumbs(List<int> variationIds)
        {
            lock (sync)
            {
                List<ApiThumbDto> result = new List<ApiThumbDto>();
                foreach (int id in (variationIds ?? new List<int>()).Distinct())
                {
                    VariationSeed variation;
                    if (!variationById.TryGetValue(id, out variation))
                        continue;

                    byte[] bytes = GetThumbBytes(variation);
                    if (bytes == null || bytes.Length == 0)
                        continue;

                    result.Add(new ApiThumbDto
                    {
                        VariationId = variation.Id,
                        Thumb = bytes,
                        Extension = "jpg",
                        IsOutdated = variation.IsOutdated,
                        Width = 0,
                        Height = 0
                    });
                }

                return result;
            }
        }

        public List<ApiVariationDto> GetVariations(int mediaItemId, bool includeThumbs)
        {
            lock (sync)
            {
                return Seed.Variations
                    .Where(v => v.MediaItemId == mediaItemId)
                    .OrderBy(v => v.Position)
                    .ThenBy(v => v.Id)
                    .Select(v => ToVariationDto(v, includeThumbs))
                    .ToList();
            }
        }

        public ApiVariationDto GetVariation(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                return variationById.TryGetValue(variationId, out variation)
                    ? ToVariationDto(variation, true)
                    : null;
            }
        }

        public ApiVariationDto CreateVariation(int mediaItemId, ApiVariationCreateDto dto)
        {
            lock (sync)
            {
                MediaSeed item;
                if (!mediaById.TryGetValue(mediaItemId, out item))
                    return null;

                VariationSeed variation = new VariationSeed
                {
                    Id = nextVariationId++,
                    MediaItemId = mediaItemId,
                    Position = Seed.Variations.Count(v => v.MediaItemId == mediaItemId) + 1,
                    Name = String.IsNullOrWhiteSpace(dto.Name) ? "Variation" : dto.Name,
                    IsSynthetic = true,
                    CategoryIds = new List<int>(item.CategoryIds),
                    ThumbBytes = dto.Thumb
                };

                if (!String.IsNullOrWhiteSpace(dto.LayerEdit))
                {
                    variation.Layers.Add(new LayerSeed
                    {
                        Position = 0,
                        Edit = dto.LayerEdit,
                        Action = dto.LayerAction ?? ""
                    });
                }

                Seed.Variations.Add(variation);
                variationById[variation.Id] = variation;

                if (dto.SetDefault)
                    SetDefaultVariationInternal(item, variation);

                if (variation.ThumbBytes != null)
                    thumbOverrides[variation.Id] = variation.ThumbBytes;

                return ToVariationDto(variation, true);
            }
        }

        public bool RemoveVariation(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation))
                    return false;

                MediaSeed item;
                mediaById.TryGetValue(variation.MediaItemId, out item);

                Seed.Variations.Remove(variation);
                variationById.Remove(variationId);
                thumbOverrides.Remove(variationId);

                if (item != null)
                {
                    VariationSeed replacement = Seed.Variations
                        .Where(v => v.MediaItemId == item.Id)
                        .OrderBy(v => v.Position)
                        .ThenBy(v => v.Id)
                        .FirstOrDefault();

                    if (replacement != null)
                    {
                        if (item.VariationId == variationId)
                            item.VariationId = replacement.Id;
                        if (item.DefaultVariationId == variationId)
                            item.DefaultVariationId = replacement.Id;
                        if (item.CurrentVariationId == variationId)
                            item.CurrentVariationId = replacement.Id;
                    }
                }

                return true;
            }
        }

        public bool RemoveVariation(int mediaItemId, string name)
        {
            lock (sync)
            {
                VariationSeed variation = Seed.Variations.FirstOrDefault(v =>
                    v.MediaItemId == mediaItemId &&
                    String.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

                return variation != null && RemoveVariation(variation.Id);
            }
        }

        public bool SetDefaultVariation(int mediaItemId, int variationId)
        {
            lock (sync)
            {
                MediaSeed item;
                VariationSeed variation;
                if (!mediaById.TryGetValue(mediaItemId, out item) ||
                    !variationById.TryGetValue(variationId, out variation) ||
                    variation.MediaItemId != mediaItemId)
                    return false;

                SetDefaultVariationInternal(item, variation);
                return true;
            }
        }

        public ApiVariationDto UpdateVariation(int variationId, ApiVariationDto dto)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation) || dto == null)
                    return null;

                if (dto.Name != null)
                    variation.Name = dto.Name;
                variation.Description = dto.Description;
                variation.IsOutdated = dto.IsOutdated;

                if (dto.Layers != null)
                {
                    variation.Layers = dto.Layers
                        .OrderBy(l => l.Position)
                        .Select(l => new LayerSeed
                        {
                            Position = l.Position,
                            Edit = l.Edit,
                            Action = l.Action
                        })
                        .ToList();
                }

                if (dto.Thumb != null && dto.Thumb.Length > 0)
                    thumbOverrides[variation.Id] = dto.Thumb;

                return ToVariationDto(variation, true);
            }
        }

        public List<ApiVariationDto> ReplaceVariations(int mediaItemId, List<ApiVariationDto> variations)
        {
            lock (sync)
            {
                MediaSeed item;
                if (!mediaById.TryGetValue(mediaItemId, out item))
                    return new List<ApiVariationDto>();

                foreach (VariationSeed existing in Seed.Variations.Where(v => v.MediaItemId == mediaItemId).ToList())
                {
                    Seed.Variations.Remove(existing);
                    variationById.Remove(existing.Id);
                    thumbOverrides.Remove(existing.Id);
                }

                List<ApiVariationDto> result = new List<ApiVariationDto>();
                int position = 1;

                foreach (ApiVariationDto dto in (variations ?? new List<ApiVariationDto>()))
                {
                    VariationSeed variation = new VariationSeed
                    {
                        Id = nextVariationId++,
                        MediaItemId = mediaItemId,
                        Position = position++,
                        Name = String.IsNullOrWhiteSpace(dto.Name) ? "Default" : dto.Name,
                        Description = dto.Description,
                        IsOutdated = dto.IsOutdated,
                        IsSynthetic = true,
                        CategoryIds = new List<int>(item.CategoryIds),
                        Layers = (dto.Layers ?? new List<ApiLayerDto>())
                            .OrderBy(l => l.Position)
                            .Select(l => new LayerSeed { Position = l.Position, Edit = l.Edit, Action = l.Action })
                            .ToList(),
                        ThumbBytes = dto.Thumb
                    };

                    Seed.Variations.Add(variation);
                    variationById[variation.Id] = variation;
                    if (dto.Thumb != null && dto.Thumb.Length > 0)
                        thumbOverrides[variation.Id] = dto.Thumb;

                    result.Add(ToVariationDto(variation, true));
                }

                VariationSeed first = Seed.Variations
                    .Where(v => v.MediaItemId == mediaItemId)
                    .OrderBy(v => v.Position)
                    .ThenBy(v => v.Id)
                    .FirstOrDefault();

                if (first != null)
                    SetDefaultVariationInternal(item, first);

                return result;
            }
        }

        public List<ApiLayerDto> GetLayers(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation))
                    return new List<ApiLayerDto>();

                return variation.Layers
                    .OrderBy(l => l.Position)
                    .Select(l => new ApiLayerDto
                    {
                        Position = l.Position,
                        Edit = l.Edit,
                        Action = l.Action
                    })
                    .ToList();
            }
        }

        public void SetLayers(int variationId, List<ApiLayerDto> layers)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation))
                    return;

                variation.Layers = (layers ?? new List<ApiLayerDto>())
                    .OrderBy(l => l.Position)
                    .Select(l => new LayerSeed
                    {
                        Position = l.Position,
                        Edit = l.Edit,
                        Action = l.Action
                    })
                    .ToList();
            }
        }

        public void SetThumb(int variationId, ApiThumbUploadDto dto)
        {
            lock (sync)
            {
                if (dto?.Thumb == null || dto.Thumb.Length == 0)
                    return;

                thumbOverrides[variationId] = dto.Thumb;
            }
        }

        public byte[] GetThumbBytes(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                return variationById.TryGetValue(variationId, out variation) ? GetThumbBytes(variation) : null;
            }
        }

        public ApiPreviewDto GetPreview(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation))
                    return null;

                MediaSeed item;
                if (!mediaById.TryGetValue(variation.MediaItemId, out item) ||
                    String.IsNullOrWhiteSpace(item.PreviewFile))
                    return null;

                byte[] bytes = GetDataFile(item.PreviewFile);
                if (bytes == null || bytes.Length == 0)
                    return null;

                return new ApiPreviewDto
                {
                    Binary = bytes,
                    Width = item.PreviewWidth,
                    Height = item.PreviewHeight,
                    Extension = "jpg",
                    TempPath = null
                };
            }
        }

        public bool PreviewExists(int variationId)
        {
            lock (sync)
            {
                VariationSeed variation;
                if (!variationById.TryGetValue(variationId, out variation))
                    return false;

                MediaSeed item;
                return mediaById.TryGetValue(variation.MediaItemId, out item) &&
                       !String.IsNullOrWhiteSpace(item.PreviewFile);
            }
        }

        public List<int> GetPreviewVariationIds()
        {
            lock (sync)
                return Seed.Variations
                    .Where(v => mediaById.TryGetValue(v.MediaItemId, out MediaSeed item) &&
                                !String.IsNullOrWhiteSpace(item.PreviewFile))
                    .Select(v => v.Id)
                    .Distinct()
                    .ToList();
        }

        public List<ApiMetaDataDto> GetMetadata(List<int> mediaItemIds)
        {
            lock (sync)
            {
                HashSet<int> ids = new HashSet<int>(mediaItemIds ?? new List<int>());
                return Seed.MediaItems
                    .Where(m => ids.Contains(m.Id))
                    .SelectMany(m => m.Metadata)
                    .Select(ToMetaDataDto)
                    .ToList();
            }
        }

        public List<string> GetMetadataKeys()
        {
            lock (sync)
                return Seed.MediaItems
                    .SelectMany(m => m.Metadata)
                    .Select(md => md.Name)
                    .Where(name => !String.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        public List<ApiDescriptionDto> GetDescriptions(List<int> mediaItemIds)
        {
            lock (sync)
            {
                HashSet<int> ids = new HashSet<int>(mediaItemIds ?? new List<int>());
                return Seed.Descriptions
                    .Where(d => d.MediaItemIds.Any(ids.Contains))
                    .Select(ToDescriptionDto)
                    .ToList();
            }
        }

        public string GetMediaDescription(int mediaItemId)
        {
            lock (sync)
            {
                MediaSeed item;
                if (!mediaById.TryGetValue(mediaItemId, out item) || !item.DescriptionId.HasValue)
                    return null;

                DescriptionSeed description = Seed.Descriptions.FirstOrDefault(d => d.Id == item.DescriptionId.Value);
                return description?.Value;
            }
        }

        public void SaveDescription(ApiDescriptionDto dto)
        {
            if (dto == null)
                return;

            lock (sync)
            {
                DescriptionSeed description;
                if (dto.Id.HasValue && dto.Id.Value > 0)
                {
                    description = Seed.Descriptions.FirstOrDefault(d => d.Id == dto.Id.Value);
                }
                else
                {
                    description = null;
                }

                if (description == null)
                {
                    description = new DescriptionSeed
                    {
                        Id = nextDescriptionId++,
                        Value = dto.Value
                    };
                    Seed.Descriptions.Add(description);
                }

                description.Value = dto.Value;
                description.Command = dto.Command;
                description.MediaItemIds = new List<int>(dto.MediaItemIds ?? new List<int>());

                foreach (int mediaItemId in description.MediaItemIds)
                {
                    MediaSeed item;
                    if (!mediaById.TryGetValue(mediaItemId, out item))
                        continue;

                    item.DescriptionId = description.Id;
                    item.Description = description.Value;
                }
            }
        }

        public List<ApiGeoPointDto> GetGpsPoints(DateTime from, DateTime to)
        {
            lock (sync)
                return Seed.GpsPoints
                    .Where(p => (from == default || p.LocalTime >= from) && (to == default || p.LocalTime <= to))
                    .Select(ToGeoPointDto)
                    .ToList();
        }

        public ApiGeoPointDto GetGpsNearest(DateTime date)
        {
            lock (sync)
            {
                GeoPointSeed nearest = Seed.GpsPoints
                    .OrderBy(p => Math.Abs((p.LocalTime - date).TotalSeconds))
                    .FirstOrDefault();

                return nearest == null ? null : ToGeoPointDto(nearest);
            }
        }

        public List<ApiGpsFileDto> GetGpsFiles()
        {
            lock (sync)
                return Seed.GpsFiles.Select(f => new ApiGpsFileDto
                {
                    Filesize = f.Filesize,
                    FileTime = f.FileTime
                }).ToList();
        }

        public int DeleteGpsFile(DateTime fileTime)
        {
            lock (sync)
            {
                int removed = Seed.GpsFiles.RemoveAll(f => Math.Abs((f.FileTime - fileTime).TotalSeconds) < 1);
                Seed.GpsPoints.RemoveAll(p => Math.Abs((p.GpsFileTime - fileTime).TotalSeconds) < 1);
                return removed;
            }
        }

        public void InsertGpsPoints(List<ApiGeoPointDto> points)
        {
            lock (sync)
            {
                foreach (ApiGeoPointDto point in points ?? new List<ApiGeoPointDto>())
                {
                    Seed.GpsPoints.Add(new GeoPointSeed
                    {
                        Longitude = point.Longitude,
                        Latitude = point.Latitude,
                        Altitude = point.Altitude,
                        LocalTime = point.LocalTime,
                        UtcTime = point.UtcTime,
                        GpsFileSize = point.GpsFileSize,
                        GpsFileTime = point.GpsFileTime,
                        DistanceMeter = point.DistanceMeter,
                        Acceleration = point.Acceleration
                    });
                }
            }
        }

        public List<ApiMeteorologyDto> GetMeteorology(DateTime from, DateTime to)
        {
            lock (sync)
                return Seed.Meteorology
                    .Where(m => (from == default || m.LogTime >= from) && (to == default || m.LogTime <= to))
                    .Select(m => new ApiMeteorologyDto
                    {
                        LogTime = m.LogTime,
                        Temperature = m.Temperature,
                        Humidity = m.Humidity
                    })
                    .ToList();
        }

        public void InsertMeteorology(List<ApiMeteorologyDto> data)
        {
            lock (sync)
            {
                foreach (ApiMeteorologyDto entry in data ?? new List<ApiMeteorologyDto>())
                {
                    Seed.Meteorology.Add(new MeteorologySeed
                    {
                        LogTime = entry.LogTime,
                        Temperature = entry.Temperature,
                        Humidity = entry.Humidity
                    });
                }
            }
        }

        public List<ApiUserRequestDto> GetRequests()
        {
            lock (sync)
                return Seed.Requests.Select(r => new ApiUserRequestDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Kind = r.Kind,
                    Payload = r.Payload,
                    Limit = r.Limit
                }).ToList();
        }

        public ApiUserRequestDto SaveRequest(ApiUserRequestDto dto)
        {
            lock (sync)
            {
                if (dto == null)
                    return null;

                RequestSeed request = dto.Id > 0
                    ? Seed.Requests.FirstOrDefault(r => r.Id == dto.Id)
                    : null;

                if (request == null)
                {
                    request = new RequestSeed
                    {
                        Id = dto.Id > 0 ? dto.Id : nextRequestId++
                    };
                    Seed.Requests.Add(request);
                }

                request.Name = dto.Name;
                request.Kind = dto.Kind;
                request.Payload = dto.Payload;
                request.Limit = dto.Limit;

                return new ApiUserRequestDto
                {
                    Id = request.Id,
                    Name = request.Name,
                    Kind = request.Kind,
                    Payload = request.Payload,
                    Limit = request.Limit
                };
            }
        }

        public void DeleteRequest(int id)
        {
            lock (sync)
                Seed.Requests.RemoveAll(r => r.Id == id);
        }

        public string GetRequestSql(int id)
        {
            lock (sync)
            {
                RequestSeed request = Seed.Requests.FirstOrDefault(r => r.Id == id);
                return request?.Payload;
            }
        }

        public void SetRequestSql(int id, string sql)
        {
            lock (sync)
            {
                RequestSeed request = Seed.Requests.FirstOrDefault(r => r.Id == id);
                if (request != null)
                    request.Payload = sql;
            }
        }

        public bool CheckDuplicate(ApiMediaActionDto action)
        {
            lock (sync)
            {
                int id = (action?.MediaItemIds ?? new List<int>()).FirstOrDefault();
                MediaSeed item;
                if (!mediaById.TryGetValue(id, out item))
                    return false;

                string criteria = (action.Criteria ?? "FILENAME").ToUpperInvariant();
                switch (criteria)
                {
                    case "FILENAME":
                        return Seed.MediaItems.Any(m => m.Id != item.Id &&
                            String.Equals(m.Filename, item.Filename, StringComparison.OrdinalIgnoreCase));
                    case "CHECKSUM":
                        return !String.IsNullOrWhiteSpace(item.Md5Value) &&
                               Seed.MediaItems.Any(m => m.Id != item.Id &&
                                   String.Equals(m.Md5Value, item.Md5Value, StringComparison.OrdinalIgnoreCase));
                    case "CONTAINS":
                        return Seed.MediaItems.Any(m => m.Id != item.Id &&
                            (m.Filename?.IndexOf(item.Filename ?? "", StringComparison.OrdinalIgnoreCase) >= 0));
                    default:
                        return false;
                }
            }
        }

        public List<ApiDuplicateDto> GetDuplicates(int id)
        {
            lock (sync)
            {
                MediaSeed item;
                if (!mediaById.TryGetValue(id, out item) || String.IsNullOrWhiteSpace(item.Md5Value))
                    return new List<ApiDuplicateDto>();

                return Seed.MediaItems
                    .Where(m => m.Id != id &&
                                String.Equals(m.Md5Value, item.Md5Value, StringComparison.OrdinalIgnoreCase))
                    .Select(m => new ApiDuplicateDto
                    {
                        MediaItemId = m.Id,
                        Value = m.Md5Value
                    })
                    .ToList();
            }
        }

        public ApiGeoClusterResultDto GetGeoClusters(GeoClusterQueryInput query, out string error)
        {
            if (query == null)
            {
                error = "Request fehlt.";
                return null;
            }

            if (!query.MinLongitude.HasValue || !query.MinLatitude.HasValue ||
                !query.MaxLongitude.HasValue || !query.MaxLatitude.HasValue ||
                !query.Zoom.HasValue)
            {
                error = "Pflichtfelder fehlen.";
                return null;
            }

            double minLongitude = query.MinLongitude.Value;
            double minLatitude = query.MinLatitude.Value;
            double maxLongitude = query.MaxLongitude.Value;
            double maxLatitude = query.MaxLatitude.Value;

            error = ValidateGeoBounds(minLongitude, minLatitude, maxLongitude, maxLatitude);

            if (error != null)
                return null;

            int zoom = query.Zoom.Value;
            if (zoom < 0 || zoom > 22)
            {
                error = "zoom muss zwischen 0 und 22 liegen.";
                return null;
            }

            int cellSize = query.CellSize ?? 64;
            if (cellSize < 16 || cellSize > 512)
            {
                error = "cellSize muss zwischen 16 und 512 liegen.";
                return null;
            }

            int limit = query.Limit ?? 2000;
            if (limit < 1 || limit > 10000)
            {
                error = "limit muss zwischen 1 und 10000 liegen.";
                return null;
            }

            bool includeSingles = query.IncludeSingles ?? true;

            lock (sync)
            {
                List<MediaSeed> points = FindGeoMedia(minLongitude, minLatitude,
                    maxLongitude, maxLatitude);
                int totalCount = points.Count;
                double worldSize = 256.0 * Math.Pow(2.0, zoom);

                Dictionary<(int X, int Y), List<MediaSeed>> cells =
                    new Dictionary<(int X, int Y), List<MediaSeed>>();

                foreach (MediaSeed point in points)
                {
                    double longitude = point.Longitude.Value;
                    double latitude = point.Latitude.Value;
                    double x = (longitude + 180.0) / 360.0 * worldSize;
                    double latRad = latitude * Math.PI / 180.0;
                    double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * worldSize;
                    int cellX = (int)Math.Floor(x / cellSize);
                    int cellY = (int)Math.Floor(y / cellSize);

                    if (!cells.TryGetValue((cellX, cellY), out List<MediaSeed> list))
                    {
                        list = new List<MediaSeed>();
                        cells[(cellX, cellY)] = list;
                    }

                    list.Add(point);
                }

                List<ApiGeoClusterDto> clusters = cells.Values
                    .Select(list => new ApiGeoClusterDto
                    {
                        Latitude = list.Average(p => p.Latitude.Value),
                        Longitude = list.Average(p => p.Longitude.Value),
                        Count = list.Count,
                        MinLatitude = list.Min(p => p.Latitude.Value),
                        MinLongitude = list.Min(p => p.Longitude.Value),
                        MaxLatitude = list.Max(p => p.Latitude.Value),
                        MaxLongitude = list.Max(p => p.Longitude.Value),
                        MediaItemId = list.Min(p => p.Id)
                    })
                    .Where(c => includeSingles || c.Count > 1)
                    .OrderByDescending(c => c.Count)
                    .ThenByDescending(c => c.Latitude)
                    .ThenBy(c => c.Longitude)
                    .ToList();

                bool truncated = clusters.Count > limit;
                if (truncated)
                    clusters = clusters.Take(limit).ToList();

                return new ApiGeoClusterResultDto
                {
                    Clusters = clusters,
                    TotalCount = totalCount,
                    Truncated = truncated,
                    Zoom = zoom,
                    CellSize = cellSize
                };
            }
        }

        public List<ApiMediaItemDto> GetGeoItems(GeoItemsQueryInput query, out string error)
        {
            if (query == null)
            {
                error = "Request fehlt.";
                return null;
            }

            if (!query.MinLongitude.HasValue || !query.MinLatitude.HasValue ||
                !query.MaxLongitude.HasValue || !query.MaxLatitude.HasValue)
            {
                error = "Pflichtfelder fehlen.";
                return null;
            }

            double minLongitude = query.MinLongitude.Value;
            double minLatitude = query.MinLatitude.Value;
            double maxLongitude = query.MaxLongitude.Value;
            double maxLatitude = query.MaxLatitude.Value;

            error = ValidateGeoBounds(minLongitude, minLatitude, maxLongitude, maxLatitude);

            if (error != null)
                return null;

            int limit = query.Limit ?? 500;
            if (limit < 1 || limit > 10000)
            {
                error = "limit muss zwischen 1 und 10000 liegen.";
                return null;
            }

            int offset = query.Offset ?? 0;
            if (offset < 0)
            {
                error = "offset muss größer oder gleich 0 sein.";
                return null;
            }

            string sort = String.IsNullOrWhiteSpace(query.Sort) ? "MEDIA_DATE_ASC" : query.Sort;
            if (!String.Equals(sort, "MEDIA_DATE_ASC", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(sort, "MEDIA_DATE_DESC", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(sort, "ID_ASC", StringComparison.OrdinalIgnoreCase))
            {
                error = "Unbekannter sort-Wert: " + sort;
                return null;
            }

            lock (sync)
            {
                IEnumerable<MediaSeed> items = FindGeoMedia(minLongitude, minLatitude,
                    maxLongitude, maxLatitude);

                if (String.Equals(sort, "MEDIA_DATE_DESC", StringComparison.OrdinalIgnoreCase))
                    items = items.OrderByDescending(i => i.MediaDate).ThenBy(i => i.Id);
                else if (String.Equals(sort, "ID_ASC", StringComparison.OrdinalIgnoreCase))
                    items = items.OrderBy(i => i.Id);
                else
                    items = items.OrderBy(i => i.MediaDate).ThenBy(i => i.Id);

                return items.Skip(offset).Take(limit).Select(ToMediaDto).ToList();
            }
        }

        private List<MediaSeed> FindGeoMedia(double minLongitude, double minLatitude,
            double maxLongitude, double maxLatitude)
        {
            return Seed.MediaItems
                .Where(item => !item.IsDeleted &&
                               item.Longitude.HasValue && item.Latitude.HasValue &&
                               !(item.Longitude.Value == 0 && item.Latitude.Value == 0) &&
                               item.Longitude.Value >= minLongitude && item.Longitude.Value <= maxLongitude &&
                               item.Latitude.Value >= minLatitude && item.Latitude.Value <= maxLatitude)
                .ToList();
        }

        private static string ValidateGeoBounds(double minLongitude, double minLatitude,
            double maxLongitude, double maxLatitude)
        {
            const double mercatorLimit = 85.05112878;

            if (minLongitude < -180 || minLongitude > 180 ||
                maxLongitude < -180 || maxLongitude > 180 ||
                minLatitude < -mercatorLimit || minLatitude > mercatorLimit ||
                maxLatitude < -mercatorLimit || maxLatitude > mercatorLimit)
                return "Ungültige Bounding Box.";

            if (minLongitude > maxLongitude || minLatitude > maxLatitude)
                return "minLongitude/minLatitude dürfen nicht größer als maxLongitude/maxLatitude sein.";

            return null;
        }

        public ApiSqlQueryDto GetSampleSqlRequest()
        {
            return new ApiSqlQueryDto
            {
                Sql = "SELECT * FROM MEDIAFILES",
                SortString = "MEDIADATE DESC",
                Limit = 100,
                Parameters = new List<ApiSqlParameterDto>()
            };
        }

        public List<ApiMediaItemDto> ExecuteSqlQuery(ApiSqlQueryDto query)
        {
            lock (sync)
            {
                int limit = query?.Limit > 0 ? query.Limit : 100;
                return Seed.MediaItems.Take(limit).Select(ToMediaDto).ToList();
            }
        }

        public ApiMediaItemDto GetImageAnalysisPlaceholder(int mediaItemId)
        {
            return null;
        }

        private void SetDefaultVariationInternal(MediaSeed item, VariationSeed variation)
        {
            item.DefaultVariationId = variation.Id;
            item.VariationId = variation.Id;
            item.CurrentVariationId = variation.Id;
        }

        private void RemoveMedia(int id)
        {
            MediaSeed item;
            if (!mediaById.TryGetValue(id, out item))
                return;

            Seed.MediaItems.Remove(item);
            mediaById.Remove(id);

            foreach (VariationSeed variation in Seed.Variations.Where(v => v.MediaItemId == id).ToList())
            {
                Seed.Variations.Remove(variation);
                variationById.Remove(variation.Id);
                thumbOverrides.Remove(variation.Id);
            }
        }

        private void ApplyCategorize(List<MediaSeed> items, List<ApiCategoryDto> categories, bool add)
        {
            HashSet<int> categoryIds = new HashSet<int>((categories ?? new List<ApiCategoryDto>()).Select(c => c.Id));

            foreach (ApiCategoryDto dto in categories ?? new List<ApiCategoryDto>())
            {
                if (dto.Id <= 0 || !categoryById.ContainsKey(dto.Id))
                {
                    ApiCategoryDto saved = SaveCategory(dto);
                    categoryIds.Add(saved.Id);
                }
            }

            foreach (MediaSeed item in items)
            {
                foreach (int categoryId in categoryIds)
                {
                    if (add)
                    {
                        if (!item.CategoryIds.Contains(categoryId))
                            item.CategoryIds.Add(categoryId);
                    }
                    else
                    {
                        item.CategoryIds.Remove(categoryId);
                    }
                }

                foreach (VariationSeed variation in Seed.Variations.Where(v => v.MediaItemId == item.Id))
                {
                    foreach (int categoryId in categoryIds)
                    {
                        if (add)
                        {
                            if (!variation.CategoryIds.Contains(categoryId))
                                variation.CategoryIds.Add(categoryId);
                        }
                        else
                        {
                            variation.CategoryIds.Remove(categoryId);
                        }
                    }
                }
            }

            foreach (CategorySeed category in Seed.Categories)
            {
                category.ItemCount = Seed.MediaItems.Count(m => m.CategoryIds.Contains(category.Id));
            }
        }

        private IEnumerable<MediaSeed> FilterFolders(IEnumerable<MediaSeed> items, List<int> folderIds)
        {
            if (folderIds == null || folderIds.Count == 0)
                return items;

            HashSet<string> names = new HashSet<string>(
                folderIds.Where(folderById.ContainsKey).Select(id => folderById[id].Foldername),
                StringComparer.OrdinalIgnoreCase);

            return items.Where(i => names.Contains(i.Foldername));
        }

        private IEnumerable<MediaSeed> FilterCategories(IEnumerable<MediaSeed> items, List<int> categoryIds,
            bool recursive, bool intersection)
        {
            if (categoryIds == null || categoryIds.Count == 0)
                return items;

            HashSet<int> ids = new HashSet<int>(categoryIds);
            if (recursive)
                ExpandCategoryIds(ids);

            if (intersection)
                return items.Where(i => ids.All(i.CategoryIds.Contains));

            return items.Where(i => ids.Overlaps(i.CategoryIds));
        }

        private IEnumerable<MediaSeed> FilterMissingCategories(IEnumerable<MediaSeed> items, string requestType)
        {
            string type = (requestType ?? String.Empty).ToUpperInvariant();

            return items.Where(item =>
            {
                List<CategorySeed> categories = item.CategoryIds
                    .Where(categoryById.ContainsKey)
                    .Select(id => categoryById[id])
                    .ToList();

                switch (type)
                {
                    case "NO_CATEGORY":
                        return !categories.Any(c => !c.IsDate && !c.IsLocation);
                    case "NO_DATE":
                        return !categories.Any(c => c.IsDate);
                    case "NO_LOCATION":
                        return !categories.Any(c => c.IsLocation);
                    case "NO_OTHER":
                        return categories.Count == 0;
                    default:
                        return true;
                }
            });
        }

        private IEnumerable<MediaSeed> FilterFiles(IEnumerable<MediaSeed> items, List<string> files)
        {
            if (files == null || files.Count == 0)
                return Enumerable.Empty<MediaSeed>();

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                if (String.IsNullOrWhiteSpace(file))
                    continue;

                names.Add(file);
                names.Add(Path.GetFileName(file));
            }

            return items.Where(i =>
                names.Contains(i.Filename ?? "") ||
                names.Contains((i.Foldername ?? "") + "\\" + (i.Filename ?? "")) ||
                names.Contains((i.Foldername ?? "") + "/" + (i.Filename ?? "")));
        }

        private IEnumerable<MediaSeed> FilterChecksums(IEnumerable<MediaSeed> items, List<string> files,
            string checksum)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!String.IsNullOrWhiteSpace(checksum))
                values.Add(checksum);
            foreach (string file in files ?? new List<string>())
            {
                if (!String.IsNullOrWhiteSpace(file))
                    values.Add(file);
            }

            return items.Where(i => !String.IsNullOrWhiteSpace(i.Md5Value) && values.Contains(i.Md5Value));
        }

        private IEnumerable<MediaSeed> FilterDuplicates(IEnumerable<MediaSeed> items)
        {
            List<MediaSeed> list = items.ToList();
            HashSet<string> duplicates = new HashSet<string>(
                list.Where(i => !String.IsNullOrWhiteSpace(i.Md5Value))
                    .GroupBy(i => i.Md5Value, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key),
                StringComparer.OrdinalIgnoreCase);

            return list.Where(i => i.IsDuplicate ||
                (!String.IsNullOrWhiteSpace(i.Md5Value) && duplicates.Contains(i.Md5Value)));
        }

        private IEnumerable<MediaSeed> FilterGeo(IEnumerable<MediaSeed> items, ApiMediaQueryDto query)
        {
            if (query.Width <= 0 || query.Height <= 0)
                return items.Where(i => i.Longitude.HasValue && i.Latitude.HasValue);

            return items.Where(i =>
                i.Longitude.HasValue && i.Latitude.HasValue &&
                i.Longitude.Value >= query.Longitude && i.Longitude.Value <= query.Longitude + query.Width &&
                i.Latitude.Value >= query.Latitude && i.Latitude.Value <= query.Latitude + query.Height);
        }

        private IEnumerable<MediaSeed> FilterIds(IEnumerable<MediaSeed> items, List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return items;

            HashSet<int> set = new HashSet<int>(ids);
            return items.Where(i => set.Contains(i.Id));
        }

        private IEnumerable<MediaSeed> FilterSearch(IEnumerable<MediaSeed> items, ApiSearchTokenDto token)
        {
            if (token == null)
                return items;

            IEnumerable<MediaSeed> result = items;

            bool hasText1 = !String.IsNullOrWhiteSpace(token.SearchText1);
            bool hasText2 = !String.IsNullOrWhiteSpace(token.SearchText2);

            if (hasText1)
                result = result.Where(i => MatchText(i, token, token.SearchText1, true));
            if (hasText2)
                result = result.Where(i => MatchText(i, token, token.SearchText2, false));

            if (hasText1 && hasText2 &&
                String.Equals(token.RelationType, "ANY", StringComparison.OrdinalIgnoreCase))
            {
                result = items.Where(i => MatchText(i, token, token.SearchText1, true) ||
                                          MatchText(i, token, token.SearchText2, false));
            }

            if (token.DateFromEnabled)
                result = result.Where(i => i.MediaDate >= token.DateFrom);
            if (token.DateToEnabled)
                result = result.Where(i => i.MediaDate <= token.DateTo);
            if (token.LengthFrom > 0)
                result = result.Where(i => i.FileLength >= token.LengthFrom);
            if (token.LengthTo > 0)
                result = result.Where(i => i.FileLength <= token.LengthTo);
            if (token.DurationFrom > 0)
                result = result.Where(i => i.Duration >= token.DurationFrom);
            if (token.DurationTo > 0)
                result = result.Where(i => i.Duration <= token.DurationTo);
            if (token.PriorityFrom > 0)
                result = result.Where(i => i.Priority >= token.PriorityFrom);
            if (token.PriorityTo > 0)
                result = result.Where(i => i.Priority <= token.PriorityTo);
            if (token.DimensionFrom > 0)
                result = result.Where(i => Math.Max(i.Width, i.Height) >= token.DimensionFrom);
            if (token.DimensionTo > 0)
                result = result.Where(i => Math.Max(i.Width, i.Height) <= token.DimensionTo);

            if (!String.IsNullOrWhiteSpace(token.MetaDataKey))
            {
                result = result.Where(i => i.Metadata.Any(md =>
                    String.Equals(md.Name, token.MetaDataKey, StringComparison.OrdinalIgnoreCase) &&
                    (String.IsNullOrWhiteSpace(token.MetaDataValue) ||
                     (md.Value ?? "").IndexOf(token.MetaDataValue, StringComparison.OrdinalIgnoreCase) >= 0)));
            }

            if (token.MediaTypeRgb || token.MediaTypeDirectShow)
            {
                result = result.Where(i =>
                    (token.MediaTypeRgb && String.Equals(i.Type, "rgb", StringComparison.OrdinalIgnoreCase)) ||
                    (token.MediaTypeDirectShow && String.Equals(i.Type, "dsh", StringComparison.OrdinalIgnoreCase)));
            }

            return result;
        }

        private bool MatchText(MediaSeed item, ApiSearchTokenDto token, string text, bool first)
        {
            bool not = first ? token.SearchText1Not : token.SearchText2Not;
            bool description = first ? token.SearchText1Description : token.SearchText2Description;
            bool category = first ? token.SearchText1Category : token.SearchText2Category;
            bool filename = first ? token.SearchText1Filename : token.SearchText2Filename;
            bool md5 = first ? token.SearchText1Md5 : token.SearchText2Md5;
            bool folder = first ? token.SearchText1Folder : token.SearchText2Folder;

            if (!description && !category && !filename && !md5 && !folder)
            {
                description = true;
                category = true;
                filename = true;
                folder = true;
            }

            bool match = false;
            if (filename)
                match |= Contains(item.Filename, text);
            if (folder)
                match |= Contains(item.Foldername, text);
            if (description)
                match |= Contains(item.Description, text);
            if (md5)
                match |= Contains(item.Md5Value, text);
            if (category)
                match |= item.CategoryIds
                    .Where(categoryById.ContainsKey)
                    .Any(id => Contains(categoryById[id].Name, text) || Contains(categoryById[id].FullPath, text));

            return not ? !match : match;
        }

        private static bool Contains(string value, string text)
        {
            return !String.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<MediaSeed> ApplySort(IEnumerable<MediaSeed> items, string sortString,
            List<string> sortFields)
        {
            List<string> fields = new List<string>();

            if (sortFields != null && sortFields.Count > 0)
                fields.AddRange(sortFields);
            else if (!String.IsNullOrWhiteSpace(sortString))
                fields.AddRange(sortString.Split(',', StringSplitOptions.RemoveEmptyEntries));

            IOrderedEnumerable<MediaSeed> ordered = null;

            foreach (string field in fields)
            {
                string trimmed = field.Trim();
                if (trimmed.Length == 0)
                    continue;

                string[] parts = trimmed.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                    parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                string name = parts[0].Trim().ToUpperInvariant();
                bool descending = parts.Length > 1 &&
                                  parts[1].Trim().StartsWith("D", StringComparison.OrdinalIgnoreCase);

                Func<MediaSeed, object> selector = GetSortSelector(name);
                if (selector == null)
                    continue;

                if (String.Equals(name, "RANDOM", StringComparison.OrdinalIgnoreCase))
                {
                    ordered = items.OrderBy(_ => Guid.NewGuid());
                    continue;
                }

                if (ordered == null)
                    ordered = descending ? items.OrderByDescending(selector) : items.OrderBy(selector);
                else
                    ordered = descending ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector);
            }

            return ordered ?? items;
        }

        private static Func<MediaSeed, object> GetSortSelector(string name)
        {
            switch (name)
            {
                case "FILENAME":
                    return i => i.Filename ?? "";
                case "FOLDERNAME":
                case "FOLDER":
                    return i => i.Foldername ?? "";
                case "SORTORDER":
                    return i => i.Sortorder ?? "";
                case "MD5VALUE":
                case "CHECKSUM":
                    return i => i.Md5Value ?? "";
                case "TYPE":
                    return i => i.Type ?? "";
                case "MEDIADATE":
                    return i => i.MediaDate;
                case "CREATIONDATE":
                    return i => i.CreationDate;
                case "EDITDATE":
                    return i => i.EditDate;
                case "INSERTDATE":
                    return i => i.InsertDate;
                case "PRIORITY":
                    return i => i.Priority;
                case "VIEWED":
                    return i => i.Viewed;
                case "LENGTH":
                    return i => i.FileLength;
                case "DURATION":
                    return i => i.Duration;
                case "WIDTH":
                    return i => i.Width;
                case "HEIGHT":
                    return i => i.Height;
                case "RANDOM":
                    return i => Guid.NewGuid();
                default:
                    return null;
            }
        }

        private void ExpandCategoryIds(HashSet<int> ids)
        {
            bool added = true;
            while (added)
            {
                added = false;
                foreach (CategorySeed category in Seed.Categories)
                {
                    if (category.ParentId > 0 && ids.Contains(category.ParentId) && ids.Add(category.Id))
                        added = true;
                }
            }
        }

        private ApiFolderDto ToFolderDto(FolderSeed folder)
        {
            return new ApiFolderDto
            {
                Id = folder.Id,
                Foldername = folder.Foldername,
                ItemCount = folder.ItemCount
            };
        }

        private ApiCategoryDto ToCategoryDto(CategorySeed category)
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
                Date = category.Date,
                Longitude = category.Longitude,
                Latitude = category.Latitude,
                ItemCount = category.ItemCount
            };
        }

        private void CopyCategory(ApiCategoryDto dto, CategorySeed category)
        {
            category.ParentId = dto.ParentId;
            category.Name = dto.Name;
            category.Sortname = dto.Sortname;
            category.Description = dto.Description;
            category.FullPath = dto.FullPath;
            category.Guid = dto.Guid;
            category.IsDate = dto.IsDate;
            category.IsLocation = dto.IsLocation;
            category.IsUnique = dto.IsUnique;
            category.Date = dto.Date;
            category.Longitude = dto.Longitude;
            category.Latitude = dto.Latitude;
            category.ItemCount = dto.ItemCount;
        }

        private ApiMediaItemDto ToMediaDto(MediaSeed item)
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
                Orientation = item.Orientation,
                Type = item.Type,
                IsBookmarked = item.IsBookmarked,
                IsDeleted = item.IsDeleted,
                IsDuplicate = item.IsDuplicate,
                IsFileNotFound = item.IsFileNotFound,
                Priority = item.Priority,
                RoleId = item.RoleId,
                VariationId = item.VariationId,
                DefaultVariationId = item.DefaultVariationId,
                CurrentVariationId = item.CurrentVariationId,
                DescriptionId = item.DescriptionId,
                Description = item.Description,
                CreationDate = item.CreationDate,
                MediaDate = item.MediaDate,
                EditDate = item.EditDate,
                InsertDate = item.InsertDate,
                Longitude = item.Longitude,
                Latitude = item.Latitude,
                IsThumbOutdated = item.IsThumbOutdated,
                Thumb = null,
                Metadata = item.Metadata.Select(ToMetaDataDto).ToList()
            };
        }

        private ApiVariationDto ToVariationDto(VariationSeed variation, bool includeThumb)
        {
            byte[] thumb = includeThumb ? GetThumbBytes(variation) : null;

            return new ApiVariationDto
            {
                Id = variation.Id,
                MediaItemId = variation.MediaItemId,
                Position = variation.Position,
                Name = variation.Name,
                Description = variation.Description,
                IsOutdated = variation.IsOutdated,
                Thumb = thumb,
                Layers = variation.Layers
                    .OrderBy(l => l.Position)
                    .Select(l => new ApiLayerDto
                    {
                        Position = l.Position,
                        Edit = l.Edit,
                        Action = l.Action
                    })
                    .ToList()
            };
        }

        private static ApiMetaDataDto ToMetaDataDto(MetaDataSeed metadata)
        {
            return new ApiMetaDataDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                GroupName = metadata.GroupName,
                Value = metadata.Value,
                Type = metadata.Type,
                IsVisible = metadata.IsVisible,
                IsNull = metadata.IsNull
            };
        }

        private ApiDescriptionDto ToDescriptionDto(DescriptionSeed description)
        {
            return new ApiDescriptionDto
            {
                Id = description.Id,
                Value = description.Value,
                Command = description.Command,
                MediaItemIds = new List<int>(description.MediaItemIds)
            };
        }

        private static ApiGeoPointDto ToGeoPointDto(GeoPointSeed point)
        {
            return new ApiGeoPointDto
            {
                Longitude = point.Longitude,
                Latitude = point.Latitude,
                Altitude = point.Altitude,
                LocalTime = point.LocalTime,
                UtcTime = point.UtcTime,
                GpsFileSize = point.GpsFileSize,
                GpsFileTime = point.GpsFileTime,
                DistanceMeter = point.DistanceMeter,
                Acceleration = point.Acceleration
            };
        }

        private byte[] GetThumbBytes(VariationSeed variation)
        {
            if (thumbOverrides.TryGetValue(variation.Id, out byte[] overrideBytes))
                return overrideBytes;

            if (variation.ThumbBytes != null && variation.ThumbBytes.Length > 0)
                return variation.ThumbBytes;

            if (!String.IsNullOrWhiteSpace(variation.ThumbFile))
                return GetDataFile(variation.ThumbFile);

            return null;
        }

        private byte[] GetDataFile(string relativePath)
        {
            if (String.IsNullOrWhiteSpace(relativePath))
                return null;

            string key = relativePath.Replace('\\', '/');
            if (dataFiles.TryGetValue(key, out byte[] bytes))
                return bytes;

            if (!String.IsNullOrWhiteSpace(dataDirectory))
            {
                string fullPath = Path.Combine(dataDirectory, key.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                    return File.ReadAllBytes(fullPath);
            }

            return null;
        }

        private static string GetArg(string[] args, string name, string defaultValue)
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
    }
}
