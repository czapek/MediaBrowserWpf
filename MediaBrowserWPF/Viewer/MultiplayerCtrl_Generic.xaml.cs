using MediaBrowser4.Objects;
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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace MediaBrowserWPF.Viewer
{
    /// <summary>
    /// Interaktionslogik für MultiplayerCtrl_Generic.xaml
    /// </summary>
    public partial class MultiplayerCtrl_Generic : UserControl, IMultiplayerCtrl
    {
        new public event EventHandler<MouseButtonEventArgs> MouseDown;
        new public event EventHandler<MouseButtonEventArgs> MouseDoubleClick;
        public event EventHandler MediaLoaded;
        private List<ViewerBaseControl> viewerBaseControlList = new List<ViewerBaseControl>();
        private bool transform;
        private int colCount, rowCount, itemCount;
        System.Windows.Forms.Screen Screen;

        public MultiplayerCtrl_Generic()
        {
            InitializeComponent();
        }

        public MultiplayerCtrl_Generic(int itemCount, int colCount, int rowCount, bool transform)
        {
            this.transform = transform;
            this.colCount = colCount;
            this.rowCount = rowCount;
            this.itemCount = itemCount;
            this.Screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(MainWindow.MainWindowStatic).Handle);

            InitializeComponent();

            for (int i = 0; i < colCount; i++)
            {
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int i = 0; i < rowCount; i++)
            {
                MainGrid.RowDefinitions.Add(new RowDefinition());
            }

            for (int x = 0; x < colCount; x++)
            {
                for (int y = 0; y < rowCount; y++)
                {
                    if (itemCount > 0)
                    {
                        AddViewerControl(x, y);
                        itemCount--;
                    }
                }
            }

            if (transform)
            {
                var rnd = new Random();
                viewerBaseControlList = viewerBaseControlList.OrderBy(item => rnd.Next()).ToList();

                MainGrid.Margin = new Thickness(Math.Sqrt(Screen.Bounds.Width * Screen.Bounds.Height) * .02);
            }
        }

        private ViewerBaseControl AddViewerControl(int col, int row)
        {
            ViewerBaseControl viewer = new ViewerBaseControl();
            viewer.IsSliderBottom = false;
            viewer.MouseDoubleClick += Viewer1_MouseDoubleClick;
            viewer.MouseDown += Viewer1_MouseDown;
            viewer.MediaLoaded += Viewer1_MediaLoaded;
            viewer.Activated += Viewer_Activated;
            viewer.Margin = new Thickness(1.5);
            viewer.RenderTransformOrigin = new Point(0.5, 0.5);

            Grid.SetRow(viewer, row);
            Grid.SetColumn(viewer, col);
            Grid.SetZIndex(viewer, 0);

            Grid.SetColumnSpan(viewer, 1);

            if (this.transform)
            {
                TransformGroup transformGroup = new TransformGroup();
                transformGroup.Children.Add(new TranslateTransform());
                transformGroup.Children.Add(new RotateTransform(0, 0, 0));
                transformGroup.Children.Add(new ScaleTransform());
                transformGroup.Children.Add(new SkewTransform());
                viewer.RenderTransform = transformGroup;
            }

            DropShadowEffect dropShadowEffect = new DropShadowEffect();
            dropShadowEffect.Color = Colors.Black;
            dropShadowEffect.Direction = -45;
            dropShadowEffect.ShadowDepth = 4;
            dropShadowEffect.Opacity = 0.5;
            dropShadowEffect.RenderingBias = RenderingBias.Performance;
            dropShadowEffect.BlurRadius = 15;
            viewer.Effect = dropShadowEffect;

            viewerBaseControlList.Add(viewer);
            MainGrid.Children.Add(viewer);

            return viewer;
        }

        private void Viewer_Activated(object sender, EventArgs e)
        {
            foreach (ViewerBaseControl viewer in viewerBaseControlList)
            {
                DropShadowEffect dropShadowEffect = viewer.Effect as DropShadowEffect;

                if (dropShadowEffect != null)
                {
                    dropShadowEffect.Color = Colors.Black;
                    dropShadowEffect.BlurRadius = 15;
                }
            }

            ViewerBaseControl viewerActive = sender as ViewerBaseControl;
            DropShadowEffect dropShadowEffectActive = viewerActive.Effect as DropShadowEffect;

            if (dropShadowEffectActive != null)
            {
                dropShadowEffectActive.Color = Colors.Blue;
                dropShadowEffectActive.BlurRadius = 40;
            }

            BringToFront(viewerActive);
        }

        public List<ViewerBaseControl> ViewerBaseControlList
        {
            get
            {
                return this.viewerBaseControlList;
            }
        }

        private void Viewer1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.MouseDoubleClick != null)
                this.MouseDoubleClick.Invoke(sender, e);
        }

        private void Viewer1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.MouseDown != null)
                this.MouseDown.Invoke(sender, e);
        }

        Random rand = new Random();
        int zIndex = 10;
        private void Viewer1_MediaLoaded(object sender, EventArgs e)
        {
            if (this.MediaLoaded != null)
                this.MediaLoaded.Invoke(sender, EventArgs.Empty);

            ViewerBaseControl viewer = sender as ViewerBaseControl;

            TransformGroup tg = viewer.RenderTransform as TransformGroup;

            if (tg != null)
            {
                double relViewer = viewer.ActualWidth / viewer.ActualHeight;
                double heightImage = Math.Sqrt(viewer.ActualWidth * viewer.ActualHeight / viewer.Source.Relation);
                double widthImage = heightImage * viewer.Source.Relation;

                double scale = viewer.Source.Relation >= relViewer ? widthImage / viewer.ActualWidth : heightImage / viewer.ActualHeight;
               
                TranslateTransform translateTransform = tg.Children[0] as TranslateTransform;
                RotateTransform rotateTransform = tg.Children[1] as RotateTransform;
                ScaleTransform scaleTransform = tg.Children[2] as ScaleTransform;
                SkewTransform skewTransform = tg.Children[3] as SkewTransform;

                translateTransform.X = 0;
                translateTransform.Y = 0;

                int moveWidth = (int)((double)this.Screen.Bounds.Width * .05);
                int moveHeight = (int)((double)this.Screen.Bounds.Height * .05);

                rotateTransform.Angle = rand.Next(20) - 10;
                translateTransform.X = rand.Next(moveWidth) - moveWidth / 2;
                translateTransform.Y = rand.Next(moveHeight) - moveHeight / 2;  

                scaleTransform.ScaleY = scale;
                scaleTransform.ScaleX = scale;   
            }

            BringToFront(viewer);
        }

        private void BringToFront(ViewerBaseControl viewer)
        {
            zIndex++;
            viewer.SetValue(Grid.ZIndexProperty, zIndex);

            if (zIndex == Int32.MaxValue)
            {
                zIndex = 0;

                foreach (ViewerBaseControl vc in viewerBaseControlList)
                {
                    vc.SetValue(Grid.ZIndexProperty, 0);
                }
            }
        }      
    }
}
