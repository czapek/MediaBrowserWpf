using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace MediaBrowser4.Objects
{
    public class MediaItemBitmap : MediaItem
    {
        internal MediaItemBitmap()
            : base()
        {

        }

        internal MediaItemBitmap(System.IO.FileInfo fileObject)
            : base(fileObject)
        {

        }

        override public System.Windows.Media.Brush MediaColor
        {
            get
            {
                if (this.IsDeleted)
                {
                    return MediaBrowserContext.DeleteBrush;
                }
                else if (this.IsFileNotFound)
                {
                    return MediaBrowserContext.FileNotFoundBrush;
                }
                else if (this.IsBookmarked)
                {
                    return MediaBrowserContext.BookmarkBrush;
                }
                else if (this.IsDublicate(DublicateCriteria.CHECKSUM))
                {
                    return MediaBrowserContext.DublicateShowBrush;
                }
                else
                {
                    return MediaBrowserContext.BackGroundBrush;
                }
            }
        }

        public Bitmap GetImage()
        {
            Bitmap sourceImage = (Bitmap)Image.FromFile(this.FileObject.FullName);
            sourceImage = MediaProcessing.RotateImage.Action90Degrees(sourceImage, (int)this.Orientation);

            return sourceImage;
        }

        public override void GetThumbnail()
        {
            Dictionary<string, Dictionary<string, string>> exif =
                    MediaProcessing.ImageExif.GetAllTags(this.FileObject.FullName);

            if (exif != null)
            {
                this.MetaData = MetaDataList.GetList(exif, "MDEX");
                this.SetOrientationByNumber(MediaProcessing.ImageExif.GetExifOrientation(exif));
            }

            System.Drawing.Bitmap sourceImage = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromFile(this.FileObject.FullName);
            this.Width = sourceImage.Width;
            this.Height = sourceImage.Height;

            GetThumbnail(sourceImage);
        }

        internal override string DBType
        {
            get { return "rgb"; }
        }
    }
}
