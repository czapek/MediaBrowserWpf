using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MediaBrowser4;
using MediaBrowser4.Objects;
using System.Windows.Input;

namespace MediaBrowserWPF.UserControls.CategoryContainer
{
    /// <summary>
    /// Interaktionslogik für CategoryEditor.xaml
    /// </summary>
    public partial class CategoryEditor : UserControl
    {
        public event EventHandler<CategoryEventArgs> NewCategory;
        public event EventHandler<CategoryEventArgs> RenameCategory;
        public event EventHandler<CategoryEventArgs> DeleteCategory;

        bool isSaving = false;

        public bool IsEdit
        {
            set
            {
                if (value)
                {
                    if (this.CheckBoxCreateNew.IsChecked.Value)
                    {
                        this.CheckBoxCreateNew.IsChecked = false;
                    }

                    this.TextBoxName.Focus();
                }
            }
        }

        public bool IsNew
        {
            set
            {
                this.CheckBoxCreateNew.IsChecked = value;
                this.TextBoxName.Focus();
            }

            get
            {
                return this.CheckBoxCreateNew.IsChecked.Value;
            }
        }

        new public void Focus()
        {
            base.Focus();
            this.TextBoxName.Focus();
            this.TextBoxName.CaretIndex = this.TextBoxName.Text.Length;
        }

        public CategoryEditor()
        {
            InitializeComponent();
        }

        public void Clear()
        {
            this.Category = null;

            this.CategoryAutoCompleter.Text = String.Empty;
            if (MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton != null)
            {
                this.CategoryAutoCompleter.ItemsSource = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;
                this.CategoryAutoCompleter.AutoCompleteManager.DataProvider = new CategoryAutoCompleteProvider(MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection);
                this.CategoryAutoCompleter.AutoCompleteManager.AutoAppend = true;
            }
            else
            {
                this.CategoryAutoCompleter.ItemsSource = null;
            }
        }

        Category lastCategory;
        public Category Category
        {
            get
            {
                return this.DataContext as Category;
            }

            set
            {
                if (this.isSaving)
                    return;

                this.ButtonCategoryDown.Content = ">";

                Category category = (value == null ? new Category(MediaBrowserContext.CategoryTreeSingelton) : value);

                if (!this.CheckBoxCreateNew.IsChecked.Value)
                {
                    this.CategoryAutoCompleter.SelectedItem = category.Parent;
                    this.DataContext = category;
                    this.ButtonSave.IsEnabled = category.Id != 0;
                    this.ButtonDelete.IsEnabled = category.Id != 0;
                }
                else
                {
                    this.CategoryAutoCompleter.SelectedItem = category;
                    this.ButtonSave.IsEnabled = true;
                    this.ButtonDelete.IsEnabled = false;
                }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Save()
        {
            this.isSaving = true;
            string messageHeader = this.CheckBoxCreateNew.IsChecked.Value ? "Kategorie umbenennen" : "Kategorie neu erstellen";
            Category category = this.Category;

            if (this.TextBoxName.Text.Trim().Length == 0)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Wählen Sie einen Namen für die Kategorie!",
                    messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);

                this.isSaving = false;
                return;
            }

            if (this.CategoryAutoCompleter.SelectedItem == category)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Elternknoten und Kindknoten können nicht gleich sein!",
                        messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                this.isSaving = false;
                return;
            }

            CategoryCollection catCol = MediaBrowserContext.CategoryTreeSingelton.Children;

            if (this.CategoryAutoCompleter.SelectedItem != null)
            {
                catCol = this.CategoryAutoCompleter.SelectedItem.Children;
            }
            else if (!String.IsNullOrWhiteSpace(this.CategoryAutoCompleter.Text))
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Der Elternknoten konnte nicht gefunden werden!",
                           messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                this.isSaving = false;
                return;
            }

            if (catCol.FirstOrDefault(x => x != category &&
                x.Name.Equals(this.TextBoxName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase)) != null)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Der Name ist auf dieser Ebene schon vergeben!",
                           messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                this.isSaving = false;
                return;
            }

            category.Parent = this.CategoryAutoCompleter.SelectedItem as Category;

            category.Name = this.TextBoxName.Text.Trim();
            category.Description = this.TextBoxDescription.Text.Trim();
            category.Sortname = this.TextBoxSort.Text.Trim();

            bool isNew = category.Id <= 0;

            try
            {
                MediaBrowserContext.SetCategory(category);
                MediaBrowserContext.CategoryTreeSingelton.ChangeVersion++;

                this.lastCategory = category;
                this.CheckBoxCreateNew.IsChecked = false;

                MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Remove(category);
                MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Add(category);

                this.isSaving = false;

                if (isNew)
                {
                    if (this.NewCategory != null)
                        this.NewCategory.Invoke(this, new CategoryEventArgs(category));
                }
                else
                {
                    if (this.RenameCategory != null)
                        this.RenameCategory.Invoke(this, new CategoryEventArgs(category));
                }
            }
            catch (Exception ex)
            {
                if (isNew)
                {
                    MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection.Remove(category);
                    category.Remove();
                    this.Category = this.lastCategory;
                }

                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message,
                            messageHeader, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            this.isSaving = false;
        }

        private void ButtonCategoryDown_Click(object sender, RoutedEventArgs e)
        {
            if ((String)ButtonCategoryDown.Content == ">")
            {
                this.CategoryAutoCompleter.SelectedItem = this.Category != null ? this.Category : this.lastCategory;
                this.ButtonCategoryDown.Content = "<";
            }
            else
            {
                this.CategoryAutoCompleter.SelectedItem = this.Category != null ? (this.Category.Parent != null ? this.Category.Parent : null) : null;
                this.ButtonCategoryDown.Content = ">";
            }
        }

        public void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            this.Delete();
        }

        public void Delete()
        {
            Category category = this.Category;

            if (category == null || category.Id <= 0)
                return;

            if (category.Children.Count > 0)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Die Kategorie enthält Unterkategorien. Löschen Sie diese zuerst.",
                           "Kategorien löschen", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            MessageBoxResult result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Möchten Sie die Kategorie löschen?\r\n" + category,
                "Kategorien löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (MediaBrowserContext.RemoveCategory(category))
                {
                    MediaBrowserContext.CategoryTreeSingelton.Remove(category);

                    if (this.DeleteCategory != null)
                        this.DeleteCategory.Invoke(this, new CategoryEventArgs(category));

                    this.DataContext = new Category(MediaBrowserContext.CategoryTreeSingelton);
                    this.Category = null;
                }
                else
                {
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Die Kategorie enthält Unterkategorien. Löschen Sie diese zuerst.",
                            "Kategorien löschen", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void CheckBoxCreateNew_Checked(object sender, RoutedEventArgs e)
        {
            this.lastCategory = this.Category;
            this.DataContext = new Category(MediaBrowserContext.CategoryTreeSingelton);
            this.TextBoxName.Text = String.Empty;
            this.TextBoxSort.Text = String.Empty;
            this.TextBoxDescription.Text = String.Empty;
            this.ButtonDelete.IsEnabled = false;
            this.ButtonSave.IsEnabled = true;
        }

        private void CheckBoxCreateNew_Unchecked(object sender, RoutedEventArgs e)
        {
            this.CategoryAutoCompleter.SelectedItem = this.lastCategory == null ? null : this.lastCategory.Parent;
            this.Category = lastCategory;
        }

        private void UserControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {  
            switch (e.Key)
            {
                case Key.Enter:
                    this.Save();
                    e.Handled = true;
                    break;

                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.IsNew = true;
                    break;
            }          
        }
    }
}
