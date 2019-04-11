using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public class MediaItemArg : EventArgs
    {
        public List<MediaItem> MediaItemList;
        public List<MediaBrowser4.Objects.Category> CategoryList;
        public bool RemoveCategory;
    }
}
