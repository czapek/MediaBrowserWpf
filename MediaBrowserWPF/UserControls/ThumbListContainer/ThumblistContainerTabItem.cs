using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using MediaBrowser4.Objects;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MediaBrowserWPF.UserControls
{
    public class ThumblistContainerTabItem : TabItem
    {

        public event EventHandler OnCloseAll;
        public event EventHandler OnCloseThis;
        public event EventHandler<MediaItemArgument> OnSelectedMediaItemsChanged;
        public event EventHandler OnOpenRequestWindow;
        public event EventHandler OnSaveRequest;

        public MediaItemRequest Request
        {
            get;
            private set;
        }

        public List<MediaItem> MediaItems
        {
            get
            {
                return this.ThumbListContainer.MediaItems;
            }
        }

        public List<MediaItem> SelectedMediaItems
        {
            get
            {
                return this.ThumbListContainer.SelectedMediaItems;
            }
        }

        public MediaItem SelectedMediaItem
        {
            get
            {
                return this.ThumbListContainer.SelectedMediaItem;
            }
        }

        public ThumblistContainer ThumbListContainer { get; private set; }
        private CloseableHeader closeableHeader;

        public ThumblistContainerTabItem(ThumblistContainer thumbListContainer, MediaItemRequest mediaItemRequest)
            : base()
        {
            ThumblistContainerTabItemContextMenu contextMenu = new ThumblistContainerTabItemContextMenu(this);
            contextMenu.OnCloseAll += new EventHandler(contextMenu_OnCloseAll);
            contextMenu.OnCloseThis += new EventHandler(contextMenu_OnCloseThis);
            contextMenu.OnOpenRequestWindow += new EventHandler(contextMenu_OnOpenRequestWindow);
            contextMenu.OnSaveRequest += new EventHandler(contextMenu_OnSaveRequest);

            this.Request = mediaItemRequest;

            this.Content = thumbListContainer;
            this.ThumbListContainer = thumbListContainer;
            this.ThumbListContainer.KeyDown += new KeyEventHandler(ThumbListContainer_KeyDown);
            this.ThumbListContainer.OnRequestChanging += new EventHandler(thumbListContainer_OnRequestChanging);
            this.ThumbListContainer.OnSelectedMediaItemsChanged += new EventHandler<MediaItemArgument>(ThumbListContainer_OnSelectedMediaItemsChanged);
          
            this.closeableHeader = new CloseableHeader();
            base.Header = this.closeableHeader;
            this.closeableHeader.ContextMenu = contextMenu;
            this.closeableHeader.CloseButton.MouseEnter += new MouseEventHandler(button_close_MouseEnter);
            this.closeableHeader.CloseButton.MouseLeave += new MouseEventHandler(button_close_MouseLeave);
            this.closeableHeader.CloseButton.Click += new RoutedEventHandler(contextMenu_OnCloseThis);
            this.closeableHeader.Title.SizeChanged += new SizeChangedEventHandler(label_TabTitle_SizeChanged);
        }

        void ThumbListContainer_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F8:
                    this.ChangeView();
                    break;
            }
        }

        public void ChangeView()
        {
            this.ThumbListContainer.ChangeView();
        }

        public void RemoveMediaItem(MediaItem mItem)
        {
            this.ThumbListContainer.RemoveMediaItem(mItem);
        }

        public void RefreshByFolder(Folder folder)
        {
            MediaItemFolderRequest folderRequest = this.ThumbListContainer.Request as MediaItemFolderRequest;
            if (folderRequest != null && folderRequest.Folders.Contains(folder))
            {                
                this.ThumbListContainer.Refresh();
            }
        }

        public void RemoveFromFolder(MediaItem mItem)
        {
            MediaItemFolderRequest folderRequest = this.ThumbListContainer.Request as MediaItemFolderRequest;
            if (folderRequest != null)
            {
                this.RemoveMediaItem(mItem);
            }
        }

        void ThumbListContainer_OnSelectedMediaItemsChanged(object sender, MediaItemArgument e)
        {
            if (this.OnSelectedMediaItemsChanged != null)
            {
                this.OnSelectedMediaItemsChanged(this, e);
            }
        }

        void contextMenu_OnSaveRequest(object sender, EventArgs e)
        {
            if (this.OnSaveRequest != null)
            {
                this.OnSaveRequest(this, EventArgs.Empty);
            }
        }

        void thumbListContainer_OnRequestChanging(object sender, EventArgs e)
        {
            MediaItemRequest request = sender as MediaItemRequest;

            if (request != null)
            {
                this.Request = request;
                this.closeableHeader.Title.Content = request.Header.Replace("_", " ");
                this.closeableHeader.Title.ToolTip = request.Description;
            }
        }

        public new string ToolTip
        {
            set
            {
                this.closeableHeader.Title.ToolTip = value;
            }
        }

        public new string Header
        {
            set
            {
                this.closeableHeader.Title.Content = value.Replace("_", " ");
            }          
        }

        void contextMenu_OnOpenRequestWindow(object sender, EventArgs e)
        {
            if (this.OnOpenRequestWindow != null)
            {
                this.OnOpenRequestWindow(sender, e);
            }
        }

        void button_close_MouseEnter(object sender, MouseEventArgs e)
        {
            this.closeableHeader.CloseButton.Foreground = Brushes.Red;
        }

        void button_close_MouseLeave(object sender, MouseEventArgs e)
        {
            this.closeableHeader.CloseButton.Foreground = Brushes.Black;
        }

        void label_TabTitle_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.closeableHeader.CloseButton.Margin = new Thickness(this.closeableHeader.Title.ActualWidth + 5, 0, 0, 0);
        }

        protected override void OnSelected(RoutedEventArgs e)
        {
            base.OnSelected(e);
            this.closeableHeader.CloseButton.Visibility = Visibility.Visible;
        }

        protected override void OnUnselected(RoutedEventArgs e)
        {
            base.OnUnselected(e);
            this.closeableHeader.CloseButton.Visibility = Visibility.Hidden;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            this.closeableHeader.CloseButton.Visibility = Visibility.Visible;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!this.IsSelected)
            {
                this.closeableHeader.CloseButton.Visibility = Visibility.Hidden;
            }
        }

        void contextMenu_OnCloseThis(object sender, EventArgs e)
        {
            if (this.OnCloseThis != null)
            {
                this.OnCloseThis(this, EventArgs.Empty);
            }
        }

        void contextMenu_OnCloseAll(object sender, EventArgs e)
        {
            if (this.OnCloseAll != null)
            {
                this.OnCloseAll(this, EventArgs.Empty);
            }
        }      
    }
}
