using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace MediaBrowser4.Objects
{
    public class MediaItemObservableCollectionRequest : MediaItemRequest
    {
        private string description, header;
        private ObservableCollection<MediaItem> mediaItemList;
        public event EventHandler<System.Collections.Specialized.NotifyCollectionChangedEventArgs> OnCollectionChanged;

        public MediaItemObservableCollectionRequest(ObservableCollection<MediaItem> mediaItemList, string header, string description)
        {
            if (mediaItemList == null)
                return;

            this.header = header;
            this.mediaItemList = mediaItemList;
            this.description = description;
            this.mediaItemList.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(mediaItemList_CollectionChanged);
        }

        void mediaItemList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (this.OnCollectionChanged != null)
            {
                this.OnCollectionChanged.Invoke(this, e);
            }
        }

        public List<MediaItem> MediaItemList
        {
            get
            {
                return this.mediaItemList.ToList();
            }
        }

        public override string Header
        {
            get { return header; }
        }

        public override string Description
        {
            get { return description; }
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }
    }
}
