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
using MediaBrowser4.Objects;
using System.IO;
using MediaBrowserWPF.UserControls;
using MediaBrowser4;
using MediaBrowserWPF.Utilities;
using MediaBrowserWPF.Dialogs;
using System.Threading;
using System.Globalization;
using System.Windows.Threading;
using MediaBrowser4.Utilities;
using MediaProcessing.FaceDetection;
using MapControl;

namespace MediaBrowserWPF
{
    public partial class MainWindow : Window
    {
        public static MainWindow MainWindowStatic;
        private const int InitalRequestTab = 0;
        private Dictionary<MediaItemRequest, ThumblistContainerTabItem> tabItemDictionary = new Dictionary<MediaItemRequest, ThumblistContainerTabItem>();
        private DispatcherTimer shortFeedbackTimer;

        public static bool BussyIndicatorIsBusy
        {
            set
            {
                MainWindowStatic.BussyIndicator.IsBusy = value;
            }

            get
            {
                return MainWindowStatic.BussyIndicator.IsBusy;
            }
        }

        public static string BussyIndicatorContent
        {
            set
            {
                MainWindowStatic.BussyIndicator.BusyContent = value;
            }
        }

        public static int BussyIndicatorDisplayAfterSeconds
        {
            set
            {
                MainWindowStatic.BussyIndicator.DisplayAfter = new TimeSpan(0, 0, value);
            }
        }

        public static ThumblistContainerTabItem SelectedThumblistContainerTabItem
        {
            get
            {
                return MainWindowStatic.ThumbListTabControl.SelectedItem as ThumblistContainerTabItem;
            }
        }

        public static void RescanFolder(string selectedPath)
        {
            MainWindowStatic.ScanRecursive(selectedPath);
        }

        public MainWindow()
        {
            InitializeComponent();

            MainWindowStatic = this;

            if (MediaBrowser4.MediaBrowserContext.MainDBProvider != null)
            {
                this.SetDatabaseMenu(MediaBrowser4.MediaBrowserContext.MainDBProvider.DBName);
                this.StatusBarTextblock.Text = MediaBrowser4.MediaBrowserContext.MainDBProvider.DBName;
            }

            this.shortFeedbackTimer = new DispatcherTimer();
            this.shortFeedbackTimer.IsEnabled = false;
            this.shortFeedbackTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            this.shortFeedbackTimer.Tick += new EventHandler(shortFeedbackTimer_Tick);

            MediaBrowser4.MediaBrowserContext.OnInsert += new EventHandler<MediaItemCallbackArgs>(MediaBrowserContext_OnInsert);
            this.DataBaseFolderTree.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.DataBaseFolderTree.DeleteFolder += new EventHandler<UserControls.FolderContainer.FolderEventArgs>(DataBaseFolderTree_DeleteFolder);
            this.DataBaseFolderTree.RenameFolder += new EventHandler<UserControls.FolderContainer.FolderEventArgs>(DataBaseFolderTree_RenameFolder);
            this.CategoryTree.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.CategoryTree.DeleteCategory += new EventHandler<UserControls.CategoryContainer.CategoryEventArgs>(CategoryTree_DeleteCategory);
            this.CategoryTree.RenameCategory += new EventHandler<UserControls.CategoryContainer.CategoryEventArgs>(CategoryTree_RenameCategory);
            this.SearchContainer.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.RequestFavorites.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.AttachmentsContainer.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.BookmarkedListContainer.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.DeletedListContainer.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            MapSearchWindow.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
            this.TabControlControls.SelectedIndex = InitalRequestTab;

            this.Title = "MediaBrowserWPF " + System.Reflection.Assembly.GetExecutingAssembly()
                                           .GetName()
                                           .Version
                                           .ToString();

        }

        public static void GiveShortFeedback()
        {
            MainWindowStatic.ShortFeedback();
        }

        public void ShortFeedback()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.shortFeedbackTimer.Start();
        }

        void shortFeedbackTimer_Tick(object sender, EventArgs e)
        {
            Mouse.OverrideCursor = null;
            this.shortFeedbackTimer.Stop();
        }

        public static void RefreshThumblistContainersByFolder(Folder folder)
        {
            foreach (ThumblistContainerTabItem thumblistContainerTabItem in MainWindowStatic.ThumbListTabControl.Items)
            {
                thumblistContainerTabItem.RefreshByFolder(folder);
            }
        }

        public static void RefreshCategoryTree()
        {
            MainWindowStatic.CategoryTree.Build();
        }

        public static void RemoveFromFolderInThumblistContainers(MediaItem mItem)
        {
            foreach (ThumblistContainerTabItem thumblistContainerTabItem in MainWindowStatic.ThumbListTabControl.Items)
            {
                thumblistContainerTabItem.RemoveFromFolder(mItem);
            }
        }

        void CategoryTree_RenameCategory(object sender, UserControls.CategoryContainer.CategoryEventArgs e)
        {
            foreach (KeyValuePair<MediaItemRequest, ThumblistContainerTabItem> kv in this.tabItemDictionary)
            {
                MediaItemCategoryRequest categoryRequest = kv.Key as MediaItemCategoryRequest;
                if (categoryRequest != null)
                {
                    kv.Value.Header = kv.Key.Header;
                    kv.Value.ToolTip = kv.Key.Description;
                }
            }
        }

        void CategoryTree_DeleteCategory(object sender, UserControls.CategoryContainer.CategoryEventArgs e)
        {
            foreach (KeyValuePair<MediaItemRequest, ThumblistContainerTabItem> kv in this.tabItemDictionary)
            {
                MediaItemCategoryRequest categoryRequest = kv.Key as MediaItemCategoryRequest;
                if (categoryRequest != null)
                {
                    categoryRequest.RemoveCategory(e.Category);
                    kv.Value.Header = kv.Key.Header;
                    kv.Value.ToolTip = kv.Key.Description;

                    if (!categoryRequest.IsValid)
                        this.CloseTab(kv.Value);
                }
            }
        }

