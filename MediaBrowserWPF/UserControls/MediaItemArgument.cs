using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class MediaItemArgument : EventArgs
    {
        private List<MediaItem> mItemList;
        private MediaItem mItem;

        public List<MediaItem> MediaItems
        {
            get
            {
                if (this.mItemList == null)
                {
                    this.mItemList = new List<MediaItem>();
                    if (this.mItem != null)
                        this.mItemList.Add(this.mItem);
                }

                return this.mItemList;
            }
        }

        public MediaItem MediaItem
        {
            get
            {
                if (this.mItem != null)
                {
                    return this.mItem;
                }
                else if (this.mItemList != null && this.mItemList.Count > 0)
                {
                    this.mItem = this.mItemList[0];
                    return this.mItem;
                }
                else
                {
                    return null;
                }
            }
        }

        public MediaItemArgument(List<MediaItem> mItemList)
        {
            this.mItemList = mItemList;
        }

        public MediaItemArgument(MediaItem mItem)
        {
            this.mItem = mItem;          
        }
    }
}
