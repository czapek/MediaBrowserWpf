using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace MediaBrowserWPF.Viewer
{
    public static class MediaControlHelper
    {
        public static void ForceRelation(double relation, IMediaControl mediaControl)
        {
            mediaControl.CropRightRel = mediaControl.CropRightRel;
            mediaControl.CropLeftRel = mediaControl.CropLeftRel;
            mediaControl.CropBottomRel = mediaControl.CropBottomRel;
            mediaControl.CropTopRel = mediaControl.CropTopRel;

            Rect size = mediaControl.MediaRenderSize;
            double sizeRel = Math.Max(size.Width, size.Height) / Math.Min(size.Width, size.Height);
            double realRelation = size.Width / size.Height;

            if ((sizeRel > relation && realRelation > 1) || (sizeRel < relation && realRelation < 1))
            {
                double fullWidth = size.Width / ((100 - mediaControl.CropLeftRel - mediaControl.CropRightRel) / 100);
                double cropInner = realRelation > 1.0 ? (size.Width - size.Height * relation) / 2 : (size.Width - size.Height / relation) / 2;
                double cropLeft = ((mediaControl.CropLeft + cropInner) / fullWidth) * 100;
                double cropRight = ((mediaControl.CropRight + cropInner) / fullWidth) * 100;

                mediaControl.CropLeftRel = cropLeft;
                mediaControl.CropRightRel = cropRight;
            }
            else
            {
                double fullHeight = size.Height / ((100 - mediaControl.CropTopRel - mediaControl.CropBottomRel) / 100);
                double cropInner = realRelation > 1.0 ? (size.Height - size.Width / relation) / 2 : (size.Height - size.Width * relation) / 2;
                double cropTop = ((mediaControl.CropTop + cropInner) / fullHeight) * 100;
                double cropBottom = ((mediaControl.CropBottom + cropInner) / fullHeight) * 100;

                mediaControl.CropTopRel = cropTop;
                mediaControl.CropBottomRel = cropBottom;
            }
        }
    }
}
