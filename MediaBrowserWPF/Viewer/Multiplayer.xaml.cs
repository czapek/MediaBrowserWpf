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
using System.Windows.Threading;
using MediaBrowser4;

namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für Multiplayer.xaml
    /// </summary>
    public partial class Multiplayer : Window, IViewer
    {
        public event EventHandler<UserControls.MediaItemArgument> OnMediaItemChanged;
        private MediaItem visibleMediaItem;
        private List<MediaItem> mediaItemList;
        private bool initialSlideshow = true;
        private const int InitialSlideShowIntervalMs = 3000;
        private List<MediaItem> mediaItemListPortrait;
        private List<MediaItem> mediaItemListLandscape;
        private List<ViewerBaseControl> viewerControlList = new List<ViewerBaseControl>();
        private ViewerBaseControl viewerBaseActive;
        private double portraitsRel;
        DispatcherTimer toolTipTimer;
        DispatcherTimer slidShowTimer;
        IMultiplayerCtrl multiplayer;
        System.Windows.Forms.Screen currentScreen;
        private Multiplayer()
        {
            InitializeComponent();

            SetBackground();
        }

        private void SetBackground()
        {
            RadialGradientBrush myBrush = new RadialGradientBrush();
            myBrush.GradientOrigin = new Point(0.75, 0.25);
            myBrush.GradientStops.Add(new GradientStop(Colors.LightSteelBlue, 0.0));
            // myBrush.GradientStops.Add(new GradientStop(Colors.LightSteelBlue, 0.5));
            myBrush.GradientStops.Add(new GradientStop(Colors.DarkSlateGray, 1.5));

            this.Background = myBrush;
        }

        public bool IsValid { get; set; }

        public Multiplayer(List<MediaItem> mediaItemList, MediaItem currentMediaItem, System.Windows.Forms.Screen screen, int colCount, int rowCount)
        {
            this.IsValid = false;
            this.currentScreen = screen;

            this.mediaItemList = mediaItemList.Where(x => !x.IsDeleted).ToList();
            this.mediaItemListLandscape = this.mediaItemList.Where(x => x.Relation >= 1.0).ToList();
            this.mediaItemListPortrait = this.mediaItemList.Where(x => x.Relation < 1.0).ToList();
            this.portraitsRel = (double)mediaItemListPortrait.Count / (double)this.mediaItemList.Count;

            int pos = this.mediaItemList.IndexOf(currentMediaItem);
            pos = pos == 0 ? this.mediaItemList.Count - 1 : pos - 1;
            this.visibleMediaItem = this.mediaItemList[pos];

            InitializeComponent();

            SetBackground();

            this.toolTipTimer = new DispatcherTimer();
            this.toolTipTimer.IsEnabled = false;
            this.toolTipTimer.Interval = new TimeSpan(0, 0, 0, 3, 0);
            this.toolTipTimer.Tick += new EventHandler(toolTipTimer_Tick);

            this.slidShowTimer = new DispatcherTimer();
            this.slidShowTimer.IsEnabled = false;
            this.slidShowTimer.Interval = new TimeSpan(0, 0, 0, 0, InitialSlideShowIntervalMs);
            this.slidShowTimer.Tick += new EventHandler(slidShowTimer_Tick);

            if (colCount < 1 || rowCount < 1)
            {
                colCount = (int)Math.Max(screen.Bounds.Width, 1600) / 800;
                rowCount = (int)Math.Max(screen.Bounds.Height, 1000) / 500;

                //alles Portraits
                if (portraitsRel == 1)
                {
                    colCount = (int)(screen.Bounds.Width / (screen.Bounds.Height * .7));
                    rowCount = 1;
                }

                double row2 = screen.Bounds.Width / (screen.Bounds.Height / (2.0 * .7));
                double row3 = screen.Bounds.Width / (screen.Bounds.Height / (3.0 * .7));
                double row4 = screen.Bounds.Width / (screen.Bounds.Height / (4.0 * .7));

                //alles Panorama
                if (this.mediaItemList.Count(x => x.Relation >= row2) == this.mediaItemList.Count)
                {
                    colCount = 1;
                    rowCount = 2;

                    if (this.mediaItemList.Count(x => x.Relation >= row3) == this.mediaItemList.Count)
                    {
                        rowCount = 3;
                    }
                    else if (this.mediaItemList.Count(x => x.Relation >= row4) == this.mediaItemList.Count)
                    {
                        rowCount = 4;
                    }
                }
            }

            this.initialSlideshow = this.mediaItemList.Count > 20;
            this.multiplayer = new MultiplayerCtrl_Generic(this.mediaItemList.Count, colCount, rowCount, this.initialSlideshow);

            this.multiplayer.MouseDoubleClick += new EventHandler<MouseButtonEventArgs>(Viewer1_MouseDoubleClick);
            this.multiplayer.MouseDown += new EventHandler<MouseButtonEventArgs>(Viewer1_MouseDown);
            this.multiplayer.MediaLoaded += new EventHandler(multiplayer_MediaLoaded);

            this.viewerControlList = this.multiplayer.ViewerBaseControlList;
            this.MainGrid.Children.Add((UserControl)this.multiplayer);

            while (this.mediaItemList.Count < this.viewerControlList.Count)
            {
                this.viewerControlList.RemoveAt(this.viewerControlList.Count - 1);
            }

            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
            {
                viewerBase.Volume = 0;
                viewerBase.EndReached += new EventHandler(viewerBase_EndReached);
            }

            this.viewerBaseActive = this.viewerControlList[0];
            this.viewerBaseActive.Volume = 100;

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                foreach (ViewerBaseControl viewer in this.viewerControlList)
                    this.SetNextItem(viewer);

                this.IsSlideShow = this.initialSlideshow;
                //this.IsVideoSlideShow = false;

                this.InfoTextToolTip = "";
                this.Cursor = Cursors.None;

            }), DispatcherPriority.ApplicationIdle);

            this.IsValid = true;
        }

        void multiplayer_MediaLoaded(object sender, EventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        void viewerBase_EndReached(object sender, EventArgs e)
        {
            if (this.isVideoSlideShow)
            {
                ViewerBaseControl viewerBase = sender as ViewerBaseControl;
                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    this.SetNextItem(viewerBase);
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        void toolTipTimer_Tick(object sender, EventArgs e)
        {
            ClearToolTip();
        }

        private void ClearToolTip()
        {
            this.InfoTextCenter.Visibility = System.Windows.Visibility.Collapsed;
            this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Collapsed;
            this.InfoTextCenter.Text = null;
            this.InfoTextCenterBlur.Text = null;
            toolTipTimer.IsEnabled = false;
        }

        private void SetPreviousItem(ViewerBaseControl viewerBase)
        {
            if (viewerBase.SetPreviousItemFromHistory() && this.Cursor != Cursors.None)
                Mouse.OverrideCursor = Cursors.Wait;

            if (this.OnMediaItemChanged != null)
                this.OnMediaItemChanged.Invoke(this, new MediaItemArgument(viewerBase.Source));

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                this.Focus();
            }), DispatcherPriority.ApplicationIdle);
        }

        private bool isSlideShow = false;
        private int slideshowCnt;
        public bool IsSlideShow
        {
            get
            {
                return this.isSlideShow;
            }

            set
            {
                if (!value)
                    this.IsVideoSlideShow = false;

                this.isSlideShow = value;
                this.MenuItemSlideshow.IsChecked = value;
                this.MenuItemSlideshowVideo.IsEnabled = this.isSlideShow;
                this.slideshowCnt = 0;
                this.InfoTextToolTip = "Diaschau: " + (value ? "an" : "aus");
                this.slidShowTimer.IsEnabled = value;
            }
        }

        private bool isVideoSlideShow = false;
        public bool IsVideoSlideShow
        {
            get
            {
                return this.isVideoSlideShow;
            }

            set
            {
                if (IsSlideShow)
                {
                    this.isVideoSlideShow = value;
                    this.MenuItemSlideshowVideo.IsChecked = value;

                    foreach (ViewerBaseControl viewer in this.viewerControlList)
                        viewer.IsLoop = !value;

                    this.InfoTextToolTip = "Video Diaschau: " + (value ? "an" : "aus");
                }
            }
        }

        void slidShowTimer_Tick(object sender, EventArgs e)
        {
            ViewerBaseControl viewerBase = this.viewerControlList[this.slideshowCnt];

            if ((this.isVideoSlideShow && viewerBase.Source is MediaItemBitmap) || !this.isVideoSlideShow)
                this.SetNextItem(viewerBase);

            this.slideshowCnt++;

            if (this.slideshowCnt >= this.viewerControlList.Count)
                this.slideshowCnt = 0;

            this.Cursor = Cursors.None;
        }

        private void SetActive(ViewerBaseControl viewerBase)
        {
            int sound = 0;
            this.viewerBaseActive = viewerBase;
            foreach (ViewerBaseControl viewer in this.viewerControlList)
            {
                if (viewer.Volume > 0)
                    sound = viewer.Volume;

                viewer.Volume = 0;
            }

            SetDefaultInfoText();
            this.viewerBaseActive.Volume = sound;
            this.viewerBaseActive.SetActive();

            if (this.OnMediaItemChanged != null)
                this.OnMediaItemChanged.Invoke(this, new MediaItemArgument(this.viewerBaseActive.Source));

            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                this.Focus();
            }), DispatcherPriority.ApplicationIdle);
        }

        private double CurrentVideoAreaRelToScreen()
        {
            double area = 0;
            foreach (ViewerBaseControl viewer in this.viewerControlList)
            {
                MediaItemVideo videoItem = viewer.Source as MediaItemVideo;

                if (videoItem != null)
                {
                    area += videoItem.Width * videoItem.Height;
                }
            }

            return area / (this.currentScreen.Bounds.Width * this.currentScreen.Bounds.Height);
        }

        private void SetNextItem(ViewerBaseControl viewerBase)
        {
            if (viewerBase.SetNextItemFromHistory())
            {
                return;
            }

            MediaItem oldMediaItem = this.visibleMediaItem == null || this.visibleMediaItem.IsDeleted ? null : this.visibleMediaItem;
            Mouse.OverrideCursor = this.Cursor != Cursors.None ? Cursors.Wait : Cursors.None;
            bool alreadyVisible = true;
            
            int cnt = 0;
            this.FindNextMediaItem(viewerBase);

            while (alreadyVisible && oldMediaItem != this.visibleMediaItem && cnt <= this.viewerControlList.Count + 1)
            {
                alreadyVisible = false;
                foreach (ViewerBaseControl viewer in this.viewerControlList)
                {
                    if (viewer.Source == this.visibleMediaItem)
                    {
                        alreadyVisible = true;
                        break;
                    }
                }

                if (alreadyVisible)
                    this.FindNextMediaItem(viewerBase);

                cnt++;
            }

            if (this.visibleMediaItem == null || alreadyVisible && (this.visibleMediaItem != oldMediaItem || viewerBase.Source != null))
            {
                if (viewerBase.Source != null && viewerBase.Source.IsDeleted
                    || this.visibleMediaItem == null || this.visibleMediaItem.IsDeleted)
                {
                    viewerBase.Source = null;
                }

                this.visibleMediaItem = oldMediaItem;
                Mouse.OverrideCursor = null;
            }
            else
            {
                if (this.OnMediaItemChanged != null)
                    this.OnMediaItemChanged.Invoke(this, new MediaItemArgument(this.VisibleMediaItem));

                viewerBase.Source = this.visibleMediaItem;


                if (this.isSlideShow && this.visibleMediaItem is MediaItemVideo && this.visibleMediaItem.Duration > this.slidShowTimer.Interval.TotalSeconds * this.viewerControlList.Count)
                {
                    viewerBase.VideoPlayer.VideoPlayerIntern.TimeMilliseconds = (long)(this.visibleMediaItem.Duration * 1000) / 2;
                }

                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    this.Focus();
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        private void FindNextMediaItem(ViewerBaseControl viewerBase)
        {
            List<MediaItem> mList = this.mediaItemList;

            //falls das visibleMediaItem nicht mehr gegeben irgendeins nehmen
            this.visibleMediaItem = this.visibleMediaItem ?? viewerBase.Source;
            if (this.visibleMediaItem == null)
            {
                foreach (ViewerBaseControl viewer in this.viewerControlList)
                {
                    if (viewer.Source != null)
                    {
                        this.visibleMediaItem = viewer.Source;
                        break;
                    }
                }
            }        
            
            int cnt = this.mediaItemList.IndexOf(this.visibleMediaItem);
            MediaItem nextMediaItem = this.visibleMediaItem;

            if (this.mediaItemList.Count > 1)
            {
                do
                {
                    cnt = (this.mediaItemList.Count > cnt + 1 ? cnt + 1 : 0);
                    nextMediaItem = mediaItemList[cnt];
                }
                while (!mList.Contains(this.visibleMediaItem) || viewerBase.Source == this.visibleMediaItem);
            }
            else if (this.mediaItemList.Count == 1)
            {
                nextMediaItem = this.mediaItemList[0];
            }

            //wenn > 1 haben wir vermutlich ein Performanceproblem
            //Ist Performancelimit ist erreicht, dann werden bestehende Bilder nicht mehr mit Videos überschrieben,
            //nur noch vorhandene videos, so lange bis das Limit wieder runtergeht
            //if (CurrentVideoAreaRelToScreen() > 1 && this.visibleMediaItem is MediaItemBitmap && nextMediaItem is MediaItemVideo)
            //{
            //    this.visibleMediaItem = nextMediaItem;
            //    //System.Diagnostics.Debug.WriteLine("Nein: " + relVideo);
            //}
            //else
            //{
            //    this.visibleMediaItem = nextMediaItem;
            //    //System.Diagnostics.Debug.WriteLine("Ja: " + relVideo);
            //}

            this.visibleMediaItem = nextMediaItem;
        }

        public MediaItem VisibleMediaItem
        {
            get
            {
                return this.visibleMediaItem;
            }

            set
            {
                this.visibleMediaItem = value;
            }
        }

        public bool ShowDeleted { get; set; }

        public void Dispose()
        {
            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
            {
                viewerBase.Dispose();
            }

            this.toolTipTimer.Stop();
            this.slidShowTimer.Stop();

            Mouse.OverrideCursor = null;

            this.Close();
        }

        private double slideShowFactor = 1.0;
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int number;
            if (e.Key.ToString().StartsWith("D")
                && Int32.TryParse(e.Key.ToString().Substring(1), out number))
            {
                if (this.viewerControlList.Count >= number && number > 0)
                {
                    this.slidShowTimer.Stop();

                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.SetPreviousItem(this.viewerControlList[number - 1]);
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        this.SetNextItem(this.viewerControlList[number - 1]);
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                        this.SetActive(this.viewerControlList[number - 1]);

                    if (IsSlideShow)
                    {
                        this.slidShowTimer.Start();
                    }
                }

                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                this.viewerBaseActive.Volume += (e.Key == Key.Up ? 5 : -5);
                this.InfoTextToolTip = "Volume: " + this.viewerBaseActive.Volume;

                return;
            }

            switch (e.SystemKey)
            {
                case Key.F10:
                    this.IsSlideShow = !this.IsSlideShow;
                    break;
            }

            if (e.SystemKey != Key.None)
            {
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case Key.Escape:
                    this.Dispose();
                    break;

                case Key.Back:
                    foreach (ViewerBaseControl viewerBase in this.viewerControlList)
                        viewerBase.ResetTransform();
                    break;

                case Key.Delete:
                    if (this.viewerBaseActive.Source != null && userSetActive)
                    {
                        MediaBrowserContext.RemoveVariation(this.viewerBaseActive.Source);
                        this.viewerBaseActive.Source.IsDeleted = true;
                        RemoveItem();
                    }
                    break;

                case Key.F9:
                    this.IsVideoSlideShow = !this.IsVideoSlideShow;
                    break;

                case Key.Add:
                case Key.OemPlus:
                    this.slideShowFactor *= 1.25;
                    this.slidShowTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(InitialSlideShowIntervalMs * slideShowFactor));
                    this.InfoTextToolTip = "Abspiel-Faktor: " + String.Format("{0:n2}x", this.slideShowFactor);
                    break;

                case Key.Subtract:
                case Key.OemMinus:
                    this.slideShowFactor *= .75;
                    this.slideShowFactor = System.Math.Max(this.slideShowFactor, 0.01);
                    this.slidShowTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(InitialSlideShowIntervalMs * slideShowFactor));
                    this.InfoTextToolTip = "Abspiel-Faktor: " + String.Format("{0:n2}x", this.slideShowFactor);
                    break;

                case Key.I:
                    this.viewerBaseActive.SetActive();
                    SetDefaultInfoText();
                    break;

                case Key.Enter:
                    this.MainView();
                    break;

                case Key.PageUp:
                    this.AllBackward();
                    break;

                case Key.PageDown:
                    this.AllForward();
                    break;

                case Key.Tab:
                    int viewPos = this.viewerControlList.FindIndex(x => x == this.viewerBaseActive);
                    viewPos = viewPos + 1 >= this.viewerControlList.Count ? 0 : viewPos + 1;
                    userSetActive = true;
                    this.SetActive(this.viewerControlList[viewPos]);
                    break;

                case Key.Space:
                    this.slidShowTimer.Stop();

                    if (Keyboard.Modifiers == ModifierKeys.None)
                        this.SetNextItem(this.viewerBaseActive);
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.SetPreviousItem(this.viewerBaseActive);

                    if (IsSlideShow)
                    {
                        this.slidShowTimer.Start();
                    }

                    break;
            }
        }

        private void SetDefaultInfoText()
        {
            this.InfoTextToolTip = this.viewerBaseActive.Source.MediaDate.ToString("D") + Environment.NewLine
                + this.viewerBaseActive.Source.MediaDate.ToString("T") + " Uhr" + Environment.NewLine
                + this.viewerBaseActive.Source.Filename + Environment.NewLine
                + (this.viewerBaseActive.Source is MediaItemVideo ? MediaViewer.GetVideoDuration(this.viewerBaseActive.Source) + Environment.NewLine : String.Empty)
                + String.Join(", ", this.viewerBaseActive.Source.Categories.Where(x => !x.IsDate).Select(x => x.NameDate));
        }

        private void AllBackward()
        {
            this.slidShowTimer.Stop();

            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
                this.SetPreviousItem(viewerBase);

            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }

        private void AllForward()
        {
            this.slidShowTimer.Stop();

            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
                this.SetNextItem(viewerBase);

            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }

        public string InfoTextToolTip
        {
            set
            {
                if (value == null || value.Trim().Length == 0)
                {
                    this.InfoTextCenter.Text = null;
                    this.InfoTextCenter.Visibility = System.Windows.Visibility.Collapsed;
                    this.InfoTextCenterBlur.Text = null;
                    this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Collapsed;
                    return;
                }

                this.InfoTextCenter.Text = value;
                this.InfoTextCenter.Visibility = System.Windows.Visibility.Visible;
                this.InfoTextCenterBlur.Text = value;
                this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Visible;
                toolTipTimer.Stop();
                toolTipTimer.Start();
                toolTipTimer.IsEnabled = true;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            this.slidShowTimer.Stop();

            if (e.Delta < 0)
            {
                this.SetNextItem(this.viewerBaseActive);
            }
            else
            {
                this.SetPreviousItem(this.viewerBaseActive);
            }

            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }

        private bool userSetActive = false;
        private void Viewer1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ViewerBaseControl viewerBase = sender as ViewerBaseControl;
                userSetActive = true;
                this.SetActive(viewerBase);
            }
        }

        private void Viewer1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.MainView();
        }

        private byte infoState = 0;
        private void MainView()
        {
            this.slidShowTimer.IsEnabled = false;

            string layers = String.Join("\r\n", this.viewerBaseActive.Source.Layers.Select(x => x.Edit + ": " + x.Action));

            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
            {
                viewerBase.Pause();
            }

            MediaViewer mediaViewer = new MediaViewer(new List<MediaItem>() { this.viewerBaseActive.Source }, this.viewerBaseActive.Source, this.viewerBaseActive.TimeMilliseconds);
            mediaViewer.Topmost = true;
            mediaViewer.InfoState = infoState;

            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(MainWindow.MainWindowStatic).Handle);
            mediaViewer.Top = screen.Bounds.Y;
            mediaViewer.Left = screen.Bounds.X;

            mediaViewer.ShowDialog();

            infoState = mediaViewer.InfoState;
            this.slidShowTimer.IsEnabled = this.IsSlideShow;

            if (this.viewerBaseActive.Source.IsDeleted)
            {
                if (RemoveItem())
                    return;
            }

            if (this.viewerBaseActive.Source != null && String.Join("\r\n", this.viewerBaseActive.Source.Layers.Select(x => x.Edit + ": " + x.Action)) != layers)
                this.viewerBaseActive.Source = this.viewerBaseActive.Source;

            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
                viewerBase.Play();
        }

        private bool RemoveItem()
        {
            this.mediaItemList.Remove(this.viewerBaseActive.Source);
            if (this.mediaItemList.Count <= 0)
            {
                this.Dispose();
                return true;
            }

            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
                if (viewerBase.Source != null && viewerBase.Source == this.viewerBaseActive.Source)
                    this.SetNextItem(viewerBase);

            return false;
        }

        private void MenuItemEsc_Click(object sender, RoutedEventArgs e)
        {
            this.Dispose();
        }

        private void MenuItemSlideshow_Click(object sender, RoutedEventArgs e)
        {
            this.IsSlideShow = this.MenuItemSlideshow.IsChecked;
        }

        private void MenuItemSlideshowVideo_Click(object sender, RoutedEventArgs e)
        {
            this.IsVideoSlideShow = this.MenuItemSlideshowVideo.IsChecked;
        }

        private void MenuItemSViewer_Click(object sender, RoutedEventArgs e)
        {
            this.MainView();
        }

        private void MenuItemAllForward_Click(object sender, RoutedEventArgs e)
        {
            this.AllForward();
        }

        private void MenuItemAllBackward_Click(object sender, RoutedEventArgs e)
        {
            this.AllBackward();
        }

        private void MenuItemBackward_Click(object sender, RoutedEventArgs e)
        {
            this.slidShowTimer.Stop();

            this.SetPreviousItem(this.viewerBaseActive);

            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }

        private void MenuItemForward_Click(object sender, RoutedEventArgs e)
        {
            this.slidShowTimer.Stop();

            this.SetNextItem(this.viewerBaseActive);

            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        Point lastMousePosition;
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            Point nowPosition = e.GetPosition(this);

            if (!(lastMousePosition.X == 0 && lastMousePosition.Y == 0)
                &&
                (Math.Abs(lastMousePosition.X - nowPosition.X) > 2
                || Math.Abs(lastMousePosition.Y - nowPosition.Y) > 2))
            {

                if (isSlideShow)
                {
                    this.slidShowTimer.Stop();
                    this.slidShowTimer.Start();
                }

                this.Cursor = Cursors.Arrow;
            }

            lastMousePosition = e.GetPosition(this);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.slidShowTimer.Stop();
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }

        private void InfoTextCenter_MouseMove(object sender, MouseEventArgs e)
        {
            ClearToolTip();
        }

        private void MenuItemResetTransform_Click(object sender, RoutedEventArgs e)
        {
            foreach (ViewerBaseControl viewerBase in this.viewerControlList)
                viewerBase.ResetTransform();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            this.slidShowTimer.Stop();
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (IsSlideShow)
            {
                this.slidShowTimer.Start();
            }
        }
    }
}
