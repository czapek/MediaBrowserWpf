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
using MediaBrowser4;
using Microsoft.Windows.Controls;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für AttachmentsContainer.xaml
    /// </summary>
    public partial class AttachmentsContainer : UserControl
    {
        private List<MediaItem> mediaItemList;
        private List<Description> descriptionList;
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;
        private bool? textChangedByUser = null;

        public AttachmentsContainer()
        {
            InitializeComponent();
        }

        public void SetInfo(List<MediaItem> mediaItemList)
        {
            if (mediaItemList != null)
            {
                if (this.textChangedByUser == true)
                {
                    if (MessageBoxResult.No == Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Ihre Änderungen an der Beschreibung gehen verloren.\nWollen Sie wirklich fortfahren?", "Vorsicht!",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning))
                        return;
                }

                this.Clear();
                this.mediaItemList = mediaItemList;
                this.Build();
            }
        }

        public void Build()
        {
            if (this.mediaItemList == null)
                return;

            Mouse.OverrideCursor = Cursors.Wait;

            this.RefreshAttachments();

            this.descriptionList = MediaBrowserContext.GetDescription(this.mediaItemList);

            this.MenuItemDescribeAll.IsEnabled = this.mediaItemList.Count > 0;
            this.MenuItemDeleteAllDescription.IsEnabled = this.mediaItemList.Count > 0;
            this.ButtonSaveDescription.IsEnabled = this.mediaItemList.Count > 0;
            this.ComboBoxDescription.ItemsSource = this.descriptionList;
            this.ComboBoxDescription.SelectedIndex = 0;

            this.SetNewDescription(0);

            Mouse.OverrideCursor = null;
        }

        private void RefreshAttachments()
        {
            if (mediaItemList == null)
                return;

            this.ListBoxAttachments.Items.Clear();
            foreach (MediaItem mItem in mediaItemList)
            {
                foreach (Attachment attachment in MediaBrowser4.Utilities.FilesAndFolders.GetAttachments(mItem))
                {
                    this.ListBoxAttachments.Items.Add(attachment);
                }
            }

            foreach (Attachment attachment in MediaBrowserContext.GetAttachment(mediaItemList))
            {
                this.ListBoxAttachments.Items.Add(attachment);
            }
        }

        private void SetNewDescription(int descriptionListPos)
        {
            if (descriptionListPos < 0 || (this.descriptionList.Count > 0 && descriptionListPos >= descriptionList.Count))
                return;

            if (this.descriptionList.Count <= 1)
            {
                this.ComboBoxDescription.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                this.ComboBoxDescription.Visibility = System.Windows.Visibility.Visible;
            }

            this.textChangedByUser = null;
            this.TextBoxDescription.Text = "";

            if (this.descriptionList.Count > 0)
            {
                this.MenuItemDeleteDescription.IsEnabled = true;
                this.textChangedByUser = null;
                this.TextBoxDescription.Text = descriptionList[descriptionListPos].Value;
                this.MenuItemDeleteDescription.Header = String.Format("Diese Beschreibung {0} mal löschen", descriptionList[descriptionListPos].MediaItemList.Count);
                this.ButtonSaveDescription.Content = String.Format("Beschreibung {0} mal speichern", descriptionList[descriptionListPos].MediaItemList.Count);
            }
            else
            {
                this.textChangedByUser = false;
                this.MenuItemDeleteDescription.IsEnabled = false;
                this.MenuItemDeleteDescription.Header = "Keine Beschreibung vorhanden";
                this.ButtonSaveDescription.Content = String.Format("Beschreibung {0} mal speichern", mediaItemList.Count);
            }

            this.ButtonSaveDescription.IsEnabled = false;
        }

        public void Clear()
        {
            this.ListBoxAttachments.Items.Clear();
            this.textChangedByUser = null;
            this.TextBoxDescription.Text = "";
        }

        private void ListBoxAttachments_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.Open();
        }

        private void Open()
        {
            if (this.ListBoxAttachments.SelectedItem != null && this.ListBoxAttachments.SelectedItems.Count == 1)
            {
                Attachment attachment = (Attachment)this.ListBoxAttachments.SelectedItem;

                if (!File.Exists(attachment.FileInfo.FullName))
                {
                    
                    System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
                    if (attachment.MediaItemList.Count > 0)
                        openFileDialog.InitialDirectory = attachment.MediaItemList[0].Foldername;

                    if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        attachment.FullName = openFileDialog.FileName;
                        MediaBrowserContext.SetAttachment(new List<Attachment>() { attachment });
                    }
                }


                if (File.Exists(attachment.FileInfo.FullName))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(attachment.FileInfo.FullName);
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        MediaBrowser4.Utilities.OpenAs.Open(attachment.FileInfo.FullName);
                    }
                }
                else
                {
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,attachment.FileInfo.FullName, "Die Datei kann nicht gefunden werden.", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuItem_Click_OpenWith(object sender, RoutedEventArgs e)
        {
            try
            {
                MediaBrowser4.Utilities.OpenAs.Open(((Attachment)this.ListBoxAttachments.SelectedItem).FileInfo.FullName);
            }
            catch (Exception ex)
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message, "Fehler beim Öffnen", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Exception(ex);
            }
        }

        private void MenuItem_Click_Open(object sender, RoutedEventArgs e)
        {
            this.Open();
        }

        private void ContextMenu_Attachments_Opened(object sender, RoutedEventArgs e)
        {
            this.MenuItemAttachmetsOpen.IsEnabled = this.MenuItemAttachmetsOpenWith.IsEnabled
                = (this.ListBoxAttachments.SelectedItem != null && this.ListBoxAttachments.SelectedItems.Count == 1);

            this.MenuItemAttachmetsDetach.IsEnabled = this.MenuItemAttachmetsCopy.IsEnabled
                = (this.ListBoxAttachments.SelectedItem != null && this.ListBoxAttachments.SelectedItems.Count > 0);

            this.MenuItemAttach.IsEnabled = this.MenuItemAttach.IsEnabled = this.MenuItemAttach.IsEnabled
                = (this.mediaItemList != null && this.mediaItemList.Count > 0);

            this.MenuItemAttachmetsCopyAttach.IsEnabled = this.MenuItemAttachmetsDetach.IsEnabled && this.MenuItemAttach.IsEnabled;
        }

        private void GridSplitter_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeNS;
        }

        private void GridSplitter_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void ComboBoxDescription_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.SetNewDescription(this.ComboBoxDescription.SelectedIndex);
        }

        private void MenuItem_Click_DeleteDescription(object sender, RoutedEventArgs e)
        {
            if (ComboBoxDescription.SelectedValue == null)
                return;

            Description description = ComboBoxDescription.SelectedValue as Description;
            description.Command = DescriptionCommand.REMOVE_FROM_MEDIAITEMS;
            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void MenuItem_Click_DescribeAll(object sender, RoutedEventArgs e)
        {
            if (ComboBoxDescription.SelectedValue == null)
                return;

            Description description = new Description();
            description.Value = ((Description)ComboBoxDescription.SelectedValue).Value;
            description.Command = DescriptionCommand.SET_AND_CREATE_DESCRIPTION;
            description.MediaItemList = this.mediaItemList;
            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void MenuItem_Click_DeleteAllDescription(object sender, RoutedEventArgs e)
        {
            Description description = new Description();
            description.Command = DescriptionCommand.REMOVE_FROM_MEDIAITEMS;
            description.MediaItemList = this.mediaItemList;
            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void ButtonSaveDescription_Click(object sender, RoutedEventArgs e)
        {
            Description description = ComboBoxDescription.SelectedValue as Description;

            if (description == null)
            {
                description = new Description();
                description.MediaItemList = this.mediaItemList;
            }

            description.Command = DescriptionCommand.SET_AND_CREATE_DESCRIPTION;
            description.Value = this.TextBoxDescription.Text.Trim();

            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void MenuItem_Click_OpenMediaItems(object sender, RoutedEventArgs e)
        {
            Description description = ComboBoxDescription.SelectedValue as Description;

            if (this.OnRequest != null && description != null)
            {
                MediaItemSortRequest sortRequest = new MediaItemSortRequest("Von Beschreibung ...", description.ShortString);
                sortRequest.UserDefinedId = description.Value.GetHashCode();
                sortRequest.MediaItemList = description.MediaItemList;
                sortRequest.ShuffleType = MediaItemRequestShuffleType.NONE;
                sortRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE, MediaItemRequestSortDirection.ASCENDING));

                this.OnRequest(this, new MediaItemRequestMessageArgs(sortRequest));
            }
        }

        private void TextBoxDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            foreach (TextChange tc in e.Changes)
            {
                if (this.textChangedByUser == false)
                {
                    this.textChangedByUser = true;
                    this.ButtonSaveDescription.IsEnabled = true;
                }
            }

            if (this.textChangedByUser == null)
                this.textChangedByUser = false;
        }

        private void MenuItem_Move_And_Rename_Click(object sender, RoutedEventArgs e)
        {
            if (this.mediaItemList == null || this.mediaItemList.Count < 1)
                return;

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MediaItem mItem = this.mediaItemList[0];
                string templateName = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(mItem.FullName), System.IO.Path.GetFileNameWithoutExtension(mItem.FullName));

                foreach (string filname in openFileDialog.FileNames)
                {
                    if (MediaBrowserContext.HasKnownMediaExtension(filname))
                        continue;

                    try
                    {
                        System.IO.File.Move(filname, templateName + System.IO.Path.GetExtension(filname));
                    }
                    catch (Exception ex)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message, "Verknüpfen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Log.Exception(ex);
                    }
                }

                this.RefreshAttachments();
            }
        }

        private void MenuItem_Click_Copy(object sender, RoutedEventArgs e)
        {
            if (this.ListBoxAttachments.SelectedItems == null || this.ListBoxAttachments.SelectedItems.Count < 1)
                return;

            System.Windows.Forms.FolderBrowserDialog directoryDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (directoryDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (Attachment attachment in this.ListBoxAttachments.SelectedItems)
                {
                    try
                    {
                        attachment.FileInfo.CopyTo(System.IO.Path.Combine(directoryDialog.SelectedPath, attachment.FileInfo.Name));
                    }
                    catch (Exception ex)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message, "Kopieren nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Log.Exception(ex);
                    }
                }
            }
        }

        private void MenuItem_Attach_Click(object sender, RoutedEventArgs e)
        {
            this.Attach(false);
        }

        private void MenuItem_Move_And_Attach_Click(object sender, RoutedEventArgs e)
        {
            this.Attach(true);
        }

        private void Attach(bool move)
        {
            if (this.mediaItemList == null || this.mediaItemList.Count < 1)
                return;

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = System.IO.Path.GetDirectoryName(this.mediaItemList[0].FullName);
                List<Attachment> attachmentList = new List<Attachment>();

                foreach (string filname in openFileDialog.FileNames)
                {
                    try
                    {
                        string attachFilname = filname;

                        if (move)
                        {
                            attachFilname = System.IO.Path.Combine(path, System.IO.Path.GetFileName(filname));
                            System.IO.File.Move(filname, attachFilname);
                        }

                        attachmentList.Add(new Attachment(this.mediaItemList, attachFilname));

                    }
                    catch (Exception ex)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message, "Verschieben nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Log.Exception(ex);
                    }
                }

                if (attachmentList.Count > 0)
                    MediaBrowserContext.SetAttachment(attachmentList);
            }

            this.RefreshAttachments();
        }

        private void MenuItem_Detach_Click(object sender, RoutedEventArgs e)
        {
            List<Attachment> attachmentList = new List<Attachment>();

            foreach (Attachment attachment in this.ListBoxAttachments.SelectedItems)
            {
                if (attachment.Type == AttachmentType.STICKY)
                {
                    MediaBrowser4.Utilities.RecycleBin.Recycle(attachment.FullName, false);
                }
                else
                {
                    attachment.Detach();
                    attachmentList.Add(attachment);
                }
            }

            MediaBrowserContext.SetAttachment(attachmentList);

            foreach (Attachment attachment in attachmentList)
            {
                if (!attachment.IsReferenced && attachment.MediaItemList != null && attachment.MediaItemList.Count > 0
                    && attachment.MediaItemList[0].Foldername.Equals(System.IO.Path.GetDirectoryName(attachment.FullName), StringComparison.InvariantCultureIgnoreCase))
                {
                    MediaBrowser4.Utilities.RecycleBin.Recycle(attachment.FullName, false);
                }
            }

            this.RefreshAttachments();
        }

        private void MenuItemAttachmetsCopyAttach_Click(object sender, RoutedEventArgs e)
        {
            List<Attachment> attachmentList = new List<Attachment>();

            foreach (Attachment attachment in this.ListBoxAttachments.SelectedItems)
            {
                if (attachment.Type == AttachmentType.ATTACHED)
                {
                    attachment.MediaItemList.Clear();
                    attachment.MediaItemList.AddRange(this.mediaItemList);
                    attachmentList.Add(attachment);
                }
                else if (attachment.Type == AttachmentType.STICKY)
                {
                    try
                    {
                        string newName =
                            System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(attachment.FullName), System.Guid.NewGuid().ToString()))
                            + System.IO.Path.GetExtension(attachment.FullName);

                        File.Move(attachment.FullName, newName);

                        Attachment newAttachment = new Attachment(this.mediaItemList, newName);
                        attachmentList.Add(newAttachment);
                    }
                    catch (Exception ex)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,ex.Message, "Verknüpfen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Log.Exception(ex);
                    }
                }
            }

            MediaBrowserContext.SetAttachment(attachmentList);
            this.RefreshAttachments();
        }
    }
}
