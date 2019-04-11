using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaBrowser4;
using MediaBrowser4.Objects;
using MediaBrowserWPF.Helpers;
using MediaBrowserWPF.UserControls.FolderContainer;
using System.IO;
using MediaBrowser4.Utilities;
using System.Windows.Threading;
using System.Threading;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für DbTree.xaml
    /// </summary>
    public partial class FolderTree : UserControl
    {
        public event EventHandler<FolderEventArgs> RenameFolder;
        public event EventHandler<FolderEventArgs> DeleteFolder;
        public event EventHandler<FolderEventArgs> NewFolder;
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;
        private TreeViewHelper treeViewHelper;
        private string lastRequestedFolder;

        public bool IsBuild
        {
            get;
            private set;
        }

        public void Clear()
        {
            this.IsBuild = false;
        }

        public FolderTree()
        {
            InitializeComponent();
            treeViewHelper = new TreeViewHelper(this.treeViewMain);
        }

        public void Build()
        {
            this.Build(true);
        }

        public void Build(bool expand)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (MediaBrowser4.MediaBrowserContext.FolderTreeSingelton == null)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            this.treeViewMain.ItemsSource = MediaBrowser4.MediaBrowserContext.FolderTreeSingelton.Children;
            ICollection<Folder> allFolders = MediaBrowser4.MediaBrowserContext.FolderTreeSingelton.FullFolderCollection;

            this.FolderEditor.Clear();

            if (allFolders.Count > 0)
            {
                this.FolderAutoCompleter.ItemsSource = allFolders.OrderBy(x => x.FullPath);
                this.FolderAutoCompleter.AutoCompleteManager.DataProvider = new FolderAutoCompleteProvider(allFolders);
                this.FolderAutoCompleter.AutoCompleteManager.AutoAppend = true;

                if (expand)
                {
                    this.lastRequestedFolder = this.GetFolderProperty();
                    this.ExpandPath(this.lastRequestedFolder);
                }
            }

            this.FolderListBox.Items.Clear();
            this.IsBuild = true;

            Mouse.OverrideCursor = null;
        }

        public void Set(MediaItemFolderRequest request)
        {
            this.MenuItemSingle.IsChecked = (request.RequestType == MediaItemRequestType.SINGLE);
            this.MenuItemRecursive.IsChecked = (request.RequestType == MediaItemRequestType.RECURSIVE);

            this.FolderListBox.Items.Clear();

            this.treeViewHelper.CollapseAll();
            this.ExpandPath(request.Folders[0].FullPath);

            if (request.Folders.Length > 1)
            {
                foreach (Folder folder in request.Folders)
                {
                    this.FolderListBox.Items.Add(folder);
                }
            }
        }

        public void SetRequestFromList(List<MediaItem> mItemList)
        {
            List<string> folderList = new List<string>();
            foreach (MediaItem mItem in mItemList)
            {
                if (!folderList.Contains(mItem.FileObject.DirectoryName))
                {
                    folderList.Add(mItem.FileObject.DirectoryName);
                }
            }

            this.FolderListBox.Items.Clear();
            foreach (string name in folderList.OrderBy(x => x))
            {
                Folder folder = MediaBrowserContext.FolderTreeSingelton.FullFolderCollection.FirstOrDefault(x => x.FullPath.Equals(name, StringComparison.InvariantCultureIgnoreCase));
                if (folder != null && !this.FolderListBox.Items.Contains(folder))
                {
                    this.FolderListBox.Items.Add(folder);
                }
            }

            this.FolderTreeExpander.IsExpanded = true;
            this.EditFolderExpander.IsExpanded = false;
            this.FolderCollectionExpander.IsExpanded = true;

            this.GetMediaItems(MediaItemRequestType.SINGLE);
        }

        public void ToggleCollaps()
        {
            if (this.treeViewHelper.IsCollapsed
                && this.lastRequestedFolder != null)
            {
                this.treeViewHelper.ExpandPath(this.lastRequestedFolder);
            }
            else
            {
                this.CollapseAll();
            }
        }

        private void Button_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (sender != null)
            {
                Folder folder = (((FrameworkElement)(sender)).DataContext) as Folder;
                StackPanel sp = sender as StackPanel;

                if (folder != null)
                {
                    sp.ToolTip =
                        $"Medien: {folder.ItemCount:n0}" + Environment.NewLine +
                        $"alle Medien: {folder.ItemCountRecursive:n0}" + Environment.NewLine +
                        $"alle DB-Ordner: {folder.ChildrenCount:n0}" + Environment.NewLine + Environment.NewLine +
                        $"HDD: {(Directory.Exists(folder.FullPath) ? Directory.GetFiles(folder.FullPath).Count(x => !x.EndsWith("Thumbs.db")) : 0):n0} Dateien, {(Directory.Exists(folder.FullPath) ? Directory.GetDirectories(folder.FullPath).Length : 0):n0} Verzeichnisse";
                }
            }
        }

        public void CollapseAll()
        {
            this.treeViewHelper.CollapseAll();
        }

        public void ExpandPath(string path)
        {
            this.treeViewHelper.ExpandPath(path);

            SelectPath(path);
        }

        public void SelectPath(string path)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(delegate ()
            {
                Folder folder = MediaBrowserContext.FolderTreeSingelton.FullFolderCollection.FirstOrDefault(x => x.FullPath == path);

                if (folder != null)
                {
                    TreeViewItem item = ContainerFromItem(treeViewMain.ItemContainerGenerator, folder);

                    if (item != null)
                        item.IsSelected = true;
                }
            }));
        }

        private static TreeViewItem ContainerFromItem(ItemContainerGenerator containerGenerator, object item)
        {
            TreeViewItem container = (TreeViewItem)containerGenerator.ContainerFromItem(item);
            if (container != null)
                return container;

            foreach (object childItem in containerGenerator.Items)
            {
                TreeViewItem parent = containerGenerator.ContainerFromItem(childItem) as TreeViewItem;
                if (parent == null)
                    continue;

                container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                    return container;

                container = ContainerFromItem(parent.ItemContainerGenerator, item);
                if (container != null)
                    return container;
            }
            return null;
        }

        private bool recentFoldersChanged = true;
        private void BuildRecentFolders()
        {
            if (!this.recentFoldersChanged)
                return;

            if (MediaBrowserContext.GetDBProperty("InitialDBTreePath") != null
                && MediaBrowserContext.GetDBProperty("InitialDBTreePath").Trim().Length > 0)
            {
                List<string> parts = new List<string>(MediaBrowserContext.GetDBProperty("InitialDBTreePath").Split(';').Reverse());
                this.MenuItemRecentFolders.Visibility = System.Windows.Visibility.Visible;
                this.MenuItemRecentFolders.Items.Clear();

                foreach (string part in parts)
                {
                    bool found = false;
                    foreach (Folder folder in MediaBrowserContext.FolderTreeSingelton.FullFolderCollection)
                    {
                        if (folder.FullPath.StartsWith(part, StringComparison.InvariantCultureIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        MenuItem newItem = new MenuItem();
                        newItem.Header = part;
                        newItem.Click += new RoutedEventHandler(MenuItemRecentFoldersNewItem_Click);
                        this.MenuItemRecentFolders.Items.Add(newItem);
                    }
                    else
                    {
                        MediaBrowserContext.SetDBProperty("InitialDBTreePath", String.Join(";",
                            this.RemoveFolderTreeProperty(part)));
                    }
                }
            }
            else
            {
                this.MenuItemRecentFolders.Visibility = System.Windows.Visibility.Collapsed;
            }

            this.recentFoldersChanged = false;
        }

        #region get MediaItems
        private void MenuItem_Recursive(object sender, RoutedEventArgs e)
        {
            this.GetMediaItems(MediaItemRequestType.RECURSIVE);
        }

        private void MenuItem_Single(object sender, RoutedEventArgs e)
        {
            this.GetMediaItems(MediaItemRequestType.SINGLE);
        }

        private void FolderListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.GetMediaItems(this.MenuItemSingle.IsChecked ? MediaItemRequestType.SINGLE : MediaItemRequestType.RECURSIVE);
        }

        private void StackPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.FolderEditor.Folder = treeViewMain.SelectedItem as Folder;

            if (this.treeViewMain.SelectedItem != null && this.OnRequest != null)
            {
                if (e.ClickCount == 2)
                {
                    this.GetSingleFromTree((Folder)this.treeViewMain.SelectedItem);
                }
                else
                {
                    System.Windows.Point pt = e.GetPosition(treeViewMain);
                    DependencyObject obj = treeViewMain.InputHitTest(pt) as DependencyObject;

                    while (obj != null)
                    {
                        if (typeof(TreeViewItem).IsInstanceOfType(obj))
                            break;
                        else
                            obj = VisualTreeHelper.GetParent(obj);
                    }

                    TreeViewItem treeViewItem = obj as TreeViewItem;

                    if (treeViewItem != null)
                    {
                        Folder folder = treeViewItem.DataContext as Folder;

                        if (folder != null)
                        {
                            if (!treeViewItem.IsExpanded)
                                this.treeViewHelper.ExpandPath(folder.FullPath);
                            else
                                this.treeViewHelper.Collapse(treeViewItem);
                        }
                    }
                }
            }
        }

        private void treeViewMain_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.FolderEditor.Folder = ((TreeView)sender).SelectedItem as Folder;

            if (e.OldValue == null && this.FolderEditor.Folder != null)
            {
                this.treeViewHelper.ExpandPath(this.FolderEditor.Folder.FullPath);
            }
        }      

        private void GetSingleFromTree(Folder folder)
        {    
            if (folder != null && folder.Id > 0)
            {
                MediaItemFolderRequest folderRequest = new MediaItemFolderRequest();

                folderRequest.AddFolder(folder);
                folderRequest.RequestType = MediaItemRequestType.SINGLE;

                if (folderRequest.IsValid)
                    this.OnRequest(this, new MediaItemRequestMessageArgs(folderRequest));

                this.SetFolderTreeProperty(folder.FullPath);
                this.lastRequestedFolder = folder.FullPath;
            }
        }

        private void GetMediaItems(MediaItemRequestType mediaItemRequestType)
        {
            if (this.FolderListBox.Items.Count == 0)
                return;

            this.MenuItemSingle.IsChecked = mediaItemRequestType == MediaItemRequestType.SINGLE;
            this.MenuItemRecursive.IsChecked = mediaItemRequestType == MediaItemRequestType.RECURSIVE;

            MediaItemFolderRequest folderRequest = new MediaItemFolderRequest();
            folderRequest.RequestType = mediaItemRequestType;
            foreach (Folder folder in this.FolderListBox.Items)
                folderRequest.AddFolder(folder);

            if (this.OnRequest != null && folderRequest.IsValid)
            {
                this.SetFolderTreeProperty(folderRequest.Folders[0].FullPath);
                this.lastRequestedFolder = folderRequest.Folders[0].FullPath;
                this.OnRequest(this, new MediaItemRequestMessageArgs(folderRequest));
            }
        }

        private void SetFolderTreeProperty(string path)
        {
            this.recentFoldersChanged = true;
            List<string> parts = this.RemoveFolderTreeProperty(path);

            parts.Add(path);

            while (parts.Count > 8)
            {
                parts.RemoveAt(0);
            }

            MediaBrowserContext.SetDBProperty("InitialDBTreePath", String.Join(";", parts));
        }

        private List<string> RemoveFolderTreeProperty(string path)
        {
            List<string> parts = null;
            if (MediaBrowserContext.GetDBProperty("InitialDBTreePath") != null
                && MediaBrowserContext.GetDBProperty("InitialDBTreePath").Trim().Length > 0)
            {
                parts = new List<string>(MediaBrowserContext.GetDBProperty("InitialDBTreePath").Split(';'));
            }
            else
            {
                parts = new List<string>();
            }

            if (parts.Contains(path))
                parts.Remove(path);

            return parts;
        }

        private string GetFolderProperty()
        {
            List<string> parts = null;
            if (MediaBrowserContext.GetDBProperty("InitialDBTreePath") != null
                && MediaBrowserContext.GetDBProperty("InitialDBTreePath").Trim().Length > 0)
            {
                parts = new List<string>(MediaBrowserContext.GetDBProperty("InitialDBTreePath").Split(';'));
            }
            else
            {
                parts = new List<string>();
            }

            if (parts.Count > 0)
                return parts[parts.Count - 1];
            else
                return null;
        }

        #endregion

        #region Drag & Drop
        Point dragStartPoint;
        private void treeViewMain_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(null);
        }

        private void treeViewMain_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {

                TreeViewItem treeViewItem =
                    FindAnchestor<TreeViewItem>((DependencyObject)e.OriginalSource);

                if (treeViewItem == null)
                    return;

                Object obj = this.treeViewMain.SelectedItem;

                if (obj == null)
                    return;

                Folder category = (Folder)obj;

                DataObject dragData = new DataObject("Folder", category);
                DragDrop.DoDragDrop(treeViewItem, dragData, DragDropEffects.Copy);
            }
        }

        private void FolderListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Folder"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FolderListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Folder"))
            {
                Folder folder = e.Data.GetData("Folder") as Folder;

                if (!this.FolderListBox.Items.Contains(folder))
                {
                    this.FolderListBox.Items.Add(folder);
                }
            }
        }

        // Helper to search up the VisualTree
        private static T FindAnchestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private void treeViewMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(int[])))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void treeViewMain_Drop(object sender, DragEventArgs e)
        {
            int[] droplist = (int[])e.Data.GetData(typeof(int[]));

            if (droplist != null)
            {
                List<MediaItem> mItemlist = new List<MediaItem>();
                foreach (int id in droplist)
                {
                    mItemlist.Add(MediaBrowserContext.GlobalMediaItemCache[id]);
                }
                MessageBox.Show(MainWindow.MainWindowStatic, mItemlist.Count + "x");
            }
        }
        #endregion

        #region events
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            this.BuildRecentFolders();
        }

        void MenuItemRecentFoldersNewItem_Click(object sender, RoutedEventArgs e)
        {
            this.treeViewHelper.CollapseAll();
            this.ExpandPath(((MenuItem)sender).Header.ToString());
        }

        private void MoveMediaItems_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.Utilities.FilesAndFolders.AddCopyMoveFromClipboard(treeViewMain.SelectedItem as Folder);
        }

        private void ContextMenu_Selected_Folders_Opened(object sender, RoutedEventArgs e)
        {
            this.MenuItemRecursive.IsEnabled = (this.FolderListBox.Items.Count > 0);
            this.MenuItemSingle.IsEnabled = (this.FolderListBox.Items.Count > 0);
            this.MenuItemExpand.IsEnabled = (this.FolderListBox.SelectedItems.Count > 0);
            this.MenuItemRemove.IsEnabled = (this.FolderListBox.SelectedItems.Count == 1);
            this.MenuItemShowInTree.IsEnabled = (this.FolderListBox.SelectedItems.Count == 1);
            this.MenuItemRemoveAll.IsEnabled = (this.FolderListBox.Items.Count > 0);
        }

        private void GridSplitter_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeNS;
        }

        private void GridSplitter_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void MenuItemDelete_Click(object sender, RoutedEventArgs e)
        {
            this.FolderEditor.DeleteFull(this.treeViewMain.SelectedItem as Folder);
        }

        private void MenuItemDeleteDb_Click(object sender, RoutedEventArgs e)
        {
            this.FolderEditor.DeleteDb(this.treeViewMain.SelectedItem as Folder);
        }

        private void MenuItemDeleteRekursive_Click(object sender, RoutedEventArgs e)
        {
            this.FolderEditor.DeleteDbRecursive(this.treeViewMain.SelectedItem as Folder);
        }

        private void treeViewMain_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.FolderEditor.DeleteDb(this.treeViewMain.SelectedItem as Folder);
                    }
                    else
                    {
                        this.FolderEditor.Delete(this.treeViewMain.SelectedItem as Folder);
                    }
                    break;

                case Key.F2:
                    this.EditFolderExpander.IsExpanded = true;
                    this.FolderCollectionExpander.IsExpanded = false;

                    if (this.FolderEditor.IsNew)
                    {
                        this.FolderEditor.IsNew = false;
                        this.FolderEditor.Folder = this.treeViewMain.SelectedItem as Folder;
                    }
                    this.FolderEditor.IsEdit = true;
                    break;

                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.EditFolderExpander.IsExpanded = true;
                        this.FolderCollectionExpander.IsExpanded = false;
                        this.FolderEditor.IsNew = true;
                        this.FolderEditor.Folder = this.treeViewMain.SelectedItem as Folder;
                    }
                    break;

                case Key.Enter:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        this.OpenInExplorer();
                    else
                        this.GetSingleFromTree((Folder)this.treeViewMain.SelectedItem);
                    break;

                case Key.F5:
                    MediaBrowserContext.ResetFolderTree();
                    this.Build();
                    break;

                case Key.F:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.EditFolderExpander.IsExpanded = false;
                        this.FolderCollectionExpander.IsExpanded = true;
                        this.FolderTreeExpander.IsExpanded = true;
                        this.FolderAutoCompleter.Focus();
                        this.UpdateLayout();
                    }
                    break;
            }
        }

        private void treeViewMain_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        List<string> clipBoardList = MediaBrowserWPF.Utilities.FilesAndFolders.AddCopyMoveFromClipboard(treeViewMain.SelectedItem as Folder);

                        List<string> xmlList = clipBoardList.Where(x => x.ToLower().EndsWith(".xml")).ToList();

                        if (xmlList.Count > 0)
                            ImportXml(treeViewMain.SelectedItem as Folder, xmlList);

                        e.Handled = true;
                    }
                    break;
            }
        }

        private void FolderEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    this.EditFolderExpander.IsExpanded = false;
                    this.FolderEditor.IsNew = false;
                    this.FolderTreeExpander.IsExpanded = true;
                    this.treeViewMain.Focus();
                    break;
            }
        }

        private void FolderListBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.S:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.GetMediaItems(MediaItemRequestType.SINGLE);
                    break;

                case Key.R:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.GetMediaItems(MediaItemRequestType.RECURSIVE);
                    break;

                case Key.Delete:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        MenuItem_RemoveAll(this.FolderListBox, null);
                    else
                        MenuItem_Remove(this.FolderListBox, null);
                    break;

                case Key.Enter:
                    this.GetMediaItems(this.MenuItemSingle.IsChecked ? MediaItemRequestType.SINGLE : MediaItemRequestType.RECURSIVE);
                    break;
            }
        }

        private void FolderAutoCompleter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetFromAutocompleter();
            }
        }

        private void FolderAutoCompleter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SetFromAutocompleter();
        }

        private void SetFromAutocompleter()
        {
            Folder folder = this.FolderAutoCompleter.SelectedItem;

            if (folder != null)
            {
                this.treeViewHelper.CollapseAll();
                this.ExpandPath(folder.FullPath);

                if (!this.FolderListBox.Items.Contains(folder))
                {
                    this.FolderListBox.Items.Add(folder);
                }

                this.FolderAutoCompleter.Focus();
            }
        }

        private void MenuItem_Remove(object sender, RoutedEventArgs e)
        {
            while (this.FolderListBox.SelectedItems.Count != 0)
                this.FolderListBox.Items.Remove(this.FolderListBox.SelectedItem);
        }

        private void MenuItem_RemoveAll(object sender, RoutedEventArgs e)
        {
            while (this.FolderListBox.Items.Count != 0)
                this.FolderListBox.Items.Remove(this.FolderListBox.Items[0]);
        }

        private void MenuItemExpand_Click(object sender, RoutedEventArgs e)
        {
            foreach (Folder folder in this.FolderListBox.Items.Cast<Folder>().ToArray())
            {
                if (folder.Children.Count > 0)
                {
                    this.FolderListBox.Items.Remove(folder);
                    foreach (Folder folderChild in folder.Children)
                    {
                        this.FolderListBox.Items.Add(folderChild);
                    }
                }
            }
        }

        private void MenuItemShowInTree_Click(object sender, RoutedEventArgs e)
        {
            if (this.FolderListBox.SelectedItems.Count == 1)
            {
                this.FolderTreeExpander.IsExpanded = true;
                this.treeViewHelper.CollapseAll();
                this.ExpandPath(((Folder)this.FolderListBox.SelectedItem).FullPath);
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.SetExpanderSize();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            this.SetExpanderSize();

            Action action = () =>
            {
                this.FolderEditor.Focus();
            };

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, action);
        }

        private void OpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            this.OpenInExplorer();
        }

        private void OpenInExplorer()
        {
            Folder folder = this.treeViewMain.SelectedItem as Folder;

            if (folder != null && System.IO.Directory.Exists(folder.FullPath))
            {
                try
                {
                    MediaBrowserWPF.Utilities.FilesAndFolders.OpenExplorer(folder.FullPath, true);
                }
                catch (System.ComponentModel.Win32Exception)
                {

                }
            }
        }

        private void FolderEditor_NewFolder(object sender, FolderContainer.FolderEventArgs e)
        {
            this.ExpandPath(e.Folder.FullPath);

            if (this.NewFolder != null)
                this.NewFolder.Invoke(this, e);
        }

        private void FolderEditor_DeleteFolder(object sender, FolderContainer.FolderEventArgs e)
        {
            if (this.DeleteFolder != null)
                this.DeleteFolder.Invoke(this, e);

            this.FolderListBox.Items.Remove(e.Folder);
        }

        private void FolderEditor_RenameFolder(object sender, FolderContainer.FolderEventArgs e)
        {
            if (this.RenameFolder != null)
                this.RenameFolder.Invoke(this, e);

            int index = this.FolderListBox.Items.IndexOf(e.Folder);

            if (index >= 0)
            {
                this.FolderListBox.Items.Remove(e.Folder);
                this.FolderListBox.Items.Insert(index, e.Folder);
            }
        }

        private void MenuItemCollapseAll_Click(object sender, RoutedEventArgs e)
        {
            this.ToggleCollaps();
        }

        private double defaultExpanderColapsedHeight;
        List<Expander> expanderList;
        private void SetExpanderSize()
        {
            if (this.FolderCollectionExpander == null
                || this.FolderTreeExpander == null
                || this.EditFolderExpander == null)
                return;

            if (this.defaultExpanderColapsedHeight == 0 && this.EditFolderExpander.RenderSize.Height > 0)
            {
                this.defaultExpanderColapsedHeight = this.EditFolderExpander.RenderSize.Height;

                this.expanderList = new List<Expander>();
                this.expanderList.Add(this.EditFolderExpander);
                this.expanderList.Add(this.FolderTreeExpander);
                this.expanderList.Add(this.FolderCollectionExpander);

            }

            if (this.expanderList != null)
            {
                int openExpanders = this.expanderList.Where(x => x.IsExpanded).Count();
                double availableHeight
                    = ((this.RenderSize.Height - (this.expanderList.Count * this.defaultExpanderColapsedHeight)) / openExpanders) + this.defaultExpanderColapsedHeight;

                if (availableHeight >= this.defaultExpanderColapsedHeight && availableHeight < 50000)
                {
                    foreach (Expander expander in this.expanderList)
                    {
                        expander.Height = expander.IsExpanded ? availableHeight : this.defaultExpanderColapsedHeight;
                    }
                }
            }
        }
        #endregion

        private void MenuItemImportMetadata_Click(object sender, RoutedEventArgs e)
        {
            if (this.treeViewMain.SelectedItem == null)
                return;

            Folder folder = this.treeViewMain.SelectedItem as Folder;

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "XML File (*.xml)|*.xml";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = true;
            openFileDialog.InitialDirectory = Directory.Exists(folder.FullPath) ? folder.FullPath :
                Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImportXml(folder, new List<string>() { openFileDialog.FileName });
            }
        }

        public void ImportXml(Folder folder, List<string> clipBoardList)
        {
            MainWindow.BussyIndicatorIsBusy = true;

            if (folder != null)
            {

                if(folder.Id <= 0)
                    MediaBrowserContext.SetFolder(folder);

                Thread thread = new Thread(() =>
                {
                    foreach (string fileName in clipBoardList)
                    {
                        XmlMetadata xml = new XmlMetadata();
                        xml.ExportMessage += new EventHandler(xml_ExportMessage);

                        xml.Import(fileName, folder);

                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                        {
                            MainWindow.BussyIndicatorIsBusy = false;
                            MediaBrowserContext.ResetCategoryTree();
                            MainWindow.RefreshCategoryTree();
                            this.GetSingleFromTree(folder);
                        }));
                    }
                });

                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        void xml_ExportMessage(object sender, EventArgs e)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                MainWindow.BussyIndicatorContent = ((XmlMetadata)sender).Message;
            }));
        }
    }
}