        void DataBaseFolderTree_RenameFolder(object sender, UserControls.FolderContainer.FolderEventArgs e)
        {
            foreach (KeyValuePair<MediaItemRequest, ThumblistContainerTabItem> kv in this.tabItemDictionary)
            {
                MediaItemFolderRequest folderRequest = kv.Key as MediaItemFolderRequest;
                if (folderRequest != null)
                {
                    kv.Value.Header = kv.Key.Header;
                    kv.Value.ToolTip = kv.Key.Description;
                }
            }
        }

        void DataBaseFolderTree_DeleteFolder(object sender, UserControls.FolderContainer.FolderEventArgs e)
        {
            foreach (KeyValuePair<MediaItemRequest, ThumblistContainerTabItem> kv in this.tabItemDictionary)
            {
                MediaItemFolderRequest folderRequest = kv.Key as MediaItemFolderRequest;
                if (folderRequest != null)
                {
                    folderRequest.RemoveFolder(e.Folder);
                    kv.Value.Header = kv.Key.Header;
                    kv.Value.ToolTip = kv.Key.Description;

                    if (!folderRequest.IsValid)
                        this.CloseTab(kv.Value);
                }
            }
        }

        public void ShowSearchTab()
        {
            this.TabControlControls.SelectedItem = this.SearchTab;
        }

