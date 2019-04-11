using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

using PaintDotNet;
using PaintDotNet.Effects;
using System.Drawing.Imaging;

namespace MediaProcessing
{
    public class SharpenImage
    {
        public enum Quality { NONE, SOFT, MEDIUM, STRONG, FORCED, EXTREME };

        public static Bitmap Work(Bitmap bmp, Quality value)
        {
            switch (value)
            {
                case Quality.EXTREME:
                    return SharpenBase(bmp, Quality.EXTREME);

                case Quality.FORCED:
                    return SharpenBase(bmp, Quality.FORCED);

                case Quality.SOFT:
                    return Action(bmp, 1, 1);

                case Quality.MEDIUM:
                    return Action(bmp, 1, 2);

                case Quality.STRONG:
                    return Action(bmp, 2, 1);

                default:
                    return bmp;
            }
        }

        private static Bitmap Action(Bitmap bmp, int amount, int cycles)
        {
            for (int i = 0; i < cycles; i++)
            {
                bmp = SharpenCycle(bmp, amount);
            }

            return bmp;
        }

        private static Bitmap SharpenCycle(Bitmap bmp, int amount)
        {
            Surface surface = Surface.CopyFromBitmap(bmp);
            RenderArgs ra = new RenderArgs(surface);

            Surface surface2 = Surface.CopyFromBitmap((Bitmap)bmp.Clone());
            RenderArgs sa = new RenderArgs(surface2);

            IConfigurableEffect ice = new SharpenEffect();
            AmountEffectConfigToken am = new AmountEffectConfigToken(amount);
            PdnRegion pdn = new PdnRegion(new Rectangle(0, 0, bmp.Width, bmp.Height));
            ice.Render(am, sa, ra, pdn);

            bmp.Dispose();
            GC.Collect();

            return sa.Bitmap;
        }

        private static double[,] Filter1
        {
            get
            {
                int filterWidth = 3;
                int filterHeight = 3;
                double[,] filter = new double[filterWidth, filterHeight];
                filter[0, 0] = filter[0, 1] = filter[0, 2] = filter[1, 0] = filter[1, 2] = filter[2, 0] = filter[2, 1] = filter[2, 2] = -1;
                filter[1, 1] = 9;

                return filter;
            }
        }

        private static double[,] Filter2
        {
            get
            {
                //This will create a softer sharpening effect. You can expand the filter array if you need to, or change the 16 to something larger, but I found this isn't as harsh as the one you have.
                const int filterWidth = 5;
                const int filterHeight = 5;

                double[,] filter = new double[filterWidth, filterHeight] {
                    { -1, -1, -1, -1, -1 },
                    { -1,  2,  2,  2, -1 },
                    { -1,  2,  16,  2, -1 },
                    { -1,  2,  2,  2, -1 },
                    { -1, -1, -1, -1, -1 }
                };

                return filter;
            }
        }

        //http://stackoverflow.com/questions/903632/sharpen-on-a-bitmap-using-c-sharp
        public static Bitmap SharpenBase(Bitmap image, Quality value)
        {
            Bitmap sharpenImage = (Bitmap)image.Clone();

            int width = image.Width;
            int height = image.Height;

            double[,] filter = value == Quality.FORCED ? Filter2 : Filter1;
            double factor = value == Quality.FORCED ? (1.0 / 16.0) : 1.0;

            // Create sharpening filter.
            int filterWidth = filter.GetLength(0);
            int filterHeight = filter.GetLength(1);            

            double bias = 0.0;

            Color[,] result = new Color[image.Width, image.Height];

            // Lock image bits for read/write.
            BitmapData pbits = sharpenImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            // Declare an array to hold the bytes of the bitmap.
            int bytes = pbits.Stride * height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(pbits.Scan0, rgbValues, 0, bytes);

            int rgb;
            // Fill the color array with the new sharpened color values.
            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    double red = 0.0, green = 0.0, blue = 0.0;

                    for (int filterX = 0; filterX < filterWidth; filterX++)
                    {
                        for (int filterY = 0; filterY < filterHeight; filterY++)
                        {
                            int imageX = (x - filterWidth / 2 + filterX + width) % width;
                            int imageY = (y - filterHeight / 2 + filterY + height) % height;

                            rgb = imageY * pbits.Stride + 3 * imageX;

                            red += rgbValues[rgb + 2] * filter[filterX, filterY];
                            green += rgbValues[rgb + 1] * filter[filterX, filterY];
                            blue += rgbValues[rgb + 0] * filter[filterX, filterY];
                        }
                        int r = Math.Min(Math.Max((int)(factor * red + bias), 0), 255);
                        int g = Math.Min(Math.Max((int)(factor * green + bias), 0), 255);
                        int b = Math.Min(Math.Max((int)(factor * blue + bias), 0), 255);

                        result[x, y] = Color.FromArgb(r, g, b);
                    }
                }
            }

            // Update the image with the sharpened pixels.
            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    rgb = y * pbits.Stride + 3 * x;

                    rgbValues[rgb + 2] = result[x, y].R;
                    rgbValues[rgb + 1] = result[x, y].G;
                    rgbValues[rgb + 0] = result[x, y].B;
                }
            }

            // Copy the RGB values back to the bitmap.
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, pbits.Scan0, bytes);
            // Release image bits.
            sharpenImage.UnlockBits(pbits);

            return sharpenImage;
        }
    }
}
