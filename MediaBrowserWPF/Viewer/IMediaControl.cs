using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Imaging;


namespace MediaBrowserWPF.Viewer
{
    public interface IMediaControl : IDisposable, IInputElement
    {
        Size RenderSize { get; set; }
        bool UsePreviewDb { get; set; }
        ViewerState ViewerState { get; set; }
        System.Windows.Rect MediaRenderSize { get; }
        MediaItem MediaItemSource { get; set; }
        MediaItem.MediaOrientation Orientation { get; set; }
        double RotateAngle { get; set; }
        MediaItem.MediaOrientation Rotate90(int rotateCounter);
        double ScaleXDistortFactor { get; set; }
        double ScaleFactor { get; set; }
        double ScaleOriginalFactor { get; set; }
        double TranslateX { get; set; }
        double TranslateY { get; set; }
        bool FlipHorizontal { get; set; }
        bool FlipVertical { get; set; }
        
        System.Drawing.Bitmap TakeSnapshot();
        BitmapSource TakeRenderTargetBitmap();
        
        Brush Background { get; set; }        

        double CropLeftRel { get; set; }
        double CropTopRel { get; set; }
        double CropRightRel { get; set; }
        double CropBottomRel { get; set; }
        double CropLeft { get; set; }
        double CropTop { get; set; }
        double CropRight { get; set; }
        double CropBottom { get; set; }
        void ResetCrop();

        RectangleGeometry ClipRectangle { get; set; }
        double ClipLeftRel { get; set; }
        double ClipTopRel { get; set; }
        double ClipRightRel { get; set; }
        double ClipBottomRel { get; set; }
        double ClipLeft { get; set; }
        double ClipTop { get; set; }
        double ClipRight { get; set; }
        double ClipBottom { get; set; }

        void ForceRelation(double relation);
        void ResetClip();
        void ResetDefaults();
    }
}
