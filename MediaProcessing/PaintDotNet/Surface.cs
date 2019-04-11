using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PaintDotNet
{
    /// <summary>
    /// This is our own Surface type. We allocate our own blocks of memory for this,
    /// and provide ways to create a GDI+ Bitmap object that aliases our surface.
    /// That way we can do everything fast and in memory and have complete control,
    /// while still being able to use GDI+ for drawing and rendering.
    /// </summary>
    [Serializable]
    public class Surface
        : IDisposable
    {
        private MemoryBlock scan0;
        private int width;
        private int height;
        private int stride;

        public MemoryBlock Scan0
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("Surface");
                }

                return scan0;
            }
        }

        public int Width
        {
            get
            {
                return width;
            }
        }

        public int Height
        {
            get
            {
                return height;
            }
        }

        public int Stride
        {
            get
            {
                return stride;
            }
        }

        public Size Size
        {
            get
            {
                return new Size(width, height);
            }
        }

        public PixelFormat PixelFormat
        {
            get
            {
                return PixelFormat.Format32bppArgb;
            }
        }

        public Rectangle Bounds
        {
            get
            {
                return new Rectangle(0, 0, width, height);
            }
        }

        public Surface(Size size)
            : this(size.Width, size.Height)
        {

        }

        public Surface(int width, int height)
        {
            int stride;
            int bytes;

            try
            {
                stride = checked(width * ColorBgra.SizeOf);
                bytes = checked((height + 1) * stride);
            }

            catch (OverflowException ex)
            {
                throw new OutOfMemoryException("Not enough memory, width=" + width.ToString() + ", height=" + height.ToString(), ex);
            }

            MemoryBlock scan0 = new MemoryBlock(bytes);
            Create(width, height, stride, scan0);
        }

        public Surface(int width, int height, int stride)
        {
            int bytes;

            try
            {
                bytes = height * stride;
            }

            catch (OverflowException ex)
            {
                throw new OutOfMemoryException("Not enough memory", ex);
            }

            MemoryBlock scan0 = new MemoryBlock(bytes);
            Create(width, height, stride, scan0);
        }

        private Surface(int width, int height, int stride, MemoryBlock scan0)
        {
            Create(width, height, stride, scan0);
        }

        private void Create(int width, int height, int stride, MemoryBlock scan0)
        {
            this.width = width;
            this.height = height;
            this.stride = stride;
            this.scan0 = scan0;
        }

        ~Surface()
        {
            Dispose(false);
        }

        public Surface CreateWindow(Rectangle bounds)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            Rectangle original = this.Bounds;
            Rectangle sub = new Rectangle(bounds.Location, bounds.Size);
            Rectangle clipped = Rectangle.Intersect(original, sub);

            if (clipped != sub)
            {
                throw new ArgumentOutOfRangeException();
            }

            return new Surface(bounds.Width, bounds.Height, stride, 
                               new MemoryBlock(scan0, 
                               ((stride * bounds.Y) + (ColorBgra.SizeOf * bounds.X)), 
                               (((bounds.Height - 1) * stride) + bounds.Width * ColorBgra.SizeOf)));
        }

        public int GetRowByteOffset(int y)
        {
            if (y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException("y", "Out of bounds: y=" + y.ToString());
            }

            return y * stride;
        }

        public int GetRowByteOffsetUnchecked(int y)
        {
            return y * stride;
        }

     //   [CLSCompliant(false)]
        public unsafe ColorBgra *GetRowAddress(int y)
        {
            return (ColorBgra *)(((byte *)scan0.VoidStar) + GetRowByteOffset(y));
        }

     //   [CLSCompliant(false)]
        public unsafe ColorBgra *GetRowAddressUnchecked(int y)
        {
            return (ColorBgra *)(((byte *)scan0.VoidStar) + GetRowByteOffsetUnchecked(y));
        }

        public int GetColumnByteOffset(int x)
        {
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException("x", "Out of bounds: x=" + x.ToString());
            }

            return x * ColorBgra.SizeOf;
        }

        public int GetColumnByteOffsetUnchecked(int x)
        {
            return x * ColorBgra.SizeOf;
        }

        public int GetPointByteOffset(int x, int y)
        {
            return GetRowByteOffset(y) + GetColumnByteOffset(x);
        }

      //  [CLSCompliant(false)]
        public unsafe ColorBgra *GetPointAddress(int x, int y)
        {
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException("x", "Out of bounds: x=" + x.ToString());
            }

            return GetRowAddress(y) + x;
        }

        private int GetPointOffset(int x, int y)
        {
            return (y * stride) + (x * ColorBgra.SizeOf);
        }

     //   [CLSCompliant(false)]
        public unsafe ColorBgra *GetPointAddressUnchecked(int x, int y)
        {
            return GetRowAddressUnchecked(y) + x;
        }

      //  [CLSCompliant(false)]
        public unsafe ColorBgra *GetPointAddress(Point pt)
        {
            return GetPointAddress(pt.X, pt.Y);
        }

      //  [CLSCompliant(false)]
        public unsafe ColorBgra *GetPointAddressUnchecked(Point pt)
        {
            return pt.X + (ColorBgra *)(((byte *)scan0.VoidStar) + (pt.Y * Stride));
        }

        public MemoryBlock GetRow(int y)
        {
            return new MemoryBlock(scan0, GetRowByteOffset(y), y * stride);
        }

        public bool IsVisible(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public bool IsVisible(Point pt)
        {
            return pt.X >= 0 && pt.X < width && pt.Y >= 0 && pt.Y < Height;
        }

        public ColorBgra this[int x, int y]
        {
            get
            {
#if DEBUG
                if (disposed)
                {
                    throw new ObjectDisposedException("Surface");
                }

                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    throw new ArgumentOutOfRangeException("(x,y)", new Point(x, y), "Coordinates out of range, max=" + new Size(width, height).ToString());
                }
#endif

                unsafe
                {
                    return *GetPointAddress(x, y);
                }
            }

            set
            {
#if DEBUG
                if (disposed)
                {
                    throw new ObjectDisposedException("Surface");
                }

                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    throw new ArgumentOutOfRangeException("(x,y)", new Point(x, y), "Coordinates out of range, max=" + new Size(width, height).ToString());
                }
#endif

                unsafe
                {
                    *GetPointAddress(x, y) = value;
                }
            }
        }

        public ColorBgra this[Point pt]
        {
            get
            {
                return this[pt.X, pt.Y];
            }

            set
            {
                this[pt.X, pt.Y] = value;
            }
        }

        public Bitmap CreateAliasedBitmap(Rectangle bounds)
        {
            return CreateAliasedBitmap(bounds, true);
        }

        /// <summary>
        /// Creates a GDI+ Bitmap object that aliases the same memory that this Surface does.
        /// Then you can use GDI+ to draw on to this surface.
        /// Note: Since the Bitmap does not hold a reference to this Surface object, nor to
        /// the MemoryBlock that it contains, you must hold a reference to the Surface object
        /// for as long as you wish to use the aliased Bitmap. Otherwise the memory will be
        /// freed and the Bitmap will look corrupt or cause other errors.
        /// </summary>
        /// <param name="bounds">The rectangle of interest within this Surface that you wish to alias.</param>
        /// <param name="alpha">If true, the returned bitmap will use PixelFormat.Format32bppArgb. 
        /// If false, the returned bitmap will use PixelFormat.Format32bppRgb.</param>
        /// <returns>A GDI+ Bitmap that aliases the requested portion of the Surface.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><b>bounds</b> was not entirely within the boundaries of the Surface</exception>
        public Bitmap CreateAliasedBitmap(Rectangle bounds, bool alpha)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            if (bounds.IsEmpty)
            {
                throw new ArgumentOutOfRangeException();
            }

            Rectangle clipped = Rectangle.Intersect(this.Bounds, bounds);

            if (clipped != bounds)
            {
                throw new ArgumentOutOfRangeException();
            }

            unsafe
            {
                return new Bitmap(bounds.Width, bounds.Height, stride, alpha ? this.PixelFormat : PixelFormat.Format32bppRgb, 
                    new IntPtr((void *)((byte *)scan0.VoidStar + GetPointByteOffset(bounds.X, bounds.Y))));
            }
        }

        /// <summary>
        /// Helper function. Same as calling CreateAliasedBounds(Bounds).
        /// </summary>
        /// <returns>A GDI+ Bitmap that aliases the entire Surface.</returns>
        public Bitmap CreateAliasedBitmap()
        {
            return CreateAliasedBitmap(this.Bounds);
        }

        public static Surface CopyFromBitmap (Bitmap bitmap)
        {
            Surface surface = new Surface(bitmap.Width, bitmap.Height);
            BitmapData bd = bitmap.LockBits(surface.Bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                for (int y = 0; y < bd.Height; ++y)
                {
                    MemoryBlock.CopyMemory((void *)surface.GetRowAddress(y), 
                        (byte *)bd.Scan0.ToPointer() + (y * bd.Stride), bd.Stride);
                }
            }

            bitmap.UnlockBits(bd);
            return surface;
        }

        public void CopySurface(Surface source)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            int width = Math.Min(Width, source.Width);
            int height = Math.Min(Height, source.Height);

            unsafe
            {
                for (int y = 0; y < height; ++y)
                {
                    MemoryBlock.CopyMemory(GetRowAddress(y), source.GetRowAddress(y), width * ColorBgra.SizeOf);
                }
            }
        }

        // copies the given surface to a certain point on ourself, with clipping
        public void CopySurface(Surface source, Point dstOffset)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            Rectangle dstRect = new Rectangle(dstOffset, source.Size);
            dstRect.Intersect(Bounds);

            if (dstRect.Width == 0 || dstRect.Height == 0)
            {
                return;
            }

            Point sourceOffset = new Point(dstRect.Location.X - dstOffset.X, dstRect.Location.Y - dstOffset.Y);
            Rectangle sourceRect = new Rectangle(sourceOffset, dstRect.Size);
            Surface sourceWindow = source.CreateWindow(sourceRect);
            Surface dstWindow = this.CreateWindow(dstRect);
            dstWindow.CopySurface(sourceWindow);

            dstWindow.Dispose();
            sourceWindow.Dispose();
        }

        // copies a portion of the given surface to a certain offset on ourself, with clipping
        public void CopySurface(Surface source, Point dstOffset, Rectangle sourceRoi)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            Rectangle dstRoi = new Rectangle(dstOffset, sourceRoi.Size);
            dstRoi.Intersect(Bounds);

            if (dstRoi.Height == 0 || dstRoi.Width == 0)
            {
                return;
            }

            sourceRoi.X += dstRoi.X - dstOffset.X;
            sourceRoi.Y += dstRoi.Y - dstOffset.Y;
            sourceRoi.Width = dstRoi.Width;
            sourceRoi.Height = dstRoi.Height;

            Surface src = source.CreateWindow(sourceRoi);
            CopySurface(src, dstOffset);
            src.Dispose();
        }

        public void CopySurface(Surface source, PdnRegion region)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            foreach (Rectangle rect in region.GetRegionScansReadOnlyInt())
            {
                CopySurface(source, rect.Location, rect);
            }
        }

        /// <summary>
        /// Clones the Surface and its contents. Note that this method name is used instead
        /// of implementing ICloneable because the entire contents of the Surface are copied,
        /// even if it is a window onto another Surface. Because of that, copying a window
        /// will produce a Surface instance that is not aliased onto another Surface.
        /// </summary>
        /// <param name="source">The Surface to duplicate.</param>
        public Surface CopyContents()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Surface");
            }

            Surface ret = new Surface(Size);
            ret.CopySurface(this);
            return ret;
        }

        /// <summary>
        /// Use this when shrinking an image to dimensions that are "much smaller".
        /// </summary>
        /// <param name="source">The Surface to read pixels from.</param>
        public void SuperSamplingFitSurface(Surface source)
        {
            if (source.Width < Width || source.Height < Height)
            {
                this.BilinearFitSurface(source);
            }
            else for (int dstY = 0; dstY < Height; ++dstY)
            {
                int srcY = (dstY * source.Height) / Height;
                int srcYplus1 = ((dstY + 1) * source.Height) / Height;
                int srcHeight = srcYplus1 - srcY;

                for (int dstX = 0; dstX < Width; ++dstX)
                {
                    int srcX = (dstX * source.Width) / Width;
                    int srcXplus1 = ((dstX + 1) * source.Width) / Width;
                    int srcWidth = srcXplus1 - srcX;
                    int blueSum = 0;
                    int greenSum = 0;
                    int redSum = 0;
                    int alphaSum = 0;
                    int factor = 0;

                    for (int sy = srcY; sy < (srcY + srcHeight); ++sy)
                    {
                        for (int sx = srcX; sx < (srcX + srcWidth); ++sx)
                        {
                            ColorBgra c = source[sx, sy];

                            blueSum += c.B;
                            greenSum += c.G;
                            redSum += c.R;
                            alphaSum += c.A;
                            ++factor;
                        }
                    }

                    if (factor == 0)
                    {
                        continue;
                    }

                    int blueAvg = blueSum / factor;
                    int greenAvg = greenSum / factor;
                    int redAvg = redSum / factor;
                    int alphaAvg = alphaSum / factor;

                    this[dstX, dstY] = ColorBgra.FromBgra((byte)blueAvg, (byte)greenAvg, (byte)redAvg, (byte)alphaAvg);
                }
            }
        }

        public void BilinearFitSurface(Surface source)
        {
            unsafe
            {
                int x;
                int y;
                uint mx;
                uint my;
                uint inRow;
                uint outRow;
                int inMaxX;
                int inMaxY;
                int srcStride;
                ColorBgra *inPtr;
                ColorBgra *outPtr;
                int dstWidth;
                int dstHeight;

                // figure out "slopes" for both horizontal and vertical, using fixed-point math
                // the slop is the x/y increment for the original image. ie, increment the x value
                // every 2 pixels for every 1 pixel being written to the Dest (that would be
                // if half-sizing Source into Dest)
                mx = ((uint)source.Width << 12) / (uint)this.Width;
                my = ((uint)source.Height << 12) / (uint)this.Height;

                outRow = 0;
                inMaxX = (source.Width - 1) << 12;
                inMaxY = (source.Height - 1) << 12;
                dstWidth = this.Width;
                dstHeight = this.Height;
                srcStride = source.Stride;

                // In order to avoid a black line on the right hand and bottom sides, we resize to
                // 1 pixel wide and 1 pixel taller.
                dstWidth++;
                dstHeight++;

                for (y = 0, inRow = 0; y < dstHeight - 1; ++y)
                {
                    uint inColumn;
                    uint inRowFract;
                    uint inRowFractInv;

                    inRowFract = inRow & 0xfff;
                    inRowFractInv = 0x1000 - inRowFract;

                    inPtr  = source.GetRowAddress((int)(inRow >> 12));
                    outPtr = this.GetRowAddress((int)outRow);

                    for (x = 0, inColumn = 0; x < dstWidth - 1; x++)
                    {
                        uint inColumnFract;
                        uint inColumnFractInv;
                        uint wa, wb, wc, wd;    // weight values

                        inColumnFract = inColumn & 0xfff;
                        inColumnFractInv = 0x1000 - inColumnFract;

                        // Compute weight values for (x,y), (x+1,y), (x,y+1), (x+1,y+1)
                        wa = (inColumnFractInv * inRowFractInv) >> 12;
                        wb = (inColumnFract    * inRowFractInv) >> 12;
                        wc = (inColumnFractInv * inRowFract)    >> 12;
                        wd = (inColumnFract    * inRowFract)    >> 12;

                        // get texel samples: (x,y), (x+1,y), (x,y+1), (x+1,y+1)
                        ColorBgra *ta = inPtr + (inColumn >> 12);
                        ColorBgra *tb = ta + 1;
                        // btw, pointer is casted to uint8* so that SrcStride can be used to increment
                        // pointer in bytes, not pixels (ie, 4 bytes)
                        ColorBgra *tc = (ColorBgra *)(((byte *)ta) + srcStride);
                        ColorBgra *td = tc + 1;

                        // now compute the resultant pixel
                        *outPtr = ColorBgra.FromBgra
                            (
                            (byte)(((ta->B * wa) + (tb->B * wb) + (tc->B * wc) + (td->B * wd)) >> 12),
                            (byte)(((ta->G * wa) + (tb->G * wb) + (tc->G * wc) + (td->G * wd)) >> 12),
                            (byte)(((ta->R * wa) + (tb->R * wb) + (tc->R * wc) + (td->R * wd)) >> 12),
                            (byte)(((ta->A * wa) + (tb->A * wb) + (tc->A * wc) + (td->A * wd)) >> 12)
                            );

                        outPtr++;
                        inColumn += mx;
                    }

                    outRow++;
                    inRow += my;
                }
            }
        }

        #region IDisposable Members
        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;

                if (disposing)
                {
                    scan0.Dispose();
                    scan0 = null;
                }
            }
        }
        #endregion
    }
}
