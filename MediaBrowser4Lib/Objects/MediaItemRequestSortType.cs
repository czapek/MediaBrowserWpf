using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public enum MediaItemRequestSortType
    {
        FOLDERNAME, FILENAME, MEDIATYPE, MEDIADATE, LENGTH, DURATION, VIEWED, AREA, RELATION, CHECKSUM, NONE
    }

    public enum MediaItemRequestShuffleType
    {
        NONE, SHUFFLE, SHUFFLE_5, SHUFFLE_MEDIA, SHUFFLE_MEDIADATE_DAY
    }

    public enum MediaItemRequestSortDirection
    {
        ASCENDING = 1, DESCENDING = -1, NONE = 0
    }
}
