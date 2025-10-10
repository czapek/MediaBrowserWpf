using MediaBrowser4.DB.SQLite;
using System;
using System.Globalization;
using System.Text;

namespace MediaBrowser4.DB.API
{
    public static class ManualJsonConverter
    {
        private static string EscapeString(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public static string CreateJsonFromDto(MediafileInsertDto dto)
        {
            if (dto == null) return "{}";

            var sb = new StringBuilder();
            sb.Append("{");

            // MediaFile fields
            sb.Append($"\"filename\":{EscapeString(dto.Filename)},");
            sb.Append($"\"sortOrder\":{EscapeString(dto.SortOrder)},");
            sb.Append($"\"md5Value\":{EscapeString(dto.Md5Value)},");
            sb.Append($"\"length\":{dto.Length},");
            sb.Append($"\"height\":{(dto.Height.HasValue ? dto.Height.Value.ToString() : "null")},");
            sb.Append($"\"width\":{(dto.Width.HasValue ? dto.Width.Value.ToString() : "null")},");
            sb.Append($"\"viewed\":{(dto.Viewed.HasValue ? dto.Viewed.Value.ToString() : "null")},");
            sb.Append($"\"duration\":{dto.Duration.ToString(CultureInfo.InvariantCulture)},");
            sb.Append($"\"frames\":{(dto.Frames.HasValue ? dto.Frames.Value.ToString() : "null")},");
            sb.Append($"\"currentVariationId\":{(dto.CurrentVariationId.HasValue ? dto.CurrentVariationId.Value.ToString() : "null")},");
            sb.Append($"\"isBookmarked\":{dto.IsBookmarked.ToString().ToLower()},");
            // HIER IST DIE ÄNDERUNG: Datumsformat "s" für "yyyy-MM-ddTHH:mm:ss"
            sb.Append($"\"mediaDate\":\"{dto.MediaDate:s}\",");
            sb.Append($"\"creationDate\":\"{dto.CreationDate:s}\",");
            sb.Append($"\"editDate\":\"{dto.EditDate:s}\",");
            sb.Append($"\"orientation\":{(dto.Orientation.HasValue ? dto.Orientation.Value.ToString() : "null")},");
            sb.Append($"\"type\":{EscapeString(dto.Type)},");
            sb.Append($"\"longitude\":{(dto.Longitude.HasValue ? dto.Longitude.Value.ToString(CultureInfo.InvariantCulture) : "null")},");
            sb.Append($"\"latitude\":{(dto.Latitude.HasValue ? dto.Latitude.Value.ToString(CultureInfo.InvariantCulture) : "null")},");

            // Variation fields
            sb.Append($"\"position\":{(dto.Position.HasValue ? dto.Position.Value.ToString() : "null")},");
            sb.Append($"\"name\":{EscapeString(dto.Name)},");
            sb.Append($"\"description\":{EscapeString(dto.Description)},");
            sb.Append($"\"isOutdated\":{dto.IsOutdated.ToString().ToLower()},");
            sb.Append($"\"thumb\":{(dto.Thumb != null ? $"\"{Convert.ToBase64String(dto.Thumb)}\"" : "null")},");

            // Folder field
            sb.Append($"\"folderName\":{EscapeString(dto.FolderName)}");

            sb.Append("}");

            return sb.ToString();
        }
    }
}