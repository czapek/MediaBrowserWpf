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

namespace MediaBrowserWPF.UserControls.Levels
{
    /// <summary>
    /// Interaktionslogik für LevelsToolbox.xaml
    /// </summary>
    public partial class LevelsToolbox : Window
    {
        public event EventHandler PreviewClicked;
        public LevelsToolbox()
        {
            InitializeComponent();
        }

        public LevelsToolbox(HistoRemap histoRemapRed, HistoRemap histoRemapGreen, HistoRemap histoRemapBlue)
        {
            InitializeComponent();
            this.RGBLevels.Set(histoRemapRed, histoRemapGreen, histoRemapBlue);
        }

        public HistoRemap HistoRemapRed
        {
            get
            {
                return this.RGBLevels.HistoRemapRed;
            }
        }

        public HistoRemap HistoRemapGreen
        {
            get
            {
                return this.RGBLevels.HistoRemapGreen;
            }
        }

        public HistoRemap HistoRemapBlue
        {
            get
            {
                return this.RGBLevels.HistoRemapBlue;
            }
        }

        public BitmapImage BitmapSource
        {
            set
            {
                this.RGBLevels.BitmapSource = value;
            }
        }

        public MediaItem MediaitemSource
        {
            set
            {
                Uri uri = new Uri(value.FileObject.FullName);
                this.RGBLevels.Source = uri;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            this.RGBLevels.Reset();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back)
            {
                this.RGBLevels.Reset();
                this.DialogResult = true;
                this.Close();
            }

            if (e.Key == Key.Escape || e.Key == Key.K)
            {
                this.Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void RGBLevels_Expanded(object sender, RoutedEventArgs e)
        {
            this.Height = 590;
        }

        private void RGBLevels_Collapsed(object sender, RoutedEventArgs e)
        {
            this.Height = 440;
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (this.PreviewClicked != null)
            {
                this.PreviewClicked.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
