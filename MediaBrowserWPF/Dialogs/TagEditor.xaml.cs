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
using System.Windows.Shapes;
using MediaBrowser4.Objects;
using MediaBrowserWPF.UserControls;
using MediaBrowser4;
using MediaBrowser4.Utilities;
using System.Windows.Threading;

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für TagEditor.xaml
    /// </summary>
    public partial class TagEditor : Window
    {
        private List<MediaItem> mediaItems;

        public TagEditor()
        {
            InitializeComponent();
        }


        public IEnumerable<Category> AllCategories
        {
            get
            {
                return MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;
            }
        }

        public Category SelectedCategory { get; set; }

        public AutoCompleteFilterPredicate<object> CategoryFilter
        {
            get
            {
                return (searchText, obj) =>
                !searchText.Trim().Contains(' ') ? (obj as Category).Name.ToLower().Contains(searchText.ToLower()) && !(obj as Category).FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName) :
                searchText.ToLower().Replace(":","").Replace("<", "").Split(' ').Where(x => x.Length >= 2).All(x => (obj as Category).FullName.ToLower().Contains(x) && !(obj as Category).FullPath.StartsWith(MediaBrowserContext.CategoryHistoryName));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = this;

            //entspricht einem Tastatur-TAB und bringt den Fokus in das nested Controll
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        new public void Close()
        {
            this.TagPanel.Children.Clear();
            this.Visibility = System.Windows.Visibility.Collapsed;
            this.SelectedCategory = null;
            this.AutoCompleteBoxCategories.Text = string.Empty;
        }

        public List<MediaItem> MediaItemList
        {
            set
            {
                this.mediaItems = value;

                this.TagPanel.Children.Clear();
                this.SelectedCategory = null;

                this.AutoCompleteBoxCategories.Text = null;
                this.AutoCompleteBoxCategories.SelectedItem = null;

                Mouse.OverrideCursor = Cursors.Wait;

                foreach (KeyValuePair<Category, int> kv in MediaBrowserContext
                .GetCategoriesFromMediaItems(this.mediaItems).OrderBy(x => x.Key.IsDate).ThenBy(x => x.Key.IsLocation).ThenBy(x => x.Key.Date).ThenBy(x => x.Key.FullPath))
                {
                    this.AddTag(kv.Key, kv.Value);
                }

                Mouse.OverrideCursor = null;
            }

            get
            {
                return this.mediaItems;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    this.Close();
                    break;
                case Key.T:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.Close();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void CategoryAutoCompleter_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            SetInfoLabel();

            if (e.Key == Key.Enter)
               this.Categorize();
        }

        private void AutoCompleteBoxCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetInfoLabel();
        }

        private void SetInfoLabel()
        {
            if (this.SelectedCategory != null)
            {     
                this.labelInfoBorder.Visibility = Visibility.Visible;
                this.labelInfo.Content = this.SelectedCategory.Name + " " + this.SelectedCategory.BreadCrumpReverse;
            }
            else
            {
                this.labelInfoBorder.Visibility = Visibility.Hidden;
                this.labelInfo.Content = String.Empty;
            }
        }

        private void Categorize()
        {
            Category category = this.SelectedCategory;

            try
            {
                if (this.SelectedCategory == null)
                {
                    if (this.AutoCompleteBoxCategories.Text == null || this.AutoCompleteBoxCategories.Text.IndexOf('\\') >= 0)
                        return;

                    foreach (Tag tag in this.TagPanel.Children)
                        if (tag.Equals(MediaBrowserContext.CategoryTagParent + "\\" + this.AutoCompleteBoxCategories.Text.Trim()))
                            return;

                    category = new Category(MediaBrowserContext.CategoryTreeSingelton);
                    category.Name = this.AutoCompleteBoxCategories.Text.Trim();
                    category.Parent = MediaBrowserContext.CategoryTagParent;

                    MediaBrowserContext.SetCategory(category);
                    MediaBrowserContext.CategoryTreeSingelton.ChangeVersion++;
                }
                else
                {
                    Tag removeTag = null;
                    foreach (Tag tag in this.TagPanel.Children)
                        if (tag.Equals(category))
                            if (tag.Count == this.MediaItemList.Count)
                            {
                                this.Close();
                                return;
                            }
                            else
                            {
                                removeTag = tag;
                                break;
                            }

                    if (removeTag != null)
                        this.TagPanel.Children.Remove(removeTag);
                }

                this.AddTag(category, 0);
                MediaBrowserContext.CategorizeMediaItems(this.MediaItemList, new List<Category>() { category });
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message,
                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            this.Close();
        }

        private void AddTag(Category category, int count)
        {
            if (category != null)
            {
                Tag label = new Tag(category, count);
                label.RemoveCategory += new EventHandler(label_RemoveCategory);
                label.Margin = new Thickness(2);
                this.TagPanel.Children.Add(label);
            }
        }

        void label_RemoveCategory(object sender, EventArgs e)
        {
            try
            {
                Category category = ((Tag)sender).Category;
                this.TagPanel.Children.Remove((Tag)sender);
                MediaBrowserContext.UnCategorizeMediaItems(this.MediaItemList, new List<Category>() { category });
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message,
                    "Ein Fehler ist aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            this.Close();
        }    
    }
}
