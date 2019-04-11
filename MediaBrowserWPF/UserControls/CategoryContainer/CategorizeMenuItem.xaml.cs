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
using MediaBrowser4;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.UserControls.CategoryContainer
{
    public class CategoryMenuItem : MenuItem
    {
        public Category Category { get; private set; }

        public CategoryMenuItem(Category category)
        {
            this.Category = category;
            this.Header = category.Name;
            if (category.Children.Count == 0)
            {
                this.IsCheckable = true;
                this.StaysOpenOnClick = true;
            }
        }
    }

    public partial class CategorizeMenuItem : MenuItem
    {
        private Brush defaultForegroundBrush;
        private Brush defaultBackgroundBrush;
        private Brush checkedForegroundBrush = Brushes.LightGray;
        private List<MediaItem> mediaItemList = new List<MediaItem>();
        private Dictionary<Category, int> categoryStatistic = new Dictionary<Category, int>();
        public List<MediaItem> MediaItemList
        {
            get
            {
                return this.mediaItemList;
            }

            set
            {
                this.mediaItemList = value;

                this.categoryStatistic = new Dictionary<Category, int>();
                foreach (MediaItem item in value)
                {
                    foreach (Category cat in item.Categories)
                    {
                        if (!this.categoryStatistic.ContainsKey(cat))
                        {
                            this.categoryStatistic.Add(cat, 1);
                        }
                        else
                        {
                            this.categoryStatistic[cat]++;
                        }
                    }
                }
            }
        }

        public bool ShowCategorizeByDate { get; set; }
        public bool ShowRemoveAll { get; set; }

        public CategorizeMenuItem()
        {
            InitializeComponent();
            this.defaultForegroundBrush = this.Foreground;
            this.defaultBackgroundBrush = this.Background;
            this.SubmenuOpened += new RoutedEventHandler(menuItemCat_SubmenuOpened);
        }

        void menuItemCategorizeByDate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                MediaBrowserContext.CategoryTreeSingelton.CategorizeByExifDate(this.MediaItemList);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message,
                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    

        void MenuItemLastCategories_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (MediaBrowserContext.CopyItemProperties.Categories != null)
                    MediaBrowserContext.CategorizeMediaItems(this.MediaItemList, MediaBrowserContext.CopyItemProperties.Categories);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message,
                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        int countBaseItems;
        int changeVersion = -1;
        MenuItem menuItemCategorizeByDate;
        MenuItem MenuItemLastCategories;
        private void MenuItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (MediaBrowserContext.CategoryTreeSingelton.ChangeVersion == this.changeVersion)
                return;

            this.Items.Clear();
            this.changeVersion = MediaBrowserContext.CategoryTreeSingelton.ChangeVersion;

            this.menuItemCategorizeByDate = null;
            if (this.ShowCategorizeByDate)
            {
                this.menuItemCategorizeByDate = new MenuItem();
                this.menuItemCategorizeByDate.Header = "Erstelle Datums-Kategorien";
                this.menuItemCategorizeByDate.Name = "MenuItemCreateDateTimeCategories";
                this.menuItemCategorizeByDate.Click += new System.Windows.RoutedEventHandler(menuItemCategorizeByDate_Click);
                this.Items.Add(menuItemCategorizeByDate);
            }

            this.MenuItemLastCategories = null;  

            if (this.ShowRemoveAll)
            {
                MenuItem menuItem = new MenuItem();
                menuItem.Header = "Alle Kategorien Entfernen";
                menuItem.Click += new RoutedEventHandler(menuItem_Click);
                this.Items.Add(menuItem);
            }

            if (this.Items.Count > 0)
            {
                this.Items.Add(new Separator());
            }

            this.countBaseItems = this.Items.Count;

            foreach (Category category in MediaBrowserContext.CategoryTreeSingelton.Children)
            {
                CategoryMenuItem menuItemCat = SetNewCategoryMenuItem(this, category);

                foreach (Category category2 in category.Children)
                {
                    SetNewCategoryMenuItem(menuItemCat, category2);
                }
            }
        }

        void menuItem_Click(object sender, RoutedEventArgs e)
        {
              MessageBoxResult result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Möchten Sie wirklich alle Kategorien entfernen?",
                "Kategorien entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question);

              if (result == MessageBoxResult.Yes)
              {
                  MediaBrowserContext.UnCategorizeMediaItems(this.mediaItemList,
                      this.mediaItemList.SelectMany(x => x.Categories).Distinct().ToList());
              }
        }

        void menuItemCat_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (this.MenuItemLastCategories != null)
            {
                this.MenuItemLastCategories.IsEnabled =
                    MediaBrowserContext.CopyItemProperties.Categories != null && MediaBrowserContext.CopyItemProperties.Categories.Count > 0;

                if (MediaBrowserContext.CopyItemProperties.Categories != null && MediaBrowserContext.CopyItemProperties.Categories.Count > 0)
                {
                    this.MenuItemLastCategories.IsEnabled = true;
                    this.MenuItemLastCategories.ToolTip = String.Join(", ", MediaBrowserContext.CopyItemProperties.Categories.Select(x => x.Name))
                        + "\r\n(Mausgeste nach unten und links)";
                    this.MenuItemLastCategories.Header = String.Format("Übernehme kopierte Kategorien ({0}x)", MediaBrowserContext.CopyItemProperties.Categories.Count);
                }
                else
                {
                    this.MenuItemLastCategories.IsEnabled = false;
                    this.MenuItemLastCategories.ToolTip = null;
                    this.MenuItemLastCategories.Header = "Übernehme kopierte Kategorien";
                }
            }

            MenuItem categoryMenuItem = sender as MenuItem;

            if (categoryMenuItem != null)
            {
                foreach (object obj in categoryMenuItem.Items)
                {
                    CategoryMenuItem categoryBase = obj as CategoryMenuItem;
                    if (categoryBase != null)
                    {
                        categoryBase.Foreground = this.defaultForegroundBrush;
                        categoryBase.Background = this.defaultBackgroundBrush;
                        if (this.categoryStatistic.ContainsKey(categoryBase.Category))
                        {
                            categoryBase.IsChecked = true;
                            if (this.categoryStatistic[categoryBase.Category] == this.MediaItemList.Count)
                            {
                                categoryBase.Background = this.checkedForegroundBrush;
                            }
                        }
                        else
                        {
                            categoryBase.IsChecked = false;
                        }

                        if (categoryBase.Items.Count == 0)
                        {
                            foreach (Category category in categoryBase.Category.Children)
                            {
                                SetNewCategoryMenuItem(categoryBase, category);
                            }
                        }
                    }
                }
            }
        }

        private CategoryMenuItem SetNewCategoryMenuItem(MenuItem baseItem, Category category)
        {
            CategoryMenuItem menuItemCat = new CategoryMenuItem(category);
            menuItemCat.SubmenuOpened += new RoutedEventHandler(menuItemCat_SubmenuOpened);

            if (menuItemCat.IsCheckable)
                menuItemCat.Click += new RoutedEventHandler(menuItemCat_Click);

            baseItem.Items.Add(menuItemCat);

            return menuItemCat;
        }

        void menuItemCat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CategoryMenuItem categoryMenuItem = sender as CategoryMenuItem;

                if (categoryMenuItem != null)
                {
                    if (categoryMenuItem.IsChecked)
                    {
                        MediaBrowserContext.CategorizeMediaItems(this.MediaItemList, new List<Category>() { categoryMenuItem.Category });
                        categoryMenuItem.Background = this.checkedForegroundBrush;
                    }
                    else
                    {
                        MediaBrowserContext.UnCategorizeMediaItems(this.MediaItemList, new List<Category>() { categoryMenuItem.Category });
                        categoryMenuItem.Foreground = this.defaultForegroundBrush;
                        categoryMenuItem.Background = this.defaultBackgroundBrush;
                    }
                }              
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message,
                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
