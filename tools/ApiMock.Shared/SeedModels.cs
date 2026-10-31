using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ApiMock.Shared
{
    public sealed class SeedRoot
    {
        public DateTime GeneratedUtc { get; set; }
        public CatalogInfoSeed Catalog { get; set; } = new CatalogInfoSeed();
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, DateTime> LastFolders { get; set; } = new Dictionary<string, DateTime>();
        public CapabilitiesSeed Capabilities { get; set; } = new CapabilitiesSeed();
        public List<FolderSeed> Folders { get; set; } = new List<FolderSeed>();
        public List<CategorySeed> Categories { get; set; } = new List<CategorySeed>();
        public List<RoleSeed> Roles { get; set; } = new List<RoleSeed>();
        public List<MediaSeed> MediaItems { get; set; } = new List<MediaSeed>();
        public List<VariationSeed> Variations { get; set; } = new List<VariationSeed>();
        public List<DescriptionSeed> Descriptions { get; set; } = new List<DescriptionSeed>();
        public List<GeoPointSeed> GpsPoints { get; set; } = new List<GeoPointSeed>();
        public List<GpsFileSeed> GpsFiles { get; set; } = new List<GpsFileSeed>();
        public List<MeteorologySeed> Meteorology { get; set; } = new List<MeteorologySeed>();
        public List<DuplicateSeed> Duplicates { get; set; } = new List<DuplicateSeed>();
        public List<RequestSeed> Requests { get; set; } = new List<RequestSeed>();
    }

    public sealed class CatalogInfoSeed
    {
        public bool Valid { get; set; } = true;
        public string Guid { get; set; }
        public string DbName { get; set; }
        public string User { get; set; }
        public string Host { get; set; }
        public string Message { get; set; }
    }

    public sealed class CapabilitiesSeed
    {
        public bool CanReadContent { get; set; } = true;
        public bool CanStreamRange { get; set; } = true;
        public bool CanReadAttachments { get; set; } = false;
        public bool CanReadSidecars { get; set; } = false;
        public bool CanWriteCatalog { get; set; } = true;
        public bool CanUploadContent { get; set; } = false;
        public bool CanModifyFiles { get; set; } = false;
        public bool ContentStoreConfigured { get; set; } = true;
    }

    public sealed class FolderSeed
    {
        public int Id { get; set; }
        public string Foldername { get; set; }
        public int ItemCount { get; set; }
    }

    public sealed class CategorySeed
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Name { get; set; }
        public string Sortname { get; set; }
        public string Description { get; set; }
        public string FullPath { get; set; }
        public string Guid { get; set; }
        public bool IsDate { get; set; }
        public bool IsLocation { get; set; }
        public bool IsUnique { get; set; }
        public DateTime? Date { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public int ItemCount { get; set; }
    }

    public sealed class RoleSeed
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Description { get; set; }
    }

    public sealed class MediaSeed
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string Foldername { get; set; }
        public string Sortorder { get; set; }
        public string Md5Value { get; set; }
        public long FileLength { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Viewed { get; set; }
        public double Duration { get; set; }
        public int Frames { get; set; }
        public int Orientation { get; set; }
        public string Type { get; set; }
        public bool IsBookmarked { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsDuplicate { get; set; }
        public bool IsFileNotFound { get; set; }
        public int Priority { get; set; }
        public int RoleId { get; set; }
        public int VariationId { get; set; }
        public int DefaultVariationId { get; set; }
        public int? CurrentVariationId { get; set; }
        public int? DescriptionId { get; set; }
        public string Description { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime MediaDate { get; set; }
        public DateTime EditDate { get; set; }
        public DateTime InsertDate { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public bool IsThumbOutdated { get; set; }
        public string ThumbFile { get; set; }
        public string ContentFile { get; set; }
        public string ContentType { get; set; }
        public string PreviewFile { get; set; }
        public int PreviewWidth { get; set; }
        public int PreviewHeight { get; set; }
        public bool IsPano { get; set; }
        public List<MetaDataSeed> Metadata { get; set; } = new List<MetaDataSeed>();
        public List<int> CategoryIds { get; set; } = new List<int>();
    }

    public sealed class VariationSeed
    {
        public int Id { get; set; }
        public int MediaItemId { get; set; }
        public int Position { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsOutdated { get; set; }
        public string ThumbFile { get; set; }
        public bool IsSynthetic { get; set; }
        public List<LayerSeed> Layers { get; set; } = new List<LayerSeed>();
        public List<int> CategoryIds { get; set; } = new List<int>();

        [JsonIgnore]
        public byte[] ThumbBytes { get; set; }
    }

    public sealed class LayerSeed
    {
        public int Position { get; set; }
        public string Edit { get; set; }
        public string Action { get; set; }
    }

    public sealed class MetaDataSeed
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public bool IsVisible { get; set; }
        public bool IsNull { get; set; }
    }

    public sealed class DescriptionSeed
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public string Command { get; set; }
        public List<int> MediaItemIds { get; set; } = new List<int>();
    }

    public sealed class GeoPointSeed
    {
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double Altitude { get; set; }
        public DateTime LocalTime { get; set; }
        public DateTime UtcTime { get; set; }
        public long GpsFileSize { get; set; }
        public DateTime GpsFileTime { get; set; }
        public double DistanceMeter { get; set; }
        public double Acceleration { get; set; }
    }

    public sealed class GpsFileSeed
    {
        public long Filesize { get; set; }
        public DateTime FileTime { get; set; }
    }

    public sealed class MeteorologySeed
    {
        public DateTime LogTime { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }

    public sealed class DuplicateSeed
    {
        public int MediaItemId { get; set; }
        public string Value { get; set; }
    }

    public sealed class RequestSeed
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Payload { get; set; }
        public int Limit { get; set; }
    }
}
