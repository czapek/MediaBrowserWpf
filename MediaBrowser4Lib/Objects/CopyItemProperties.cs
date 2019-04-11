using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public class CopyItemProperties
    {
        public List<Category> Categories;
        public MediaItem.MediaOrientation? Orientation;
        public String CropLayer, ClipLayer, RotateLayer, LevelsLayer, ZoomLayer, FlipLayer, TrimLayer;

        public bool IsEmpty
        {
            get
            {
                return this.Categories == null
                    && TrimLayer == null
                    && Orientation == null
                    && CropLayer == null
                    && ClipLayer == null
                    && RotateLayer == null
                    && LevelsLayer == null
                    && FlipLayer == null
                    && ZoomLayer == null;
            }
        }
    }
}
