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
using _3DTools;
using MediaBrowser4.Objects;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Diagnostics;

namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für SphereViewer.xaml
    /// </summary>
    public partial class SphereViewer : Window
    {
        Trackball trackball = new Trackball();
        private List<MediaItem> mediaItemList;
        private int selectedIndex = 0;
        private Storyboard storyboardSphere;
        private DispatcherTimer pauseAnimationTimer;
        private DispatcherTimer slideShowTimer;
        private const int videoMinSeconds = 30;
        private const double fadeInSeconds = 10;
        private const double fadeOutSeconds = 3;
        private double maxOpacity = .8;

        public SphereViewer()
        {
            InitializeComponent();
        }

        public SphereViewer(List<MediaItem> mediaItems, MediaItem selectedItem)
        {
            this.mediaItemList = mediaItems.Where(x => !x.IsDeleted && x.FileObject.Exists).ToList();
            this.selectedIndex = this.mediaItemList.IndexOf(selectedItem);
            this.selectedIndex = this.selectedIndex < 0 ? 0 : this.selectedIndex;

            this.pauseAnimationTimer = new DispatcherTimer();
            this.pauseAnimationTimer.IsEnabled = true;
            this.pauseAnimationTimer.Interval = new TimeSpan(0, 0, 0, 5, 0);
            this.pauseAnimationTimer.Tick += new EventHandler(pauseAnimationTimer_Tick);

            this.slideShowTimer = new DispatcherTimer();
            this.slideShowTimer.IsEnabled = false;
            this.slideShowTimer.Interval = new TimeSpan(0, 0, 0, videoMinSeconds, 0);
            this.slideShowTimer.Tick += new EventHandler(slideShowTimer_Tick);

            InitializeComponent();

            RadialGradientBrush myBrush = new RadialGradientBrush();
            myBrush.GradientOrigin = new Point(0.75, 0.25);
            myBrush.GradientStops.Add(new GradientStop(Colors.LightSteelBlue, 0.0));
            myBrush.GradientStops.Add(new GradientStop(Colors.DarkSlateGray, 1.5));

            // this.Background = myBrush;

            //http://www.ericsink.com/wpf3d/9_Rotate_Zoom.html     
            this.camera.Transform = trackball.Transform;
            trackball.EventSource = OverlayCanvas;

            this.FrontVisualBrush.Opacity = 0;
            this.BackVisualBrush.Opacity = this.FrontVisualBrush.Opacity;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            this.BackColor.RadiusX = .5;
            this.BackColor.RadiusY = .5 * (this.RenderSize.Width / this.RenderSize.Height);

            this.SetSource();

            Mouse.OverrideCursor = Cursors.None;
            storyboardSphere = this.Resources["storyboardSphere"] as Storyboard;
            storyboardSphere.Begin();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.SystemKey == Key.F10)
            {
                this.isSlideshow = !this.isSlideshow;
                return;
            }

            int number;
            if (e.Key.ToString().StartsWith("D")
                   && Int32.TryParse(e.Key.ToString().Substring(1), out number))
            {
                if (Keyboard.Modifiers == ModifierKeys.Control || this.visibleMediaItem is MediaItemBitmap)
                {
                    this.maxOpacity = number == 0 ? 1 : (double)number / 10;
                    this.FadeIn();
                }
                else
                {
                    this.mediaElementMaterial.Position = new TimeSpan(0, 0, (int)(((double)number / 10) * this.mediaElementMaterial.NaturalDuration.TimeSpan.TotalSeconds));
                    this.mediaElementBackMaterial.Position = new TimeSpan(0, 0, (int)(((double)number / 10) * this.mediaElementMaterial.NaturalDuration.TimeSpan.TotalSeconds));
                }
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        this.StepPrevious();
                    else
                        this.StepNext();
                    break;

                case Key.Escape:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.Shutdown();
                    else
                        this.Dispose();
                    break;

                case Key.OemPlus:
                case Key.Add:
                    this.ScaleTransform.ScaleX *= 1.02;
                    this.ScaleTransform.ScaleY *= 1.02;
                    this.TranslateTransform.Y = (1 - this.ScaleTransform.ScaleY) / 2;
                    this.TranslateTransform.X = (1 - this.ScaleTransform.ScaleX) / 2;

                    this.ScaleTransform2.ScaleX *= 1.02;
                    this.ScaleTransform2.ScaleY *= 1.02;
                    this.TranslateTransform2.Y = (1 - this.ScaleTransform2.ScaleY) / 2;
                    this.TranslateTransform2.X = (1 - this.ScaleTransform2.ScaleX) / 2;
                    break;

                case Key.OemMinus:
                case Key.Subtract:
                    this.ScaleTransform.ScaleX /= 1.02;
                    this.ScaleTransform.ScaleY /= 1.02;
                    this.TranslateTransform.Y = (1 - this.ScaleTransform.ScaleY) / 2;
                    this.TranslateTransform.X = (1 - this.ScaleTransform.ScaleX) / 2;

                    this.ScaleTransform2.ScaleX /= 1.02;
                    this.ScaleTransform2.ScaleY /= 1.02;
                    this.TranslateTransform2.Y = (1 - this.ScaleTransform2.ScaleY) / 2;
                    this.TranslateTransform2.X = (1 - this.ScaleTransform2.ScaleX) / 2;
                    break;

                case Key.Left:
                    this.RotateTransform.Angle += .2;
                    this.RotateTransform2.Angle += .2;
                    break;

                case Key.Right:
                    this.RotateTransform.Angle -= .2;
                    this.RotateTransform2.Angle -= .2;

                    break;

                case Key.Down:
                    this.ScaleTransform2.ScaleY -= .02;
                    this.TranslateTransform2.Y = (1 - this.ScaleTransform2.ScaleY) / 2;
                    this.ScaleTransform.ScaleY -= .02;
                    this.TranslateTransform.Y = (1 - this.ScaleTransform.ScaleY) / 2;
                    break;

                case Key.Up:
                    this.ScaleTransform2.ScaleY += .02;
                    this.TranslateTransform2.Y = (1 - this.ScaleTransform2.ScaleY) / 2;
                    this.ScaleTransform.ScaleY += .02;
                    this.TranslateTransform.Y = (1 - this.ScaleTransform.ScaleY) / 2;
                    break;

                case Key.Back:
                    this.RotateTransform.Angle = 0;
                    this.RotateTransform2.Angle = 0;

                    this.ScaleTransform.ScaleX = 1;
                    this.ScaleTransform.ScaleY = this.ScaleTransform.ScaleX;
                    this.TranslateTransform.Y = 0;
                    this.TranslateTransform.X = this.TranslateTransform.Y;

                    this.ScaleTransform2.ScaleX = 1;
                    this.ScaleTransform2.ScaleY = this.ScaleTransform.ScaleX;
                    this.TranslateTransform2.Y = 0;
                    this.TranslateTransform2.X = this.TranslateTransform.Y;

                    this.ScaleTransformSphere.ScaleX = 1;
                    this.ScaleTransformSphere.ScaleY = 1;
                    this.ScaleTransformSphere.ScaleZ = 1;

                    camera.Position = new System.Windows.Media.Media3D.Point3D(0, 0, 10);
                    camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                    camera.LookDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, -4);

                    break;

                case Key.PageUp:
                    this.ScaleTransformSphere.ScaleX /= 1.1;
                    this.ScaleTransformSphere.ScaleY *= 1.1;
                    this.ScaleTransformSphere.ScaleZ *= 1.1;
                    break;

                case Key.PageDown:
                    this.ScaleTransformSphere.ScaleX *= 1.1;
                    this.ScaleTransformSphere.ScaleY /= 1.1;
                    this.ScaleTransformSphere.ScaleZ /= 1.1;
                    break;

                case Key.Pause:
                    if (this.pauseAnimation)
                        PauseAnimation();
                    else
                        ResumeAnimation();

                    this.pauseAnimation = !this.pauseAnimation;                    
                    break;
            }

        }

        bool isSlideshow = true;
        private void PauseAnimation()
        {
            storyboardSphere.Pause();

            //  Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
            // storyBoardPanorama.Pause();

        }

        private void ResumeAnimation()
        {
            storyboardSphere.Resume();

            // Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
            // storyBoardPanorama.Resume();
        }

        Stopwatch sp = new Stopwatch();
        void slideShowTimer_Tick(object sender, EventArgs e)
        {
            if (this.visibleMediaItem is MediaItemBitmap && isSlideshow)
            {
                this.StepNext();
            }

        }

        private void OverlayCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                camera.Position = new System.Windows.Media.Media3D.Point3D(camera.Position.X, camera.Position.Y, camera.Position.Z - .1);
            }
            else
            {
                camera.Position = new System.Windows.Media.Media3D.Point3D(camera.Position.X, camera.Position.Y, camera.Position.Z + .1);
            }
        }

        private void Dispose()
        {
            this.Close();
            Mouse.OverrideCursor = null;
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (storyboardSphere != null && !this.pauseAnimationTimer.IsEnabled)
            {
                //storyboardSphere.Pause();
                this.pauseAnimationTimer.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        void pauseAnimationTimer_Tick(object sender, EventArgs e)
        {
            if (!this.mouseDown)
            {
                //storyboardSphere.Resume();
                this.pauseAnimationTimer.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.None;
            }
        }

        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {

            if (sp.ElapsedMilliseconds / 1000 > videoMinSeconds && isSlideshow)
            {
                this.StepNext();
            }
            else
            {
                this.mediaElementMaterial.Position = TimeSpan.Zero;
                this.mediaElementMaterial.Play();

                this.mediaElementBackMaterial.Position = TimeSpan.Zero;
                this.mediaElementBackMaterial.Play();
            }
        }

        private bool mouseDown = false;
        private bool pauseAnimation = false;
        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.mouseDown = false;
        }

        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.mouseDown = true;

            if (e.ClickCount == 2)
                this.StepNext();
        }

        private void Storyboard_FadeOut_Completed(object sender, EventArgs e)
        {
            SetSource();
        }

        private void StepNext()
        {
            this.selectedIndex++;
            if (this.selectedIndex >= this.mediaItemList.Count)
                this.selectedIndex = 0;

            FadeOut();
        }

        private void StepPrevious()
        {
            this.selectedIndex--;
            if (this.selectedIndex < 0)
                this.selectedIndex = this.mediaItemList.Count - 1;

            FadeOut();
        }

        MediaItem visibleMediaItem;
        private void SetSource()
        {
            try
            {
                this.visibleMediaItem = this.mediaItemList[this.selectedIndex];
                this.mediaElementMaterial.Source = new Uri(this.mediaItemList[this.selectedIndex].FullName);
                this.mediaElementBackMaterial.Source = new Uri(this.mediaItemList[this.selectedIndex].FullName);

                sp.Restart();
                this.slideShowTimer.IsEnabled = this.visibleMediaItem is MediaItemBitmap;

                FadeIn();
            }
            catch { }
        }

        private void FadeOut()
        {
            Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];
            storyBoardPanorama.Stop();

            storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeOut"];

            DoubleAnimation db1 = storyBoardPanorama.Children[0] as DoubleAnimation;
            DoubleAnimation db2 = storyBoardPanorama.Children[1] as DoubleAnimation;

            db1.From = this.FrontVisualBrush.Opacity;
            db2.From = this.FrontVisualBrush.Opacity;

            db1.To = 0;
            db2.To = 0;

            db1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, (int)(1000 * fadeOutSeconds * this.FrontVisualBrush.Opacity)));
            db2.Duration = db1.Duration;

            storyBoardPanorama.Begin();

        }

        private void FadeIn()
        {
            Storyboard storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeOut"];
            storyBoardPanorama.Stop();


            storyBoardPanorama = (Storyboard)Resources["StoryBoardFadeIn"];

            DoubleAnimation db1 = storyBoardPanorama.Children[0] as DoubleAnimation;
            DoubleAnimation db2 = storyBoardPanorama.Children[1] as DoubleAnimation;

            db1.From = this.FrontVisualBrush.Opacity;
            db2.From = this.FrontVisualBrush.Opacity;

            db1.To = maxOpacity;
            db2.To = maxOpacity;

            db1.Duration = new Duration(new TimeSpan(0, 0, 0, 0, (int)(1000 * fadeInSeconds * (1 - this.FrontVisualBrush.Opacity))));
            db2.Duration = db1.Duration;

            storyBoardPanorama.Begin();

        }
    }
}
