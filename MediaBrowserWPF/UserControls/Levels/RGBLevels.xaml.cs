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
using MediaBrowserWPF.UserControls.Levels;
using System.Diagnostics;

namespace MediaBrowserWPF.UserControls.Levels
{
    /// <summary>
    /// Interaktionslogik für RGBLevels.xaml
    /// </summary>
    public partial class RGBLevels : UserControl
    {
        public class LevelsCalculatedArgs : EventArgs
        {
            public long ElapsedMilliseconds { get; set; }
        }

        public event EventHandler<RoutedEventArgs> Expanded;
        public event EventHandler<RoutedEventArgs> Collapsed;

        public HistoRemap HistoRemapRed { get; set; }
        public HistoRemap HistoRemapBlue { get; set; }
        public HistoRemap HistoRemapGreen { get; set; }

        WriteableBitmap writeableBitmap;
        byte[] originalPixels;

        public RGBLevels()
        {
            InitializeComponent();
            this.HistoRemapRed = new HistoRemap();
            this.HistoRemapBlue = this.HistoRemapRed;
            this.HistoRemapGreen = this.HistoRemapRed;
        }

        public BitmapImage BitmapSource
        {
            set
            {
                int height = 280;
                int width = (int)((double)height * ((double)value.PixelWidth) / (double)value.PixelHeight);

                Rect rect = new Rect(0, 0, width, height);

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(value, rect);
                }

                RenderTargetBitmap resizedImage = new RenderTargetBitmap(
                    width, height, 96, 96, PixelFormats.Default);

                resizedImage.Render(drawingVisual);

                writeableBitmap = new WriteableBitmap(resizedImage);
                MainImage.Source = writeableBitmap;

                originalPixels = new byte[writeableBitmap.PixelHeight * writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8];
                writeableBitmap.CopyPixels(originalPixels, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8, 0);
            }
        }

        public Uri Source
        {
            set
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = value;
                bitmapImage.DecodePixelHeight = 280;
                bitmapImage.EndInit();

                writeableBitmap = new WriteableBitmap(bitmapImage);
                MainImage.Source = writeableBitmap;

                originalPixels = new byte[writeableBitmap.PixelHeight * writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8];
                writeableBitmap.CopyPixels(originalPixels, writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8, 0);
            }
        }

        //http://www.codeproject.com/Articles/83953/Image-Magic-Image-Levels-using-Custom-Controls
        //http://www.i-programmer.info/programming/wpf-workings/527-writeablebitmap.html    
        private void SetGamma()
        {
            if (writeableBitmap == null || this.isReset)
                return;

 

            writeableBitmap.Lock();
            IntPtr buff = writeableBitmap.BackBuffer;
            int Stride = writeableBitmap.BackBufferStride;

            unsafe
            {
                byte* pbuff = (byte*)buff.ToPointer();
                for (int x = 0; x < (writeableBitmap.PixelHeight * writeableBitmap.PixelWidth); x++)
                {
                    int loc = x * 4;
                    byte tmp = pbuff[loc];
                    pbuff[loc] = pbuff[loc + 1];
                    pbuff[loc + 1] = pbuff[loc + 2];
                    pbuff[loc + 2] = pbuff[loc + 3];
                    pbuff[loc + 3] = tmp;

                    pbuff[loc] = (byte)HistoRemapBlue[originalPixels[loc]];
                    pbuff[loc + 1] = (byte)HistoRemapGreen[originalPixels[loc + 1]];
                    pbuff[loc + 2] = (byte)HistoRemapRed[originalPixels[loc + 2]];
                    pbuff[loc + 3] = (byte)originalPixels[loc + 3];
                }
            }

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();          
        }

        bool isReset;
        public void Reset()
        {
            this.isReset = true;
            this.HistogrammSliderGlobal.Reset();
            this.HistogrammSliderRed.Reset();
            this.HistogrammSliderGreen.Reset();
            this.HistogrammSliderBlue.Reset();
            this.isReset = false;

            this.SetGamma();
        }

        public void Set(HistoRemap histoRemapRed, HistoRemap histoRemapGreen, HistoRemap histoRemapBlue)
        {
            this.isReset = true;
            this.HistogrammSliderRed.HistoRemap = histoRemapRed;
            this.HistogrammSliderGreen.HistoRemap = histoRemapGreen;
            this.HistogrammSliderBlue.HistoRemap = histoRemapBlue;
            this.HistoRemapRed = this.HistogrammSliderRed.HistoRemap;
            this.HistoRemapGreen = this.HistogrammSliderGreen.HistoRemap;
            this.HistoRemapBlue = this.HistogrammSliderBlue.HistoRemap;

            if (this.HistoRemapRed == this.HistoRemapGreen && this.HistoRemapGreen == this.HistoRemapBlue)
            {
                this.HistogrammSliderGlobal.HistoRemap = histoRemapRed;
            }
            else
            {
                this.RGBExpander.IsExpanded = true;
            }

            this.isReset = false;
            this.SetGamma();
        }

        private void HistogrammSliderGlobal_LevelsValueChanged(object sender, HistogrammSlider.LevelsValueChangedArgs e)
        {
            this.isReset = true;
            this.HistogrammSliderRed.Add(e.HistoRemap);
            this.HistogrammSliderGreen.Add(e.HistoRemap);
            this.HistogrammSliderBlue.Add(e.HistoRemap);
            this.isReset = false;
            this.SetGamma();
        }

        private void HistogrammSliderRed_LevelsValueChanged(object sender, HistogrammSlider.LevelsValueChangedArgs e)
        {
            if (this.isReset) return;
            this.HistoRemapRed = e.HistoRemap;
            this.SetGamma();
        }

        private void HistogrammSliderGreen_LevelsValueChanged(object sender, HistogrammSlider.LevelsValueChangedArgs e)
        {
            if (this.isReset) return;
            this.HistoRemapGreen = e.HistoRemap;
            this.SetGamma();
        }

        private void HistogrammSliderBlue_LevelsValueChanged(object sender, HistogrammSlider.LevelsValueChangedArgs e)
        {
            if (this.isReset) return;
            this.HistoRemapBlue = e.HistoRemap;
            this.SetGamma();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            if (this.Expanded != null)
            {
                this.Expanded.Invoke(sender, e);
            }
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            if (this.Collapsed != null)
            {
                this.Collapsed.Invoke(sender, e);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.SetGamma();
        }
    }
}
