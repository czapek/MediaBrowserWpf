using System;
using System.Collections.Generic;

namespace MediaBrowser4.DB.Api
{
    public sealed class ApiCatalogInfoDto
    {
        public bool Valid { get; set; }
        public string Guid { get; set; }
        public string DbName { get; set; }
        public string User { get; set; }
        public string Host { get; set; }
        public string Message { get; set; }
    }

    public sealed class ApiLoginRequestDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Tenant { get; set; }
    }

    public sealed class ApiRefreshTokenDto
    {
        public string RefreshToken { get; set; }
    }

    public sealed class ApiTokenDto
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
    }

    public sealed class ApiFolderDto
    {
        public int Id { get; set; }
        public string Foldername { get; set; }
        public int ItemCount { get; set; }
    }

    public sealed class ApiCategoryDto
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

    public sealed class ApiCategoryCountDto
    {
        public ApiCategoryDto Category { get; set; }
        public int Count { get; set; }
    }

    public sealed class ApiLayerDto
    {
        public int Position { get; set; }
        public string Edit { get; set; }
        public string Action { get; set; }
    }

    public sealed class ApiVariationDto
    {
        public int Id { get; set; }
        public int MediaItemId { get; set; }
        public int Position { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsOutdated { get; set; }
        public byte[] Thumb { get; set; }
        public List<ApiLayerDto> Layers { get; set; }
    }

    public sealed class ApiVariationCreateDto
    {
        public string Name { get; set; }
        public bool SetDefault { get; set; }
        public string LayerAction { get; set; }
        public string LayerEdit { get; set; }
        public byte[] Thumb { get; set; }
    }

    public sealed class ApiMediaItemDto
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
        public byte[] Thumb { get; set; }
        public List<ApiMetaDataDto> Metadata { get; set; }
    }

    public sealed class ApiMediaQueryDto
    {
        public string Mode { get; set; }
        public List<int> MediaItemIds { get; set; }
        public List<int> FolderIds { get; set; }
        public List<int> CategoryIds { get; set; }
        public List<int> VariationIds { get; set; }
        public List<string> FileNames { get; set; }
        public string FilePath { get; set; }
        public string Checksum { get; set; }
        public string SortString { get; set; }
        public int Limit { get; set; }
        public bool StoreView { get; set; }
        public bool IsIntersection { get; set; }
        public bool IsRecursive { get; set; }
        public string CategoryRequestType { get; set; }
        public ApiSearchTokenDto SearchToken { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public long? RequestTicket { get; set; }
        public string Sql { get; set; }
        public List<ApiSqlParameterDto> Parameters { get; set; }
        public List<string> SortFields { get; set; }
        public string Criteria { get; set; }
    }

    public sealed class ApiSqlParameterDto
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }

    public sealed class ApiSqlQueryDto
    {
        public string Sql { get; set; }
        public string SortString { get; set; }
        public int Limit { get; set; }
        public List<ApiSqlParameterDto> Parameters { get; set; }
    }

    public sealed class ApiMediaActionDto
    {
        public string Action { get; set; }
        public List<int> MediaItemIds { get; set; }
        public List<int> VariationIds { get; set; }
        public List<int> CategoryIds { get; set; }
        public List<int> FolderIds { get; set; }
        public int? RoleId { get; set; }
        public int? Priority { get; set; }
        public bool? Flag { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public string NewName { get; set; }
        public DateTime? MediaDate { get; set; }
        public string VariationName { get; set; }
        public List<ApiLayerDto> Layers { get; set; }
        public List<ApiCategoryDto> Categories { get; set; }
        public string Criteria { get; set; }
        public int? Seconds { get; set; }
    }

    public sealed class ApiMediaItemInsertDto
    {
        public int? Id { get; set; }
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
        public int Priority { get; set; }
        public int RoleId { get; set; }
        public int? CurrentVariationId { get; set; }
        public string Description { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime MediaDate { get; set; }
        public DateTime EditDate { get; set; }
        public DateTime InsertDate { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public string VariationName { get; set; }
        public int VariationPosition { get; set; }
        public List<ApiLayerDto> Layers { get; set; }
        public List<ApiCategoryDto> Categories { get; set; }
        public List<ApiMetaDataDto> Metadata { get; set; }
        public byte[] Thumb { get; set; }
    }

    public sealed class ApiInsertResultDto
    {
        public int MediaItemId { get; set; }
        public int VariationId { get; set; }
        public int DefaultVariationId { get; set; }
        public bool IsDuplicate { get; set; }
        public string Message { get; set; }
    }

    public sealed class ApiImageObjectDto
    {
        public int Id { get; set; }
        public string ObjectName { get; set; }
    }

    public sealed class ApiImageReadableTextDto
    {
        public int Id { get; set; }
        public string TextLine { get; set; }
    }

    public sealed class ApiImageAnalysisDto
    {
        public int Id { get; set; }
        public int MediaItemId { get; set; }
        public string Filename { get; set; }
        public string Foldername { get; set; }
        public string MediaType { get; set; }
        public DateTime MediaDate { get; set; }
        public int AnimalCount { get; set; }
        public string WeatherDescription { get; set; }
        public int ObjectCount { get; set; }
        public DateTime? CreationDate { get; set; }
        public string LocationDescription { get; set; }
        public string FormatOfDocumentOrImage { get; set; }
        public string DetailedImageAnalysis { get; set; }
        public int? TemperaturMinCelsius { get; set; }
        public int? TemperaturMaxCelsius { get; set; }
        public double? ModelTemperature { get; set; }
        public long? CreatedInMilliseconds { get; set; }
        public string Model { get; set; }
        public int PersonCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<ApiImageObjectDto> Objects { get; set; }
        public List<ApiImageReadableTextDto> ReadableText { get; set; }
    }

    public sealed class ApiImageAnalysisUpsertDto
    {
        public int AnimalCount { get; set; }
        public string WeatherDescription { get; set; }
        public int ObjectCount { get; set; }
        public DateTime? CreationDate { get; set; }
        public string LocationDescription { get; set; }
        public string FormatOfDocumentOrImage { get; set; }
        public string DetailedImageAnalysis { get; set; }
        public int? TemperaturMinCelsius { get; set; }
        public int? TemperaturMaxCelsius { get; set; }
        public double? ModelTemperature { get; set; }
        public long? CreatedInMilliseconds { get; set; }
        public string Model { get; set; }
        public int PersonCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<string> Objects { get; set; }
        public List<string> ReadableText { get; set; }
    }

    public sealed class ApiImageAnalysisQueryDto
    {
        public List<int> MediaItemIds { get; set; }
        public string Model { get; set; }
        public int? AnimalCountFrom { get; set; }
        public int? AnimalCountTo { get; set; }
        public int? ObjectCountFrom { get; set; }
        public int? ObjectCountTo { get; set; }
        public int? PersonCountFrom { get; set; }
        public int? PersonCountTo { get; set; }
        public int? TemperaturMinCelsiusFrom { get; set; }
        public int? TemperaturMinCelsiusTo { get; set; }
        public int? TemperaturMaxCelsiusFrom { get; set; }
        public int? TemperaturMaxCelsiusTo { get; set; }
        public DateTime? CreationDateFrom { get; set; }
        public DateTime? CreationDateTo { get; set; }
        public string ObjectName { get; set; }
        public string Text { get; set; }
        public bool? HasObjects { get; set; }
        public bool? HasReadableText { get; set; }
        public bool IncludeObjects { get; set; }
        public bool IncludeReadableText { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public string Sort { get; set; }
    }

    public sealed class ApiNumericRangeDto
    {
        public double? Min { get; set; }
        public double? Max { get; set; }
    }

    public sealed class ApiDateRangeDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public sealed class ApiImageAnalysisFilterDto
    {
        public string Text { get; set; }
        public string TextMode { get; set; } = "NATURAL";
        public List<string> TextFields { get; set; }
        public List<string> Models { get; set; }
        public List<string> ObjectNames { get; set; }
        public string ObjectMatch { get; set; } = "ANY";
        public List<string> WeatherDescriptions { get; set; }
        public List<string> LocationDescriptions { get; set; }
        public List<string> Formats { get; set; }
        public List<string> MediaTypes { get; set; }
        public ApiNumericRangeDto PersonCount { get; set; }
        public ApiNumericRangeDto AnimalCount { get; set; }
        public ApiNumericRangeDto ObjectCount { get; set; }
        public ApiNumericRangeDto TemperaturMinCelsius { get; set; }
        public ApiNumericRangeDto TemperaturMaxCelsius { get; set; }
        public ApiNumericRangeDto ModelTemperature { get; set; }
        public ApiNumericRangeDto CreatedInMilliseconds { get; set; }
        public ApiDateRangeDto CreationDate { get; set; }
        public ApiDateRangeDto CreatedAt { get; set; }
        public ApiDateRangeDto MediaDate { get; set; }
        public List<int> MediaItemIds { get; set; }
        public List<int> FolderIds { get; set; }
        public List<int> CategoryIds { get; set; }
        public bool CategoryRecursive { get; set; }
        public bool IncludeDeleted { get; set; }
        public bool? HasObjects { get; set; }
        public bool? HasReadableText { get; set; }
    }

    public sealed class ApiImageAnalysisSearchRequestDto
    {
        public ApiImageAnalysisFilterDto Filter { get; set; }
        public string Sort { get; set; } = "MEDIA_DATE_DESC";
        public int Offset { get; set; }
        public int Limit { get; set; } = 100;
        public bool IncludeObjects { get; set; }
        public bool IncludeReadableText { get; set; }
    }

    public sealed class ApiImageAnalysisSearchResultDto
    {
        public long Total { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public List<ApiImageAnalysisMediaItemDto> Items { get; set; }
    }

    public sealed class ApiImageAnalysisMediaItemDto
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string Foldername { get; set; }
        public DateTime MediaDate { get; set; }
        public string Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int VariationId { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsBookmarked { get; set; }
        public ApiImageAnalysisSummaryDto Analysis { get; set; }
    }

    public sealed class ApiImageAnalysisSummaryDto
    {
        public int Id { get; set; }
        public string Model { get; set; }
        public int PersonCount { get; set; }
        public int AnimalCount { get; set; }
        public int ObjectCount { get; set; }
        public int? TemperaturMinCelsius { get; set; }
        public int? TemperaturMaxCelsius { get; set; }
        public double? ModelTemperature { get; set; }
        public long? CreatedInMilliseconds { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CreationDate { get; set; }
        public string WeatherDescription { get; set; }
        public string LocationDescription { get; set; }
        public string FormatOfDocumentOrImage { get; set; }
        public List<string> Objects { get; set; }
        public List<string> ReadableText { get; set; }
    }

    public sealed class ApiImageAnalysisValueSearchResultDto
    {
        public List<string> Values { get; set; }
    }

    public sealed class ApiImageAnalysisFilterFieldDto
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public string Control { get; set; }
        public bool ValueSearch { get; set; }
        public string Source { get; set; }
        public List<string> FixedValues { get; set; }
    }

    public sealed class ApiImageAnalysisRangesDto
    {
        public ApiNumericRangeDto PersonCount { get; set; }
        public ApiNumericRangeDto AnimalCount { get; set; }
        public ApiNumericRangeDto ObjectCount { get; set; }
        public ApiNumericRangeDto TemperaturMinCelsius { get; set; }
        public ApiNumericRangeDto TemperaturMaxCelsius { get; set; }
        public ApiNumericRangeDto ModelTemperature { get; set; }
        public ApiNumericRangeDto CreatedInMilliseconds { get; set; }
    }

    public sealed class ApiMetaDataDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public bool IsVisible { get; set; }
        public bool IsNull { get; set; }
    }

    public sealed class ApiAttachmentDto
    {
        public int? Id { get; set; }
        public string FullName { get; set; }
        public string Type { get; set; }
        public List<int> MediaItemIds { get; set; }
        public bool IsReferenced { get; set; }
        public bool IsShared { get; set; }
    }

    public sealed class ApiDescriptionDto
    {
        public int? Id { get; set; }
        public string Value { get; set; }
        public string Command { get; set; }
        public List<int> MediaItemIds { get; set; }
    }

    public sealed class ApiRoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Description { get; set; }
    }

    public sealed class ApiGeoPointDto
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

    public sealed class ApiGpsFileDto
    {
        public long Filesize { get; set; }
        public DateTime FileTime { get; set; }
    }

    public sealed class ApiMeteorologyDto
    {
        public DateTime LogTime { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }

    public sealed class ApiPreviewDto
    {
        public byte[] Binary { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Extension { get; set; }
        public string TempPath { get; set; }
    }

    public sealed class ApiFaceDto
    {
        public int VariationId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double RelativeSize { get; set; }
    }

    public sealed class ApiFacePreviewDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<ApiFaceDto> Faces { get; set; }
    }

    public sealed class ApiFaceCountDto
    {
        public int VariationId { get; set; }
        public int Count { get; set; }
    }

    public sealed class ApiDuplicateDto
    {
        public int MediaItemId { get; set; }
        public string Value { get; set; }
    }

    public sealed class ApiThumbDto
    {
        public int VariationId { get; set; }
        public byte[] Thumb { get; set; }
        public string Extension { get; set; }
        public bool IsOutdated { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class ApiThumbUploadDto
    {
        public byte[] Thumb { get; set; }
        public string Extension { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsOutdated { get; set; }
    }

    public sealed class ApiUserRequestDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Payload { get; set; }
        public int Limit { get; set; }
    }

    public sealed class ApiXmlNodeDto
    {
        public string SourceId { get; set; }
        public string Xml { get; set; }
    }

    public sealed class ApiXmlImportRequestDto
    {
        public int FolderId { get; set; }
        public List<int> CategoryIds { get; set; }
        public List<ApiXmlNodeDto> Nodes { get; set; }
    }

    public sealed class ApiSearchTokenDto
    {
        public string SearchText1 { get; set; }
        public bool SearchText1Not { get; set; }
        public bool SearchText1Description { get; set; }
        public bool SearchText1Category { get; set; }
        public bool SearchText1Filename { get; set; }
        public bool SearchText1Md5 { get; set; }
        public bool SearchText1Folder { get; set; }
        public string SearchText2 { get; set; }
        public bool SearchText2Not { get; set; }
        public bool SearchText2Description { get; set; }
        public bool SearchText2Category { get; set; }
        public bool SearchText2Filename { get; set; }
        public bool SearchText2Md5 { get; set; }
        public bool SearchText2Folder { get; set; }
        public string MetaDataKey { get; set; }
        public string MetaDataValue { get; set; }
        public bool DateFromEnabled { get; set; }
        public DateTime DateFrom { get; set; }
        public bool DateToEnabled { get; set; }
        public DateTime DateTo { get; set; }
        public double LengthFrom { get; set; }
        public double LengthTo { get; set; }
        public double DurationFrom { get; set; }
        public double DurationTo { get; set; }
        public int PriorityFrom { get; set; }
        public int PriorityTo { get; set; }
        public int DimensionFrom { get; set; }
        public int DimensionTo { get; set; }
        public string RelationType { get; set; }
        public bool MediaTypeRgb { get; set; }
        public bool MediaTypeDirectShow { get; set; }
    }

    public sealed class ApiTextDto
    {
        public string Value { get; set; }
    }

    public sealed class ApiPathDto
    {
        public string Path { get; set; }
        public string SortPath { get; set; }
    }

    public sealed class ApiThumbBuildDto
    {
        public List<string> Files { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
