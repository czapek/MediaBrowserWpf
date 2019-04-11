using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MediaProcessing
{
    public class ResizeImage
    {
        public static Bitmap ActionCropIn(Bitmap sourceImage, Size cropIn, Size borderSize, Color borderColor, int relativeCropIn)
        {
            Bitmap resultImage = new Bitmap(cropIn.Width, cropIn.Height);
            cropIn.Width -= 2 * borderSize.Width;
            cropIn.Height -= 2 * borderSize.Height;
            Rectangle rec = CropIn(sourceImage.Size, cropIn, relativeCropIn);
            Graphics g = Graphics.FromImage((Image)resultImage);
            g.Clear(borderColor);
            g.Clip = new Region(new Rectangle(borderSize.Width, borderSize.Height, cropIn.Width, cropIn.Height));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawImage(sourceImage, rec.X + borderSize.Width, rec.Y + borderSize.Height, rec.Width, rec.Height);
            g.Dispose();

            sourceImage = null;
            GC.Collect();

            return resultImage;
        }

        public static byte[] GetThumbnail(System.Drawing.Bitmap bmp, int size, int jpegQuality)
        {
            if (bmp != null)
            {
                bmp = ActionFitIn(bmp, size);
                bmp = MediaProcessing.SharpenImage.Work(bmp, MediaProcessing.SharpenImage.Quality.SOFT);

                System.IO.MemoryStream imageStream = new System.IO.MemoryStream();
                MediaProcessing.EncodeImage.SaveJPGStream(bmp, imageStream, jpegQuality);

                byte[] newThumb = new Byte[imageStream.Length];
                imageStream.Position = 0;
                imageStream.Read(newThumb, 0, (int)imageStream.Length);
                imageStream.Close();

                return newThumb;
            }
            else
                return null;
        }

        public static Bitmap ActionFitIn(Bitmap sourceImage, Size fitIn)
        {
            return ActionFitIn(sourceImage, fitIn, new Size(0, 0), Color.Black);
        }

        public static Bitmap ActionFitIn(Bitmap sourceImage, Size fitIn, Size borderSize, Color borderColor)
        {
            fitIn.Width -= 2 * borderSize.Width;
            fitIn.Height -= 2 * borderSize.Height;

            Size sz = FitIn(sourceImage.Size, fitIn);

            Bitmap resultImage = new Bitmap(sz.Width + (2 * borderSize.Width), sz.Height + (2 * borderSize.Height));
            Graphics g = Graphics.FromImage((Image)resultImage);
            g.Clear(borderColor);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawImage(sourceImage, borderSize.Width, borderSize.Height, sz.Width, sz.Height);
            g.Dispose();
            sourceImage = null;
            GC.Collect();

            return resultImage;
        }

        public static Bitmap ActionFitInFull(Bitmap sourceImage, Size fitIn, Size borderSize, Color borderColor)
        {
            if ((sourceImage.Width > sourceImage.Height && fitIn.Width < fitIn.Height)
                ||
               (sourceImage.Width < sourceImage.Height && fitIn.Width > fitIn.Height))
            {
                int a = fitIn.Width;
                fitIn.Width = fitIn.Height;
                fitIn.Height = a;
            }

            if (sourceImage.Height < fitIn.Height - (2 * borderSize.Height))
            {
                double rel = (double)fitIn.Height / (double)fitIn.Width;
                fitIn.Height = sourceImage.Height + (2 * borderSize.Height);
                fitIn.Width = (int)((double)fitIn.Height / rel);
            }

            if (sourceImage.Width < fitIn.Width - (2 * borderSize.Width))
            {
                double rel = (double)fitIn.Width / (double)fitIn.Height;
                fitIn.Width = sourceImage.Width + (2 * borderSize.Width);
                fitIn.Height = (int)((double)fitIn.Width / rel);
            }

            Bitmap resultImage = new Bitmap(fitIn.Width, fitIn.Height);

            fitIn.Width -= 2 * borderSize.Width;
            fitIn.Height -= 2 * borderSize.Height;

            Size sz = FitIn(sourceImage.Size, fitIn);
            
            Graphics g = Graphics.FromImage((Image)resultImage);
            g.Clear(borderColor);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawImage(sourceImage, borderSize.Width + ((fitIn.Width-sz.Width) / 2), borderSize.Height +  ((fitIn.Height-sz.Height) / 2), sz.Width, sz.Height);
            g.Dispose();
            sourceImage = null;
            GC.Collect();

            return resultImage;
        }

        public static Bitmap ActionFitIn(Bitmap sourceImage, int fitIn)
        {
            return ActionFitIn(sourceImage, new Size(fitIn, fitIn));
        }

        private static Rectangle CropIn(Size imgSize, Size cropInSize, int relativeCropIn)
        {
            Rectangle rec = CropIn(imgSize, cropInSize);
            double relCrop = (double)relativeCropIn / 100.0;

            if (relativeCropIn >= 100 || relativeCropIn <= 0)
                return rec;

            if (rec.Width == cropInSize.Width)
            {
                double rel = 1.0 - ((double)cropInSize.Height / (double)rec.Height);

                if (relCrop < rel)
                {
                    rec.Height = -(int)(((double)cropInSize.Height) / (relCrop - 1.0));
                    rec.Width = (int)Math.Round(((double)imgSize.Width / (double)imgSize.Height) * rec.Height);
                    rec.Y = -(rec.Height - cropInSize.Height) / 2;
                    rec.X = -(rec.Width - cropInSize.Width) / 2;
                }
            }
            else
            {
                double rel = 1.0 - ((double)cropInSize.Width / (double)rec.Width);

                if (relCrop < rel)
                {
                    rec.Width = -(int)(((double)cropInSize.Width) / (relCrop - 1.0));
                    rec.Height = (int)Math.Round(((double)imgSize.Height / (double)imgSize.Width) * rec.Width);
                    rec.Y = -(rec.Height - cropInSize.Height) / 2;
                    rec.X = -(rec.Width - cropInSize.Width) / 2;
                }
            }


            return rec;
        }

        private static Rectangle CropIn(Size imgSize, Size cropInSize)
        {
            Rectangle rec = new Rectangle();

            if (imgSize.Width > cropInSize.Width && imgSize.Height > cropInSize.Height)
            {
                if (((double)imgSize.Height / (double)imgSize.Width)
                    > ((double)cropInSize.Height / (double)cropInSize.Width))
                {
                    rec.Width = cropInSize.Width;
                    rec.Height = (int)Math.Round(((double)imgSize.Height / (double)imgSize.Width) * cropInSize.Width);
                    rec.Y = -(rec.Height - cropInSize.Height) / 2;
                    rec.X = 0;
                }
                else
                {
                    rec.Height = cropInSize.Height;
                    rec.Width = (int)Math.Round(((double)imgSize.Width / (double)imgSize.Height) * cropInSize.Height);
                    rec.Y = 0;
                    rec.X = -(rec.Width - cropInSize.Width) / 2;
                }
            }
            else if (imgSize.Width > cropInSize.Width && imgSize.Height <= cropInSize.Height)
            {
                rec.Height = imgSize.Height;
                rec.Width = imgSize.Width;
                rec.Y = (cropInSize.Height - rec.Height) / 2;
                rec.X = -(rec.Width - cropInSize.Width) / 2;
            }
            else if (imgSize.Width <= cropInSize.Width && imgSize.Height > cropInSize.Height)
            {
                rec.Height = imgSize.Height;
                rec.Width = imgSize.Width;
                rec.Y = -(rec.Height - cropInSize.Height) / 2;
                rec.X = (cropInSize.Width - rec.Width) / 2;
            }
            else
            {
                rec = new Rectangle((cropInSize.Width - imgSize.Width) / 2, (cropInSize.Height - imgSize.Height) / 2, imgSize.Width, imgSize.Height);
            }


            return rec;
        }

        private static Size FitIn(Size imgSize, Size fitInSize)
        {
            Size sz = imgSize;

            if (imgSize.Width > fitInSize.Width && imgSize.Height > fitInSize.Height)
            {
                if (((double)imgSize.Height / (double)imgSize.Width)
                   < ((double)fitInSize.Height / (double)fitInSize.Width))
                {
                    sz.Width = fitInSize.Width;
                    sz.Height = (int)Math.Round(((double)imgSize.Height / (double)imgSize.Width) * fitInSize.Width);
                }
                else
                {
                    sz.Height = fitInSize.Height;
                    sz.Width = (int)Math.Round(((double)imgSize.Width / (double)imgSize.Height) * fitInSize.Height);
                }
            }
            else if (imgSize.Width > fitInSize.Width && imgSize.Height <= fitInSize.Height)
            {
                sz.Width = fitInSize.Width;
                sz.Height = (int)Math.Round(((double)imgSize.Height / (double)imgSize.Width) * fitInSize.Width);
            }
            else if (imgSize.Width <= fitInSize.Width && imgSize.Height > fitInSize.Height)
            {
                sz.Height = fitInSize.Height;
                sz.Width = (int)Math.Round(((double)imgSize.Width / (double)imgSize.Height) * fitInSize.Height);
            }

            return sz;
        }
    }
}
