using System;

namespace MediaBrowser4.DB.API
{
    /// <summary>
    /// Data Transfer Object for inserting a new media file.
    /// </summary>
    [Serializable]
    public class MediafileInsertDto
    {
        // MediaFile fields
        public string Filename { get; set; }
        public string SortOrder { get; set; }
        public string Md5Value { get; set; }
        public long Length { get; set; }
        public int? Height { get; set; }
        public int? Width { get; set; }
        public int? Viewed { get; set; }
        public double Duration { get; set; }
        public int? Frames { get; set; }
        public int? CurrentVariationId { get; set; }
        public bool IsBookmarked { get; set; }
        public DateTime MediaDate { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime EditDate { get; set; }
        public int? Orientation { get; set; }
        public string Type { get; set; } // Corresponds to MediaType in Java, likely 'rgb' or 'dsh'
        public decimal? Longitude { get; set; }
        public decimal? Latitude { get; set; }

        // Variation fields
        public int? Position { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsOutdated { get; set; }
        public byte[] Thumb { get; set; }

        // Folder field
        public string FolderName { get; set; }
    }
}