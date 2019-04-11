using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowser4
{
    public class MediaItemCache : Dictionary<int, MediaItem>
    {
        public event EventHandler<MediaItemCallbackArgs> OnRemove;
        public void Remove(MediaItem mItem)
        {
            this.Remove(mItem.Id);
        }

        public new void Remove(int id)
        {
            MediaItem mItem;

            if (TryGetValue(id, out mItem))
            {
                if (OnRemove != null)
                {
                    this.OnRemove.Invoke(this, new MediaItemCallbackArgs(0, 0, mItem));
                }

                base.Remove(id);
            }
        }
    }
}
