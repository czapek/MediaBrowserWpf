using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MediaBrowser4;
using MediaBrowser4.Objects;
using System.Windows.Input;
namespace MediaBrowserWPF.UserControls.FolderContainer
{
    /// <summary>
    /// Interaktionslogik für FolderEditor.xaml
    /// </summary>
    public partial class FolderEditor : UserControl
    {
        public event EventHandler<FolderEventArgs> RenameFolder;
        public event EventHandler<FolderEventArgs> DeleteFolder;
        public event EventHandler<FolderEventArgs> NewFolder;
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

        public FolderEditor()
        {
            InitializeComponent();
        }

        public void Clear()
        {
            this.Folder = null;

            this.FolderAutoCompleter.Text = String.Empty;
            this.FolderAutoCompleter.ItemsSource = MediaBrowser4.MediaBrowserContext.FolderTreeSingelton.FullFolderCollection;
            this.FolderAutoCompleter.AutoCompleteManager.DataProvider = new FolderAutoCompleteProvider(MediaBrowser4.MediaBrowserContext.FolderTreeSingelton.FullFolderCollection);
            this.FolderAutoCompleter.AutoCompleteManager.AutoAppend = true;
        }

        Folder lastFolder;
        public Folder Folder
        {
            get
            {
                return this.DataContext as Folder;
            }

            set
            {
                if (this.isSaving)
                    return;

                Folder folder = value == null ? new Folder(MediaBrowserContext.FolderTreeSingelton) : value;

                if (!this.CheckBoxCreateNew.IsChecked.Value)
                {
                    this.FolderAutoCompleter.Text = folder.Parent != null ? folder.Parent.FullPath : String.Empty;
                    this.DataContext = folder;
                    this.ButtonSave.IsEnabled = folder.Id != 0;
                    this.ButtonDelete.IsEnabled = folder.Id != 0;
                }
                else
                {
                    this.FolderAutoCompleter.Text = folder.FullPath;
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
            string messageHeader = this.CheckBoxCreateNew.IsChecked.Value ? "Ordner umbenennen" : "Ordner neu erstellen";

            if (this.TextBoxName.Text.Trim().Replace("\\", "").Length == 0)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Wählen Sie einen Namen für den Ordner!",
                    messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                this.isSaving = false;
                return;
            }

            if (this.FolderAutoCompleter.SelectedItem == this.Folder)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Elternknoten und Kindknoten können nicht gleich sein!",
                    messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                this.isSaving = false;
                return;
            }

            FolderCollection folderCollection = MediaBrowserContext.FolderTreeSingelton.Children;

            if (this.FolderAutoCompleter.SelectedItem != null)
            {
                folderCollection = this.FolderAutoCompleter.SelectedItem.Children;
            }

            if (folderCollection.FirstOrDefault(x => x != this.Folder &&
                x.Name.Equals(this.TextBoxName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase)) != null)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Der Name ist auf dieser Ebene schon vergeben!",
                    messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                this.isSaving = false;
                return;
            }


            Folder folderParentByTextBox = null;
            if (this.FolderAutoCompleter.SelectedItem == null && this.FolderAutoCompleter.Text.Trim().Length > 0)
            {
                FolderCollection children = MediaBrowserContext.FolderTreeSingelton.Children;

                foreach (string part in MediaBrowser4.Objects.FolderTree.GetPathParts(this.FolderAutoCompleter.Text.Trim()))
                {             
                    if (!String.IsNullOrWhiteSpace(part))
                    {
                        Folder folderChild = children.FirstOrDefault(x => x.Name.Equals(part.Trim(), StringComparison.InvariantCultureIgnoreCase));

                        if (folderChild == null)
                        {
                            folderChild = new Folder(part, folderParentByTextBox, MediaBrowserContext.FolderTreeSingelton);
                            children.Add(folderChild);
                        }

                        children = folderChild.Children;
                        folderParentByTextBox = folderChild;
                    }
                }

                if (folderParentByTextBox == null && !String.IsNullOrWhiteSpace(this.FolderAutoCompleter.Text))
                {
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Der Elternknoten konnte nicht gefunden werden!",
                        messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.isSaving = false;
                    return;
                }
            }


