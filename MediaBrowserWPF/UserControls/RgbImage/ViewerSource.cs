using System;
using System.Windows;
using System.Windows.Media.Imaging;
using MediaBrowser4;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.UserControls.RgbImage
{
    public static class ViewerSource
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public static int CountFrames(MediaBrowser4.Objects.MediaItem mItem)
        {
            if(!mItem.FileObject.Exists)
                return 0;

            //Create an image object from a file on disk
            System.Drawing.Image MyImage = System.Drawing.Image.FromFile(mItem.FileObject.FullName);

            //Create a new FrameDimension object from this image
            System.Drawing.Imaging.FrameDimension FrameDimensions = new System.Drawing.Imaging.FrameDimension(MyImage.FrameDimensionsList[0]);

            //Determine the number of frames in the image
            //Note that all images contain at least 1 frame, but an animated GIF
            //will contain more than 1 frame.
            int NumberOfFrames = MyImage.GetFrameCount(FrameDimensions);

            return NumberOfFrames;
        }

        public static BitmapSource GetRGB(MediaBrowser4.Objects.MediaItem mItem)
        {
            BitmapSource bitmapSource = null;
            System.Drawing.Bitmap sourceImage = null;

            if (mItem is MediaBrowser4.Objects.MediaItemBitmap)
            {
                sourceImage = GetBitmapImage(mItem);
            }

            if (sourceImage == null)
                return null;

            IntPtr hBitmap = sourceImage.GetHbitmap();
            try
            {
                bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
                sourceImage.Dispose();
            }

            return bitmapSource;
        }

        private static System.Drawing.Bitmap GetBitmapImage(MediaBrowser4.Objects.MediaItem mItem)
        {
            System.Drawing.Bitmap sourceImage = null;

            try
            {
                sourceImage = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(mItem.FileObject.FullName);

                sourceImage = MediaProcessing.RotateImage.Action90Degrees(sourceImage, (int)mItem.Orientation);

                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ROTC");
                if (layer != null)
                    sourceImage = MediaProcessing.RotateImage.RotateFromCommand(sourceImage, true, layer.Action);

                layer = mItem.FindLayer("ROT");
                if (layer != null)
                    sourceImage = MediaProcessing.RotateImage.RotateFromCommand(sourceImage, false, layer.Action);

                layer = mItem.FindLayer("CROP");
                if (layer != null)
                    sourceImage = MediaProcessing.CropImage.CropFromCommand(sourceImage, layer.Action);


                layer = mItem.FindLayer("CONT");
                if (layer != null)
                    sourceImage = MediaProcessing.BitmapFilter.ContrastFromCommand(sourceImage, layer.Action);

                layer = mItem.FindLayer("GAMM");
                if (layer != null)
                    sourceImage = MediaProcessing.BitmapFilter.GammaFromCommand(sourceImage, layer.Action);

            }
            catch (Exception ex)
            {
                Log.Exception(ex, mItem.FileObject.FullName);
            }

            return sourceImage;
        }
    }
}
