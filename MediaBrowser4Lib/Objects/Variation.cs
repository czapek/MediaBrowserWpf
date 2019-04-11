using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Imaging;

namespace MediaBrowser4.Objects
{
    public class Variation
    {
        public int Id
        {
            get;
            set;
        }

        public int Position
        {
            get;
            set;
        }

        public int MediaItemId
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        private List<Layer> layers;
        public List<Layer> Layers
        {
            get
            {
                if (this.layers == null)
                    return MediaBrowserContext.GetLayers(this);
                else
                    return this.layers;
            }

            set
            {
                this.layers = value;
            }
        }

        public override string ToString()
        {
            return this.Position + ". " + this.Name + (InfoText != null ? InfoText : String.Empty);
        }

        public bool IsOutdated { get; set; }
        public string InfoText { get; set; }

        public byte[] ThumbJpegData { get; set; }

        public BitmapSource Bitmap
        {
            get
            {
                if (ThumbJpegData != null)
                {
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream(this.ThumbJpegData))
                    {
                        var decoder = BitmapDecoder.Create(ms,
                            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        return decoder.Frames[0];
                    }
                }
                else
                    return null;
            }
        }
    }
}
