using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public class MediaItemVirtualRequest : MediaItemRequest
    {
        public List<string> FileList;
        public MediaItemVirtualRequest(List<string> fileList)
        {
            this.IsValid = true;
            this.FileList = fileList;
            this.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FOLDERNAME, MediaItemRequestSortDirection.ASCENDING));
            this.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FILENAME, MediaItemRequestSortDirection.ASCENDING));
        }


        public override string Header
        {
            get
            {
                return "Virtuelle Medien";
            }
        }

        public override string Description
        {
            get
            {
                return "Diese Medien sind nicht Teil einer Datenbank.";
            }
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            return this.GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            return this.Header.GetHashCode();
        }
    }
}
