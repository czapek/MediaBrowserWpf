﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MapControl;
using MediaBrowser4;
using MediaBrowser4.Objects;
using MediaBrowserWPF;
using MediaBrowserWPF.UserControls;

namespace MapControl
{
    /// <summary>This is the main window for the sample application.</summary>
    public sealed partial class MapSearchWindow : Window
    {
        private int _maxDownload = -1;
        public static event EventHandler<MediaItemRequestMessageArgs> OnRequest;
        private Category category;
        private String searchPath;
        private List<MediaItem> mediaItems;

        /// <summary>Initializes a new instance of the MainWindow class.</summary>
        public MapSearchWindow()
        {
            Init();
        }

        public MapSearchWindow(String searchPath, Category category)
        {
            this.category = category;
            this.searchPath = searchPath;
            Init();
            saveLocationBtn.Visibility = Visibility.Visible;
            selectItemsBtn.Visibility = Visibility.Collapsed;
            selectDaysBtn.Visibility = Visibility.Collapsed;
            selectLocationBtn.Visibility = Visibility.Collapsed;
        }

        public MapSearchWindow(List<MediaItem> mediaItems)
        {
            this.mediaItems = mediaItems;
            Init();
            saveLocationBtn.Visibility = Visibility.Visible;
            cbxOverwrite.Visibility = Visibility.Visible;
            selectItemsBtn.Visibility = Visibility.Collapsed;
            selectDaysBtn.Visibility = Visibility.Collapsed;
            selectLocationBtn.Visibility = Visibility.Collapsed;
        }

        private void Init()
        {
            // Very important we set the CacheFolder before doing anything so the MapCanvas knows where
            // to save the downloaded files to.
            TileGenerator.CacheFolder = @"ImageCache";
            TileGenerator.DownloadCountChanged += this.OnDownloadCountChanged;
            TileGenerator.DownloadError += this.OnDownloadError;

            this.InitializeComponent();
            CommandManager.AddPreviewExecutedHandler(this, this.PreviewExecuteCommand); // We're going to do some effects when zooming.
        }

