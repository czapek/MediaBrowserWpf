using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using MapControl;
using MediaBrowser4;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public partial class GeoDataContainer : UserControl
    {
        private readonly DispatcherTimer viewportTimer;
        private readonly List<UIElement> markers = new List<UIElement>();
        private readonly List<GeoListItem> listItems = new List<GeoListItem>();
        private List<MediaItem> currentItems = new List<MediaItem>();
        private int requestVersion;
        private bool initialized;
        private bool suppressViewport;

        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;

        public bool IsBuild { get; private set; }

        public GeoDataContainer()
        {
            InitializeComponent();

            TileGenerator.CacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MediaBrowserWpf", "osm-cache");
            TileGenerator.UserAgent = "MediaBrowserWPF/1.0";

            this.ComboBoxSort.ItemsSource = new List<SortOption>
            {
                new SortOption("Älteste zuerst", GeoItemsSort.MediaDateAsc),
                new SortOption("Neueste zuerst", GeoItemsSort.MediaDateDesc),
                new SortOption("ID aufsteigend", GeoItemsSort.IdAsc)
            };
            this.ComboBoxSort.SelectedIndex = 0;

            this.viewportTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            this.viewportTimer.Tick += this.ViewportTimer_Tick;

            this.mapCanvas.ViewportChanged += this.MapCanvas_ViewportChanged;
            this.mapCanvas.SizeChanged += this.MapCanvas_SizeChanged;
        }

        public void Build()
        {
            if (this.IsBuild)
                return;

            this.IsBuild = true;

            if (!MediaBrowserContext.SupportsGeoData)
            {
                this.MapUnavailable.Visibility = Visibility.Visible;
                this.RefreshButton.IsEnabled = false;
                this.ShowAllButton.IsEnabled = false;
                this.CheckBoxSingles.IsEnabled = false;
                this.ComboBoxSort.IsEnabled = false;
                this.StatusText.Text = "Geodaten-Suche ist nur im API-Modus verfügbar.";
                return;
            }

            if (!this.initialized)
            {
                this.CenterFromProperties();
                this.initialized = true;
            }

            this.ScheduleLoad();
        }

        private void CenterFromProperties()
        {
            double latitude = this.ReadDoubleProperty("GeoMapLatitude", 51.0);
            double longitude = this.ReadDoubleProperty("GeoMapLongitude", 10.0);
            int zoom = (int)this.ReadDoubleProperty("GeoMapZoom", 6);

            this.suppressViewport = true;
            this.mapCanvas.Center(latitude, longitude, Math.Max(1, Math.Min(18, zoom)));
            this.suppressViewport = false;
        }

        public void SetInfo(List<MediaItem> mediaItems)
        {
            if (!this.IsBuild)
                this.Build();
        }

        private void ScheduleLoad()
        {
            if (!this.IsBuild || !this.initialized || !MediaBrowserContext.SupportsGeoData)
                return;

            this.viewportTimer.Stop();
            this.viewportTimer.Start();
        }

        public void SetLargeView()
        {
            this.LargeMapButton.Visibility = Visibility.Collapsed;
        }

        public void Clear()
        {
            this.currentItems = new List<MediaItem>();
            this.listItems.Clear();
            this.ListViewItems.ItemsSource = null;
            this.ClearMarkers();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!this.IsBuild)
                    this.Build();
                else if (this.initialized)
                    this.ScheduleLoad();
            }), DispatcherPriority.Loaded);
        }

        private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.mapCanvas.ActualWidth <= 0 || this.mapCanvas.ActualHeight <= 0)
                return;

            if (this.mapCanvas.Viewport.Width <= 0 || this.mapCanvas.Viewport.Height <= 0)
            {
                if (!this.initialized)
                    this.initialized = true;
                this.CenterFromProperties();
                this.ScheduleLoad();
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            this.viewportTimer.Stop();
            this.SaveViewport();
        }

        private void MapCanvas_ViewportChanged(object sender, EventArgs e)
        {
            if (this.suppressViewport || !this.initialized || !this.IsBuild)
                return;

            this.viewportTimer.Stop();
            this.viewportTimer.Start();
        }

        private void ViewportTimer_Tick(object sender, EventArgs e)
        {
            this.viewportTimer.Stop();
            this.LoadAsync();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            this.viewportTimer.Stop();
            this.LoadAsync();
        }

        private void OptionsChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsBuild)
            {
                this.viewportTimer.Stop();
                this.viewportTimer.Start();
            }
        }

        private void ListOptionsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsBuild)
            {
                this.viewportTimer.Stop();
                this.viewportTimer.Start();
            }
        }

        private async void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MediaBrowserContext.SupportsGeoData)
                return;

            Rect viewport = this.mapCanvas.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return;

            this.ShowAllButton.IsEnabled = false;
            this.LoadingText.Visibility = Visibility.Visible;
            this.LoadingText.Text = "Lade alle Geodaten ...";

            try
            {
                GeoItemsSort sort = this.GetSelectedSort();
                List<MediaItem> items = await Task.Run(() => MediaBrowserContext.GetGeoItems(
                    viewport.X, viewport.Y, viewport.Right, viewport.Bottom, 10000, 0, sort));

                if (items == null || items.Count == 0)
                {
                    this.StatusText.Text = "Keine Medien im Ausschnitt.";
                    return;
                }

                MediaItemSortRequest request = new MediaItemSortRequest("Geodaten",
                    "Geodaten im Kartenausschnitt");
                request.MediaItemList = items;
                request.ShuffleType = MediaItemRequestShuffleType.NONE;
                request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE,
                    MediaItemRequestSortDirection.ASCENDING));

                if (this.OnRequest != null)
                    this.OnRequest(this, new MediaItemRequestMessageArgs(request));
            }
            catch (Exception ex)
            {
                this.StatusText.Text = "Fehler: " + ex.Message;
            }
            finally
            {
                this.ShowAllButton.IsEnabled = true;
                this.LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void LargeMapButton_Click(object sender, RoutedEventArgs e)
        {
            this.SaveViewport();
            GeoMapWindow window = new GeoMapWindow(MainWindow.MainWindowStatic);
            window.ShowDialog();
        }

        private async void LoadAsync()
        {
            if (!this.IsBuild || !MediaBrowserContext.SupportsGeoData)
                return;

            Rect viewport = this.mapCanvas.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                this.CenterFromProperties();
                this.viewportTimer.Start();
                return;
            }

            int version = ++this.requestVersion;
            bool includeSingles = this.CheckBoxSingles.IsChecked == true;
            GeoItemsSort sort = this.GetSelectedSort();
            int zoom = this.mapCanvas.Zoom;
            int cellSize = 64;

            this.LoadingText.Text = "Lade Geodaten ...";
            this.LoadingText.Visibility = Visibility.Visible;
            this.StatusText.Text = "Lade ...";

            try
            {
                Tuple<GeoClusterResult, List<MediaItem>> result = await Task.Run(() =>
                {
                    GeoClusterResult clusters = MediaBrowserContext.GetGeoClusters(
                        viewport.X, viewport.Y, viewport.Right, viewport.Bottom,
                        zoom, cellSize, 2000, includeSingles);

                    List<MediaItem> items = MediaBrowserContext.GetGeoItems(
                        viewport.X, viewport.Y, viewport.Right, viewport.Bottom,
                        250, 0, sort);

                    return Tuple.Create(clusters, items);
                });

                if (version != this.requestVersion)
                    return;

                this.currentItems = result.Item2 ?? new List<MediaItem>();
                this.UpdateMarkers(result.Item1);
                this.UpdateList(this.currentItems);

                int clusterCount = result.Item1 == null ? 0 : result.Item1.Clusters.Count;
                int total = result.Item1 == null ? this.currentItems.Count : result.Item1.TotalCount;
                this.StatusText.Text = total + " Medien, " + clusterCount + " Cluster" +
                    (result.Item1 != null && result.Item1.Truncated ? " (gekürzt)" : string.Empty);
            }
            catch (Exception ex)
            {
                if (version == this.requestVersion)
                {
                    this.StatusText.Text = "Fehler: " + ex.Message;
                    this.ClearMarkers();
                }
            }
            finally
            {
                if (version == this.requestVersion)
                    this.LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateMarkers(GeoClusterResult result)
        {
            this.ClearMarkers();

            if (result == null || result.Clusters == null)
                return;

            foreach (GeoCluster cluster in result.Clusters)
            {
                Border marker = this.CreateMarker(cluster);
                this.markers.Add(marker);
                this.mapCanvas.Children.Add(marker);
            }
        }

        private Border CreateMarker(GeoCluster cluster)
        {
            double size = cluster.Count <= 1
                ? 18
                : Math.Min(54, 22 + Math.Log(cluster.Count) * 7);

            Border marker = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2.0),
                Background = cluster.Count <= 1
                    ? new SolidColorBrush(Color.FromRgb(0x2A, 0x7B, 0xD1))
                    : new SolidColorBrush(Color.FromRgb(0xD1, 0x3A, 0x2A)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Tag = cluster,
                ToolTip = cluster.Count + " Medien\n" +
                          cluster.Latitude.ToString("0.00000", CultureInfo.InvariantCulture) + ", " +
                          cluster.Longitude.ToString("0.00000", CultureInfo.InvariantCulture)
            };

            marker.Child = new TextBlock
            {
                Text = cluster.Count.ToString(CultureInfo.InvariantCulture),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = cluster.Count > 99 ? 9 : 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            marker.MouseLeftButtonUp += this.Marker_MouseLeftButtonUp;
            MapCanvas.SetLatitude(marker, cluster.Latitude);
            MapCanvas.SetLongitude(marker, cluster.Longitude);

            return marker;
        }

        private void Marker_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Border marker = sender as Border;
            GeoCluster cluster = marker == null ? null : marker.Tag as GeoCluster;
            if (cluster == null)
                return;

            e.Handled = true;

            int zoom = cluster.Count > 1
                ? Math.Min(18, this.mapCanvas.Zoom + 1)
                : Math.Min(18, this.mapCanvas.Zoom + 2);

            this.mapCanvas.Center(cluster.Latitude, cluster.Longitude, zoom);

            if (cluster.MediaItemId.HasValue)
            {
                GeoListItem item = this.listItems.FirstOrDefault(x => x.Item.Id == cluster.MediaItemId.Value);
                if (item != null)
                    this.ListViewItems.SelectedItem = item;
            }
        }

        private void UpdateList(List<MediaItem> items)
        {
            this.listItems.Clear();

            foreach (MediaItem item in items ?? new List<MediaItem>())
            {
                if (item.Longitude.HasValue && item.Latitude.HasValue)
                    this.listItems.Add(new GeoListItem(item));
            }

            this.ListViewItems.ItemsSource = null;
            this.ListViewItems.ItemsSource = this.listItems;
        }

        private void ListViewItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GeoListItem item = this.ListViewItems.SelectedItem as GeoListItem;
            if (item == null || !item.Item.Longitude.HasValue || !item.Item.Latitude.HasValue)
                return;

            this.suppressViewport = true;
            try
            {
                this.mapCanvas.Center(item.Item.Latitude.Value, item.Item.Longitude.Value,
                    Math.Max(12, this.mapCanvas.Zoom));
            }
            finally
            {
                this.suppressViewport = false;
            }
        }

        private void ListViewItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            GeoListItem item = this.ListViewItems.SelectedItem as GeoListItem;
            if (item == null || this.currentItems.Count == 0)
                return;

            MediaItemSortRequest request = new MediaItemSortRequest("Geodaten",
                "Geodaten im Kartenausschnitt");
            request.MediaItemList = this.currentItems;
            request.SelectedMediaItem = item.Item;
            request.ShuffleType = MediaItemRequestShuffleType.NONE;
            request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE,
                MediaItemRequestSortDirection.ASCENDING));

            if (this.OnRequest != null)
                this.OnRequest(this, new MediaItemRequestMessageArgs(request));
        }

        private GeoItemsSort GetSelectedSort()
        {
            SortOption option = this.ComboBoxSort.SelectedItem as SortOption;
            return option == null ? GeoItemsSort.MediaDateAsc : option.Sort;
        }

        private void ClearMarkers()
        {
            foreach (UIElement marker in this.markers)
                this.mapCanvas.Children.Remove(marker);

            this.markers.Clear();
        }

        private double ReadDoubleProperty(string key, double defaultValue)
        {
            string value = MediaBrowserContext.GetDBProperty(key);
            double result;
            return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                ? result
                : defaultValue;
        }

        private void SaveViewport()
        {
            if (!this.initialized || MediaBrowserContext.MainDBProvider == null)
                return;

            Rect viewport = this.mapCanvas.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return;

            double latitude = viewport.Y + viewport.Height / 2.0;
            double longitude = viewport.X + viewport.Width / 2.0;

            MediaBrowserContext.SetDBProperty("GeoMapLatitude",
                latitude.ToString(CultureInfo.InvariantCulture));
            MediaBrowserContext.SetDBProperty("GeoMapLongitude",
                longitude.ToString(CultureInfo.InvariantCulture));
            MediaBrowserContext.SetDBProperty("GeoMapZoom",
                this.mapCanvas.Zoom.ToString(CultureInfo.InvariantCulture));
            MediaBrowserContext.SaveDBProperties();
        }

        private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
        }

        private sealed class SortOption
        {
            public SortOption(string name, GeoItemsSort sort)
            {
                this.Name = name;
                this.Sort = sort;
            }

            public string Name { get; private set; }
            public GeoItemsSort Sort { get; private set; }

            public override string ToString()
            {
                return this.Name;
            }
        }

        private sealed class GeoListItem
        {
            public GeoListItem(MediaItem item)
            {
                this.Item = item;
            }

            public MediaItem Item { get; private set; }

            public string Filename
            {
                get { return this.Item.Filename; }
            }

            public string MediaDateText
            {
                get { return this.Item.MediaDate.ToString("g"); }
            }

            public string Coordinates
            {
                get
                {
                    return this.Item.Latitude.HasValue && this.Item.Longitude.HasValue
                        ? this.Item.Latitude.Value.ToString("0.00000", CultureInfo.InvariantCulture) + ", " +
                          this.Item.Longitude.Value.ToString("0.00000", CultureInfo.InvariantCulture)
                        : string.Empty;
                }
            }
        }
    }
}
