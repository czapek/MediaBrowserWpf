using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Globalization;

namespace MediaProcessing
{
    public class CropImage
    {
        public static Bitmap CropRelativ(Bitmap bmp, double left, double top, double right, double bottom)
        {
            return CropFromBorder(bmp, (int)((double)bmp.Width * left / 100)
                , (int)((double)bmp.Height * top / 100)
                , (int)((double)bmp.Width * right / 100)
                , (int)((double)bmp.Height * bottom / 100));
        }

        public static Bitmap CropFromCommand(Bitmap bmp, string command)
        {
            int left = (int)((double)bmp.Width * Convert.ToDouble(command.Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat) / 100);
            int top = (int)((double)bmp.Height * Convert.ToDouble(command.Split(' ')[1], CultureInfo.InvariantCulture.NumberFormat) / 100);
            int right = (int)((double)bmp.Width * Convert.ToDouble(command.Split(' ')[2], CultureInfo.InvariantCulture.NumberFormat) / 100);
            int bottom = (int)((double)bmp.Height * Convert.ToDouble(command.Split(' ')[3], CultureInfo.InvariantCulture.NumberFormat) / 100);

            return CropFromBorder(bmp, left, top, right, bottom);
        }

        public static Bitmap CropFromBorder(Bitmap bmp, int left, int top, int right, int bottom)
        {
            return Crop(bmp, left, top, bmp.Width - left - right, bmp.Height - top - bottom);
        }

        public static Bitmap Crop(Bitmap bmp, int x, int y, int width, int height)
        {

            if ((x + width) > bmp.Width
               || (y + height) > bmp.Height
               || y < 0
               || x < 0
               || width <= 0
               || height <= 0)
                return null;

            Rectangle cropRectangle = new Rectangle(x, y, width, height);

            Bitmap newBmp = new Bitmap(cropRectangle.Width,
                                       cropRectangle.Height,
                                       System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            Graphics newBmpGraphics = Graphics.FromImage(newBmp);

            newBmpGraphics.DrawImage(bmp,
                                     new Rectangle(0, 0, cropRectangle.Width, cropRectangle.Height),
                                     cropRectangle,
                                     GraphicsUnit.Pixel);
            newBmpGraphics.Dispose();

            bmp.Dispose();
            return newBmp;
        }
    }
}
