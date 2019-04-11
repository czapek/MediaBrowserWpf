using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaBrowser4.Objects;
using MediaBrowserWPF.Helpers;
using MediaBrowser4;
using MediaBrowserWPF.UserControls.CategoryContainer;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für CategoryTree.xaml
    /// </summary>
    public partial class CategoryTree : UserControl
    {
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;
        private TreeViewHelper treeViewHelper;
        private string lastRequestedCategory;
        private bool isLoaded = false;

        // public event EventHandler<CategoryEventArgs> NewCategory;
        public event EventHandler<CategoryEventArgs> RenameCategory;
        public event EventHandler<CategoryEventArgs> DeleteCategory;

        public bool IsBuild
        {
            get;
            private set;
        }

        public CategoryTree()
        {
            InitializeComponent();

            try
            {
                treeViewHelper = new TreeViewHelper(this.treeViewMain);
            }
            catch
            {
            }
        }

        public void Clear()
        {
            this.IsBuild = false;
            this.CategoryEditor.Clear();
        }

        public void Build()
        {
            this.Build(true);

            isLoaded = true;
        }

        public void ResetCalendarCategory()
        {
            this.CalendarCategory.SelectedDate = null;
        }

        public void Build(bool expand)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton == null)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            try
            {
                this.CalendarCategory.SelectedDate = DateTime.Now;
            }
            catch { this.CalendarCategory.BlackoutDates.Clear(); }

            this.treeViewMain.ItemsSource = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.Children;
            this.IsBuild = true;

            this.CategoryEditor.Clear();

            if (MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Count > 0)
            {
                this.CategoryAutoCompleter.Text = String.Empty;
                this.CategoryAutoCompleter.ItemsSource = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;
                this.CategoryAutoCompleter.AutoCompleteManager.DataProvider = new CategoryAutoCompleteProvider(MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection);
                this.CategoryAutoCompleter.AutoCompleteManager.AutoAppend = true;

                if (expand)
                    this.PrepareDateCategories();
            }

            this.CategoryListBox.Items.Clear();

            Mouse.OverrideCursor = null;
        }

        private void BlackoutCalendar()
        {
            this.CalendarCategory.BlackoutDates.Clear();
            try
            {
                DateTime date = this.CalendarCategory.SelectedDate.HasValue ? this.CalendarCategory.SelectedDate.Value.Date : DateTime.MinValue;

                var dateList =
                MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Where(x => x.IsDate && x.Children.Count == 0 && !x.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                    .Select(x => x.Date.Date).Union(new DateTime[] { date }).OrderBy(x => x);

                DateTime startDate = DateTime.MinValue;

                foreach (DateTime stopDate in dateList)
                {
                    if (startDate.AddDays(1) != stopDate)
                        this.CalendarCategory.BlackoutDates.Add(new CalendarDateRange(startDate.AddDays(1), stopDate.AddDays(-1)));


                    startDate = stopDate;
                }

                this.CalendarCategory.BlackoutDates.Add(new CalendarDateRange(startDate.AddDays(1), DateTime.MaxValue));
            }
            catch { }
        }

        private void PrepareDateCategories()
        {
            var maxDateTime = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Where(x => x.IsDate && x.Children.Count == 0 && !x.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                .Select(x => x.Date).Union(new List<DateTime>() { DateTime.MinValue }).Max();

            if (maxDateTime > DateTime.Now.AddDays(-100))
            {
                Category maxDateCategory = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.First(x => x.IsDate && x.Date == maxDateTime);

                try
                {
                    this.CalendarCategory.SelectedDate = maxDateCategory.Date;
                }
                catch { this.CalendarCategory.BlackoutDates.Clear(); }

                ExpandCategory(maxDateCategory);
            }
            else
            {
                this.lastRequestedCategory = this.GetCategoryProperty();
                this.ExpandPath(this.lastRequestedCategory);
            }

            if (MediaBrowserContext.BookmarkedSingleton.Count > 0)
            {
                MediaItemObservableCollectionRequest observableCollectionRequest = new MediaItemObservableCollectionRequest(MediaBrowserContext.BookmarkedSingleton, "Lesezeichen", "Liste aller markierten Medien");
                observableCollectionRequest.UserDefinedId = "Lesezeichen".GetHashCode();
                observableCollectionRequest.ShuffleType = MediaItemRequestShuffleType.NONE;
                observableCollectionRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE, MediaItemRequestSortDirection.ASCENDING));

                if (this.OnRequest != null)
                {
                    this.OnRequest(this, new MediaItemRequestMessageArgs(observableCollectionRequest));
                }
            }

            //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            //sw.Start();
            BlackoutCalendar();

            //sw.Stop();

            this.GetMediaItems(MediaItemRequestType.INTERSECT);
        }

        private void ExpandCategory(Category maxDateCategory)
        {
            this.lastRequestedCategory = maxDateCategory.FullPath;

            MediaItemCategoryRequest categoryRequest = new MediaItemCategoryRequest();
            categoryRequest.RequestType = MediaItemRequestType.INTERSECT;
            categoryRequest.AddCategory(maxDateCategory);
            this.OnRequest(this, new MediaItemRequestMessageArgs(categoryRequest));
        }

        public void SetRequestFromList(List<Category> categoryList)
        {
            this.CategoryListBox.Items.Clear();
            foreach (Category cat in categoryList.OrderBy(x => x.IsDate).ThenBy(x => x.IsLocation).ThenBy(x => x.Date).ThenBy(x => x.FullPath))
            {
                Category category = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(cat.Id);

                if (category != null && !this.CategoryListBox.Items.Contains(category))
                {
                    this.CategoryListBox.Items.Add(category);
                }
            }

            this.CategoryCollectionExpander.IsExpanded = true;
            this.EditCategoryExpander.IsExpanded = false;
            this.CategoryCollectionExpander.IsExpanded = true;

            this.GetMediaItems(MediaItemRequestType.UNION);
        }

        public void Set(MediaItemCategoryRequest request)
        {
            if (request.Categories.Length == 0)
                return;

            this.CategoryListBox.Items.Clear();

            this.treeViewHelper.CollapseAll();
            this.ExpandPath(request.Categories[0].FullPath);

            if (request.Categories.Length > 1)
            {
                this.MenuItemIntersect.IsChecked = (request.RequestType == MediaItemRequestType.INTERSECT);
                this.MenuItemUnion.IsChecked = (request.RequestType == MediaItemRequestType.UNION);
                this.MenuItemSingle.IsChecked = (request.RequestType == MediaItemRequestType.SINGLE);

                foreach (Category category in request.Categories)
                {
                    this.CategoryListBox.Items.Add(category);
                }
            }
        }

        public void ToggleCollaps()
        {
            if (this.treeViewHelper.IsCollapsed
               && this.lastRequestedCategory != null)
            {
                this.treeViewHelper.ExpandPath(this.lastRequestedCategory);
            }
            else
            {
                this.treeViewHelper.CollapseAll();
            }
        }

        private void MenuItemNewest_Click(object sender, RoutedEventArgs e)
        {
            ICollection<Category> allCats = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;

            var maxDateTime = (from p in allCats where p.IsDate && p.Children.Count == 0 select (p.Date)).Max();

            if (maxDateTime > DateTime.Now.AddDays(-10000))
            {
                this.treeViewHelper.CollapseAll();
                Category maxDateCategory = allCats.First(x => x.IsDate && x.Date == maxDateTime);
                this.ExpandPath(maxDateCategory.FullPath);
            }
        }

        public void ExpandPath(string path)
        {
            Category folder = MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.FirstOrDefault(x => x.FullPath == path);

            if (folder != null)
            {
                folder.IsSelected = true;
            }

            this.treeViewHelper.ExpandPath(path);
        }

        #region get MediaItems
        private void StackPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.CategoryEditor.Category = treeViewMain.SelectedItem as Category;

            if (this.treeViewMain.SelectedItem != null && this.OnRequest != null)
            {
                if (e.ClickCount == 2)
                {
                    this.GetSingleFromTree();
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
                        Category category = treeViewItem.DataContext as Category;

                        if (category != null)
                        {
                            if (!treeViewItem.IsExpanded)
                                this.treeViewHelper.ExpandPath(category.FullPath);
                            else
                                this.treeViewHelper.Collapse(treeViewItem);
                        }
                    }
                }
            }
        }

        private void treeViewMain_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.CategoryEditor.Category = ((TreeView)sender).SelectedItem as Category;

            if (e.OldValue == null && this.CategoryEditor.Category != null)
            {
                this.treeViewHelper.ExpandPath(this.CategoryEditor.Category.FullPath);
            }
        }

        private void GetSingleFromTree()
        {
            Category category = (Category)this.treeViewMain.SelectedItem;
            if (category.Id > 0)
            {
                MediaItemCategoryRequest categoryRequest = new MediaItemCategoryRequest();

                categoryRequest.AddCategory(category);

                categoryRequest.RequestType = this.MenuItemIntersect.IsChecked ? MediaItemRequestType.INTERSECT : MediaItemRequestType.UNION;

                this.OnRequest(this, new MediaItemRequestMessageArgs(categoryRequest));

                this.SetCategoryTreeProperty(category.FullPath);
                this.lastRequestedCategory = category.FullPath;
            }
        }

        private void MenuItem_Intersect(object sender, RoutedEventArgs e)
        {
            this.GetMediaItems(MediaItemRequestType.INTERSECT);
        }

        private void MenuItem_Union(object sender, RoutedEventArgs e)
        {
            this.GetMediaItems(MediaItemRequestType.UNION);
        }

        private void MenuItem_Single(object sender, RoutedEventArgs e)
        {
            this.GetMediaItems(MediaItemRequestType.SINGLE);
        }

        private void CategoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.GetMediaItems(this.MenuItemIntersect.IsChecked ? MediaItemRequestType.INTERSECT : (this.MenuItemSingle.IsChecked ? MediaItemRequestType.SINGLE : MediaItemRequestType.UNION));
        }

        private void GetMediaItems(MediaItemRequestType mediaItemRequestType)
        {
            if (CategoryListBox.Items.Count == 0)
                return;

            this.MenuItemUnion.IsChecked = mediaItemRequestType == MediaItemRequestType.UNION;
            this.MenuItemIntersect.IsChecked = mediaItemRequestType == MediaItemRequestType.INTERSECT;
            this.MenuItemSingle.IsChecked = mediaItemRequestType == MediaItemRequestType.SINGLE;

            MediaItemCategoryRequest categoryRequest = new MediaItemCategoryRequest();
            categoryRequest.RequestType = mediaItemRequestType;

            foreach (Category category in CategoryListBox.Items)
                categoryRequest.AddCategory(category);

            if (this.OnRequest != null && categoryRequest.IsValid)
            {
                this.SetCategoryTreeProperty(categoryRequest.Categories[0].FullPath);
                this.lastRequestedCategory = categoryRequest.Categories[0].FullPath;
                this.OnRequest(this, new MediaItemRequestMessageArgs(categoryRequest));
            }
        }

        #endregion

        #region events
        private void GridSplitter_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeNS;
        }

        private void GridSplitter_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void CategoryListBox_KeyDown(object sender, KeyEventArgs e)
        {
            bool controlKey = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            switch (e.Key)
            {
                case Key.U:
                    if (controlKey)
                        this.GetMediaItems(MediaItemRequestType.UNION);
                    break;

                case Key.I:
                    if (controlKey)
                        this.GetMediaItems(MediaItemRequestType.INTERSECT);
                    break;

                case Key.Delete:
                    if (controlKey)
                        MenuItem_RemoveAll(this.CategoryListBox, null);
                    else
                        MenuItem_Remove(this.CategoryListBox, null);
                    break;

                case Key.Enter:
                    this.GetMediaItems(this.MenuItemIntersect.IsChecked ? MediaItemRequestType.INTERSECT : (this.MenuItemSingle.IsChecked ? MediaItemRequestType.SINGLE : MediaItemRequestType.UNION));
                    break;
            }
        }

        private void CategoryAutoCompleter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetFromAutocompleter();
            }
        }

        private void CategoryEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    this.EditCategoryExpander.IsExpanded = false;
                    this.CategoryEditor.IsNew = false;
                    this.CategoryTreeExpander.IsExpanded = true;
                    this.treeViewMain.Focus();
                    break;
            }
        }

        private void treeViewMain_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F12:
                    if (MediaBrowserWPF.Utilities.CategoryTreeWebPage.IsIMP)
                    {
                        Category cat = MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.FirstOrDefault(x => x.Name == "InMediasP Portal");

                        if (cat != null)
                        {
                            MainWindow.BussyIndicatorContent = "Erstelle IMP Webseite";
                            MainWindow.BussyIndicatorIsBusy = true;

                            Thread thread = new Thread(() =>
                            {
                                using (MediaBrowserWPF.Utilities.CategoryTreeWebPage catTree = new Utilities.CategoryTreeWebPage(cat))
                                {
                                    catTree.OnUpdate += new EventHandler<MediaItemCallbackArgs>(catTree_OnUpdate);
                                    catTree.Start();
                                }

                            });

                            thread.IsBackground = true;
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.Start();
                        }
                    }
                    break;

                case Key.Delete:
                    this.CategoryEditor.Delete();
                    break;

                case Key.F:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.EditCategoryExpander.IsExpanded = false;
                        this.CategoryCollectionExpander.IsExpanded = true;
                        this.CategoryTreeExpander.IsExpanded = true;
                        this.CategoryAutoCompleter.Focus();
                        this.UpdateLayout();
                    }
                    break;

                case Key.F2:
                    this.EditCategoryExpander.IsExpanded = true;
                    this.CategoryCollectionExpander.IsExpanded = false;

                    if (this.CategoryEditor.IsNew)
                    {
                        this.CategoryEditor.IsNew = false;
                        this.CategoryEditor.Category = this.treeViewMain.SelectedItem as Category;
                    }
                    this.CategoryEditor.IsEdit = true;
                    break;

                case Key.F5:
                    MediaBrowserContext.ResetCategoryTree();
                    this.Build();
                    break;

                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.EditCategoryExpander.IsExpanded = true;
                        this.CategoryCollectionExpander.IsExpanded = false;
                        this.CategoryEditor.IsNew = true;
                        this.CategoryEditor.Category = this.treeViewMain.SelectedItem as Category;
                    }
                    break;

                case Key.Enter:
                    this.GetSingleFromTree();
                    break;
            }
        }

        void catTree_OnUpdate(object sender, MediaItemCallbackArgs e)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                MainWindow.BussyIndicatorContent = "Bearbeite Webseit: " + e.Pos + " von " + e.MaxCount + " Dateien ...";

                if (e.UpdateDone)
                    MainWindow.BussyIndicatorIsBusy = false;
            }));
        }

        private void CategoryAutoCompleter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SetFromAutocompleter();
        }

        private void SetFromAutocompleter()
        {
            Category category = this.CategoryAutoCompleter.SelectedItem;

            if (category != null)
            {
                this.treeViewHelper.CollapseAll();
                this.ExpandPath(category.FullPath);

                if (!this.CategoryListBox.Items.Contains(category))
                {
                    this.CategoryListBox.Items.Add(category);
                }

                this.CategoryAutoCompleter.Focus();
            }
        }

        private void MenuItem_Remove(object sender, RoutedEventArgs e)
        {
            while (this.CategoryListBox.SelectedItems.Count != 0)
                this.CategoryListBox.Items.Remove(this.CategoryListBox.SelectedItem);
        }

        private void MenuItem_RemoveAll(object sender, RoutedEventArgs e)
        {
            while (this.CategoryListBox.Items.Count != 0)
                this.CategoryListBox.Items.Remove(this.CategoryListBox.Items[0]);
        }

        private void MenuItemExpand_Click(object sender, RoutedEventArgs e)
        {
            foreach (Category category in this.CategoryListBox.Items.Cast<Category>().ToArray())
            {
                if (category.Children.Count > 0)
                {
                    this.CategoryListBox.Items.Remove(category);
                    foreach (Category categoryChild in category.Children)
                    {
                        this.CategoryListBox.Items.Add(categoryChild);
                    }
                }
            }
        }

        private void MenuItemShowInTree_Click(object sender, RoutedEventArgs e)
        {
            if (this.CategoryListBox.SelectedItems.Count == 1)
            {
                this.CategoryTreeExpander.IsExpanded = true;
                this.treeViewHelper.CollapseAll();
                this.ExpandPath(((Category)this.CategoryListBox.SelectedItem).FullPath);
            }
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

                Category category = (Category)obj;

                DataObject dragData = new DataObject("Category", category);
                DragDrop.DoDragDrop(treeViewItem, dragData, DragDropEffects.Copy);
            }
        }

        private void CategoryListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Category"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void CategoryListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Category"))
            {
                Category category = e.Data.GetData("Category") as Category;

                if (!this.CategoryListBox.Items.Contains(category))
                {
                    this.CategoryListBox.Items.Add(category);
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
        #endregion

        private void ContextMenu_Selected_Categories_Opened(object sender, RoutedEventArgs e)
        {
            this.MenuItemUnion.IsEnabled = (this.CategoryListBox.Items.Count > 0);
            this.MenuItemIntersect.IsEnabled = (this.CategoryListBox.Items.Count > 0);
            this.MenuItemSingle.IsEnabled = (this.CategoryListBox.Items.Count > 0);
            this.MenuItemExpand.IsEnabled = (this.CategoryListBox.SelectedItems.Count > 0);
            this.MenuItemRemove.IsEnabled = (this.CategoryListBox.SelectedItems.Count == 1);
            this.MenuItemShowInTree.IsEnabled = (this.CategoryListBox.SelectedItems.Count == 1);
            this.MenuItemRemoveAll.IsEnabled = (this.CategoryListBox.Items.Count > 0);
        }

        private double defaultExpanderColapsedHeight;
        List<Expander> expanderList;

        private void SetExpanderSize()
        {
            if (this.EditCategoryExpander == null
                || this.CategoryTreeExpander == null
                || this.CategoryCollectionExpander == null)
                return;

            if (this.defaultExpanderColapsedHeight == 0 && this.EditCategoryExpander.RenderSize.Height > 0)
            {
                this.defaultExpanderColapsedHeight = this.EditCategoryExpander.RenderSize.Height;

                this.expanderList = new List<Expander>();
                this.expanderList.Add(this.EditCategoryExpander);
                this.expanderList.Add(this.CategoryTreeExpander);
                this.expanderList.Add(this.CategoryCollectionExpander);
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

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.SetExpanderSize();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            this.SetExpanderSize();

            Action action = () =>
            {
                this.CategoryEditor.Focus();
            };

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, action);
        }

        private void CategoryEditor_NewCategory(object sender, CategoryContainer.CategoryEventArgs e)
        {
            this.ExpandPath(e.Category.FullPath);

            //if (this.NewCategory != null)
            //    this.NewCategory.Invoke(this, e);
        }

        private void CategoryEditor_DeleteCategory(object sender, CategoryContainer.CategoryEventArgs e)
        {
            if (this.DeleteCategory != null)
                this.DeleteCategory.Invoke(this, e);

            this.CategoryListBox.Items.Remove(e.Category);
        }

        private void CategoryEditor_RenameCategory(object sender, CategoryContainer.CategoryEventArgs e)
        {
            if (this.RenameCategory != null)
                this.RenameCategory.Invoke(this, e);

            int index = this.CategoryListBox.Items.IndexOf(e.Category);

            if (index >= 0)
            {
                this.CategoryListBox.Items.Remove(e.Category);
                this.CategoryListBox.Items.Insert(index, e.Category);
            }
        }

        private void MenuItemCollapseAll_Click(object sender, RoutedEventArgs e)
        {
            this.ToggleCollaps();
        }

        private bool recentFoldersChanged = true;
        private void BuildRecentFolders()
        {
            if (!this.recentFoldersChanged)
                return;

            if (MediaBrowserContext.GetDBProperty("InitialCategoryTreePath") != null
                && MediaBrowserContext.GetDBProperty("InitialCategoryTreePath").Trim().Length > 0)
            {
                List<string> parts = new List<string>(MediaBrowserContext.GetDBProperty("InitialCategoryTreePath").Split(';').Reverse());
                this.MenuItemRecentCategories.Visibility = System.Windows.Visibility.Visible;
                this.MenuItemRecentCategories.Items.Clear();

                foreach (string part in parts)
                {
                    bool found = false;
                    foreach (Category category in MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection)
                    {
                        if (category.FullPath.StartsWith(part, StringComparison.InvariantCultureIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        MenuItem newItem = new MenuItem();
                        newItem.Header = part;
                        newItem.Click += new RoutedEventHandler(MenuItemRecentCategoryNewItem_Click);
                        this.MenuItemRecentCategories.Items.Add(newItem);
                    }
                    else
                    {
                        MediaBrowserContext.SetDBProperty("InitialCategoryTreePath", String.Join(";",
                            this.RemoveCategoryTreeProperty(part)));
                    }
                }
            }
            else
            {
                this.MenuItemRecentCategories.Visibility = System.Windows.Visibility.Collapsed;
            }

            this.recentFoldersChanged = false;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            this.BuildRecentFolders();
        }

        void MenuItemRecentCategoryNewItem_Click(object sender, RoutedEventArgs e)
        {
            this.treeViewHelper.CollapseAll();
            this.ExpandPath(((MenuItem)sender).Header.ToString());
        }

        private void SetCategoryTreeProperty(string path)
        {
            this.recentFoldersChanged = true;
            List<string> parts = this.RemoveCategoryTreeProperty(path);

            parts.Add(path);

            while (parts.Count > 8)
            {
                parts.RemoveAt(0);
            }

            MediaBrowserContext.SetDBProperty("InitialCategoryTreePath", String.Join(";", parts));
        }

        private List<string> RemoveCategoryTreeProperty(string path)
        {
            List<string> parts = null;
            if (MediaBrowserContext.GetDBProperty("InitialCategoryTreePath") != null
                && MediaBrowserContext.GetDBProperty("InitialCategoryTreePath").Trim().Length > 0)
            {
                parts = new List<string>(MediaBrowserContext.GetDBProperty("InitialCategoryTreePath").Split(';'));
            }
            else
            {
                parts = new List<string>();
            }

            if (parts.Contains(path))
                parts.Remove(path);
            return parts;
        }

        private string GetCategoryProperty()
        {
            List<string> parts = null;
            if (MediaBrowserContext.GetDBProperty("InitialCategoryTreePath") != null
                && MediaBrowserContext.GetDBProperty("InitialCategoryTreePath").Trim().Length > 0)
            {
                parts = new List<string>(MediaBrowserContext.GetDBProperty("InitialCategoryTreePath").Split(';'));
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

        private void Button_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (sender != null && ((FrameworkElement)(sender)).DataContext is Category)
            {
                Category category = ((Category)(((FrameworkElement)(sender)).DataContext));
                StackPanel sp = sender as StackPanel;

                sp.ToolTip =
                    $"Medien: {category.ItemCount:n0}" + Environment.NewLine +
                    $"alle Medien: {category.ItemCountRecursive:n0}" + Environment.NewLine +
                    $"alle Kategorien: {category.ChildrenCount:n0}";
            }
        }

        private bool calendarChange = false;
        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            DatePicker datePicker = sender as DatePicker;

            if (datePicker.SelectedDate != null)
            {
                DateTime selectedDate = datePicker.SelectedDate.Value.Date.AddDays(.5);

                if (isLoaded && !calendarChange)
                {
                    calendarChange = true;

                    var orderedCategories = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Where(x => x.IsDate && x.Children.Count == 0 && !x.FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName))
                                 .OrderBy(x => x.Date);

                    Category maxCategory = orderedCategories.FirstOrDefault(x => x.Date >= selectedDate);
                    Category minCategory = orderedCategories.OrderByDescending(x => x.Date).FirstOrDefault(x => x.Date <= selectedDate);
                    maxCategory = maxCategory ?? minCategory;
                    minCategory = minCategory ?? maxCategory;

                    if (minCategory != null && maxCategory != null)
                    {
                        Category selectedCategory = minCategory;
                        if (minCategory != maxCategory)
                        {
                            long maxDiff = System.Math.Max(selectedDate.Ticks, maxCategory.Date.Ticks) - System.Math.Min(selectedDate.Ticks, maxCategory.Date.Ticks);
                            long minDiff = System.Math.Max(selectedDate.Ticks, minCategory.Date.Ticks) - System.Math.Min(selectedDate.Ticks, minCategory.Date.Ticks);

                            if (maxDiff >= minDiff)
                            {
                                selectedCategory = minCategory;
                            }
                            else
                            {
                                selectedCategory = maxCategory;
                            }
                        }

                        treeViewHelper.CollapseAll();
                        datePicker.SelectedDate = selectedCategory.Date;
                        ExpandCategory(selectedCategory);
                        this.ExpandPath(selectedCategory.FullPath);
                        this.CategoryEditor.Category = selectedCategory;
                    }

                    calendarChange = false;
                }
            }
        }

        private void CalendarCategory_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (MediaBrowserContext.UpdateCatecoryCalendar)
            {
                this.BlackoutCalendar();
                this.CalendarCategory.SelectedDate = null;
                MediaBrowserContext.UpdateCatecoryCalendar = false;
            }
        }
    }
}