        public string InitFolder { get; set; }
        public string InitPath { get; set; }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.InitFolder != null)
            {
                MediaItemFolderRequest folderRequest = new MediaItemFolderRequest();

                Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(this.InitFolder);

                if (folder == null)
                {
                    folder = new Folder();
                    folder.Name = this.InitFolder;
                    MediaBrowserContext.SetFolder(folder);
                    MediaBrowserContext.ResetContext();
                    folder = MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(this.InitFolder);
                }

                if (folder != null)
                {
                    folderRequest.AddFolder(folder);
                    folderRequest.RequestType = MediaItemRequestType.SINGLE;
                    this.AddRequest(folderRequest);
                    this.TabControlControls.SelectedItem = this.FolderTab;

                    if (MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE)
                    {
                        refreshTabItem = this.tabItemDictionary[folderRequest];
                        this.RescanRequest();
                        ScanRecursive(this.InitFolder);
                    }
                }
            }
            else if (this.InitPath != null)
            {
                this.LoadVirtual(Directory.GetFiles(this.InitPath, "*", SearchOption.AllDirectories));
            }

            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(MainWindowStatic).Handle);
            if (System.Net.Dns.GetHostName() == "BARATHEON" || System.Net.Dns.GetHostName() == "GRAUFREUD")
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                // this.Title = screen.ToString();
                if (screen.Bounds.Width < 1200 || screen.Bounds.Height < 900)
                {
                    this.WindowState = WindowState.Maximized;
                }
                else if (screen.Bounds.Width > 2500 || screen.Bounds.Height > 1200)
                {
                    this.Width = 1900;
                    this.Height = 1030;

                }
                else
                {
                    this.Width = screen.Bounds.Width * .7;
                    this.Height = screen.Bounds.Height * .7;
                    this.Top = screen.WorkingArea.Y + screen.Bounds.Height * .15;
                    this.Left = screen.WorkingArea.X + screen.Bounds.Width * .15;
                }
            }
        }

        void OnMediaItemRequest(object sender, MediaItemRequestMessageArgs e)
        {
            this.AddRequest(e.Request);
        }

        private void AddRequest(MediaItemRequest request)
        {
            ThumblistContainer thumbListContainer;

            if (!this.tabItemDictionary.ContainsKey(request))
            {
                thumbListContainer = new ThumblistContainer();
                thumbListContainer.OnRequest += new EventHandler<MediaItemRequestMessageArgs>(OnMediaItemRequest);
                thumbListContainer.OnShowInDbTree += new EventHandler<MediaItemArgument>(thumbListContainer_OnShowInDbTree);

                ThumblistContainerTabItem tabItem = new ThumblistContainerTabItem(thumbListContainer, request);
                tabItem.OnOpenRequestWindow += new EventHandler(tabItem_OnOpenRequestWindow);
                tabItem.OnCloseAll += new EventHandler(tabItem_OnCloseAll);
                tabItem.OnCloseThis += new EventHandler(tabItem_OnCloseThis);
                tabItem.OnSaveRequest += new EventHandler(tabItem_OnSaveRequest);
                tabItem.PreviewKeyDown += new KeyEventHandler(tabItem_PreviewKeyDown);
                tabItem.OnSelectedMediaItemsChanged += new EventHandler<MediaItemArgument>(tabItem_OnSelectedMediaItemsChanged);
                tabItem.IsSelected = true;

                this.ThumbListTabControl.Items.Add(tabItem);

                this.tabItemDictionary.Add(request, tabItem);
            }
            else
            {
                this.tabItemDictionary[request].IsSelected = true;
                this.tabItemDictionary[request].Header = request.Header;
                this.tabItemDictionary[request].ToolTip = request.Description;

                thumbListContainer = (ThumblistContainer)this.tabItemDictionary[request].Content;
            }

            thumbListContainer.Request = request;
        }

        void thumbListContainer_OnShowInDbTree(object sender, MediaItemArgument e)
        {
            this.TabControlControls.SelectedItem = this.FolderTab;
            this.DataBaseFolderTree.CollapseAll();
            this.DataBaseFolderTree.ExpandPath(e.MediaItem.Foldername);

            MediaItemFolderRequest folderRequest = new MediaItemFolderRequest();

            folderRequest.AddFolder(e.MediaItem.Folder);
            folderRequest.RequestType = MediaItemRequestType.SINGLE;
            folderRequest.SelectedMediaItem = e.MediaItem;

            this.OnMediaItemRequest(this.DataBaseFolderTree, new MediaItemRequestMessageArgs(folderRequest));
        }

        void tabItem_OnSaveRequest(object sender, EventArgs e)
        {
            if (sender is ThumblistContainerTabItem)
            {
                this.RequestFavorites.SaveRequest(((ThumblistContainerTabItem)sender).Request);
            }
        }

        ThumblistContainerTabItem refreshTabItem;
        void tabItem_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                refreshTabItem = sender as ThumblistContainerTabItem;

                if (refreshTabItem != null)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (refreshTabItem.Request is MediaItemFolderRequest)
                            this.RescanRequest();
                    }
                    else
                    {
                        refreshTabItem.ThumbListContainer.Refresh();
                        refreshTabItem = null;
                    }
                }
            }
        }

        public static void RescanRequest(ThumblistContainerTabItem tabItem)
        {
            MainWindowStatic.refreshTabItem = tabItem;
            MainWindowStatic.RescanRequest();
        }

        private void RescanRequest()
        {
            if (refreshTabItem.Request is MediaItemVirtualRequest)
                return;

            List<string> fileList = new List<string>();
            List<string> oldList = refreshTabItem.MediaItems.Select(x => x.FullName).ToList();

            if (refreshTabItem.Request is MediaItemFolderRequest)
            {
                foreach (Folder folder in ((MediaItemFolderRequest)refreshTabItem.Request).Folders)
                {
                    if (Directory.Exists(folder.FullPath))
                        foreach (string file in System.IO.Directory.GetFiles(folder.FullPath, "*.*", ((MediaItemFolderRequest)refreshTabItem.Request).RequestType == MediaItemRequestType.RECURSIVE
                            ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                        {
                            if (!oldList.Contains(file))
                                fileList.Add(file);
                        }
                }
            }
            else
            {
                foreach (string path in refreshTabItem.MediaItems.Select(x => x.Foldername).Distinct())
                    foreach (string file in System.IO.Directory.GetFiles(path.EndsWith(":") ? path + "\\" : path))
                    {
                        if (!oldList.Contains(file))
                            fileList.Add(file);
                    }
            }

            MainWindow.BussyIndicatorContent = "Importiere ...";
            MainWindow.BussyIndicatorIsBusy = true;

            this.newMediaItemList = new List<MediaItem>();

            if (MediaBrowser4.MediaBrowserContext.InsertMediaItems(fileList).Count == 0)
            {
                MainWindow.BussyIndicatorIsBusy = false;
                MainWindow.GiveShortFeedback();
            }
        }

        void tabItem_OnOpenRequestWindow(object sender, EventArgs e)
        {
            ThumblistContainerTabItem tabItem = sender as ThumblistContainerTabItem;

            if (tabItem != null)
            {
                if (tabItem.Request is MediaItemSearchRequest)
                {
                    this.TabControlControls.SelectedItem = this.SearchTab;
                    this.SearchContainer.Set(tabItem.Request as MediaItemSearchRequest);
                }
                else if (tabItem.Request is MediaItemCategoryRequest)
                {
                    if (((MediaItemCategoryRequest)tabItem.Request).Categories.Length == 0)
                    {
                        this.TabControlControls.SelectedItem = this.RequestTab;
                    }
                    else
                    {
                        this.TabControlControls.SelectedItem = this.CategoryTab;
                        this.CategoryTree.Set(tabItem.Request as MediaItemCategoryRequest);
                    }
                }
                else if (tabItem.Request is MediaItemFolderRequest)
                {
                    this.TabControlControls.SelectedItem = this.FolderTab;
                    this.DataBaseFolderTree.Set(tabItem.Request as MediaItemFolderRequest);
                }

                if (!(tabItem.Request is MediaItemSearchRequest))
                {
                    this.SearchContainer.SetSearchToken(tabItem.Request);
                }
            }
        }

        void tabItem_OnCloseThis(object sender, EventArgs e)
        {
            this.CloseTab(sender);
        }

        private void CloseTab(object sender)
        {
            if (this.ThumbListTabControl.Items.Contains(sender))
            {
                this.ThumbListTabControl.Items.Remove(sender);
                this.tabItemDictionary.Remove(((ThumblistContainerTabItem)sender).Request);

                if (((ThumblistContainerTabItem)sender).Request is MediaItemCategoryRequest)
                {
                    this.CategoryTree.ResetCalendarCategory();
                }
            }
        }

        void tabItem_OnCloseAll(object sender, EventArgs e)
        {
            foreach (ThumblistContainerTabItem item in (from object item in this.ThumbListTabControl.Items
                                                        where item != sender
                                                        select item).ToArray())
            {
                this.ThumbListTabControl.Items.Remove(item);
                this.tabItemDictionary.Remove(item.Request);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.Source is TabControl))
                return;

            this.SelectedTabChanged((TabItem)((TabControl)sender).SelectedItem);
        }

        private void SelectedTabChanged(TabItem tabItem)
        {
            if (tabItem.Name == "CategoryTab" && !this.CategoryTree.IsBuild)
            {
                this.CategoryTree.Build();
            }
            else if (tabItem.Name == "FolderTab" && !this.DataBaseFolderTree.IsBuild)
            {
                this.DataBaseFolderTree.Build();
            }
            else if (tabItem.Name == "SearchTab" && !this.SearchContainer.IsBuild)
            {
                this.SearchContainer.Build();
            }
            else if (tabItem.Name == "RequestTab" && !this.RequestFavorites.IsBuild)
            {
                this.RequestFavorites.Build();
            }
            else if (tabItem.Name == "MediaItemInfoTab")
            {
                if (this.ThumbListTabControl.SelectedItem != null)
                    this.MediaItemInfoContainer.SetInfo(((ThumblistContainerTabItem)this.ThumbListTabControl.SelectedItem).SelectedMediaItems);
            }
            else if (tabItem.Name == "BookmarkedTab" && !this.BookmarkedListContainer.IsBuild)
            {
                this.BookmarkedListContainer.Build(MediaBrowserContext.BookmarkedSingleton, "Lesezeichen", "Liste aller markierten Medien");
            }
            else if (tabItem.Name == "DeletedTab" && !this.DeletedListContainer.IsBuild)
            {
                this.DeletedListContainer.Build(MediaBrowserContext.DeletedSingleton, "Gelöscht", "Liste aller als gelöscht markierten Medien");
            }
            else if (tabItem.Name == "AttachmentsTab")
            {
                if (this.ThumbListTabControl.SelectedItem != null)
                    this.AttachmentsContainer.SetInfo(((ThumblistContainerTabItem)this.ThumbListTabControl.SelectedItem).SelectedMediaItems);
            }
        }

        private void ThumbListTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ThumbListTabControl.SelectedItem == null)
                return;

            if (((TabItem)TabControlControls.SelectedItem).Name == "MediaItemInfoTab")
            {
                this.MediaItemInfoContainer.SetInfo(((ThumblistContainerTabItem)this.ThumbListTabControl.SelectedItem).SelectedMediaItems);
                e.Handled = true;
            }
            else if (((TabItem)TabControlControls.SelectedItem).Name == "AttachmentsTab")
            {
                this.AttachmentsContainer.SetInfo(((ThumblistContainerTabItem)this.ThumbListTabControl.SelectedItem).SelectedMediaItems);
                e.Handled = true;
            }
        }

        void tabItem_OnSelectedMediaItemsChanged(object sender, MediaItemArgument e)
        {
            if (((TabItem)TabControlControls.SelectedItem).Name == "MediaItemInfoTab")
            {
                this.MediaItemInfoContainer.SetInfo(e.MediaItems);
            }
            else if (((TabItem)TabControlControls.SelectedItem).Name == "AttachmentsTab")
            {
                this.AttachmentsContainer.SetInfo(((ThumblistContainerTabItem)this.ThumbListTabControl.SelectedItem).SelectedMediaItems);
            }
        }

        private void GridSplitter_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeWE;
        }

        private void GridSplitter_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void SetDatabaseMenu(string path)
        {
            this.DatabasesInFolder.Items.Clear();
            string extension = System.IO.Path.GetExtension(path);

            AddDbPath(MediaBrowserContext.MyDocumentsFolder, extension, path);
            AddDbPath(System.IO.Path.GetDirectoryName(path), extension, path);

            foreach (var drive in DriveInfo.GetDrives())
            {
                AddDbPath(System.IO.Path.Combine(drive.Name, "Fotos\\DB"), extension, path);
            }

            this.DatabasesInFolder.IsEnabled = this.DatabasesInFolder.Items.Count > 0;
        }

        private void AddDbPath(string path, string extension, string currentDB)
        {
            if (Directory.Exists(path))
                foreach (string name in Directory.GetFiles(path).Where(x => x.EndsWith(extension)))
                {
                    if (!this.DatabasesInFolder.Items.Cast<MenuItem>().Any(x => x.Header.ToString().Equals(System.IO.Path.Combine(path, name), StringComparison.InvariantCultureIgnoreCase)))
                    {
                        MenuItem menuItem = new MenuItem();
                        menuItem.IsEnabled = !name.Equals(currentDB, StringComparison.InvariantCultureIgnoreCase);
                        menuItem.Header = System.IO.Path.Combine(path, name);
                        menuItem.Click += new System.Windows.RoutedEventHandler(menuItemDatabaseOpen_Click);
                        this.DatabasesInFolder.Items.Add(menuItem);
                    }
                }
        }

        void menuItemDatabaseOpen_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.SetDbPath(((MenuItem)sender).Header.ToString());
        }

        private void NewDatabase()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "Mediabrowser4 DB (*.mb4)|*.mb4";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = false;
            openFileDialog.FileName = "MediaBrowser4DB.mb4";
            openFileDialog.InitialDirectory = MediaBrowserContext.WorkingDirectory;

            if (MediaBrowserWPF.Properties.Settings.Default.DBPath.Trim().Length > 0 &&
                System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(MediaBrowserWPF.Properties.Settings.Default.DBPath)))
                openFileDialog.InitialDirectory =
                    System.IO.Path.GetDirectoryName(MediaBrowserWPF.Properties.Settings.Default.DBPath);

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    this.SetDbPath(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(MainWindow.MainWindowStatic, ex.Message);
                }

                Mouse.OverrideCursor = null;
            }
        }

        private void SetDbPath(string filepath)
        {
            if ((MediaBrowserContext.DBPath == null || !MediaBrowserContext.DBPath.Equals(filepath, StringComparison.InvariantCultureIgnoreCase))
                && MediaBrowserContext.Init(filepath))
            {
                MediaBrowserWPF.Properties.Settings.Default.DBPath = filepath;

                this.ResetAll();
                this.SetDatabaseMenu(MediaBrowserWPF.Properties.Settings.Default.DBPath);
                this.StatusBarTextblock.Text = MediaBrowser4.MediaBrowserContext.MainDBProvider.DBName;
            }
        }

        private void ResetAll()
        {
            this.ThumbListTabControl.Items.Clear();
            this.tabItemDictionary.Clear();

            MediaBrowserContext.ResetContext();

            this.CategoryTree.Clear();
            this.DataBaseFolderTree.Clear();
            this.SearchContainer.Clear();
            this.RequestFavorites.Clear();
            this.BookmarkedListContainer.Clear();
            this.DeletedListContainer.Clear();
            this.MediaItemInfoContainer.Clear();
            this.AttachmentsContainer.Clear();

            this.TabControlControls.SelectedIndex = InitalRequestTab;
            this.SelectedTabChanged((TabItem)((TabControl)this.TabControlControls).SelectedItem);
        }

        private void MenuItem_Click_NewDatabase(object sender, RoutedEventArgs e)
        {
            this.NewDatabase();
        }

        private void RemoveMediaItemsFinally()
        {
            List<MediaItem> mediaItems = MediaBrowserContext.GetDeletedMediaItems();

            if (mediaItems == null || mediaItems.Count == 0)
            {
                ShortFeedback();
                return;
            }

            if (MessageBoxResult.Yes == MessageBox.Show(MainWindow.MainWindowStatic, "Wollen sie " + mediaItems.Count
                 + " Medien irreversibel aus der Datenbank löschen\r\nund die zugehörigen Dateien in den Windows Papierkorb schieben?", "Löschen von Dateien bestätigen",
                 MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No))
            {
                List<MediaItem> removedItems = new List<MediaItem>();
                List<Exception> exceptions = new List<Exception>();

                MainWindow.BussyIndicatorContent = "Lösche Dateien ...";
                MainWindow.BussyIndicatorIsBusy = true;

                Thread thread = new Thread(() =>
                {
                    int cnt = 0;
                    foreach (MediaItem mItem in mediaItems)
                    {
                        cnt++;
                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                        {
                            MainWindow.BussyIndicatorContent = String.Format("Lösche {0} / {1}", cnt, mediaItems.Count);
                        }));

                        Exception ex = MediaBrowserContext.RemoveAndRecycle(mItem);

                        if (ex == null)
                        {
                            removedItems.Add(mItem);
                        }
                        else
                        {
                            exceptions.Add(ex);
                        }
                    }

                    Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                    {
                        MainWindow.BussyIndicatorIsBusy = false;

                        if (exceptions.Count > 0)
                        {
                            MessageBox.Show(MainWindow.MainWindowStatic, exceptions.Count + " Medien konnten nicht gelöscht werden:\r\n"
                                + String.Join("\r\n", exceptions.Select(x => x.Message).Distinct()));
                        }
                    }));
                });

                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        private void MenuItemRemoveDeleted_Click(object sender, RoutedEventArgs e)
        {
            this.RemoveMediaItemsFinally();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.Shutdown();
                    break;

                case Key.Q:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.UseCopyTool();
                    }
                    else
                    {
                        if (((TabItem)TabControlControls.SelectedItem).Name == "FolderTab")
                        {
                            this.DataBaseFolderTree.ToggleCollaps();
                        }
                        else if (((TabItem)TabControlControls.SelectedItem).Name == "CategoryTab")
                        {
                            this.CategoryTree.ToggleCollaps();
                        }
                    }
                    break;

                case Key.Delete:
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        this.RemoveMediaItemsFinally();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.refreshTabItem = null;
                        MediaBrowserWPF.Utilities.FilesAndFolders.AddCopyMoveFromClipboard(null);
                        e.Handled = true;
                    }
                    break;

                case Key.G:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        MapControl.MapSearchWindow sc = new MapControl.MapSearchWindow();
                        sc.Owner = MainWindow.MainWindowStatic;
                        sc.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        sc.ShowDialog();
                    }
                    break;

            }
        }

        private void MenuItemAddFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            this.refreshTabItem = null;
            MediaBrowserWPF.Utilities.FilesAndFolders.AddCopyMoveFromClipboard(null);
        }

        private void MenuItemOpen_Click(object sender, RoutedEventArgs e)
        {
            ((MediaItemListContainer)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget).OpenList();
        }

        private void MenuItemAddSingle_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "Medien (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = false;
            openFileDialog.Multiselect = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = MediaBrowserContext.WorkingDirectory;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.refreshTabItem = null;
                AddNewMediaFiles(new List<string>(openFileDialog.FileNames));
            }
        }

        public static void AddNewMediaFiles(List<string> fileList)
        {
            MainWindow.BussyIndicatorContent = "Importiere ...";
            MainWindow.BussyIndicatorIsBusy = true;

            MainWindowStatic.newMediaItemList = new List<MediaItem>();

            if (MediaBrowser4.MediaBrowserContext.InsertMediaItems(fileList).Count == 0)
            {
                MainWindow.BussyIndicatorIsBusy = false;
            }
        }

        List<MediaItem> newMediaItemList;
        private void MenuItemAddDirectory_Click(object sender, RoutedEventArgs e)
        {
            this.refreshTabItem = null;
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = MediaBrowserContext.WorkingDirectory;

            if (openFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ScanRecursive(openFolderDialog.SelectedPath);
            }
        }

        private void ScanRecursive(string selectedPath)
        {
            MainWindow.BussyIndicatorContent = "Importiere ...";
            MainWindow.BussyIndicatorIsBusy = true;

            this.newMediaItemList = new List<MediaItem>();
            List<string> fileList = new List<string>();
            scan(selectedPath, fileList);

            if (MediaBrowser4.MediaBrowserContext.InsertMediaItems(fileList).Count == 0)
            {
                MainWindow.BussyIndicatorIsBusy = false;
            }
        }

        private void MenuItemVorschauDB_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = Directory.Exists(MediaBrowserWPF.Utilities.FilesAndFolders.DesktopPreviewDbFolder) ? MediaBrowserWPF.Utilities.FilesAndFolders.DesktopPreviewDbFolder : MediaBrowserContext.WorkingDirectory;

            if (openFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MainWindow.BussyIndicatorContent = "Importiere ...";
                MainWindow.BussyIndicatorIsBusy = true;

                Thread thread = new Thread(() =>
                {
                    MainWindow.CreateContactprintFromThumbs(openFolderDialog.SelectedPath);
                    List<string> fileList = new List<string>(System.IO.Directory.GetFiles(openFolderDialog.SelectedPath, "*.prv"));
                    MediaBrowserContext.WriteToPreviewDB(fileList);
                });

                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }


        /// <summary>
        /// Erstellt aus im übergebenen Verzeichnis vorhandenen Einzelbildern einen Kontaktabzug. 
        /// Je einen pro Unterordner mit der Endung .prv. Es weden maximal 25 Einzelbilder vom Format .png zu einem 5x5-Kontaktabzug aggregiert.
        /// </summary>
        /// <param name="exportPath"></param>
        public static void CreateContactprintFromThumbs(string exportPath)
        {
            int cnt = 0;
            String[] directories = System.IO.Directory.GetDirectories(exportPath, "*.prv");
            foreach (string directory in directories)
            {
                System.Drawing.Bitmap newBmp = null;
                System.Drawing.Graphics newBmpGraphics = null;
                int col = 0;
                int row = 0;

                if (System.IO.Directory.GetFiles(directory, "*.png").Length >= 24)
                    foreach (string file in System.IO.Directory.GetFiles(directory, "*.png"))
                    {
                        using (System.Drawing.Image thumbImg = System.Drawing.Image.FromFile(file))
                        {
                            if (newBmp == null)
                            {
                                newBmp = new System.Drawing.Bitmap(thumbImg.Width * 5, thumbImg.Height * 5, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                                newBmpGraphics = System.Drawing.Graphics.FromImage(newBmp);
                            }

                            newBmpGraphics.DrawImage(thumbImg, new System.Drawing.Point(thumbImg.Width * col, thumbImg.Height * row));

                            col++;

                            if (col == 5)
                            {
                                col = 0;
                                row++;
                            }
                        }
                    }

                if (newBmp != null)
                {
                    MediaProcessing.EncodeImage.SaveJPGFile(newBmp, exportPath + "\\" + System.IO.Path.GetFileName(directory).Replace(".avi.", ".jpg."), 100);
                }

                System.IO.Directory.Delete(directory, true);

                newBmp = null;
                cnt++;
            }
        }

        private void MenuItemVirtuell_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "Medien (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = false;
            openFileDialog.Multiselect = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = MediaBrowserContext.WorkingDirectory;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.LoadVirtual(openFileDialog.FileNames);
            }
        }

        private void LoadVirtual(string[] filelist)
        {
            KeyValuePair<MediaItemRequest, ThumblistContainerTabItem>
                   tabItem = tabItemDictionary.FirstOrDefault(x => x.Key is MediaItemVirtualRequest);

            if (tabItem.Key != null)
            {
                MediaItemVirtualRequest request = tabItem.Key as MediaItemVirtualRequest;

                foreach (string path in filelist)
                    if (!request.FileList.Contains(path))
                        request.FileList.Add(path);

                this.AddRequest(request);
            }
            else
            {
                this.AddRequest(new MediaItemVirtualRequest(new List<string>(filelist)));
            }
        }

        void MediaBrowserContext_OnInsert(object sender, MediaItemCallbackArgs e)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                MainWindow.BussyIndicatorContent = "Importiere " + e.Pos + " von " + e.MaxCount + " Dateien ...";

                if (e.MediaItem != null && !this.newMediaItemList.Contains(e.MediaItem))
                    this.newMediaItemList.Add(e.MediaItem);

                if (e.UpdateDone)
                {
                    if (e.MediaItem != null)
                    {
                        if (e.FolderAdded)
                        {
                            MediaBrowserContext.ResetFolderTree();
                            this.DataBaseFolderTree.Build(false);
                            this.DataBaseFolderTree.ExpandPath(e.MediaItem.FileObject.DirectoryName);
                        }

                        if (MediaBrowserContext.AutoCategorizeDate)
                        {
                            try
                            {
                                MainWindow.BussyIndicatorContent = "Erstelle Kategorien ...";
                                List<Category> categoryList = MediaBrowserContext.CategoryTreeSingelton.CategorizeByExifDate(
                                     this.newMediaItemList.Where(x => !x.Filename.Contains("_panorama")).ToList()
                                     );

                                if (categoryList.Count > 0)
                                {
                                    MediaBrowserContext.ResetCategoryTree();
                                    this.CategoryTree.Build(false);
                                    this.CategoryTree.ExpandPath(categoryList[0].FullPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Exception(ex);
                                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, ex.Message,
                                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            MediaBrowserContext.AutoCategorizeDate = oldAutoCategorize;
                        }

                        if (refreshTabItem == null)
                        {
                            this.DataBaseFolderTree.SetRequestFromList(this.newMediaItemList);
                            this.TabControlControls.SelectedItem = this.FolderTab;
                        }
                        else
                        {
                            refreshTabItem.ThumbListContainer.Refresh();
                            refreshTabItem = null;
                        }
                    }

                    MainWindow.BussyIndicatorIsBusy = false;

                    if (this.newMediaItemList != null)
                        this.newMediaItemList.Clear();
                }
            }));
        }

        private static void scan(string path, List<string> fileList)
        {
            List<string> dirs = new List<string>();
            foreach (string dir in System.IO.Directory.GetDirectories(path))
            {
                try
                {
                    scan(dir, fileList);
                }
                catch (Exception ex)
                {
                    dirs.Add(dir + $" ({ex.GetType().Name})");
                }
            }

            foreach (string file in System.IO.Directory.GetFiles(path))
            {
                try
                {
                    fileList.Add(file);
                }
                catch
                {

                }
            }

            if (dirs.Count > 0)
            {
                MessageBox.Show(String.Join(Environment.NewLine, dirs), "Fehler beim Einlesen einiger Verzeichnisse", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MenuItemBookmarkedListContainer_RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.SetBookmark(this.BookmarkedListContainer.SelectedMediaItems, false);
        }

        private void MenuItemBookmarkedListContainer_RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.SetBookmark(this.BookmarkedListContainer.MediaItems, false);
        }

        private void MenuItemCleanDB_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.BussyIndicatorIsBusy = true;
            MainWindow.BussyIndicatorContent = "Datenbank bereinigen ...";

            Thread thread = new Thread(() =>
            {
                MediaBrowserContext.CleanDB();

                Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    this.ResetAll();
                    MainWindow.BussyIndicatorIsBusy = false;
                }));
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            MediaBrowserContext.AbortInsert();

            if (Directory.GetFiles(MediaItem.GetCacheFolder()).Length > 1000)
            {
                DirectoryInfo dInfo = new DirectoryInfo(MediaItem.GetCacheFolder());
                foreach(FileInfo fInfo in dInfo.GetFiles().OrderByDescending(x => x.LastWriteTime).Skip(1000))
                {
                    fInfo.Delete();
                }
            }


            Application.Current.Shutdown();
        }

        private void MenuItemAddSDCard_Click(object sender, RoutedEventArgs e)
        {
            this.UseCopyTool();
        }

        private void UseCopyTool()
        {
            this.refreshTabItem = null;
            CopySdCardDialog sd = new CopySdCardDialog();
            sd.OnCopied += new EventHandler<EventArgs>(sd_OnCopied);
            sd.ShowDialog();
        }

        bool oldAutoCategorize = MediaBrowserContext.AutoCategorizeDate;
        void sd_OnCopied(object sender, EventArgs e)
        {
            if (((CopySdCardDialog)sender).NewMediaItems != null && ((CopySdCardDialog)sender).NewMediaItems.Count > 0)
            {
                oldAutoCategorize = MediaBrowserContext.AutoCategorizeDate;
                MediaBrowserContext.AutoCategorizeDate = true;
                MainWindow.BussyIndicatorContent = "Importiere ...";
                MainWindow.BussyIndicatorIsBusy = true;

                this.newMediaItemList = new List<MediaItem>();
                MediaBrowser4.MediaBrowserContext.InsertMediaItems(((CopySdCardDialog)sender).NewMediaItems);
            }
        }

        private void FreetecDatalogger_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "Kommaseparierte Daten (*.csv)|*.csv";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = false;
            openFileDialog.Multiselect = false;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = MediaBrowserContext.WorkingDirectory;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SortedDictionary<DateTime, Tuple<double, double>> dic = new SortedDictionary<DateTime, Tuple<double, double>>();

                int counter = 0;
                string line;
                System.IO.StreamReader file = new System.IO.StreamReader(openFileDialog.FileName);
                while ((line = file.ReadLine()) != null)
                {
                    counter++;
                    if (counter <= 1)
                        continue;

                    String[] parts = line.Split(',');

                    if (parts.Length < 3)
                        continue;

                    dic.Add(DateTime.ParseExact(parts[0].Replace("\"", ""), "yyyy-M-d H:mm", CultureInfo.InvariantCulture)
                        , Tuple.Create(Double.Parse(parts[1], new CultureInfo("en-US")), Double.Parse(parts[2], new CultureInfo("en-US"))));

                }
                file.Close();

                int cnt1 = dic.Count;
                MediaBrowserContext.InsertMeteorologyData(dic);

                MessageBox.Show(MainWindow.MainWindowStatic, String.Format("{1} Datensätze von {0} wurden neu hinzugefügt.", cnt1, dic.Count), "Meteorologie-Daten",
                 MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuItemMissingFileBehavior_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE)
            {
                this.MenuItemMissingFileBehaviorShow.IsEnabled = false;
                this.MenuItemMissingFileBehaviorIgnore.IsEnabled = false;
            }
            else
            {
                this.MenuItemMissingFileBehaviorDelete.Visibility = System.Windows.Visibility.Collapsed;
            }

            this.MenuItemMissingFileBehaviorShow.IsChecked = MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.SHOW;
            this.MenuItemMissingFileBehaviorIgnore.IsChecked = MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.IGNORE;
            this.MenuItemMissingFileBehaviorDelete.IsChecked = MediaBrowserContext.MissingFileBehavior == MediaBrowserContext.MissingFileBehaviorType.DELETE;
        }

        private void MenuItemMissingFileBehaviorShow_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.MissingFileBehavior = MediaBrowserContext.MissingFileBehaviorType.SHOW;
        }

        private void MenuItemMissingFileBehaviorIgnore_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.MissingFileBehavior = MediaBrowserContext.MissingFileBehaviorType.IGNORE;
        }

        private void MenuItemMissingFileBehaviorDelete_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.MissingFileBehavior = MediaBrowserContext.MissingFileBehaviorType.DELETE;
        }

        private void MenuItemHelp_Click(object sender, RoutedEventArgs e)
        {
            Help help = new Help();
            help.ShowDialog();
        }

        private void MenuItemOpenFromList_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "Dateiliste (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = false;
            openFileDialog.Multiselect = false;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.InitialDirectory = MediaBrowserContext.WorkingDirectory;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MediaItemFilesRequest fileRequest = new MediaItemFilesRequest(System.IO.Path.GetExtension(openFileDialog.FileName).ToLower() == ".hash" ? MediaItemFilesRequestType.FilesFromChecksum : MediaItemFilesRequestType.FilesFromList);
                fileRequest.FileListName = openFileDialog.FileName;
                this.AddRequest(fileRequest);
            }
        }

        ImageViewer imageViewer;
        public void ShowSelectedImage(bool showOnlyIfClosed, bool? showFaces, MediaItem mItem)
        {
            if (mItem != null && (showOnlyIfClosed || (this.imageViewer != null && !this.imageViewer.IsClosed)))
            {
                PreviewObject previewObject = MediaBrowserContext.GetImagePreviewDB(mItem.VariationId);

                if (previewObject.Binary != null)
                {
                    System.Drawing.SolidBrush shadowBrushBlack = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(150, System.Drawing.Color.Black));
                    System.Drawing.SolidBrush shadowBrushWhite = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, System.Drawing.Color.White));
                    System.Drawing.Font drawFont = new System.Drawing.Font("Arial", 16);

                    using (var ms = new MemoryStream(previewObject.Binary))
                    {
                        System.Drawing.Bitmap bitmap = (System.Drawing.Bitmap)System.Drawing.Image.FromStream(ms);

                        if (!(showFaces != null && !showFaces.Value) &&
                            ((this.imageViewer != null && this.imageViewer.ShowFaces)
                            || (showFaces != null && showFaces.Value)))
                        {
                            Faces faces = MediaBrowserContext.GetFaceDetectionPreviewDB(mItem.VariationId);

                            if (faces.Facelist.Count > 0)
                            {
                                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                                {
                                    graphics.FillRectangles(shadowBrushBlack, faces.Facelist.ToArray());

                                    int i = 0;
                                    foreach (System.Drawing.RectangleF rect in faces.Facelist)
                                    {
                                        i++;

                                        graphics.DrawString(
                                            String.Format("[{0}] {1:0.0}%", i, (100 * (double)(rect.Width * rect.Height) / (double)(faces.Width * faces.Height))),
                                            drawFont,
                                            shadowBrushWhite,
                                            rect.X,
                                            rect.Y);
                                    }
                                }
                            }
                        }

                        using (MemoryStream ms2 = new MemoryStream())
                        {
                            ((System.Drawing.Bitmap)bitmap).Save(ms2, System.Drawing.Imaging.ImageFormat.Bmp);
                            System.Windows.Media.Imaging.BitmapImage image = new System.Windows.Media.Imaging.BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            ms2.Seek(0, SeekOrigin.Begin);
                            image.StreamSource = ms2;
                            image.EndInit();

                            if (this.imageViewer == null || this.imageViewer.IsClosed)
                            {
                                this.imageViewer = new ImageViewer(image);
                                this.imageViewer.Show();
                            }
                            else
                            {
                                this.imageViewer.ImageSource = image;
                            }

                            this.imageViewer.ShowFaces = showFaces == null ? this.imageViewer.ShowFaces : showFaces.Value;
                            this.imageViewer.Title = mItem.FullName;
                            this.imageViewer.Activate();
                        }
                    }
                }
                else if (this.imageViewer != null && !this.imageViewer.IsClosed)
                {
                    this.imageViewer.ImageSource = null;
                    this.imageViewer.Title = "Kein Vorschaubild vorhanden";
                }
            }
        }

        private void MenuItemImportMetadata_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "XML File (*.xml)|*.xml";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = true;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                String directoryName = System.IO.Path.GetDirectoryName(openFileDialog.FileName);

                Folder folder = Folder.CreateFolder(directoryName);
                this.DataBaseFolderTree.ExpandPath(directoryName);
                this.TabControlControls.SelectedItem = this.FolderTab;

                this.DataBaseFolderTree.ImportXml(folder, new List<string>() { openFileDialog.FileName });
            }
        }

        private void MenuItemImportGeoData_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            openFolderDialog.SelectedPath = Directory.Exists(MediaBrowserContext.GPSLoggerPath) ? MediaBrowserContext.GPSLoggerPath : MediaBrowserContext.WorkingDirectory;
            openFolderDialog.RootFolder = Environment.SpecialFolder.UserProfile;
            openFolderDialog.Description = "Lese KML-Dateien aus diesem Ordner in die GeoDaten-Tabelle ein.";

            if (openFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                List<GpsFile> fileList = MediaBrowserContext.GetGpsFileList();
                List<GeoPoint> gpsList = KmlHelper.ParseFolder(openFolderDialog.SelectedPath, fileList);
                MediaBrowserContext.HasGeodata = fileList.Count > 0 || gpsList.Count > 0;
                MediaBrowserContext.InsertGpsPoints(gpsList);
            }
        }

        private void StatusBarTextblock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (File.Exists(StatusBarTextblock.Text))
                {
                    MediaBrowserWPF.Utilities.FilesAndFolders.OpenExplorer(StatusBarTextblock.Text, false);
                }
            }
        }

        private void MenuItemClearLocalImageCache_Click(object sender, RoutedEventArgs e)
        {   
            foreach(String file in Directory.GetFiles(MediaItem.GetCacheFolder()))
            {
                File.Delete(file);
            }
        }
    }
}
