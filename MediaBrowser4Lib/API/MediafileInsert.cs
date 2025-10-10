using MediaBrowser4.DB.SQLite;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser4.DB.API
{
    public class MediafileInsert
    {
        public static void insert(MediaItem mItem) {

            try
            {
                var apiClient = new MediaFileApiClient(
                    "http://localhost:9099",
                    "user",
                    "meineWelt");

                var dto = new MediafileInsertDto
                {
                    Filename = mItem.Filename,
                    SortOrder = mItem.Sortorder,
                    Md5Value = mItem.Md5Value,
                    Length = mItem.FileObject.Length,
                    Height = mItem.Height,
                    Width = mItem.Width,
                    Viewed = 0,
                    Duration = mItem.Duration,
                    Frames = mItem.Frames,
                    IsBookmarked = false,
                    MediaDate = mItem.MediaDate,
                    CreationDate = mItem.CreationDate,
                    EditDate = mItem.LastWriteDate,
                    Orientation = (int)mItem.Orientation,
                    Position = 1,
                    Type = mItem.DBType,
                    Latitude = (decimal?)mItem.Latitude,
                    Longitude = (decimal?)mItem.Longitude,
                    FolderName = MediaBrowser4.Utilities.FilesAndFolders.CleanPath(mItem.FileObject.DirectoryName),
                    Thumb = mItem.ThumbJpegData,
                };

                apiClient.InsertMediaFileAsync(dto).GetAwaiter().GetResult();
            }
            catch (TypeInitializationException ex)
            {
                // Halte hier mit einem Breakpoint an und schau dir 'ex.InnerException' an.
                // Oder logge die innere Ausnahme:
                Console.WriteLine(ex.InnerException);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Failed to send MediafileInsertDto to API for file: " + mItem.FullName);
            }
        }
    }
}