            string testName = this.TextBoxName.Text.Trim();
            if (this.FolderAutoCompleter.SelectedItem == null)
            {
                if (testName.StartsWith("\\\\"))
                    testName = testName.Substring(2);
                else if (testName.EndsWith(":"))
                    testName = testName.TrimEnd(':');
            }

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                if (testName.Contains(c))
                {
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Ungültiges Zeichen im Namen!",
                            messageHeader, MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.isSaving = false;
                    return;
                }
            }

            Folder newParentFolder = this.FolderAutoCompleter.SelectedItem as Folder;

            if (!this.CheckBoxOnlyDb.IsChecked.Value && newParentFolder != null)
            {
                try
                {
                    string newpath = newParentFolder.FullPath + "\\" + this.TextBoxName.Text.Trim();

                    if (this.CheckBoxCreateNew.IsChecked.Value)
                    {
                        if (!System.IO.Directory.Exists(newpath))
                            System.IO.Directory.CreateDirectory(newpath);
                    }
                    else
                    {
                        if (System.IO.Directory.Exists(this.Folder.FullPath)
                            && !this.Folder.FullPath.Equals(newpath, StringComparison.InvariantCultureIgnoreCase))
                            System.IO.Directory.Move(this.Folder.FullPath, newpath);
                    }

                }
                catch (Exception ex)
                {
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, ex.Message,
                            messageHeader, MessageBoxButton.OK, MessageBoxImage.Error);
                    MessageBox.Show(MainWindow.MainWindowStatic, ex.Message);
                    this.isSaving = false;
                    return;
                }
            }

            this.Folder.Parent = newParentFolder ?? folderParentByTextBox;
            this.Folder.Name = this.TextBoxName.Text.Trim();

            bool isNew = this.Folder.Id <= 0;

            try
            {
                MediaBrowserContext.SetFolder(this.Folder);
                this.lastFolder = this.Folder;
                this.CheckBoxCreateNew.IsChecked = false;

                MediaBrowserContext.FolderTreeSingelton.FullFolderCollection.Remove(this.Folder);
                MediaBrowserContext.FolderTreeSingelton.FullFolderCollection.Add(this.Folder);

                this.isSaving = false;

                if (isNew)
                {
                    if (this.NewFolder != null)
                        this.NewFolder.Invoke(this, new FolderEventArgs(this.Folder));
                }
                else
                {
                    if (this.RenameFolder != null)
                        this.RenameFolder.Invoke(this, new FolderEventArgs(this.Folder));
                }
            }
            catch (Exception ex)
            {
                if (isNew)
                {
                    MediaBrowserContext.FolderTreeSingelton.FullFolderCollection.Remove(this.Folder);
                    this.Folder.Remove();
                    this.Folder = this.lastFolder;
                }

                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, ex.Message,
                            messageHeader, MessageBoxButton.OK, MessageBoxImage.Error);
            }


            this.isSaving = false;
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            this.Delete(this.Folder);
        }

        public void DeleteFull(Folder folder)
        {
            this.CheckBoxOnlyDb.IsChecked = false;
            this.Delete(this.Folder);
        }

        public void DeleteDb(Folder folder)
        {
            this.CheckBoxOnlyDb.IsChecked = true;
            this.Delete(this.Folder);
        }

        public void DeleteDbRecursive(Folder folder)
        {
            if (folder == null)
            {
                return;
            }

            List<Folder> folderList = folder.ChildrenRecursive().Where(x => x.Id > 0).ToList();
            int countItems = folderList.Sum(x => MediaBrowserContext.CountItemsInFolder(x));

            if (countItems > 100 || folderList.Count > 10)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Aus Sicherheitsgründen werden maximal 10 Ordner mit maximal 100 Medien rekursiv gelöscht."
                    + "\r\n\r\nSie möchten " + folderList.Count + " Ordner von der Datenbank löschen. "
                    + "Diese verwalten " + countItems + " Medien in der Datenbank. Löschen Sie zuerst die Medien oder Gruppen von Unterordnern.",
                  "Löschen verweigert", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,
                "Möchten Sie diese " + folderList.Count + " Ordner von der Datenbank löschen?\r\n\r\n"
                + (countItems > 0 ? "Sie verwalten " + countItems + " Medien in der Datenbank.\r\n\r\n" : String.Empty)
                + String.Join("\r\n", folderList.Select(x => x.FullPath)),
                "Ordner rekursiv löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (Folder f in folderList)
                    this.RemoveFolderFromDb(f);
            }
        }

        public void Delete(Folder folder)
        {
            if (folder == null || folder.Id < 0)
            {
                return;
            }

            int countItems = MediaBrowserContext.CountItemsInFolder(folder);

            MessageBoxResult result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Möchten Sie diesen einzelnen Ordner "
                + (this.CheckBoxOnlyDb.IsChecked.Value ? "von der Datenbank" : "vollständig (Datenträger und Datenbank)") + " löschen?\r\n\r\n" + folder
                + (countItems > 0 ? "\r\n\r\nEr verwaltet " + countItems + " Medien in der Datenbank." : String.Empty),
                "Ordner löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (!this.CheckBoxOnlyDb.IsChecked.Value)
                {
                    if (System.IO.Directory.Exists(folder.FullPath) &&
                        System.IO.Directory.GetDirectories(folder.FullPath).Length > 0)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Der Ordner enthält Unterordner auf dem Dateisystem. Löschen Sie diese zuerst oder treffen Sie zum Löschen die Auswahl 'nur DB'.",
                          "Ordner löschen", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (System.IO.Directory.Exists(folder.FullPath))
                    {
                        if (System.IO.Directory.GetFiles(folder.FullPath).Length > 0 && !(System.IO.Directory.GetFiles(folder.FullPath).Length == 1
                            && System.IO.Path.GetFileName(System.IO.Directory.GetFiles(folder.FullPath)[0]) == "Thumbs.db"))
                        {
                            string[] allFiles = System.IO.Directory.GetFiles(folder.FullPath);

                            result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Der Ordner enthält " + allFiles.Length
                                + " Datei" + (allFiles.Length > 1 ? "en" : String.Empty) + " (" + String.Join(", ", allFiles.Select(x => "*"
                                    + System.IO.Path.GetExtension(x).ToLower()).Distinct().OrderBy(x => x)) + ")"
                                + ".\r\n\r\nMöchten Sie diese löschen?", "Ordner mit Inhalt löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                try
                                {
                                    MediaBrowser4.Utilities.RecycleBin.Recycle(folder.FullPath, false);
                                }
                                catch (Exception ex)
                                {
                                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, ex.Message,
                                        "Ordner löschen", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            try
                            {
                                MediaBrowser4.Utilities.RecycleBin.Recycle(folder.FullPath, false);
                            }
                            catch (Exception ex)
                            {
                                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, ex.Message,
                                    "Ordner löschen", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }

                this.RemoveFolderFromDb(folder);
            }
        }

        private void RemoveFolderFromDb(Folder folder)
        {
            MediaBrowserContext.RemoveFolder(folder);

            if (this.DeleteFolder != null)
                this.DeleteFolder.Invoke(this, new FolderEventArgs(folder));

            if (folder.Children.Count == 0)
            {
                Folder removeFolder = folder.Parent;
                MediaBrowserContext.FolderTreeSingelton.Remove(folder);

                while (removeFolder != null
                    && removeFolder.Children.Count == 0 && removeFolder.Id <= 0)
                {
                    MediaBrowserContext.FolderTreeSingelton.Remove(removeFolder);
                    removeFolder = removeFolder.Parent;
                }
            }

            this.Folder = null;
        }

        private void ButtonFolderDialog_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = this.FolderAutoCompleter.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.FolderAutoCompleter.Text = dialog.SelectedPath;
            }
        }

        private void CheckBoxCreateNew_Checked(object sender, RoutedEventArgs e)
        {
            this.lastFolder = this.Folder;
            this.DataContext = new Folder(MediaBrowserContext.FolderTreeSingelton);
            this.TextBoxName.Text = String.Empty;
            this.ButtonDelete.IsEnabled = false;
            this.ButtonSave.IsEnabled = true;
        }

        private void CheckBoxCreateNew_Unchecked(object sender, RoutedEventArgs e)
        {
            this.FolderAutoCompleter.SelectedItem = this.lastFolder == null ? null : this.lastFolder.Parent;
            this.Folder = lastFolder;
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
