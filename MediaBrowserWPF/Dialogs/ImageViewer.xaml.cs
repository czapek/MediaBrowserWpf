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

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für ImageViewer.xaml
    /// </summary>
    public partial class ImageViewer : Window
    {
        public ImageViewer()
        {
            InitializeComponent();
            this.Closed += ImageViewer_Closed;
            this.IsClosed = false;
        }

        public ImageViewer(BitmapImage imageSource)
        {
            InitializeComponent();

            this.ImageSource = imageSource;
            this.Closed += ImageViewer_Closed;
            this.IsClosed = false;
        }

        void ImageViewer_Closed(object sender, EventArgs e)
        {
            IsClosed = true;
        }

        public bool IsClosed { get; private set; }
        public bool ShowFaces { get; set; }

        public BitmapImage ImageSource
        {
            set
            {
                this.MainImage.Source = value;
            }
        }
    }
}
