using MediaBrowser4.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4
{
    internal delegate void OnMediaItemInsert();

    public class MediaItemCallbackArgs : EventArgs
    {
        private int maxCount, pos;
        private bool updateDone = false;
        private bool folderAdded = false;
        MediaBrowser4.Objects.MediaItem mediaItem;

        public bool ItemChanged { get; set; }       

        public int MaxCount
        {
            get { return maxCount; }
        }

        public bool FolderAdded
        {
            get { return folderAdded; }
        }

        public bool UpdateDone
        {
            get { return updateDone; }
        }

        public int Pos
        {
            get { return pos; }
        }    

        public MediaBrowser4.Objects.MediaItem MediaItem
        {
            get { return mediaItem; }
        }

        public MediaItemCallbackArgs(int pos, int maxCount, MediaBrowser4.Objects.MediaItem mediaItem)
        {
            this.pos = pos;
            this.maxCount = maxCount;
            this.mediaItem = mediaItem;
            this.ItemChanged = false;
        }

        public MediaItemCallbackArgs(int pos, int maxCount, MediaBrowser4.Objects.MediaItem mediaItem, bool updateDone, bool folderAdded)
        {
            this.pos = pos;
            this.maxCount = maxCount;
            this.mediaItem = mediaItem;
            this.updateDone = updateDone;
            this.ItemChanged = false;
            this.folderAdded = folderAdded;
        }
    }

    public enum MediaItemCommand
    {
        ROTATE, REMOVE, UNDOREMOVE, BOOKMARK, SELECT,
        VARIATIONS_SET, VARIATIONS_CREATE, VARIATIONS_RENAME, VARIATIONS_REMOVE_CURRENT, VARIATIONS_REMOVE_ALL
    }

    public class MediaItemNewThumbArgs : EventArgs
    {
        System.Drawing.Bitmap bmp;
        MediaBrowser4.Objects.MediaItem mediaItem;

        public System.Drawing.Bitmap Bitmap
        {
            get { return bmp; }
        }

        public MediaBrowser4.Objects.MediaItem MediaItem
        {
            get { return mediaItem; }
        }

        public MediaItemNewThumbArgs(System.Drawing.Bitmap bmp, MediaBrowser4.Objects.MediaItem mediaItem)
        {
            this.bmp = bmp;
            this.mediaItem = mediaItem;
        }
    }

    public class MediaItemCommandArgs : EventArgs
    {
        MediaBrowser4.Objects.MediaItem mediaItem;
        MediaItemCommand command;

        public MediaBrowser4.Objects.MediaItem MediaItem
        {
            get { return mediaItem; }
        }

        public MediaItemCommand Command
        {
            get { return command; }
        }

        public int IntValue
        {
            get;
            private set;
        }

        public string StringValue
        {
            get;
            private set;
        }

        public MediaItemCommandArgs(MediaItemCommand command, MediaBrowser4.Objects.MediaItem mediaItem)
        {
            this.command = command;
            this.mediaItem = mediaItem;
        }

        public MediaItemCommandArgs(MediaItemCommand command, int intValue, MediaBrowser4.Objects.MediaItem mediaItem)
        {
            this.command = command;
            this.mediaItem = mediaItem;
            this.IntValue = intValue;
        }

        public MediaItemCommandArgs(MediaItemCommand command, string stringValue, MediaBrowser4.Objects.MediaItem mediaItem)
        {
            this.command = command;
            this.mediaItem = mediaItem;
            this.StringValue = stringValue;
        }
    }

}
