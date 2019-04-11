using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace MediaBrowserWPF.UserControls.RgbImage
{
    public class CacheBitmapImage
    {
        public DateTime LastUsed = DateTime.Now;
        public BitmapImage BitmapImage;
    }
}
