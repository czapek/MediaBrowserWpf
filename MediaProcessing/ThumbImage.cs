using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing;

namespace MediaProcessing
{
    public static class ThumbImage
    {
        public static Bitmap GetFitImage(byte[] thumb, int thumbnailSize, int borderWidth)
        {
            return GetFitImage(thumb, Color.Black, null, null, null, null, thumbnailSize, borderWidth);
        }

        public static Bitmap GetFitImage(byte[] thumb, Color color, Bitmap nw, Bitmap ne, Bitmap se, Bitmap sw, int thumbnailSize, int borderWidth)
        {
            return GetFitImage(thumb, color, nw, ne, se, sw, false, thumbnailSize, borderWidth);
        }

        public static Bitmap GetFitImage(byte[] thumb, Color color, Bitmap nw, Bitmap ne, Bitmap se, Bitmap sw, bool isDeleted, int thumbnailSize, int borderWidth)
        {
            Bitmap bmp = null;
            if (thumb != null && thumb.Length > 0)
            {
                MemoryStream stm = new MemoryStream(thumb);
                bmp = (new Bitmap(stm));
                stm.Close();
            }
            return GetFitImage(bmp, color, nw, ne, se, sw, false, thumbnailSize, borderWidth);
        }

        public static Bitmap GetFitImage(Bitmap bmp, Color color, Bitmap nw, Bitmap ne, Bitmap se, Bitmap sw, int thumbnailSize, int borderWidth)
        {
            return GetFitImage(bmp, color, nw, ne, se, sw, false, thumbnailSize, borderWidth);
        }

        public static Bitmap GetFitImage(Bitmap bmp, Color color, Bitmap nw, Bitmap ne, Bitmap se, Bitmap sw, bool isDeleted, int thumbnailSize, int borderWidth)
        {
            Bitmap newBmp = new Bitmap(thumbnailSize + borderWidth,
                                       thumbnailSize + borderWidth,
                                       PixelFormat.Format24bppRgb);


            Graphics newBmpGraphics = Graphics.FromImage(newBmp);
            newBmpGraphics.Clear(color);

            newBmpGraphics.DrawImage(bmp, new Point(
                                                    ((thumbnailSize + borderWidth) - bmp.Width) / 2,
                                                    ((thumbnailSize + borderWidth) - bmp.Height) / 2));

            if (nw != null)
            {
                newBmpGraphics.DrawImage(nw, 10, 10);
            }

            if (ne != null)
            {
                newBmpGraphics.DrawImage(ne, (thumbnailSize - 20), 10);
            }

            if (se != null)
            {
                newBmpGraphics.DrawImage(se, (thumbnailSize - 20), (thumbnailSize - 20));
            }

            if (sw != null)
            {
                newBmpGraphics.DrawImage(sw, 10, (thumbnailSize - 20));
            }

            if (isDeleted)
            {
                Pen pen = new Pen(Color.Red);
                newBmpGraphics.DrawLine(pen, new Point(0, 0), new Point((thumbnailSize + borderWidth), (thumbnailSize + borderWidth)));
            }

            newBmpGraphics.Dispose();

            bmp.Dispose();

            GC.Collect();

            return newBmp;
        }
    }
}
