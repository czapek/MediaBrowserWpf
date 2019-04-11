using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class MediaItemSortRequest : MediaItemRequest
    {       
        private string header;
        override public string Header
        {
            get
            {
                return this.UserDefinedName == null ? this.header : this.UserDefinedName;
            }
        }

        private string description;
        override public string Description
        {
            get
            {
                return this.description;
            }
        }

        public List<MediaItem> MediaItemList;

        public MediaItemSortRequest(string header, string description)
        {
            this.header = header;
            this.description = description;
            this.IsValid = true;
        }

        public override bool Equals(object obj)
        {
            MediaItemSortRequest other = obj as MediaItemSortRequest;

            if (other == null)
                return false;

            if (other.UserDefinedId > 0 && this.UserDefinedId > 0)
            {
                return other.UserDefinedId == this.UserDefinedId;
            }
            else if (!(other.UserDefinedId == 0 && this.UserDefinedId == 0))
            {
                return false;
            }

            return this.GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            if (this.UserDefinedId > 0)
            {
                return this.UserDefinedId;
            }
            else
            {
                return (this.description + this.header).GetHashCode();
            }
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }
    }
}
