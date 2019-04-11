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

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für ThumblistContainerTabItemContextMenu.xaml
    /// </summary>
    public partial class ThumblistContainerTabItemContextMenu : ContextMenu
    {

        public event EventHandler OnCloseAll;
        public event EventHandler OnCloseThis;
        public event EventHandler OnOpenRequestWindow;
        public event EventHandler OnSaveRequest;

        private ThumblistContainerTabItem tabItem;

        public ThumblistContainerTabItemContextMenu(ThumblistContainerTabItem tabItem)
        {
            this.tabItem = tabItem;
            InitializeComponent();
        }

        private void MenuItem_CloseThis(object sender, RoutedEventArgs e)
        {
            if (this.OnCloseThis != null)
            {
                this.OnCloseThis(this.tabItem, EventArgs.Empty);
            }
        }

        private void MenuItem_CloseAll(object sender, RoutedEventArgs e)
        {
            if (this.OnCloseAll != null)
            {
                this.OnCloseAll(this.tabItem, EventArgs.Empty);
            }
        }

        private void MenuItemOpenRequest_Click(object sender, RoutedEventArgs e)
        {
            if (this.OnOpenRequestWindow != null)
            {
                this.OnOpenRequestWindow(this.tabItem, EventArgs.Empty);
            }
        }

        private void MenuItemRefreshRequest_Click(object sender, RoutedEventArgs e)
        {
            this.tabItem.ThumbListContainer.Refresh();
        }

        private void MenuItemRefreshEditSql_Click(object sender, RoutedEventArgs e)
        {
            this.tabItem.ThumbListContainer.RefreshSql();
        }

        private void MenuItemSaveRequest_Click(object sender, RoutedEventArgs e)
        {
            if (this.OnSaveRequest != null)
            {
                this.OnSaveRequest(this.tabItem, EventArgs.Empty);
            }
        }

        private void MenuItemRescan_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.RescanRequest(this.tabItem);
        }

        private void MenuItem_ChangeView(object sender, RoutedEventArgs e)
        {
            this.tabItem.ChangeView();
        }

        private void MenuItemRescanRecursive_Click(object sender, RoutedEventArgs e)
        {
            if (this.tabItem.Request is MediaItemFolderRequest)
            {
                MediaItemFolderRequest folderRequest = ((MediaItemFolderRequest)this.tabItem.Request);

                foreach (Folder folder in folderRequest.Folders)
                    MainWindow.RescanFolder(folder.FullPath);
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            this.MenuItemRescanRecursive.Visibility = this.tabItem.Request is MediaItemFolderRequest ?
                System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            this.MenuItemRescan.Visibility = this.MenuItemRescanRecursive.Visibility;
        }
    }
}
