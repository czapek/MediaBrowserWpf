using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using MediaBrowser4;
using MediaBrowser4.Objects;
using System.IO;
using MediaBrowserWPF.Viewer;
using MediaBrowserWPF.Utilities;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media;
using MediaBrowserWPF.Dialogs;
using SmartRename;
using System.ComponentModel;
using System.Windows.Threading;
using MediaProcessing.FaceDetection;
using System.Windows.Media.Imaging;
using System.Net.Cache;
using MediaBrowser4.Utilities;
using System.Device.Location;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für ThumblistContainer.xaml
    /// </summary>
    public partial class ThumblistContainer : UserControl, INotifyPropertyChanged
    {
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;
        private ThumblistContainerContextMenu thumblistContainerContextMenu; //Das zentrale Contextmenü
        private MediaItemRequest request; //Die Abfragegrundlage auf deren Basis die MediaItems erstellt wurden.
        private List<MediaItem> mediaItemList; //Interne Liste aller aus dem Request stammenden MediaItems
        private List<MediaItem> selectedMediaItemList;
        private const int maxLoad = 200; //Die Maximalgröße fürs Paging der Listview
        private MediaItem lastMediaItemUpdatetd; //Das letzte der Listview hinzugefügte MediaItem
        private IViewer mediaViewer; //Der viewer für diesen Thumbcontainer

        public event EventHandler<MediaItemArgument> OnShowInDbTree;
        public event EventHandler<MediaItemArgument> OnSelectedMediaItemsChanged;
        public event EventHandler OnRequestChanging;

        private Visibility textBoxVisibility = MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE ? Visibility.Visible : Visibility.Collapsed;
        public Visibility TextBoxVisibility
        {
            get { return textBoxVisibility; }
            set
            {
                textBoxVisibility = value;
                this.OnPropertyChanged("TextBoxVisibility");
                this.OnPropertyChanged("ShowTooltip");
                this.OnPropertyChanged("TextBoxWidth");
            }
        }
        public bool ShowTooltip
        {
            get { return textBoxVisibility != System.Windows.Visibility.Visible; }
        }

        private double currentWidth = TextBoxBigWidth;
        private const double TextBoxSmallWidth = 250;
        private const double TextBoxBigWidth = 600;
        public GridLength TextBoxWidth
        {
            get { return textBoxVisibility == System.Windows.Visibility.Visible ? new GridLength(currentWidth, GridUnitType.Pixel) : new GridLength(1, GridUnitType.Star); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Konstruktor
        /// </summary>
        public ThumblistContainer()
        {
            InitializeComponent();

            MediaBrowserContext.GlobalMediaItemCache.OnRemove += new EventHandler<MediaItemCallbackArgs>(GlobalMediaItemCache_OnRemove);
            this.thumblistContainerContextMenu = new ThumblistContainerContextMenu();
            this.thumblistContainerContextMenu.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(thumblistContainerContextMenu_OnRequest);
            this.MediaItemContainer.ContextMenu = this.thumblistContainerContextMenu;
            this.MediaItemContainer.ContextMenuOpening += new ContextMenuEventHandler(MediaItemContainer_ContextMenuOpening);
        }

        void thumblistContainerContextMenu_OnRequest(object sender, MediaItemRequestMessageArgs e)
        {
            if (this.OnRequest != null)
            {
                this.OnRequest(this, e);
            }
        }

        void MediaItemContainer_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (this.thumblistContainerContextMenu != null)
                this.thumblistContainerContextMenu.ThumblistContainer = this;
        }

        public List<MediaItem> MediaItemList
        {
            get
            {
                return this.mediaItemList;
            }
        }

        /// <summary>
        /// Setzen der Abfragegrundlage provoziert ein neu Laden aller MediaItems
        /// </summary>
        public MediaItemRequest Request
        {
            get
            {
                return this.request;
            }

            set
            {
                MediaItem selectedOld = this.mediaItemList != null ? this.mediaItemList.FirstOrDefault(x => this.selectedMediaItemList.Contains(x)) : null;

                if (value.SelectedMediaItem != null)
                {
                    selectedOld = value.SelectedMediaItem;
                }

                if (this.OnRequestChanging != null)
                {
                    this.OnRequestChanging(value, EventArgs.Empty);
                }

                //mitten im Renderprozess
                if (this.lastMediaItemUpdatetd != null)
                    return;

                Mouse.OverrideCursor = Cursors.Wait;
                this.IsEnabled = false;

                this.request = value;
                if (this.request.SortTypeList.Count == 0)
                {
                    if (this.request is MediaItemCategoryRequest
                        && ((MediaItemCategoryRequest)this.request).Categories.Count(x => x.IsDate)
                        == ((MediaItemCategoryRequest)this.request).Categories.Length)
                    {
                        this.request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE, MediaItemRequestSortDirection.ASCENDING));
                    }
                    else
                    {
                        this.request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FOLDERNAME, MediaItemRequestSortDirection.ASCENDING));
                    }

                    this.request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FILENAME, MediaItemRequestSortDirection.ASCENDING));
                }

                this.thumblistContainerContextMenu.SetSortTypeList(this.request.SortTypeList, this.request.ShuffleType);

                if (this.request is MediaItemObservableCollectionRequest)
                {
                    this.mediaItemList = ((MediaItemObservableCollectionRequest)this.request).MediaItemList;
                    ((MediaItemObservableCollectionRequest)this.request).OnCollectionChanged += new EventHandler<System.Collections.Specialized.NotifyCollectionChangedEventArgs>(ThumblistContainer_OnCollectionChanged);

                }
                else
                {
                    this.mediaItemList = MediaBrowserContext.GetMediaItems(this.request);
                }

                this.selectedMediaItemList = new List<MediaItem>();

                SetThumblist(selectedOld);
            }
        }

        public void Refresh()
        {
            this.Request.Refresh();
            this.Request = this.Request;
        }

        public void RefreshSql()
        {
            if (this.Request != null)
            {
                MediaItemSqlRequest request = this.Request is MediaItemSqlRequest ? (MediaItemSqlRequest)this.Request : this.Request.MediaItemSqlRequest;

                if (request == null && this.Request is MediaItemObservableCollectionRequest)
                {
                    request = MediaBrowserContext.GetSqlRequest(((MediaItemObservableCollectionRequest)this.Request).MediaItemList);
                }

                if (request != null)
                {
                    string sqlname = System.IO.Path.Combine(
                         System.IO.Path.GetTempPath(),
                         "MediaBrowserWpf.sql");

                    System.IO.File.WriteAllText(sqlname, request.Sql);
                    Process.Start(sqlname);

                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,
                           "Das SQL im Temp-Ordner kann editiert werden (MediaBrowserWpf.sql). Nach dem speichern ok drücken!",
                               "SQL kann editiert werden", MessageBoxButton.OK, MessageBoxImage.Information);

                    request.Sql = System.IO.File.ReadAllText(sqlname);

                    this.Request = request;
                }
            }
        }

        private void SetThumblist(MediaItem selectedOld)
        {
            if (this.mediaItemList == null)
            {
                Mouse.OverrideCursor = null;
                this.IsEnabled = true;
                return;
            }

            this.SortMediaItemList();

            this.AddMediaItems();

            this.SelectedMediaItem = selectedOld;

            Thread thread = new Thread(() =>
            {
                foreach (MediaItem mItem in this.mediaItemList)
                {
                    if (!File.Exists(mItem.FullName))
                    {
                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                        {
                            mItem.IsFileNotFound = true;
                        }));
                    }
                    else if (File.Exists(mItem.FullName))
                    {
                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                        {
                            mItem.IsFileNotFound = false;
                        }));
                    }
                }
            });

            this.thumblistContainerContextMenu.ThumblistContainer = this;
            thread.IsBackground = true;
            thread.Start();
        }

        public void UpdateFileName(MediaItem mItem)
        {
            if (mItem == null)
                return;

            MediaItem localItem = this.mediaItemList.Find(item => item.Id == mItem.Id);

            if (localItem != null)
            {
                localItem.Rename(mItem.FullName);
            }
        }

        public void UpdateFolder(Folder folder)
        {
            if (folder == null)
                return;

            List<MediaItem> allLocalItems = this.mediaItemList.FindAll(
                item => item.Foldername.ToLower() == folder.FullPath.ToLower());

            foreach (MediaItem localItem in allLocalItems)
            {
                localItem.Rename(folder.FullPath + Path.DirectorySeparatorChar + localItem.Filename);
            }
        }

        public List<MediaItem> MediaItems
        {
            get
            {
                return new List<MediaItem>(this.mediaItemList);
            }
        }

        public void Select(List<MediaItem> mediaItemList)
        {
            for (int i = 0; i < this.MediaItemContainer.Items.Count; i++)
            {
                MediaItem mItem = this.MediaItemContainer.Items[i] as MediaItem;
                (this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem).IsSelected
                    = mediaItemList.Contains(mItem);
            }
        }

        public void SelectDeleted()
        {
            for (int i = 0; i < this.MediaItemContainer.Items.Count; i++)
            {
                MediaItem mItem = this.MediaItemContainer.Items[i] as MediaItem;
                (this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem).IsSelected
                    = mItem.IsDeleted;
            }
        }

        public MediaItem SelectedMediaItem
        {
            get
            {
                if (this.selectedMediaItemList != null && this.selectedMediaItemList.Count > 0)
                {
                    return this.selectedMediaItemList[0];
                }
                else
                {
                    return null;
                }
            }

            set
            {
                if (value != null)
                {
                    this.MediaItemContainer.ScrollIntoView(value);
                    this.MediaItemContainer.SelectedItem = value;

                    if (!(this.mediaViewer != null && this.mediaViewer.IsVisible))
                    {
                        ListBoxItem listBoxItem = this.MediaItemContainer.ItemContainerGenerator.ContainerFromItem(value) as ListBoxItem;
                        if (listBoxItem != null)
                            this.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                listBoxItem.Focus();
                            }), DispatcherPriority.ApplicationIdle);
                    }

                }

                if (this.MediaItemContainer.Items.Count > 0 && this.MediaItemContainer.SelectedItem == null)
                {
                    this.MediaItemContainer.SelectedIndex = 0;
                }
            }
        }

        public List<MediaItem> SelectedMediaItems
        {
            get
            {
                return this.selectedMediaItemList;
            }
        }

        public void SelectMediaItems(List<MediaItem> mItems)
        {
            this.MediaItemContainer.UnselectAll();

            this.selectedMediaItemList.Clear();

            for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
                this.selectedMediaItemList.Add(this.mediaItemList[i]);

            foreach (MediaItem mItem in mItems)
            {
                ListViewItem lv = (ListViewItem)this.MediaItemContainer.ItemContainerGenerator.ContainerFromItem(mItem);

                this.selectedMediaItemList.Add(mItem);

                if (lv != null)
                    lv.IsSelected = true;
            }

            this.SetStatusMessage2();
        }

        private void AddMediaItems()
        {
            this.MediaItemContainer.Items.Clear();

            if (this.mediaItemList.Count == 0)
            {
                Mouse.OverrideCursor = null;
                this.IsEnabled = true;
                return;
            }

            foreach (MediaItem mItem in this.mediaItemList)
            {
                if (this.MediaItemContainer.Items.Count < maxLoad)
                {
                    this.MediaItemContainer.Items.Add(mItem);
                }
            }


            this.SetStatusMessage();

            this.lastMediaItemUpdatetd = (MediaItem)this.MediaItemContainer.Items[this.MediaItemContainer.Items.Count - 1];

            Mouse.OverrideCursor = null;
        }

        private void SetStatusMessage()
        {
            if (this.MediaItemContainer.Dispatcher.CheckAccess())
            {
                this.SetStatusMessage2();
            }
            else
            {
                Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    this.SetStatusMessage2();
                }));
            }
        }

        private void SetStatusMessage2()
        {
            double seconds = this.SelectedMediaItems.Where(x => x is MediaItemVideo).Select(x => x as MediaItemVideo).Sum(x => x.CroppedDuration);
            string duration = seconds > 300 ? (int)(seconds / 60) + " min" : (int)seconds + " s";
            int selectedItems = this.selectedMediaItemList.Count;
            this.InfoText.Content = this.mediaItemList.Count
               + " Medien gefunden (" + this.MediaItemContainer.Items.Count + " geladen"
               + (selectedItems > 1 ? ", " + selectedItems + " ausgewählt, " + String.Format("{0:0,0} KB", this.SelectedMediaItems.Sum(x => x.FileLength) / 1024)
               : (selectedItems == 1 ? $", {this.MediaItemContainer.SelectedIndex + 1}. Position, {this.SelectedMediaItem.FileLength / 1024:n0} KByte" : ""))
               + (seconds > 0 ? ", " + duration : String.Empty) + ")";
        }

        #region sort
        public void SortMediaItemList()
        {
            if (this.request.SortTypeList.Count == 0)
                return;

            Mouse.OverrideCursor = Cursors.Wait;
            if (this.request.ShuffleType != MediaItemRequestShuffleType.NONE)
            {
                if (this.request.ShuffleType == MediaItemRequestShuffleType.SHUFFLE)
                {
                    this.SortByShuffle(this.mediaItemList);
                }
                else if (this.request.ShuffleType == MediaItemRequestShuffleType.SHUFFLE_5)
                {
                    this.ShortByShuffleN(5);
                }
                else if (this.request.ShuffleType == MediaItemRequestShuffleType.SHUFFLE_MEDIA)
                {
                    this.SortByShuffleMedia();
                }
                else if (this.request.ShuffleType == MediaItemRequestShuffleType.SHUFFLE_MEDIADATE_DAY)
                {
                    this.SortByShuffleMediadateDay();
                }
            }
            else
            {
                this.SortByType();
            }

            this.AddMediaItems();

            if (this.OnSelectedMediaItemsChanged != null)
            {
                this.OnSelectedMediaItemsChanged(this, new MediaItemArgument(this.SelectedMediaItems));
            }

        }

        private void SortByShuffleMediadateDay()
        {
            Dictionary<DateTime, List<MediaItem>> sortDic = new Dictionary<DateTime, List<MediaItem>>();

            foreach (MediaItem mItem in this.mediaItemList)
            {
                if (!sortDic.ContainsKey(mItem.MediaDate.Date))
                {
                    sortDic.Add(mItem.MediaDate.Date, new List<MediaItem>());
                }

                sortDic[mItem.MediaDate.Date].Add(mItem);
            }

            Random rand = new Random();
            sortDic = sortDic.OrderBy(x => rand.Next())
              .ToDictionary(item => item.Key, item => item.Value);

            int cnt = 0;
            foreach (List<MediaItem> mItemList in sortDic.Values)
            {
                foreach (MediaItem mItem in mItemList)
                {
                    mItem.ShuffleValue = cnt;
                    cnt++;
                }
            }

            this.mediaItemList.Sort(delegate (MediaItem m1, MediaItem m2)
            {
                return m1.ShuffleValue.CompareTo(m2.ShuffleValue);
            });
        }

        private void SortByShuffleMedia()
        {
            List<MediaItem> mlist1 = new List<MediaItem>();
            List<MediaItem> mlist2 = new List<MediaItem>();

            foreach (MediaItem mItem in this.mediaItemList)
            {
                if (mItem is MediaItemBitmap)
                {
                    mlist2.Add(mItem);
                }
                else
                {
                    mlist1.Add(mItem);
                }
            }

            if (mlist2.Count == 0 || mlist1.Count == 0)
            {
                this.SortByShuffle(this.mediaItemList);
                return;
            }

            this.SortByShuffle(mlist1);
            this.SortByShuffle(mlist2);

            if (mlist1.Count > mlist2.Count)
            {
                this.SortByShuffleMediaMix(mlist1, mlist2);
            }
            else
            {
                this.SortByShuffleMediaMix(mlist2, mlist1);
            }

            this.mediaItemList.Sort(delegate (MediaItem m1, MediaItem m2)
            {
                return m1.ShuffleValue.CompareTo(m2.ShuffleValue);
            });
        }

        private void SortByShuffleMediaMix(List<MediaItem> listBig, List<MediaItem> listSmall)
        {
            double factor = (double)listBig.Count / (double)listSmall.Count;

            for (int j = 0; j < listBig.Count; j++)
            {
                listBig[j].ShuffleValue = j * 1000;
            }

            for (int j = 0; j < listSmall.Count; j++)
            {
                listSmall[j].ShuffleValue = (int)((j * 1000.0 + 1) * factor);
            }
        }

        private void ShortByShuffleN(int n)
        {
            this.SortByType();

            Random random = new Random();

            List<int> randomNumbers = new List<int>();
            for (int i = 0; i < this.mediaItemList.Count; i++)
            {
                if (i % n == 0)
                {
                    randomNumbers.Add(i);
                }
            }

            int randVal = 0;

            for (int i = 0; i < this.mediaItemList.Count; i++)
            {
                if (i % n == 0)
                {
                    int pos = random.Next(0, randomNumbers.Count);
                    randVal = randomNumbers[pos];
                    randomNumbers.RemoveAt(pos);
                }

                this.mediaItemList[i].ShuffleValue = randVal;
                randVal++;
            }

            this.mediaItemList.Sort(delegate (MediaItem m1, MediaItem m2)
            {
                return m1.ShuffleValue.CompareTo(m2.ShuffleValue);
            });
        }

        private void SortByShuffle(List<MediaItem> mList)
        {
            Random random = new Random();

            foreach (MediaItem mItem in mList)
            {
                mItem.ShuffleValue = random.Next(Int32.MinValue, Int32.MaxValue);
            }

            mList.Sort(delegate (MediaItem m1, MediaItem m2)
            {
                return m1.ShuffleValue.CompareTo(m2.ShuffleValue);
            });
        }

        private void SortByType()
        {
            this.mediaItemList.Sort(delegate (MediaItem m1, MediaItem m2)
            {
                int sort = 0;

                foreach (Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection> tuple in this.request.SortTypeList)
                {
                    sort = this.CompareMediaItems(m1, m2, tuple);
                    if (sort != 0)
                        return sort;
                }

                return sort;
            });
        }

        private int CompareMediaItems(MediaItem m1, MediaItem m2, Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection> tuple)
        {
            int sort = 0;

            switch (tuple.Item1)
            {
                case MediaItemRequestSortType.DURATION:
                    sort = m1.Duration.CompareTo(m2.Duration);
                    break;

                case MediaItemRequestSortType.FILENAME:
                    sort = m1.Filename.ToLower().CompareTo(m2.Filename.ToLower());
                    break;

                case MediaItemRequestSortType.FOLDERNAME:
                    sort = m1.Foldername.ToLower().CompareTo(m2.Foldername.ToLower());
                    break;

                case MediaItemRequestSortType.LENGTH:
                    sort = m1.FileLength.CompareTo(m2.FileLength);
                    break;

                case MediaItemRequestSortType.MEDIADATE:
                    sort = m1.MediaDate.CompareTo(m2.MediaDate);
                    break;

                case MediaItemRequestSortType.MEDIATYPE:
                    sort = (m1 is MediaItemBitmap).CompareTo(m2 is MediaItemBitmap);
                    break;

                case MediaItemRequestSortType.VIEWED:
                    sort = m1.Viewed.CompareTo(m2.Viewed);
                    break;

                case MediaItemRequestSortType.CHECKSUM:
                    sort = m1.Md5Value.CompareTo(m2.Md5Value);
                    break;

                case MediaItemRequestSortType.AREA:
                    sort = (m1.Width * m1.Height).CompareTo((m2.Width * m2.Height));
                    break;

                case MediaItemRequestSortType.RELATION:
                    double rel1, rel2;
                    if (m1.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                        || m1.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    {
                        rel1 = (double)m1.Width / (double)m1.Height;
                    }
                    else
                    {
                        rel1 = (double)m1.Height / (double)m1.Width;
                    }

                    if (m2.Orientation == MediaItem.MediaOrientation.BOTTOMisBOTTOM
                        || m2.Orientation == MediaItem.MediaOrientation.TOPisBOTTOM)
                    {
                        rel2 = (double)m2.Width / (double)m2.Height;
                    }
                    else
                    {
                        rel2 = (double)m2.Height / (double)m2.Width;
                    }
                    sort = rel1.CompareTo(rel2);
                    break;
            }

            return (int)tuple.Item2 * sort;
        }
        #endregion

        #region events
        void ThumblistContainer_OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (MediaItem mitem in e.OldItems)
                {
                    if (this.mediaItemList.Contains(mitem))
                    {
                        this.mediaItemList.Remove(mitem);

                        if (this.MediaItemContainer.Items.Contains(mitem))
                            this.MediaItemContainer.Items.Remove(mitem);
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (MediaItem mitem in e.NewItems)
                {
                    if (!this.mediaItemList.Contains(mitem))
                    {
                        this.mediaItemList.Add(mitem);

                        if (!this.MediaItemContainer.Items.Contains(mitem))
                            this.MediaItemContainer.Items.Add(mitem);
                    }
                }
            }

            this.SetStatusMessage();
            this.lastMediaItemUpdatetd = this.MediaItemContainer.Items.Count > 0 ? (MediaItem)this.MediaItemContainer.Items[this.MediaItemContainer.Items.Count - 1] : null;
        }

        void GlobalMediaItemCache_OnRemove(object sender, MediaItemCallbackArgs e)
        {
            if (e.MediaItem != null)
            {
                RemoveMediaItem(e.MediaItem);
            }
        }

        public void RemoveMediaItem(MediaItem mItem)
        {
            if (this.mediaItemList.Contains(mItem))
            {
                this.mediaItemList.Remove(mItem);

                if (this.MediaItemContainer.Items.Contains(mItem))
                    if (this.MediaItemContainer.Dispatcher.CheckAccess())
                    {
                        this.MediaItemContainer.Items.Remove(mItem);
                    }
                    else
                    {
                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                        {
                            this.MediaItemContainer.Items.Remove(mItem);
                        }));
                    }

                this.SetStatusMessage();
                this.lastMediaItemUpdatetd = this.MediaItemContainer.Items.Count > 0 ? (MediaItem)this.MediaItemContainer.Items[this.MediaItemContainer.Items.Count - 1] : null;
            }
        }

        private void TopExpander_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void MediaItemContainer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!this.noSelectionEvent)
                {
                    if (e.RemovedItems.Count >= 1 && this.MediaItemContainer.SelectedItems.Count == 1)
                    {
                        this.selectedMediaItemList.Clear();
                        this.selectedMediaItemList.Add((MediaItem)this.MediaItemContainer.SelectedItem);
                    }
                    else
                    {
                        if (this.selectedMediaItemList.Count > this.MediaItemContainer.Items.Count
                            && Keyboard.Modifiers != ModifierKeys.Control)
                        {
                            for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
                                this.selectedMediaItemList.Remove(this.mediaItemList[i]);
                        }

                        foreach (MediaItem mItem in e.RemovedItems)
                            this.selectedMediaItemList.Remove(mItem);

                        foreach (MediaItem mItem in e.AddedItems)
                            this.selectedMediaItemList.Add(mItem);
                    }
                }

                if (this.OnSelectedMediaItemsChanged != null)
                {
                    this.OnSelectedMediaItemsChanged(this, new MediaItemArgument(this.SelectedMediaItems));
                }

                this.SetStatusMessage();

                ((MainWindow)Application.Current.MainWindow).ShowSelectedImage(false, null, this.SelectedMediaItem);

                e.Handled = true;
            }
            catch
            {

            }
        }

        private void ToggleTagEditor()
        {
            if (this.SelectedMediaItem != null)
            {
                ListViewItem lvi = (ListViewItem)this.MediaItemContainer.ItemContainerGenerator.ContainerFromItem(this.SelectedMediaItem);
                Point pointItem = lvi.PointToScreen(new Point(0, 0));
                Point pointContainer = this.MediaItemContainer.PointToScreen(new Point(0, 0));

                if (pointItem.Y < pointContainer.Y
                    || pointItem.Y > pointContainer.Y + this.MediaItemContainer.ActualHeight)
                {
                    pointItem = pointContainer;
                }

                if (pointItem.X > pointContainer.X + this.MediaItemContainer.ActualWidth - 310)
                {
                    pointItem = new Point(pointContainer.X + this.MediaItemContainer.ActualWidth - 310, pointItem.Y);
                }

                MediaBrowserWPF.Dialogs.TagEditorSingleton.ShowTagEditor(this.SelectedMediaItems, pointItem, App.Current.MainWindow);
            }
        }

        private void MediaItemContainer_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        MediaBrowserWPF.Utilities.FilesAndFolders.AddCopyMoveFromClipboard(
                            MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(
                            this.request is MediaItemFolderRequest ? ((MediaItemFolderRequest)this.request).Folders[0].FullPath :
                                (this.SelectedMediaItem != null ? this.SelectedMediaItem.FileObject.DirectoryName : null)));

                        e.Handled = true;
                    }
                    break;
            }
        }

        public void ShowRenameDialog()
        {
            this.ShowRenameDialog("%filename%");
        }

        public delegate string GetTimeSpanName(MediaItem mItem);
        public string SelectByTimeSpan(GetTimeSpanName timeSpanName)
        {
            if (this.SelectedMediaItem == null)
                return null;

            string selectedWeek = timeSpanName(this.SelectedMediaItem);
            this.selectedMediaItemList = new List<MediaItem>();

            if (selectedWeek != null)
            {
                string currentWeek;
                for (int i = 0; i < this.mediaItemList.Count; i++)
                {
                    MediaItem currentItem = this.mediaItemList[i];
                    currentWeek = timeSpanName(currentItem);
                    currentItem.IsSelected = selectedWeek == currentWeek;

                    if (this.MediaItemContainer.Items.Count > i)
                    {
                        ListViewItem item = this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;

                        if (item.IsSelected && currentItem.IsSelected)
                            item.IsSelected = false;

                        item.IsSelected = currentItem.IsSelected;
                    }
                    else if (currentItem.IsSelected)
                    {
                        this.selectedMediaItemList.Add(currentItem);
                    }
                }


                Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(MediaBrowserContext.DefaultMediaArchivRoot);

                if (folder == null)
                {
                    this.InfoText.Content = "Zwischenablage: " + selectedWeek;

                    try
                    {
                        Clipboard.SetText(selectedWeek);
                    }
                    catch { }
                }
            }

            return selectedWeek;
        }

        public void Next50Selection()
        {
            MediaItem lastItem = this.mediaItemList.LastOrDefault(x => this.selectedMediaItemList.Contains(x));
            int pos = lastItem == null ? 0 : this.mediaItemList.IndexOf(lastItem) + 1;
            int cnt = this.selectedMediaItemList.Count != 1 ? 50 : 49;


            for (int i = pos; i < pos + cnt; i++)
            {
                if (i < this.MediaItemContainer.Items.Count)
                {
                    ListViewItem lItem = this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                    lItem.IsSelected = true;
                }
                else if (i < this.mediaItemList.Count)
                {
                    if (!this.selectedMediaItemList.Contains(this.mediaItemList[i]))
                        this.selectedMediaItemList.Add(this.mediaItemList[i]);
                    else
                        this.selectedMediaItemList.Remove(this.mediaItemList[i]);
                }
            }

            this.SetStatusMessage2();
        }

        public void ShowRenameDialog(string templateText)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            RenameDialog sd = new RenameDialog(this.SelectedMediaItems, templateText);
            Mouse.OverrideCursor = null;
            sd.ShowDialog();
        }

        private void MediaItemContainer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            MediaItemRequestSortDirection sortDirection = MediaItemRequestSortDirection.ASCENDING;

            switch (e.Key)
            {
                //case Key.Escape:
                //    MediaBrowserWPF.Dialogs.TagEditorSingleton.ShowTagEditor()
                case Key.F4:
                    ((MainWindow)Application.Current.MainWindow).ShowSelectedImage(true, false, this.SelectedMediaItem);
                    break;

                case Key.F6:
                    this.thumblistContainerContextMenu.UncheckBaseSortType();
                    this.thumblistContainerContextMenu.MenuItemSortShuffle.IsChecked = Keyboard.Modifiers != ModifierKeys.Shift && Keyboard.Modifiers != ModifierKeys.Control;
                    this.thumblistContainerContextMenu.MenuItemSortShuffle5.IsChecked = Keyboard.Modifiers == ModifierKeys.Shift && Keyboard.Modifiers != ModifierKeys.Control;
                    this.thumblistContainerContextMenu.MenuItemSortShuffleMedia.IsChecked = Keyboard.Modifiers != ModifierKeys.Shift && Keyboard.Modifiers == ModifierKeys.Control;
                    this.thumblistContainerContextMenu.MenuItemSortAsc.IsChecked = true;
                    this.thumblistContainerContextMenu.Sort(MediaItemRequestSortDirection.ASCENDING);
                    break;

                case Key.F7:
                    if (this.thumblistContainerContextMenu.MenuItemSortMediadate.IsChecked)
                        sortDirection = this.thumblistContainerContextMenu.MenuItemSortDesc.IsChecked ? MediaItemRequestSortDirection.ASCENDING : MediaItemRequestSortDirection.DESCENDING;
                    else
                        sortDirection = MediaItemRequestSortDirection.ASCENDING;

                    this.thumblistContainerContextMenu.UncheckAllSortTypes();
                    this.thumblistContainerContextMenu.MenuItemSortMediadate.IsChecked = true;
                    this.thumblistContainerContextMenu.MenuItemSortFilename.IsChecked = true;
                    this.thumblistContainerContextMenu.MenuItemSortAsc.IsChecked = (sortDirection == MediaItemRequestSortDirection.ASCENDING);
                    this.thumblistContainerContextMenu.MenuItemSortDesc.IsChecked = (sortDirection == MediaItemRequestSortDirection.DESCENDING);
                    this.thumblistContainerContextMenu.Sort(sortDirection);
                    break;

                case Key.F12:
                    this.Next50Selection();
                    break;

                case Key.F2:
                    if (Keyboard.Modifiers == ModifierKeys.Control
                        || Keyboard.Modifiers == ModifierKeys.Shift)
                        ShowRenameDialog("%mediadate%{yyMMdd-HHmm-ss}");
                    else
                        ShowRenameDialog();
                    break;

                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.CopyToClipboard();
                    }
                    break;

                case Key.O:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.SelectFolder();
                    }
                    break;

                case Key.Delete:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.RemoveTemp();
                    }
                    else
                    {
                        this.ToggleMarkDelete();
                    }
                    break;

                case Key.X:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.CutToClipboard();
                    }
                    break;

                case Key.K:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                        this.thumblistContainerContextMenu.Copy(this.SelectedMediaItems);
                    else if (Keyboard.Modifiers == ModifierKeys.Control && MediaBrowserContext.CopyItemProperties.Categories != null)
                        this.thumblistContainerContextMenu.Paste(this.SelectedMediaItems);
                    break;
                case Key.T:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.ToggleTagEditor();
                        e.Handled = true;
                    }
                    break;

                case Key.MediaPlayPause:
                case Key.Enter:
                    if ((ModifierKeys.Control | ModifierKeys.Shift) == Keyboard.Modifiers)
                        this.ShowInDbTree();
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.OpenWith();
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        this.OpenInExplorer();
                    else
                        this.ShowMediaItem();
                    break;

                case Key.M:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.ShowMultiplayer();
                    break;

                case Key.W:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.ShowSphere3D();
                    break;

                case Key.A:
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        ShowGeoAdress(true);
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
                            this.selectedMediaItemList.Add(this.mediaItemList[i]);

                        this.MediaItemContainer.SelectAll();
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        ShowGeoAdress(false);
                    }
                    break;

                case Key.B:
                    //  if (Keyboard.Modifiers == ModifierKeys.Control)
                    this.ToggleBookmark();
                    break;

                case Key.MediaNextTrack:
                    this.MediaItemContainer.SelectedIndex = (this.MediaItemContainer.SelectedIndex + 10 >= this.MediaItemContainer.Items.Count - 1 ? this.MediaItemContainer.Items.Count - 1 : this.MediaItemContainer.SelectedIndex + 10);
                    this.MediaItemContainer.ScrollIntoView(this.MediaItemContainer.SelectedItem);
                    break;

                case Key.MediaPreviousTrack:
                    this.MediaItemContainer.SelectedIndex = (this.MediaItemContainer.SelectedIndex - 10 > 0 ? this.MediaItemContainer.SelectedIndex - 10 : 0);
                    this.MediaItemContainer.ScrollIntoView(this.MediaItemContainer.SelectedItem);
                    break;

            }
        }

        public void ChangeView()
        {
            if (currentWidth == TextBoxSmallWidth)
            {
                currentWidth = TextBoxBigWidth;
                this.TextBoxVisibility = System.Windows.Visibility.Visible;
            }
            else
            {
                currentWidth = TextBoxSmallWidth;
                this.TextBoxVisibility = this.TextBoxVisibility == System.Windows.Visibility.Visible ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
                if (this.TextBoxVisibility != System.Windows.Visibility.Visible)
                    currentWidth = TextBoxBigWidth;
            }
        }

        public void ShowGeoAdress(bool messageBox)
        {
            if (this.SelectedMediaItem != null)
            {
                GeoPoint gps = MediaBrowserContext.GetGpsNearest(this.SelectedMediaItem.MediaDate);

                CivicAddressResolver cs = new CivicAddressResolver();
                CivicAddress ca = cs.ResolveAddress(gps);

                if (gps != null)
                {
                    GeoAdress adress = GeoAdress.GetAdressXml(gps);
                    if (adress.StreetAddressFormatted != null)
                    {
                        Clipboard.SetText(adress.StreetAddressFormatted);
                        if (messageBox)
                        {
                            MessageBox.Show(adress.StreetAddressFormatted);
                        }
                        else
                        {
                            string url = null;
                            if (!adress.StreetAddressFormatted.ToLower().Contains("unnamed"))
                            {
                                string adressEnc = System.Web.HttpUtility.UrlEncode(adress.StreetAddressFormatted);
                                url = $"https://www.google.com/maps/place/{adressEnc}/@{gps.Latitude},{gps.Longitude},15z&language=de".Replace(",", ".").Replace(" ", ",");
                            }
                            else
                            {
                                url = $"http://maps.google.com/maps?&z=10&q={gps.Latitude}+{gps.Longitude}&ll={gps.Longitude}+{gps.Longitude}".Replace(",", ".").Replace(" ", ",");
                            }

                            System.Diagnostics.Process.Start(url);
                        }
                    }
                }
            }
        }

        public void OpenInExplorer()
        {
            if (this.SelectedMediaItem != null)
            {
                try
                {
                    if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(this.SelectedMediaItem.FileObject.FullName)))
                    {
                        MediaBrowserWPF.Utilities.FilesAndFolders.OpenExplorer(this.SelectedMediaItem.FileObject.FullName, false);
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {

                }
            }
        }

        public void RemoveTemp()
        {
            if (this.SelectedMediaItems.Count == 0)
                return;

            int i = this.mediaItemList != null ? this.mediaItemList.FindIndex(x => this.selectedMediaItemList.Contains(x)) : 0;

            foreach (MediaItem mItem in this.SelectedMediaItems.ToList())
                this.RemoveMediaItem(mItem);

            i = i < this.MediaItemList.Count ? i : this.MediaItemList.Count - 1;

            if (this.MediaItemList.Count > 0)
            {
                this.SelectedMediaItem = this.MediaItemList[i];
            }
        }

        public void RemoveFromDb()
        {
            if (this.selectedMediaItemList.Count == 0)
                return;

            MessageBoxResult result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, String.Format("Möchten Sie {0} von der Datenbank entfernen?\r\nEs werden dabei keine Dateien gelöscht.",
                this.selectedMediaItemList.Count > 1 ? this.selectedMediaItemList.Count + "Medien" : "ein Medium"),
                "Medien von Datenbank löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                MediaBrowserContext.RemoveFromDB(this.SelectedMediaItems);
                Mouse.OverrideCursor = null;
            }
        }

        public void CopyToClipboard()
        {
            System.Collections.Specialized.StringCollection paths = new System.Collections.Specialized.StringCollection();
            foreach (MediaItem mItem in this.SelectedMediaItems)
                paths.Add(mItem.FullName);

            if (paths.Count > 0)
            {
                MainWindow.GiveShortFeedback();
                Clipboard.SetFileDropList(paths);

                this.InfoText.Content = $"Zwischenablage: {paths.Count} Medien";
            }
        }

        public void CutToClipboard()
        {
            Clipboard.Clear();
            System.Collections.Specialized.StringCollection paths = new System.Collections.Specialized.StringCollection();
            foreach (MediaItem mItem in this.SelectedMediaItems)
            {
                //if (mItem.IsVirtual)
                paths.Add(mItem.FullName);
            }

            if (paths.Count > 0)
            {
                MainWindow.GiveShortFeedback();

                DataObject data = new DataObject();
                data.SetFileDropList(paths);

                byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
                MemoryStream dropEffect = new MemoryStream();
                dropEffect.Write(moveEffect, 0, moveEffect.Length);

                data.SetData("Preferred DropEffect", dropEffect);

                Clipboard.SetDataObject(data, true);
            }
        }

        public void ToggleBookmark()
        {
            if (this.SelectedMediaItem != null)
            {
                MainWindow.GiveShortFeedback();
                MediaBrowserContext.SetBookmark(this.SelectedMediaItems, !this.SelectedMediaItem.IsBookmarked);
            }
        }

        public void ToggleMarkDelete()
        {
            if (this.SelectedMediaItem != null)
            {
                MainWindow.GiveShortFeedback();
                MediaBrowserContext.SetDeleted(this.SelectedMediaItems, !this.SelectedMediaItem.IsDeleted);
            }
        }

        public void OpenWith()
        {
            if (this.SelectedMediaItem != null && this.SelectedMediaItem.FileObject.Exists)
            {
                MediaBrowserContext.SetTemporaryWriteProtection(this.SelectedMediaItem.FileObject.FullName);
                Rundll32.OpenAs(this.SelectedMediaItem.FileObject);
            }
        }

        private void MediaItemContainer_LayoutUpdated(object sender, EventArgs e)
        {
            if (this.MediaItemContainer.Items.Count == 0)
                return;

            MediaItem mItem = (MediaItem)this.MediaItemContainer.Items[this.MediaItemContainer.Items.Count - 1];
            if (this.lastMediaItemUpdatetd == mItem)
            {
                this.lastMediaItemUpdatetd = null;
                Mouse.OverrideCursor = null;
                this.IsEnabled = true;
            }
        }

        private void MediaItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
            {
                this.ShowMediaItem();
            }
        }

        private void viewer_OnMediaItemChanged(object sender, MediaItemArgument e)
        {
            this.SelectedMediaItem = e.MediaItem;
        }

        private bool noSelectionEvent = false;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalOffset > 0
                && this.MediaItemContainer.Items.Count < this.mediaItemList.Count - 1
                && e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 200)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.IsEnabled = false;

                //bool allSelected = this.MediaItemContainer.SelectedItems.Count == this.MediaItemContainer.Items.Count;

                this.noSelectionEvent = true;
                int maxLoadNew = ((int)(this.MediaItemContainer.Items.Count / maxLoad) + 1) * maxLoad;
                for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
                {
                    if (this.MediaItemContainer.Items.Count < maxLoadNew)
                    {
                        this.MediaItemContainer.Items.Add(this.mediaItemList[i]);
                        if (this.selectedMediaItemList.Contains(this.mediaItemList[i]))
                            (this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(this.MediaItemContainer.Items.Count - 1) as ListViewItem).IsSelected = true;

                    }
                }
                this.noSelectionEvent = false;
                //if (allSelected)
                //{
                //    this.MediaItemContainer.SelectAll();
                //}

                this.SetStatusMessage();

                this.lastMediaItemUpdatetd = (MediaItem)this.MediaItemContainer.Items[this.MediaItemContainer.Items.Count - 1];
            }
        }
        #endregion

        public void ShowAllMediaItems()
        {
            for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
            {
                this.MediaItemContainer.Items.Add(this.mediaItemList[i]);
            }
        }

        public void ShowInDbTree()
        {
            if (this.SelectedMediaItem != null)
            {
                if (this.OnShowInDbTree != null)
                    this.OnShowInDbTree.Invoke(this, new MediaItemArgument(this.SelectedMediaItem));
            }
        }

        public void SelectFolder()
        {
            List<int> folders = this.SelectedMediaItems.Select(x => x.FolderId).ToList();

            for (int i = 0; i < this.MediaItemContainer.Items.Count; i++)
            {
                ListViewItem lItem = this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                lItem.IsSelected = folders.Contains(this.mediaItemList[i].FolderId);
            }

            for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
            {
                if (folders.Contains(this.mediaItemList[i].FolderId))
                    this.selectedMediaItemList.Add(this.mediaItemList[i]);
                else
                    this.selectedMediaItemList.Remove(this.mediaItemList[i]);
            }

            this.SetStatusMessage2();
        }

        public void InvertSelection()
        {
            for (int i = 0; i < this.MediaItemContainer.Items.Count; i++)
            {
                ListViewItem lItem = this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                lItem.IsSelected = !lItem.IsSelected;
            }

            for (int i = this.MediaItemContainer.Items.Count; i < this.mediaItemList.Count; i++)
            {
                if (!this.selectedMediaItemList.Contains(this.mediaItemList[i]))
                    this.selectedMediaItemList.Add(this.mediaItemList[i]);
                else
                    this.selectedMediaItemList.Remove(this.mediaItemList[i]);
            }

            this.SetStatusMessage2();
        }

        public void ShowMediaItem()
        {
            if (this.SelectedMediaItem == null || MediaBrowserWPF.Dialogs.TagEditorSingleton.IsVisble(App.Current.MainWindow))
            {
                return;
            }

            Mouse.GetPosition(Application.Current.MainWindow);

            Mouse.OverrideCursor = Cursors.Wait;

            if (this.mediaViewer == null || !this.mediaViewer.IsLoaded)
            {
                if (!this.SelectedMediaItem.IsDeleted)
                {
                    this.mediaViewer = new MediaViewer(this.mediaItemList, this.SelectedMediaItem);
                    this.mediaViewer.OnMediaItemChanged += new EventHandler<MediaItemArgument>(viewer_OnMediaItemChanged);
                    this.mediaViewer.Topmost = true;
                    ((MediaViewer)this.mediaViewer).Closed += ThumblistContainer_Closed;

                    //ListViewItem item = MediaItemContainer.ItemContainerGenerator.ContainerFromItem(MediaItemContainer.SelectedItem) as ListViewItem;
                    //Point relativePoint = item == null ? new Point(0, 0) : item.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));
                    //this.mediaViewer.Width = System.Windows.SystemParameters.PrimaryScreenWidth / 2;
                    //this.mediaViewer.Height = System.Windows.SystemParameters.PrimaryScreenHeight / 2;
                    //this.mediaViewer.Top = Application.Current.MainWindow.Top - System.Windows.SystemParameters.PrimaryScreenHeight / 4 + relativePoint.Y;
                    //this.mediaViewer.Left = Application.Current.MainWindow.Left - System.Windows.SystemParameters.PrimaryScreenWidth / 4 + relativePoint.X;

                    System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(MainWindow.MainWindowStatic).Handle);
                    this.mediaViewer.Top = screen.Bounds.Y;
                    this.mediaViewer.Left = screen.Bounds.X;

                    this.mediaViewer.Show();
                }
            }
            else
            {
                if (!this.SelectedMediaItem.IsDeleted || this.mediaViewer.ShowDeleted)
                    this.mediaViewer.VisibleMediaItem = this.SelectedMediaItem;
            }

            Mouse.OverrideCursor = null;
        }

        void ThumblistContainer_Closed(object sender, EventArgs e)
        {
            if (SelectedMediaItem != null)
            {
                ListBoxItem listBoxItem = this.MediaItemContainer.ItemContainerGenerator.ContainerFromItem(SelectedMediaItem) as ListBoxItem;
                if (listBoxItem != null)
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        listBoxItem.Focus();
                    }), DispatcherPriority.ApplicationIdle);
            }
        }

        public void ShowSphere3D()
        {
            List<MediaItem> list = (this.selectedMediaItemList.Count > 1 ? this.selectedMediaItemList : this.mediaItemList);
            SphereViewer sphere3d = new SphereViewer(list, this.SelectedMediaItem);
            sphere3d.Topmost = true;

            ListViewItem item = MediaItemContainer.ItemContainerGenerator.ContainerFromItem(MediaItemContainer.SelectedItem) as ListViewItem;
            Point relativePoint = item == null ? new Point(0, 0) : item.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));

            sphere3d.Width = System.Windows.SystemParameters.PrimaryScreenWidth / 2;
            sphere3d.Height = System.Windows.SystemParameters.PrimaryScreenHeight / 2;
            sphere3d.Top = Application.Current.MainWindow.Top - System.Windows.SystemParameters.PrimaryScreenHeight / 4 + relativePoint.Y;
            sphere3d.Left = Application.Current.MainWindow.Left - System.Windows.SystemParameters.PrimaryScreenWidth / 4 + relativePoint.X;

            sphere3d.Show();
        }

        public void ShowCube3D()
        {
            List<MediaItem> list = (this.selectedMediaItemList.Count > 1 ? this.selectedMediaItemList : this.mediaItemList);
            Cube3D cube3d = Cube3D.Cube3DFactory(list, Cube3D.Projection.BITMAP);
            cube3d.Topmost = true;

            ListViewItem item = MediaItemContainer.ItemContainerGenerator.ContainerFromItem(MediaItemContainer.SelectedItem) as ListViewItem;
            Point relativePoint = item == null ? new Point(0, 0) : item.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));

            cube3d.Width = System.Windows.SystemParameters.PrimaryScreenWidth / 2;
            cube3d.Height = System.Windows.SystemParameters.PrimaryScreenHeight / 2;
            cube3d.Top = Application.Current.MainWindow.Top - System.Windows.SystemParameters.PrimaryScreenHeight / 4 + relativePoint.Y;
            cube3d.Left = Application.Current.MainWindow.Left - System.Windows.SystemParameters.PrimaryScreenWidth / 4 + relativePoint.X;

            cube3d.Show();
        }

        public void ShowMultiplayer()
        {
            ShowMultiplayer(-1, -1);
        }

        public void ShowMultiplayer(int colCount, int rowCount)
        {
            if (this.SelectedMediaItem == null)
                return;

            Mouse.OverrideCursor = Cursors.Wait;

            if (this.mediaViewer == null || !this.mediaViewer.IsLoaded)
            {
                if ((this.selectedMediaItemList.Count > 1 && this.selectedMediaItemList.Count(x => !x.IsDeleted) > 1)
                    || (this.selectedMediaItemList.Count == 1 && this.mediaItemList.Count(x => !x.IsDeleted) > 1))
                {
                    ListViewItem item = MediaItemContainer.ItemContainerGenerator.ContainerFromItem(MediaItemContainer.SelectedItem) as ListViewItem;
                    System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(MainWindow.MainWindowStatic).Handle);

                    Multiplayer multiplayer = new Multiplayer(this.selectedMediaItemList.Count == 1 ? this.mediaItemList : this.selectedMediaItemList, this.SelectedMediaItem, screen, colCount, rowCount);

                    if (multiplayer.IsValid)
                    {
                        this.mediaViewer = multiplayer;
                        this.mediaViewer.OnMediaItemChanged += new EventHandler<MediaItemArgument>(viewer_OnMediaItemChanged);
                        this.mediaViewer.Topmost = true;

                        this.mediaViewer.Top = screen.Bounds.Y;
                        this.mediaViewer.Left = screen.Bounds.X;

                        this.mediaViewer.Show();
                    }
                }
            }
            else
            {
                if (!this.SelectedMediaItem.IsDeleted || this.mediaViewer.ShowDeleted)
                    this.mediaViewer.VisibleMediaItem = this.SelectedMediaItem;
            }

            Mouse.OverrideCursor = null;
        }

        private void MediaItemContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //dragStartPoint = e.GetPosition(null);
        }

        //Point dragStartPoint;
        private void MediaItemContainer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            //if (!(Mouse.DirectlyOver.GetType().Equals(typeof(System.Windows.Controls.Image))
            //    || Mouse.DirectlyOver.GetType().Equals(typeof(System.Windows.Controls.Grid))))
            //    return;

            //if (e.LeftButton == MouseButtonState.Pressed)
            //{
            //    Vector diff = dragStartPoint - e.GetPosition(null);

            //    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            //        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            //    {

            //        if (Keyboard.Modifiers == ModifierKeys.Control)
            //        {
            //            string[] pathList = this.SelectedMediaItems.Where(x => !x.IsFileNotFound).Select(x => x.FullName).ToArray();

            //            if (pathList.Length > 0)
            //            {
            //                System.Windows.Forms.DataObject dataObj = new System.Windows.Forms.DataObject();
            //                dataObj.SetData(DataFormats.FileDrop, pathList);
            //                dataObj.SetData("Preferred DropEffect", DragDropEffects.Copy);
            //                dataObj.SetData("InShellDragLoop", 1);
            //                DragDrop.DoDragDrop(this.MediaItemContainer, dataObj, DragDropEffects.Copy);
            //            }
            //        }
            //        else if (this.SelectedMediaItems.Count > 0)
            //        {
            //            System.Windows.Forms.DataObject dataObj = new System.Windows.Forms.DataObject();
            //            dataObj.SetData(this.SelectedMediaItems.Select(x => x.Id).ToArray());
            //            DragDrop.DoDragDrop(this.MediaItemContainer, dataObj, DragDropEffects.Move);
            //        }
            //    }
            //}
        }

        //Verhindert beim scrollen ständig einen neuen Tooltip
        Point mousePosition = new Point();
        private void Thumbnail_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (mousePosition != Mouse.GetPosition(this))
            {
                mousePosition = Mouse.GetPosition(this);
            }
            else
            {
                e.Handled = true;
            }
        }

        private void MediaItemContainer_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!TagEditorSingleton.IsVisble(App.Current.MainWindow))
                this.MediaItemContainer.Focus();

            //if (this.MediaItemContainer.SelectedIndex > -1)
            //    ((ListViewItem)this.MediaItemContainer.ItemContainerGenerator.ContainerFromIndex(this.MediaItemContainer.SelectedIndex)).Focus();

            //ListViewItem lv = this.MediaItemContainer.ItemContainerGenerator.ContainerFromItem(this.SelectedMediaItem) as ListViewItem;

            //if (lv != null && !lv.IsFocused)
            //    lv.Focus();
        }
    }
}