        private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri); // Launch the site in the user's default browser.
        }

        private void OnDownloadCountChanged(object sender, EventArgs e)
        {
            if (this.Dispatcher.Thread != Thread.CurrentThread)
            {
                this.Dispatcher.BeginInvoke(new Action(() => this.OnDownloadCountChanged(sender, e)), null);
                return;
            }
            if (TileGenerator.DownloadCount == 0)
            {
                this.label.Visibility = Visibility.Hidden;
                this.progress.Visibility = Visibility.Hidden;
                _maxDownload = -1;
            }
            else
            {
                this.errorBar.Visibility = Visibility.Collapsed;

                if (_maxDownload < TileGenerator.DownloadCount)
                {
                    _maxDownload = TileGenerator.DownloadCount;
                }
                this.progress.Value = 100 - (TileGenerator.DownloadCount * 100.0 / _maxDownload);
                this.progress.Visibility = Visibility.Visible;
                this.label.Text = string.Format(
                    CultureInfo.CurrentUICulture,
                    "Downloading {0} item{1}",
                    TileGenerator.DownloadCount,
                    TileGenerator.DownloadCount != 1 ? 's' : ' ');
                this.label.Visibility = Visibility.Visible;
            }
        }

        private void OnDownloadError(object sender, EventArgs e)
        {
            if (this.Dispatcher.Thread != Thread.CurrentThread)
            {
                this.Dispatcher.BeginInvoke(new Action(() => this.OnDownloadError(sender, e)), null);
                return;
            }

            this.errorBar.Text = "Unable to contact the server to download map data.";
            this.errorBar.Visibility = Visibility.Visible;
        }

        private void OnSearchControlNavigate(object sender, NavigateEventArgs e)
        {
            if (e.Result == null) // The results have been cleared - hide the marker.
            {
                this.searchMarker.Visibility = Visibility.Hidden;
            }
            else
            {
                this.searchMarker.Visibility = Visibility.Visible;

                this.tileCanvas.Focus();
                if (e.Result.Size.IsEmpty)
                {
                    this.tileCanvas.Center(e.Result.Latitude, e.Result.Longitude, this.tileCanvas.Zoom);
                }
                else
                {
                    this.tileCanvas.Center(e.Result.Latitude, e.Result.Longitude, e.Result.Size);
                }
            }
            this.searchMarker.DataContext = e.Result;
        }

        private void OnZoomStoryboardCompleted(object sender, EventArgs e)
        {
            this.zoomGrid.Visibility = Visibility.Hidden;
            this.zoomImage.Source = null;
        }

        private void PreviewExecuteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == NavigationCommands.DecreaseZoom)
            {
                if (this.tileCanvas.Zoom > 0) // Make sure we can actualy zoom out
                {
                    this.StartZoom("zoomOut", 1);
                }
            }
            else if (e.Command == NavigationCommands.IncreaseZoom)
            {
                if (this.tileCanvas.Zoom < TileGenerator.MaxZoom)
                {
                    this.StartZoom("zoomIn", 0.5);
                }
            }
        }

        private void StartZoom(string name, double scale)
        {
            this.zoomImage.Source = this.tileCanvas.CreateImage();
            this.zoomRectangle.Height = this.tileCanvas.ActualHeight * scale;
            this.zoomRectangle.Width = this.tileCanvas.ActualWidth * scale;

            this.zoomGrid.RenderTransform = new ScaleTransform(); // Clear the old transform
            this.zoomGrid.Visibility = Visibility.Visible;
            ((Storyboard)this.zoomGrid.FindResource(name)).Begin();
        }

        private void selectLocationBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchResult searchResult = this.searchMarker.DataContext as SearchResult;
            if (searchResult != null)
            {
                List<Category> categories = MediaBrowserContext.GetCategoriesLocationGeoData(searchResult.Longitude, searchResult.Size.Width, searchResult.Latitude, searchResult.Size.Height);
                MainWindow.MainWindowStatic.CategoryTree.SetCategories(categories);
                this.Close();
            }
        }

        private void selectDaysBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchResult searchResult = this.searchMarker.DataContext as SearchResult;
            if (searchResult != null)
            {
                List<Category> categories = MediaBrowserContext.GetCategoriesDiaryGeoData(searchResult.Longitude, searchResult.Size.Width, searchResult.Latitude, searchResult.Size.Height);
                MainWindow.MainWindowStatic.CategoryTree.SetCategories(categories);
                this.Close();
            }
        }

        private void selectItemsBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchResult searchResult = this.searchMarker.DataContext as SearchResult;
            if (searchResult != null)
            {
                MediaItemRequestGeoData request = new MediaItemRequestGeoData(searchResult.Longitude, searchResult.Size.Width, searchResult.Latitude, searchResult.Size.Height, searchResult.DisplayName);
                if (OnRequest != null)
                    OnRequest(this, new MediaItemRequestMessageArgs(request));

                this.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(searchPath))
            {
                searchCtrl.Search(searchPath);
            }
        }

        private void saveLocation_Click(object sender, RoutedEventArgs e)
        {
            SearchResult searchResult = this.searchMarker.DataContext as SearchResult;
            if (searchResult != null)
            {
                if (category != null)
                {
                    category.Longitude = searchResult.Longitude;
                    category.Latitude = searchResult.Latitude;
                    MediaBrowserContext.SetCategory(category);
                }

                if (mediaItems != null)
                {
                    if (!(cbxOverwrite.IsChecked.HasValue && cbxOverwrite.IsChecked.Value))
                        mediaItems.RemoveAll(x => x.Latitude.HasValue);

                    mediaItems.ForEach(item =>
                    {
                        item.Latitude = searchResult.Latitude;
                        item.Longitude = searchResult.Longitude;
                    });    

                    MediaBrowserContext.SetGeodata(mediaItems);
                }
            }
            this.Close();
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
