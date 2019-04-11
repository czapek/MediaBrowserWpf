using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using System.IO;

namespace MediaBrowser4.Objects
{
    public enum AttachmentType { STICKY, ATTACHED, DETACHE }
    public class Attachment
    {
        private MediaItem mediaItem;
        private FileInfo fileInfo;
        private string fullname;
        public int? Id;

        public bool IsReferenced = true;
        public bool IsShared = false;

        public string FullName { 
            get { return this.fullname; }
            set
            {
                this.fullname = value;
                this.fileInfo = null;
            }
        }

        public AttachmentType Type { get; private set; }

        public void Detach()
        {
            this.Type = AttachmentType.DETACHE;
        }

        public string Name
        {
            get
            {
                return System.IO.Path.GetFileName(FullName);
            }
        }

        public string GuiName
        {
            get
            {
                return this.Name + (this.MediaItemList == null ? "" : " " + this.MediaItemList.Count + "x");
            }
        }

        public string Description
        {
            get
            {
                if (this.FileInfo.Exists)
                {
                    return this.FullName + " (" + string.Format("{0:0,0}", this.FileInfo.Length) + " Byte)";
                }
                else
                {
                    return this.FullName + " (Datei nicht gefunden)";
                }
            }
        }

        public FileInfo FileInfo
        {
            get
            {
                if (this.fileInfo == null)
                {
                    this.fileInfo = new FileInfo(this.FullName);
                }

                return this.fileInfo;
            }
        }

        public List<MediaItem> MediaItemList { get; private set; }

        public Attachment(List<MediaItem> mItemList, string fullName)
        {
            this.Type = AttachmentType.ATTACHED;
            this.MediaItemList = mItemList;
            this.FullName = fullName;
        }

        public Attachment(MediaItem mItem, string fullName)
        {
            this.Type = AttachmentType.STICKY;
            this.mediaItem = mItem;
            this.FullName = fullName;
        }

        public override string ToString()
        {
            return this.Description;
        }

        public override bool Equals(object obj)
        {
            Attachment other = obj as Attachment;

            if (other == null)
                return false;

            if (this.Id != null && other.Id != null)
            {
                return this.Id.Equals(other.Id);
            }
            else
            {
                return this.FullName.ToLower().Equals(other.FullName.ToLower());
            }

        }

        public override int GetHashCode()
        {
            if (this.Id == null)
                return this.FullName.GetHashCode();
            else
                return this.Id.Value;
        }
    }
}
