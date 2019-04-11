using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaProcessing.FaceDetection
{
    [Serializable]
    public struct Faces
    {
        public int Height, Width;
        public List<System.Drawing.Rectangle> Facelist;
    }
}
