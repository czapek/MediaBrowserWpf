using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaBrowser4;
using MediaBrowser4.Objects;
using Microsoft.Windows.Controls;

namespace MediaBrowserWPF.UserControls
{
    public partial class DescriptionContainer : UserControl
    {
        private List<MediaItem> mediaItemList;
        private List<Description> descriptionList;
        private bool? textChangedByUser;

        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;

        public DescriptionContainer()
        {
            InitializeComponent();
        }

        public void SetInfo(List<MediaItem> mediaItemList)
        {
            if (mediaItemList == null)
                return;

            if (this.textChangedByUser == true)
            {
                if (System.Windows.MessageBoxResult.No == System.Windows.MessageBox.Show(MainWindow.MainWindowStatic,
                    "Ihre Änderungen an der Beschreibung gehen verloren.\nWollen Sie wirklich fortfahren?",
                    "Vorsicht!", MessageBoxButton.YesNo, MessageBoxImage.Warning))
                    return;
            }

            this.Clear();
            this.mediaItemList = mediaItemList;
            this.Build();
        }

        public void Clear()
        {
            this.descriptionList = null;
            this.textChangedByUser = null;
            if (this.TextBoxDescription != null)
                this.TextBoxDescription.Text = string.Empty;
            if (this.ComboBoxDescription != null)
                this.ComboBoxDescription.ItemsSource = null;
        }

        public void Build()
        {
            if (this.mediaItemList == null)
                return;

            Mouse.OverrideCursor = Cursors.Wait;

            this.descriptionList = MediaBrowserContext.GetDescription(this.mediaItemList);

            this.MenuItemDescribeAll.IsEnabled = this.mediaItemList.Count > 0;
            this.MenuItemDeleteAllDescription.IsEnabled = this.mediaItemList.Count > 0;
            this.ButtonSaveDescription.IsEnabled = this.mediaItemList.Count > 0;
            this.ComboBoxDescription.ItemsSource = this.descriptionList;
            this.ComboBoxDescription.SelectedIndex = 0;

            this.SetNewDescription(0);

            Mouse.OverrideCursor = null;
        }

        private void SetNewDescription(int descriptionListPos)
        {
            if (this.descriptionList == null ||
                descriptionListPos < 0 ||
                (this.descriptionList.Count > 0 && descriptionListPos >= this.descriptionList.Count))
                return;

            this.ComboBoxDescription.Visibility = this.descriptionList.Count <= 1
                ? Visibility.Hidden
                : Visibility.Visible;

            this.textChangedByUser = null;
            this.TextBoxDescription.Text = string.Empty;

            if (this.descriptionList.Count > 0)
            {
                this.MenuItemDeleteDescription.IsEnabled = true;
                this.textChangedByUser = null;
                this.TextBoxDescription.Text = this.descriptionList[descriptionListPos].Value;
                this.MenuItemDeleteDescription.Header = string.Format("Diese Beschreibung {0} mal löschen",
                    this.descriptionList[descriptionListPos].MediaItemList.Count);
                this.ButtonSaveDescription.Content = string.Format("Beschreibung {0} mal speichern",
                    this.descriptionList[descriptionListPos].MediaItemList.Count);
            }
            else
            {
                this.textChangedByUser = false;
                this.MenuItemDeleteDescription.IsEnabled = false;
                this.MenuItemDeleteDescription.Header = "Keine Beschreibung vorhanden";
                this.ButtonSaveDescription.Content = string.Format("Beschreibung {0} mal speichern",
                    this.mediaItemList.Count);
            }

            this.ButtonSaveDescription.IsEnabled = false;
        }

        private void ComboBoxDescription_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.SetNewDescription(this.ComboBoxDescription.SelectedIndex);
        }

        private void MenuItem_Click_DeleteDescription(object sender, RoutedEventArgs e)
        {
            Description description = this.ComboBoxDescription.SelectedValue as Description;
            if (description == null)
                return;

            description.Command = DescriptionCommand.REMOVE_FROM_MEDIAITEMS;
            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void MenuItem_Click_DescribeAll(object sender, RoutedEventArgs e)
        {
            Description selected = this.ComboBoxDescription.SelectedValue as Description;
            if (selected == null)
                return;

            Description description = new Description
            {
                Value = selected.Value,
                Command = DescriptionCommand.SET_AND_CREATE_DESCRIPTION,
                MediaItemList = this.mediaItemList
            };
            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void MenuItem_Click_DeleteAllDescription(object sender, RoutedEventArgs e)
        {
            Description description = new Description
            {
                Command = DescriptionCommand.REMOVE_FROM_MEDIAITEMS,
                MediaItemList = this.mediaItemList
            };
            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void ButtonSaveDescription_Click(object sender, RoutedEventArgs e)
        {
            Description description = this.ComboBoxDescription.SelectedValue as Description;
            if (description == null)
            {
                description = new Description
                {
                    MediaItemList = this.mediaItemList
                };
            }

            description.Command = DescriptionCommand.SET_AND_CREATE_DESCRIPTION;
            description.Value = this.TextBoxDescription.Text.Trim();

            MediaBrowserContext.SetDescription(description);
            this.Build();
        }

        private void MenuItem_Click_OpenMediaItems(object sender, RoutedEventArgs e)
        {
            Description description = this.ComboBoxDescription.SelectedValue as Description;
            if (this.OnRequest == null || description == null)
                return;

            MediaItemSortRequest sortRequest = new MediaItemSortRequest("Von Beschreibung ...", description.ShortString);
            sortRequest.UserDefinedId = description.Value.GetHashCode();
            sortRequest.MediaItemList = description.MediaItemList;
            sortRequest.ShuffleType = MediaItemRequestShuffleType.NONE;
            sortRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE,
                MediaItemRequestSortDirection.ASCENDING));

            this.OnRequest(this, new MediaItemRequestMessageArgs(sortRequest));
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
    }
}
