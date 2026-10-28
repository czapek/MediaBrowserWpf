using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;

namespace MediaBrowser4.DB.Api
{
    internal sealed class ApiInsert : InsertItems
    {
        internal event EventHandler<MediaItemCallbackArgs> OnInsert;

        private readonly SpringApiClient client;
        private int insertCount;

        internal ApiInsert(ApiConnectionOptions options, SpringApiClient client)
            : base(options == null ? String.Empty : options.BaseUrl)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            this.client = client;
        }

        protected override void InsertInDB()
        {
            int maxValue = insertQueque.Count;
            int count = 0;
            bool folderAdded = false;
            MediaItem mediaItem = null;

            MediaBrowserContext.abortInsert = false;

            while (insertQueque.Count > 0)
            {
                mediaItem = insertQueque.Dequeue();

                if (mediaItem == null)
                    continue;

                try
                {
                    if (mediaItem.ThumbJpegData == null)
                        mediaItem.GetThumbnail();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, mediaItem.FileObject == null ? null : mediaItem.FileObject.FullName);
                }

                if (mediaItem.MediaDate == DateTime.MinValue)
                    DateAndTime.GetMediaDate(mediaItem, MediaBrowserContext.MediaDateDefaultFormatString);

                if (mediaItem.FileObject != null)
                {
                    mediaItem.CreationDate = mediaItem.FileObject.CreationTime;
                    mediaItem.LastWriteDate = mediaItem.FileObject.LastWriteTime;
                    mediaItem.FileLength = mediaItem.FileObject.Length;
                }

                if (String.IsNullOrWhiteSpace(mediaItem.Md5Value))
                    Crypto.GetMD5Value(mediaItem, MediaBrowserContext.CheckSumMaxLength);

                mediaItem.RoleId = MediaBrowserContext.DefaultRoleID;

                ApiMediaItemInsertDto dto = new ApiMediaItemInsertDto
                {
                    Id = mediaItem.Id > 0 ? (int?)mediaItem.Id : null,
                    Filename = mediaItem.Filename,
                    Foldername = FilesAndFolders.CleanPath(mediaItem.Foldername),
                    Sortorder = mediaItem.Sortorder,
                    Md5Value = mediaItem.Md5Value,
                    FileLength = mediaItem.FileLength,
                    Width = mediaItem.Width,
                    Height = mediaItem.Height,
                    Viewed = mediaItem.Viewed,
                    Duration = mediaItem.Duration,
                    Frames = mediaItem.Frames,
                    Orientation = (int)mediaItem.Orientation,
                    Type = mediaItem.DBType,
                    IsBookmarked = mediaItem.IsBookmarked,
                    IsDeleted = mediaItem.IsDeleted,
                    IsDuplicate = mediaItem.IsMd5Dublicate,
                    Priority = mediaItem.Priority,
                    RoleId = mediaItem.RoleId,
                    CurrentVariationId = mediaItem.VariationId > 0 ? (int?)mediaItem.VariationId : null,
                    Description = mediaItem.Description,
                    CreationDate = mediaItem.CreationDate,
                    MediaDate = mediaItem.MediaDate,
                    EditDate = mediaItem.LastWriteDate,
                    InsertDate = DateTime.Now,
                    Longitude = mediaItem.Longitude,
                    Latitude = mediaItem.Latitude,
                    VariationName = mediaItem.VariationId > 0 ? "Default" : null,
                    VariationPosition = 1,
                    Layers = mediaItem.Layers == null
                        ? new List<ApiLayerDto>()
                        : mediaItem.Layers.Select(x => new ApiLayerDto
                        {
                            Edit = x.Edit,
                            Action = x.Action,
                            Position = x.Position
                        }).ToList(),
                    Categories = new List<ApiCategoryDto>(),
                    Metadata = mediaItem.MetaData == null
                        ? new List<ApiMetaDataDto>()
                        : mediaItem.MetaData.Select(x => new ApiMetaDataDto
                        {
                            Id = x.Id,
                            Name = x.Name,
                            GroupName = x.GroupName,
                            Value = x.Value,
                            Type = x.Type,
                            IsVisible = x.IsVisible,
                            IsNull = x.Null
                        }).ToList(),
                    Thumb = mediaItem.ThumbJpegData
                };

                ApiInsertResultDto result = this.client.Post<ApiInsertResultDto>("/mediafiles/insert", dto);

                if (result != null)
                {
                    mediaItem.Id = result.MediaItemId;
                    mediaItem.VariationId = result.VariationId;
                    mediaItem.VariationIdDefault = result.DefaultVariationId > 0
                        ? result.DefaultVariationId
                        : result.VariationId;
                    mediaItem.IsMd5Dublicate = result.IsDuplicate;

                    if (!String.IsNullOrWhiteSpace(result.Message))
                        Log.Info("API-Insert: " + result.Message);
                }

                count++;
                insertCount = count;

                if (this.OnInsert != null)
                {
                    if (maxValue < insertQueque.Count)
                        maxValue = insertQueque.Count + 1;

                    if (insertCount > maxValue)
                        maxValue = insertCount;

                    this.OnInsert(this, new MediaItemCallbackArgs(insertCount, maxValue, mediaItem, false, folderAdded));
                }

                if (MediaBrowserContext.abortInsert)
                    insertQueque.Clear();
            }

            if (this.OnInsert != null)
                this.OnInsert(this, new MediaItemCallbackArgs(insertCount, insertCount, mediaItem, true, folderAdded));

            insertCount = 0;
        }
    }
}
