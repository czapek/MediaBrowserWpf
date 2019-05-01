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
using MediaBrowser4;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für RequestContainer.xaml
    /// </summary>
    public partial class RequestContainer : UserControl
    {
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;

        public bool IsBuild
        {
            get;
            private set;
        }

        public RequestContainer()
        {
            InitializeComponent();
        }

        public void Clear()
        {
            this.IsBuild = false;
            this.ListXBoxRequestElements.Items.Clear();
        }

        public void Build()
        {
            if (MediaBrowserContext.MainDBProvider == null)
                return;

            Mouse.OverrideCursor = Cursors.Wait;

            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Kategorien: jüngster Tagebucheintrag") { DescriptionX = "Zeigt alle Medien des jüngsten Tagebucheintrags.", ElementType = RequestContainerElementType.NEWEST_CATEGORY });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Kategorien: die letzten {d} Tage") { DescriptionX = "Zeigt alle Tagebucheinträge der letzten {d} Tage an.", ElementType = RequestContainerElementType.LAST_CATEGORY, Days = 7 });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Kategorien: die letzten {d} Tage") { DescriptionX = "Zeigt alle Tagebucheinträge der letzten {d} Tage an.", ElementType = RequestContainerElementType.LAST_CATEGORY, Days = 30 });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Kategorien: die letzten {d} Tage") { DescriptionX = "Zeigt alle Tagebucheinträge der letzten {d} Tage an.", ElementType = RequestContainerElementType.LAST_CATEGORY, Days = 100 });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Kategorien: {d} zufällige Tage") { DescriptionX = "Wählt {d} Tagebucheinträge zufällig aus und zeigt diese an.", ElementType = RequestContainerElementType.SHUFFLE_CATEGORY, Days = 10 });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Die {l} am häufigsten betrachteten") { DescriptionX = "Zeigt die Ersten {l} am häufigsten betrachteten Medien an. Kann über die Suche eingeschränkt werden.", ElementType = RequestContainerElementType.SORT, SortType = RequestContainerSortType.VIEWED, Limit = 200 });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("{l} zufällig ausgewählte") { DescriptionX = "Zeigt {l} per Zufall ausgewählte Medien an. Kann über die Suche eingeschränkt werden.", ElementType = RequestContainerElementType.SORT, SortType = RequestContainerSortType.SHUFFLE, Limit = 200 });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Alle ohne Kategorie") { DescriptionX = "Zeigt alle Medien an, die keine Kategorie besitzen. Kann über die Suche eingeschränkt werden.", ElementType = RequestContainerElementType.CATEGORY_NO });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Alle ohne Orts-Kategorie") { DescriptionX = "Zeigt alle Medien an die noch keine Orts-Kategorie besitzen. Kann über die Suche eingeschränkt werden.", ElementType = RequestContainerElementType.CATEGORY_NO_LOCATION });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Alle ohne Datums-Kategorie") { DescriptionX = "Zeigt alle Medien an die noch keine Datums-Kategorie besitzen. Kann über die Suche eingeschränkt werden.", ElementType = RequestContainerElementType.CATEGORY_NO_DATE });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Alle Dubletten") { DescriptionX = "Zeigt alle Medien an, die entsprechend ihrer MD5-Prüfsumme mehrfach in der Datenbank vorhanden sind.", ElementType = RequestContainerElementType.DUBLICATES });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Ohne Sonstige-Kategorie") { DescriptionX = "Zeigt alle Medien an, die keine Kategorie, höchstens Datum und Ort, haben. Kann über die Suche eingeschränkt werden.", ElementType = RequestContainerElementType.CATEGORY_ONLY_DATE });
            this.ListXBoxRequestElements.Items.Add(new RequestContainerElement("Unauffindbare Datei") { DescriptionX = "Zeigt alle Medien an, bei denen die zugehörige Datei nicht gefunden werden kann.", ElementType = RequestContainerElementType.FILE_NOT_EXISTS, Limit = 1000 });

            foreach (MediaItemRequest request in MediaBrowserContext.GetUserDefinedRequests())
            {
                this.ListXBoxRequestElements.Items.Add(new RequestContainerElement(request));
            }

            this.IsBuild = true;

            Mouse.OverrideCursor = null;
        }

        public void SaveRequest(MediaItemRequest request)
        {
            if (request is MediaItemObservableCollectionRequest)
                return;

            if (request is MediaItemVirtualRequest)
                return;

            if (!this.IsBuild)
            {
                this.Build();
            }

            MediaItemRequest requestClone = request.Clone();

            if (requestClone.UserDefinedName == null)
            {
                requestClone.UserDefinedName = "Abfrage: " + request.Header;
            }

            if (requestClone is MediaItemSearchRequest)
            {
                ((MediaItemSearchRequest)requestClone).WindowIdentifier
                    = ((MediaItemSearchRequest)requestClone).Description.GetHashCode();
            }//Kein zusätzliches Searchtoken für die Suche
            else if (MediaBrowserContext.SearchTokenGlobal != null)
            {
                if (MediaBrowserContext.SearchTokenGlobal.IsValid)
                {
                    requestClone.UserDefinedSearchToken = MediaBrowserContext.SearchTokenGlobal;
                }
                else
                {
                    requestClone.UserDefinedSearchToken = request.UserDefinedSearchToken = null;
                }
            }

            RequestContainerElement containerElement =
                this.ListXBoxRequestElements.Items.Cast<RequestContainerElement>()
                    .FirstOrDefault(x => x.ElementType == RequestContainerElementType.USER_DEFINED
                        && x.Request.UserDefinedId == request.UserDefinedId);

            bool isInserted = MediaBrowserContext.SaveUserDefinedRequest(requestClone);

            if (containerElement == null && isInserted)
            {
                this.ListXBoxRequestElements.Items.Add(new RequestContainerElement(requestClone));
            }
            else
            {
                containerElement.Request = request;
            }
        }

        private void Open(RequestContainerElement requestElement)
        {
            MediaItemRequest mediaItemRequest = null;

            switch (requestElement.ElementType)
            {
                case RequestContainerElementType.USER_DEFINED:
                    mediaItemRequest = requestElement.Request.Clone();
                    break;

                case RequestContainerElementType.NEWEST_CATEGORY:
                    mediaItemRequest = new MediaItemCategoryRequest() { CategoryRequestType = MediaItemCategoryRequestType.NEWEST_DAY };
                    break;

                case RequestContainerElementType.LAST_CATEGORY:
                    mediaItemRequest = new MediaItemCategoryRequest() { Days = requestElement.Days, CategoryRequestType = MediaItemCategoryRequestType.LAST_DAYS };
                    break;

                case RequestContainerElementType.SHUFFLE_CATEGORY:
                    mediaItemRequest = new MediaItemCategoryRequest() { Days = requestElement.Days, CategoryRequestType = MediaItemCategoryRequestType.SHUFFLE_DAYS };
                    break;

                case RequestContainerElementType.SORT:
                    mediaItemRequest = this.SetSortRequest(requestElement);
                    break;

                case RequestContainerElementType.FILE_NOT_EXISTS:
                    mediaItemRequest = new MediaItemFilesRequest(MediaItemFilesRequestType.FilesNotExist);
                    break;

                case RequestContainerElementType.DUBLICATES:
                    mediaItemRequest = this.SetDublicatesRequest(requestElement);
                    break;

                case RequestContainerElementType.CATEGORY_NO:
                    mediaItemRequest = new MediaItemCategoryRequest() { LimitRequest = requestElement.Limit, CategoryRequestType = MediaItemCategoryRequestType.NO_CATEGORY };
                    break;

                case RequestContainerElementType.CATEGORY_NO_DATE:
                    mediaItemRequest = new MediaItemCategoryRequest() { LimitRequest = requestElement.Limit, CategoryRequestType = MediaItemCategoryRequestType.NO_DATE };
                    break;

                case RequestContainerElementType.CATEGORY_NO_LOCATION:
                    mediaItemRequest = new MediaItemCategoryRequest() { LimitRequest = requestElement.Limit, CategoryRequestType = MediaItemCategoryRequestType.NO_LOCATION };
                    break;

                case RequestContainerElementType.CATEGORY_ONLY_DATE:
                    mediaItemRequest = new MediaItemCategoryRequest() { LimitRequest = requestElement.Limit, CategoryRequestType = MediaItemCategoryRequestType.NO_OTHER };
                    break;
            }

            if (mediaItemRequest != null && mediaItemRequest.IsValid)
            {
                this.OnRequest(this, new MediaItemRequestMessageArgs(mediaItemRequest));
            }

        }


        private RequestContainerElement editElement;
        private void RenameUserDefindedRequest()
        {
            RequestContainerElement requestElement = this.SelectedUserRequestElement;

            if (requestElement == null || this.editElement != null)
                return;

            this.editElement = requestElement;
            requestElement.IsEditable = true;
        }

        private void DeleteUserDefindedRequest()
        {
            RequestContainerElement requestElement = this.SelectedUserRequestElement;

            if (requestElement == null)
                return;

            if (MessageBoxResult.Yes == Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,String.Format("Soll die Abfrage \"{0}\" wirklich gelöscht werden?", requestElement.Name), "Abfrage löschen",
                   MessageBoxButton.YesNo, MessageBoxImage.Warning))
            {


                MediaBrowserContext.DeleteUserDefinedRequest(requestElement.Request);
                this.ListXBoxRequestElements.Items.Remove(requestElement);
            }
        }

        private MediaItemRequest SetDublicatesRequest(RequestContainerElement requestElement)
        {
            MediaItemDublicatesRequest dublicatesRequest = new MediaItemDublicatesRequest();
            dublicatesRequest.LimitRequest = requestElement.Limit;
            dublicatesRequest.ShuffleType = MediaItemRequestShuffleType.NONE;
            dublicatesRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.CHECKSUM, MediaItemRequestSortDirection.DESCENDING));
            dublicatesRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FOLDERNAME, MediaItemRequestSortDirection.DESCENDING));

            return dublicatesRequest;
        }

        private MediaItemRequest SetSortRequest(RequestContainerElement requestElement)
        {
            MediaItemSortRequest sortRequest = new MediaItemSortRequest(requestElement.Name, requestElement.Description);
            sortRequest.LimitRequest = requestElement.Limit;
            sortRequest.ShuffleType = MediaItemRequestShuffleType.NONE;

            switch (requestElement.SortType)
            {
                case RequestContainerSortType.VIEWED:
                    sortRequest.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.VIEWED, MediaItemRequestSortDirection.DESCENDING));
                    break;

                case RequestContainerSortType.SHUFFLE:    
                    sortRequest.ShuffleType = MediaItemRequestShuffleType.SHUFFLE;
                    break;
            }

            return sortRequest;
        }

        private void ListXBoxRequestElements_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.editElement == null && this.ListXBoxRequestElements.SelectedItem != null)
            {
                this.Open((RequestContainerElement)this.ListXBoxRequestElements.SelectedItem);
            }
        }

        private void ListXBoxRequestElements_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F2:
                    this.RenameUserDefindedRequest();
                    break;

                case Key.Delete:
                    if (this.editElement == null)
                    {
                        this.DeleteUserDefindedRequest();
                    }
                    break;

                case Key.Escape:
                    if (this.editElement != null)
                    {
                        this.editElement.EditEscape = true;
                        this.editElement.IsEditable = false;
                        this.editElement = null;
                    }
                    break;

                case Key.Enter:
                    if (this.editElement == null)
                    {
                        if (this.ListXBoxRequestElements.SelectedItem != null)
                        {
                            this.Open((RequestContainerElement)this.ListXBoxRequestElements.SelectedItem);
                        }
                    }
                    else
                    {
                        this.editElement.IsEditable = false;
                        this.editElement = null;
                    }
                    break;
            }
        }

        private void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            this.RenameUserDefindedRequest();
        }

        private void MenuItemDelete_Click(object sender, RoutedEventArgs e)
        {
            this.DeleteUserDefindedRequest();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (this.SelectedUserRequestElement != null)
            {
                this.MenuItemRename.IsEnabled = true;
                this.MenuItemDelete.IsEnabled = true;
            }
            else
            {
                this.MenuItemRename.IsEnabled = false;
                this.MenuItemDelete.IsEnabled = false;
            }
        }

        private RequestContainerElement SelectedUserRequestElement
        {
            get
            {
                RequestContainerElement requestElement = this.ListXBoxRequestElements.SelectedItem as RequestContainerElement;

                if (requestElement != null
                   && requestElement.ElementType == RequestContainerElementType.USER_DEFINED)
                {
                    return requestElement;
                }
                else
                {
                    return null;
                }
            }
        }

        private void TextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ((TextBox)sender).Focus();
            ((TextBox)sender).SelectAll();
        }
    }
}
