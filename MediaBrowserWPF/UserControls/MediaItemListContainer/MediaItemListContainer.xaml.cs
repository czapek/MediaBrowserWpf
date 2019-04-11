using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für MediaItemListContainer.xaml
    /// </summary>
    public partial class MediaItemListContainer : UserControl
    {
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;      
        private ObservableCollection<MediaItem> mediaItemList;
        private string name, description;

        public bool IsBuild
        {
            get;
            private set;
        }

        public MediaItemListContainer()
        {
            InitializeComponent();            
        }

        public void Build(ObservableCollection<MediaItem> mediaItemList, string name, string description)
        {
            if (mediaItemList == null)
                return;

            this.name = name;
            this.description = description;
            this.mediaItemList = mediaItemList;
            this.mediaItemList.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(mediaItemList_CollectionChanged);
            Mouse.OverrideCursor = Cursors.Wait;
            this.ListBoxMediaItems.ItemsSource = mediaItemList;
            this.IsBuild = true;
            this.InfoText.Text = this.mediaItemList.Count + " Medien gefunden";
            Mouse.OverrideCursor = null;           
        }

        void mediaItemList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.InfoText.Text = this.mediaItemList.Count + " Medien gefunden";
        }

        public void Clear()
        {
            this.IsBuild = false;
            this.ListBoxMediaItems.ItemsSource = null;
        }

        public List<MediaItem> SelectedMediaItems
        {
            get
            {
                return this.ListBoxMediaItems.SelectedItems.Cast<MediaItem>().ToList();
            }
        }

        public List<MediaItem> MediaItems
        {
            get
            {
                return this.mediaItemList.ToList();
            }
        }

        public void OpenList()
        {
            if (this.OnRequest != null && this.name != null)
            {
                MediaItemObservableCollectionRequest observableCollectionRequest = new MediaItemObservableCollectionRequest(this.mediaItemList, this.name, this.description);
                observableCollectionRequest.UserDefinedId = name.GetHashCode();      
                observableCollectionRequest.ShuffleType = MediaItemRequestShuffleType.NONE;
                observableCollectionRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE, MediaItemRequestSortDirection.ASCENDING));

                this.OnRequest(this, new MediaItemRequestMessageArgs(observableCollectionRequest));
            }
        }

        private void ListBoxMediaItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.OpenList();
        }
    }
}
