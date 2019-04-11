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
using System.Windows.Threading;
using System.Windows.Media.Media3D;
using MediaBrowser4.Objects;
using System.Threading;
using MediaBrowserWPF.UserControls.RgbImage;
using System.IO;
using MediaBrowser4;

namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für Cube3D.xaml
    /// </summary>
    public partial class Cube3D : Window
    {
        DispatcherTimer _timer = null;
        PerspectiveCamera _perspectiveCamera = null;
        double _angle = 0;
        private List<DiffuseMaterial> diffuseMaterialList = new List<DiffuseMaterial>();
        private List<MediaItem> mediaItemListBitmap;
        private List<MediaItem> mediaItemListVideo;
        private List<MediaItem> mediaItemList;
        private List<MediaItem> mediaItemListBackground;
        DispatcherTimer toolTipTimer;
        DispatcherTimer slidShowTimer;

        public enum Projection { BITMAP, VIDEO, MIXED, THUMBNAIL }
        Projection projection = Projection.THUMBNAIL;

        private Cube3D(List<MediaItem> mediaItemList, Projection projection)
        {
            this.projection = projection;
            this.mediaItemList = new List<MediaItem>(mediaItemList);

            if (projection == Projection.BITMAP)
            {
                this.mediaItemListBitmap = mediaItemList.Where(x => x is MediaItemBitmap).ToList();
            }
            else if (projection == Projection.VIDEO)
            {
                this.mediaItemListVideo = mediaItemList.Where(x => x is MediaItemVideo).ToList();
            }

            this.mediaItemListBackground = MediaBrowserContext.GetBookmarkedMediaItems()
                .Where(x => x is MediaItemBitmap && x.Relation > 1.2).ToList();

            if (this.mediaItemListBackground.Count == 0)
                this.mediaItemListBackground = this.mediaItemList.Where(x => x is MediaItemBitmap && x.Relation > 1.2).ToList();

            this.toolTipTimer = new DispatcherTimer();
            this.toolTipTimer.IsEnabled = false;
            this.toolTipTimer.Interval = new TimeSpan(0, 0, 0, 3, 0);
            this.toolTipTimer.Tick += new EventHandler(toolTipTimer_Tick);

            this.slidShowTimer = new DispatcherTimer();
            this.slidShowTimer.IsEnabled = false;
            this.slidShowTimer.Interval = new TimeSpan(0, 0, 0, 60, 0);
            this.slidShowTimer.Tick += new EventHandler(slidShowTimer_Tick);

            InitializeComponent();
        }

        public static Cube3D Cube3DFactory(List<MediaItem> mediaItemList, Projection projection)
        {
            return new Cube3D(mediaItemList, projection);
        }
        void slidShowTimer_Tick(object sender, EventArgs e)
        {
            this.StepNext();
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            this.GenerateViewPort();
            this.StepNext();
        }

        Thread thread;
        bool isStepNext;
        List<string> filenames;
        Object lockObject = new Object();
        private void StepNext()
        {
            if (projection == Projection.BITMAP)
            {
                lock (lockObject)
                {
                    if (thread != null && thread.IsAlive)
                        return;

                    Mouse.OverrideCursor = Cursors.Wait;
                    thread = new Thread(new ThreadStart(this.work));
                    thread.IsBackground = true;
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    isStepNext = true;
                }
            }
            else if (projection == Projection.THUMBNAIL)
            {
                Random rand = new Random();
                MediaItem mItem = this.mediaItemListBackground[rand.Next(this.mediaItemListBackground.Count)];
                this.SetBackground(mItem.FullName);

                ImageBrush imageBrush = new ImageBrush();
                BitmapImage bmp = new BitmapImage();
                foreach (DiffuseMaterial diff in this.diffuseMaterialList)
                {
                    mItem = this.mediaItemList[rand.Next(this.mediaItemList.Count)];

                    try
                    {
                        ImageBrush brush;
                        BitmapImage bi;
                        using (var ms = new MemoryStream(mItem.ThumbJpegData))
                        {
                            brush = new ImageBrush();

                            bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CreateOptions = BitmapCreateOptions.None;
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.StreamSource = ms;
                            bi.EndInit();
                        }

                        brush.Stretch = Stretch.UniformToFill;
                        brush.ImageSource = bi;
                        diff.Brush = imageBrush;
                        isStepNext = true;
                    }
                    catch { }
                }

            }
        }

        private void SetBackground(String filename)
        {
            ImageBrush imageBrush = new ImageBrush();
            BitmapImage bmp = new BitmapImage();

            try
            {
                bmp.BeginInit();
                bmp.UriSource = new Uri(filename);
                bmp.EndInit();
                imageBrush.Stretch = Stretch.UniformToFill;
                imageBrush.ImageSource = bmp;
                this.Background = imageBrush;
            }
            catch { }
        }

        private void SetImages()
        {
            ImageBrush imageBrush = new ImageBrush();
            BitmapImage bmp = new BitmapImage();

            this.SetBackground(this.filenames[0]);

            int cnt = 1;
            foreach (DiffuseMaterial diff in this.diffuseMaterialList)
            {
                try
                {
                    imageBrush = new ImageBrush();
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(this.filenames[cnt]);
                    bmp.EndInit();

                    imageBrush.Stretch = Stretch.UniformToFill;
                    imageBrush.ImageSource = bmp;
                    diff.Brush = imageBrush;
                }
                catch { }

                cnt++;
            }
        }

        void work()
        {
            int width = (int)(Math.Max(System.Windows.SystemParameters.PrimaryScreenWidth, System.Windows.SystemParameters.PrimaryScreenHeight) * .8);
            ImageControl imageControl = new ImageControl();
            imageControl.IsInvisibleRender = true;
            Random rand = new Random();
            this.filenames = new List<string>();
            MediaItem mItem = null;

            for (int i = 0; i <= 7; i++)
            {
                if (i == 0 && this.mediaItemListBackground.Count > 0)
                    mItem = this.mediaItemListBackground[rand.Next(this.mediaItemListBackground.Count)];
                else
                    mItem = this.mediaItemListBitmap[rand.Next(this.mediaItemListBitmap.Count)];

                string filename = MediaBrowserContext.DBTempFolder + "\\3DCube_" + mItem.Md5Value + i + ".jpg";
                if (i == 0)
                    filename = MediaBrowserContext.DBTempFolder + "\\3DCube_background_" + mItem.Md5Value + i + ".jpg";

                if (mItem.Height > width && mItem.Width > width)
                {
                    if (!File.Exists(filename))
                    {
                        System.Drawing.Bitmap bmp = System.Drawing.Image.FromFile(mItem.FullName) as System.Drawing.Bitmap;

                        if (i == 0)
                            bmp = MediaProcessing.ResizeImage.ActionCropIn(bmp, new System.Drawing.Size((int)System.Windows.SystemParameters.PrimaryScreenWidth, (int)System.Windows.SystemParameters.PrimaryScreenHeight), new System.Drawing.Size(), System.Drawing.Color.Black, 0);
                        else
                            bmp = MediaProcessing.ResizeImage.ActionCropIn(bmp, new System.Drawing.Size(width, width), new System.Drawing.Size(5, 5), System.Drawing.Color.Azure, 0);

                        bmp = MediaProcessing.SharpenImage.Work(bmp, MediaProcessing.SharpenImage.Quality.SOFT);
                        MediaProcessing.EncodeImage.SaveJPGFile(bmp, filename, 100);
                    }
                    filenames.Add(filename);
                }
                else
                {
                    filenames.Add(mItem.FullName);
                }
            }
        }

        private void GenerateViewPort()
        {
            MakeCamera();

            Viewport3D Viewport3D1 = new Viewport3D();
            Viewport3D1.Camera = _perspectiveCamera;
            mainContainer.Children.Add(Viewport3D1);
            Viewport3D1.Loaded += new RoutedEventHandler(Viewport3D1_Loaded);

            ModelVisual3D ModelVisual3D1 = new ModelVisual3D();
            Viewport3D1.Children.Add(ModelVisual3D1);

            Model3DGroup Model3DGroup1 = new Model3DGroup();
            ModelVisual3D1.Content = Model3DGroup1;

            AmbientLight AmbientLight1 = new AmbientLight();
            AmbientLight1.Color = Colors.LightGray;
            Model3DGroup1.Children.Add(AmbientLight1);

            DirectionalLight DirectionalLight1 = new DirectionalLight();
            DirectionalLight1.Color = Colors.White;
            DirectionalLight1.Direction = ((Vector3D)new Vector3DConverter().ConvertFromString("-1,-3,-2"));
            Model3DGroup1.Children.Add(DirectionalLight1);

            DirectionalLight1 = new DirectionalLight();
            DirectionalLight1.Color = Colors.White;
            DirectionalLight1.Direction = ((Vector3D)new Vector3DConverter().ConvertFromString("1,-2,3"));
            Model3DGroup1.Children.Add(DirectionalLight1);

            Model3DGroup Model3DGroup2 = new Model3DGroup();
            Model3DGroup1.Children.Add(Model3DGroup2);

            GeometryModel3D GeometryModel3D1 = new GeometryModel3D();
            Model3DGroup2.Children.Add(GeometryModel3D1);

            MeshGeometry3D MeshGeometry3D1 = new MeshGeometry3D();
            MeshGeometry3D1.Positions = ((Point3DCollection)new Point3DCollectionConverter().ConvertFromString("-1,-1,-1 1,-1,-1 1,-1,1 -1,-1,1"));
            MeshGeometry3D1.TriangleIndices = ((Int32Collection)new Int32CollectionConverter().ConvertFromString("0,1,2 0,2,3"));
            MeshGeometry3D1.TextureCoordinates = ((PointCollection)new PointCollectionConverter().ConvertFromString("0,0 0,1 1,1 1,0"));
            GeometryModel3D1.Geometry = MeshGeometry3D1;

            DiffuseMaterial DiffuseMaterial1 = new DiffuseMaterial();
            GeometryModel3D1.Material = DiffuseMaterial1;
            this.diffuseMaterialList.Add(DiffuseMaterial1);

            GeometryModel3D GeometryModel3D2 = new GeometryModel3D();
            Model3DGroup2.Children.Add(GeometryModel3D2);

            MeshGeometry3D1 = new MeshGeometry3D();
            MeshGeometry3D1.Positions = ((Point3DCollection)new Point3DCollectionConverter().ConvertFromString("1,1,1 1,1,-1 -1,1,-1 -1,1,1"));
            MeshGeometry3D1.TriangleIndices = ((Int32Collection)new Int32CollectionConverter().ConvertFromString("0,1,2 0,2,3"));
            MeshGeometry3D1.TextureCoordinates = ((PointCollection)new PointCollectionConverter().ConvertFromString("0,0 0,1 1,1 1,0"));
            GeometryModel3D2.Geometry = MeshGeometry3D1;

            DiffuseMaterial DiffuseMaterial2 = new DiffuseMaterial();
            GeometryModel3D2.Material = DiffuseMaterial2;
            this.diffuseMaterialList.Add(DiffuseMaterial2);

            GeometryModel3D GeometryModel3D3 = new GeometryModel3D();
            Model3DGroup2.Children.Add(GeometryModel3D3);

            MeshGeometry3D1 = new MeshGeometry3D();
            MeshGeometry3D1.Positions = ((Point3DCollection)new Point3DCollectionConverter().ConvertFromString("-1,1,-1 -1,-1,-1 -1,-1,1 -1,1,1"));
            MeshGeometry3D1.TriangleIndices = ((Int32Collection)new Int32CollectionConverter().ConvertFromString("0,1,2 0,2,3"));
            MeshGeometry3D1.TextureCoordinates = ((PointCollection)new PointCollectionConverter().ConvertFromString("0,0 0,1 1,1 1,0"));
            GeometryModel3D3.Geometry = MeshGeometry3D1;

            DiffuseMaterial DiffuseMaterial3 = new DiffuseMaterial();
            GeometryModel3D3.Material = DiffuseMaterial3;
            this.diffuseMaterialList.Add(DiffuseMaterial3);

            GeometryModel3D GeometryModel3D4 = new GeometryModel3D();
            Model3DGroup2.Children.Add(GeometryModel3D4);

            MeshGeometry3D1 = new MeshGeometry3D();
            MeshGeometry3D1.Positions = ((Point3DCollection)new Point3DCollectionConverter().ConvertFromString("1,1,1 1,-1,1 1,-1,-1 1,1,-1"));
            MeshGeometry3D1.TriangleIndices = ((Int32Collection)new Int32CollectionConverter().ConvertFromString("0,1,2 0,2,3"));
            MeshGeometry3D1.TextureCoordinates = ((PointCollection)new PointCollectionConverter().ConvertFromString("0,0 0,1 1,1 1,0"));
            GeometryModel3D4.Geometry = MeshGeometry3D1;

            DiffuseMaterial DiffuseMaterial4 = new DiffuseMaterial();
            GeometryModel3D4.Material = DiffuseMaterial4;
            this.diffuseMaterialList.Add(DiffuseMaterial4);

            GeometryModel3D GeometryModel3D5 = new GeometryModel3D();
            Model3DGroup2.Children.Add(GeometryModel3D5);

            MeshGeometry3D1 = new MeshGeometry3D();
            MeshGeometry3D1.Positions = ((Point3DCollection)new Point3DCollectionConverter().ConvertFromString("1,1,-1 1,-1,-1 -1,-1,-1 -1,1,-1"));
            MeshGeometry3D1.TriangleIndices = ((Int32Collection)new Int32CollectionConverter().ConvertFromString("0,1,2 0,2,3"));
            MeshGeometry3D1.TextureCoordinates = ((PointCollection)new PointCollectionConverter().ConvertFromString("0,0 0,1 1,1 1,0"));
            GeometryModel3D5.Geometry = MeshGeometry3D1;

            DiffuseMaterial DiffuseMaterial5 = new DiffuseMaterial();
            GeometryModel3D5.Material = DiffuseMaterial5;
            this.diffuseMaterialList.Add(DiffuseMaterial5);

            GeometryModel3D GeometryModel3D6 = new GeometryModel3D();
            Model3DGroup2.Children.Add(GeometryModel3D6);

            MeshGeometry3D1 = new MeshGeometry3D();
            MeshGeometry3D1.Positions = ((Point3DCollection)new Point3DCollectionConverter().ConvertFromString("-1,1,1 -1,-1,1 1,-1,1 1,1,1"));
            MeshGeometry3D1.TriangleIndices = ((Int32Collection)new Int32CollectionConverter().ConvertFromString("0,1,2 0,2,3"));
            MeshGeometry3D1.TextureCoordinates = ((PointCollection)new PointCollectionConverter().ConvertFromString("0,0 0,1 1,1 1,0"));
            GeometryModel3D6.Geometry = MeshGeometry3D1;

            DiffuseMaterial DiffuseMaterial6 = new DiffuseMaterial();
            GeometryModel3D6.Material = DiffuseMaterial6;
            this.diffuseMaterialList.Add(DiffuseMaterial6);

            //VisualBrush VisualBrush1 = new VisualBrush();
            //DiffuseMaterial6.Brush = VisualBrush1;            
            //VisualBrush1.Visual = viewer1;            
        }

        private void MakeCamera()
        {
            Transform3DGroup transform3DGroup = new Transform3DGroup();
            RotateTransform3D rotateTransform3D_1 = new RotateTransform3D();
            AxisAngleRotation3D axisAngleRotation3D_1 = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);

            transform3DGroup.Children.Add(rotateTransform3D_1);

            _perspectiveCamera = new PerspectiveCamera();
            _perspectiveCamera.Position = new Point3D(0, 0, 5);
            _perspectiveCamera.LookDirection = new Vector3D(0, 0, -5);
            _perspectiveCamera.UpDirection = new Vector3D(0, 1, 0);
            _perspectiveCamera.FieldOfView = 45;
            _perspectiveCamera.Transform = transform3DGroup;
        }

        void Viewport3D1_Loaded(object sender, RoutedEventArgs e)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(interval);
            _timer.Tick += new EventHandler(_timer_Tick);
            _timer.IsEnabled = true;
        }

        double x = 1.0;
        double y = 1.0;
        double z = 0.1;

        double x1 = 0.005;
        double y1 = 0.005;
        double z1 = 0.005;

        double angle = .5;
        double interval = .02;

        void _timer_Tick(object sender, EventArgs e)
        {
            Transform3DGroup transform3DGroup = new Transform3DGroup();
            RotateTransform3D rotateTransform3D_1 = new RotateTransform3D();
            AxisAngleRotation3D axisAngleRotation3D_1 = new AxisAngleRotation3D(new Vector3D(x, y, z), _angle);
            rotateTransform3D_1.Rotation = axisAngleRotation3D_1;
            transform3DGroup.Children.Add(rotateTransform3D_1);
            _perspectiveCamera.Transform = transform3DGroup;
            _angle = _angle + angle;

            if (x >= 1.0) x1 = -1 * Math.Abs(x1);
            if (x <= -1.0) x1 = Math.Abs(x1);
            x += x1;

            if (y >= 1.0) y1 = -1 * Math.Abs(y1);
            if (y <= -1.0) y1 = Math.Abs(y1);
            y += y1;

            if (z >= 1.0) z1 = -1 * Math.Abs(z1);
            if (z <= -1.0) z1 = Math.Abs(z1);
            z += z1;

            if (((thread != null && !thread.IsAlive) || projection != Projection.BITMAP) && isStepNext)
            {
                isStepNext = false;
                this.InfoTextToolTip = "";
                if (projection == Projection.BITMAP)
                    this.SetImages();
                this.slidShowTimer.IsEnabled = true;
                Mouse.OverrideCursor = Cursors.None;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Random ran = new Random();
                    x = ran.NextDouble() * 2 - 1;
                    y = ran.NextDouble() * 2 - 1;
                    z = ran.NextDouble() * 2 - 1;
                    break;

                case Key.Escape:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.Shutdown();
                    else
                        this.Dispose();
                    break;


                case Key.Right:
                    this.angle = (this.angle >= 3.0 ? 3.0 : this.angle + .1);
                    InfoTextToolTip = String.Format("Winkel: {0:n1}°", this.angle);
                    break;

                case Key.Left:
                    this.angle = (this.angle <= .1 ? .1 : this.angle - .1);
                    InfoTextToolTip = String.Format("Winkel: {0:n1}°", this.angle);
                    break;

                case Key.Down:
                    this.interval = (this.interval <= 0.01 ? 0.01 : this.interval - .01);
                    _timer.Interval = TimeSpan.FromSeconds(interval);
                    InfoTextToolTip = String.Format("Interval: {0:n2}s", this.interval);
                    break;

                case Key.Up:
                    this.interval = (this.interval >= 1.0 ? 1.0 : this.interval + .01);
                    _timer.Interval = TimeSpan.FromSeconds(interval);
                    InfoTextToolTip = String.Format("Interval: {0:n2}s", this.interval);
                    break;

                case Key.Space:
                    this.StepNext();
                    break;

            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Dispose();
        }

        private void Dispose()
        {
            this.Close();
            Mouse.OverrideCursor = null;
        }

        void toolTipTimer_Tick(object sender, EventArgs e)
        {
            this.InfoTextCenter.Visibility = System.Windows.Visibility.Collapsed;
            this.InfoTextCenterBlur.Visibility = System.Windows.Visibility.Collapsed;
            this.InfoTextCenter.Text = null;
            this.InfoTextCenterBlur.Text = null;
            toolTipTimer.IsEnabled = false;
        }
    }
}
