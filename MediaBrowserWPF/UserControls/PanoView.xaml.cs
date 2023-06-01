using MediaBrowser4.Objects;
using MediaBrowserWPF.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace MediaBrowserWPF.UserControls
{ /// <summary>
  /// Interaktionslogik für PanoView.xaml
  /// </summary>
    public partial class PanoView : UserControl
    {
        private MeshGeometry3D sphereMesh = null; // Tessellated sphere mesh
        private double camTheta = 180;            // Camera horizontal orientation
        private double camPhi = 90;               // Camera vertical orientation
        private double camThetaSpeed = 0;         // Camera horizontal movement speed
        private double camPhiSpeed = 0;           // Camera vertical movement speed
        private double clickX, clickY;            // Coordinates of the mouse press
        private DispatcherTimer timer;            // Timer for animating camera
        private bool isMouseDown = false;         // Is the mouse pressed

        /// <summary>
        /// Camera horizontal FOV
        /// </summary>
        public double Hfov { get { return MyCam.FieldOfView; } }

        /// <summary>
        /// Camera vertical FOV
        /// </summary>
        public double Vfov { get { return 2 * Math.Atan(ActualHeight / ActualWidth * Math.Tan(MyCam.FieldOfView * Math.PI / 180 / 2)) * 180 / Math.PI; } }

        /// <summary>
        /// Camera horizontal orientation
        /// </summary>
        public double Theta { get { return camTheta; } }

        /// <summary>
        /// Camera vertical orientation
        /// </summary>
        public double Phi { get { return camPhi; } }

        /// <summary>
        /// Constructor
        /// </summary>
        public PanoView()
        {
            InitializeComponent();
            sphereMesh = GeometryHelper.CreateSphereMesh(160, 80, 10); // Initialize mesh 

            timer = new DispatcherTimer(); // Initialize timer
            timer.Interval = TimeSpan.FromMilliseconds(25);
            timer.Tick += timer_Tick;
        }

        public void OpenMedia(MediaItem  mItem)
        {
            MyModel.Children.Clear();


            TileBrush brush = null;
            if (mItem is MediaItemVideo)
            {
                VisualBrush visualbrush = new VisualBrush();
                visualbrush.TileMode = TileMode.Tile;

                MediaElement mediaElement = new MediaElement();
                mediaElement.LoadedBehavior = MediaState.Play;
                mediaElement.MediaEnded += MediaElement_MediaEnded;
                mediaElement.Source = new Uri(mItem.FileObject.FullName);
                visualbrush.Visual = mediaElement;
                brush = visualbrush;
            }
            else
            {
                ImageBrush imageBrush = new ImageBrush();
                imageBrush.TileMode = TileMode.Tile;
                imageBrush.ImageSource = new BitmapImage(new Uri(mItem.FileObject.FullName));
                brush = imageBrush;
            }

            ModelVisual3D sphereModel = new ModelVisual3D();
            sphereModel.Content = new GeometryModel3D(sphereMesh, new DiffuseMaterial(brush));
            MyModel.Children.Add(sphereModel);

            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            ((MediaElement)sender).Position = TimeSpan.FromSeconds(0);
        }


        // Timer: animate camera
        private void timer_Tick(object sender, EventArgs e)
        {
            if (!isMouseDown) return;
            camTheta += camThetaSpeed / 50;
            camPhi += camPhiSpeed / 50;

            if (camTheta < 0) camTheta += 360;
            else if (camTheta > 360) camTheta -= 360;

            if (camPhi < 0.01) camPhi = 0.01;
            else if (camPhi > 179.99) camPhi = 179.99;

            MyCam.LookDirection = GeometryHelper.GetNormal(
                GeometryHelper.Deg2Rad(camTheta),
                GeometryHelper.Deg2Rad(camPhi));

            RaisePropertyChanged("Theta");
            RaisePropertyChanged("Phi");
        }

        // Mouse move: set camera movement speed
        private void vp_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isMouseDown) return;
            camThetaSpeed = Mouse.GetPosition(vp).X - clickX;
            camPhiSpeed = Mouse.GetPosition(vp).Y - clickY;
        }

        // Mouse down: start moving camera
        private void vp_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isMouseDown = true;
            this.Cursor = Cursors.SizeAll;
            clickX = Mouse.GetPosition(vp).X;
            clickY = Mouse.GetPosition(vp).Y;
            camThetaSpeed = camPhiSpeed = 0;
            timer.Start();
        }

        // Mouse up: stop moving camera
        private void vp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isMouseDown = false;
            this.Cursor = Cursors.Arrow;
            timer.Stop();
        }

        // Mouse wheel: zoom
        private void vp_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            MyCam.FieldOfView -= e.Delta / 100;
            if (MyCam.FieldOfView < 1) MyCam.FieldOfView = 1;
            else if (MyCam.FieldOfView > 140) MyCam.FieldOfView = 140;

            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
            e.Handled = true;
        }

        // Size changed: notify FOV change
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        // Helper function for INPC
        private void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Property changed event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
