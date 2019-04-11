using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security;

namespace PaintDotNet
{
    /// <summary>
    /// Designed as a proxy to the GDI+ Region class, while allowing for a
    /// replacement that won't break code. The main reason for having this
    /// right now is to work around some bugs in System.Drawing.Region,
    /// especially the memory leak in GetRegionScans().
    /// </summary>
    public sealed class PdnRegion
        : MarshalByRefObject,
          IDisposable
    {
        private object lockObject = new object();
        private Region gdiRegion;
        bool changed = true;
        private int cachedArea = -1;
        private MemoryBlock scansBlock = null;
        private RectangleF[] cachedRectsF = null;
        private Rectangle[] cachedRects = null;

        [ThreadStatic]
        private Graphics nullGraphics;
        
        private Graphics NullGraphics
        {   
            get
            {
                if (nullGraphics == null)
                {
                    nullGraphics = Graphics.FromImage(new Bitmap(16, 16));
                }

                return nullGraphics;
            }
        }

        public object SyncRoot
        {
            get
            {
                return lockObject;
            }
        }

        public int GetArea()
        {
            lock (lockObject)
            {
                int theCachedArea = cachedArea;

                if (theCachedArea == -1)
                {
                    int ourCachedArea = 0;

                    foreach (Rectangle rect in GetRegionScansReadOnlyInt())
                    {
                        try
                        {
                            ourCachedArea += rect.Width * rect.Height;
                        }

                        catch (System.OverflowException)
                        {
                            ourCachedArea = int.MaxValue;
                            break;
                        }
                    }

                    cachedArea = ourCachedArea;
                    return ourCachedArea;
                }
                else
                {
                    return theCachedArea;
                }
            }
        }

        private bool IsChanged()
        {
            return changed;
        }

        private void Changed()
        {
            lock (lockObject)
            {
                changed = true;
                cachedArea = -1;
            }
        }

        private void ResetChanged()
        {
            lock (lockObject)
            {
                changed = false;
            }
        }

        #region Construction
        public PdnRegion()
        {
            gdiRegion = new Region();
        }

        public PdnRegion(GraphicsPath path)
        {
            gdiRegion = new Region(path);
        }

        public PdnRegion(PdnGraphicsPath pdnPath)
            : this(pdnPath.GetRegionCache())
        {
        }

        public PdnRegion(Rectangle rect)
        {
            gdiRegion = new Region(rect);
        }

        public PdnRegion(RectangleF rectF)
        {
            gdiRegion = new Region(rectF);
        }

        public PdnRegion(RegionData regionData)
        {
            gdiRegion = new Region(regionData);
        }

        public PdnRegion(Region region)
        {
            gdiRegion = region.Clone();
        }

        private PdnRegion(PdnRegion pdnRegion)
        {
            lock (pdnRegion.lockObject)
            {
                this.gdiRegion = pdnRegion.gdiRegion.Clone();
                this.changed = pdnRegion.changed;
                this.cachedArea = pdnRegion.cachedArea;
                this.cachedRectsF = pdnRegion.cachedRectsF; 
                this.cachedRects = pdnRegion.cachedRects;
            }
        }

        // DEPRECATED
        [Obsolete]
        private PdnRegion(IntPtr hrgn)
        {
            lock (lockObject)
            {
                gdiRegion = Region.FromHrgn(hrgn);
            }
        }

        public static PdnRegion CreateEmpty()
        {
            PdnRegion region = new PdnRegion();
            region.MakeEmpty();
            return region;
        }

        public PdnRegion Clone()
        {
            return new PdnRegion(this);
        }
        #endregion

        #region Destruction
        ~PdnRegion()
        {
            Dispose(false);
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    gdiRegion.Dispose();
                    gdiRegion = null;
                }

                disposed = true;
            }
        }
        #endregion

        //[Obsolete]
        public static implicit operator Region(PdnRegion convert)
        {
            return convert.gdiRegion;
        }

        public void Complement(GraphicsPath path)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Complement(path);
            }
        }

        public void Complement(Rectangle rect)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Complement(rect);
            }
        }

        public void Complement(RectangleF rectF)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Complement(rectF);
            }
        }

        public void Complement(Region region)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Complement(region);
            }
        }

        public void Complement(PdnRegion region2)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Complement(region2.gdiRegion);
            }
        }

        public bool Equals(Region region, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.Equals(region, g);
            }
        }

        public bool Equals(PdnRegion region2, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.Equals(region2.gdiRegion, g);
            }
        }

        public void Exclude(GraphicsPath path)
        {
            lock (lockObject)
            {
                gdiRegion.Exclude(path);
            }
        }

        public void Exclude(Rectangle rect)
        {
            lock (lockObject)
            {
                gdiRegion.Exclude(rect);
            }
        }

        public void Exclude(RectangleF rectF)
        {
            lock (lockObject)
            {
                gdiRegion.Exclude(rectF);
            }
        }

        public void Exclude(Region region)
        {
            lock (lockObject)
            {
                gdiRegion.Exclude(region);
            }
        }

        public void Exclude(PdnRegion region2)
        {
            lock (lockObject)
            {
                gdiRegion.Exclude(region2.gdiRegion);
            }
        }

        [Obsolete]
        public static PdnRegion FromHrgn(IntPtr hrgn)
        {
            return new PdnRegion(hrgn);
        }

        public RectangleF GetBounds(Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.GetBounds(g);
            }
        }

        public RectangleF GetBounds()
        {
            lock (lockObject)
            {
                return gdiRegion.GetBounds(NullGraphics);
            }
        }

        [Obsolete]
        public IntPtr GetHrgn(Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.GetHrgn(g);
            }
        }

        public RegionData GetRegionData()
        {
            lock (lockObject)
            {
                return gdiRegion.GetRegionData();
            }
        }

        private sealed class NativeMethods
        {
            private NativeMethods()
            {
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RGNDATAHEADER 
            { 
                public uint dwSize; 
                public uint iType; 
                public uint nCount; 
                public uint nRgnSize; 
                public RECT rcBound; 
            };

            [StructLayout(LayoutKind.Sequential)]
            public struct RGNDATA
            {
                public RGNDATAHEADER rdh;

                public unsafe static RECT *GetRectsPointer(RGNDATA *me)
                {
                    return (RECT *)((byte *)me + sizeof(RGNDATAHEADER));
                }
            }

            [SuppressUnmanagedCodeSecurity]
            [DllImport("Gdi32.dll", SetLastError = true)]
            public unsafe static extern uint GetRegionData(
                IntPtr hRgn,         // handle to region
                uint dwCount,        // size of region data buffer
                RGNDATA *lpRgnData   // region data buffer
                );
        }

        public RectangleF[] GetRegionScans(System.Drawing.Drawing2D.Matrix matrix)
        {
            return GetRegionScans();
        }

        public RectangleF[] GetRegionScans()
        {
            return (RectangleF[])GetRegionScansReadOnly().Clone();
        }

        /// <summary>
        /// This is an optimized version of GetRegionScans that returns a reference to the array
        /// that is used to cache the region scans. This mitigates performance when this array
        /// is requested many times on an unmodified PdnRegion.
        /// Thus, by using this method you are promising to not modify the array that is returned.
        /// </summary>
        /// <returns></returns>
        public RectangleF[] GetRegionScansReadOnly()
        {
            lock (lockObject)
            {
                if (changed)
                {
                    UpdateCachedRegionScans();
                }

                if (cachedRectsF == null)
                {
                    cachedRectsF = new RectangleF[cachedRects.Length];

                    for (int i = 0; i < cachedRectsF.Length; ++i)
                    {
                        cachedRectsF[i] = (RectangleF)cachedRects[i];
                    }
                }

                return this.cachedRectsF;
            }
        }

        public Rectangle[] GetRegionScansInt(System.Drawing.Drawing2D.Matrix matrix)
        {
            return GetRegionScansInt();
        }

        public Rectangle[] GetRegionScansInt()
        {
            return (Rectangle[])GetRegionScansReadOnlyInt().Clone();
        }

        public Rectangle[] GetRegionScansReadOnlyInt()
        {
            lock (lockObject)
            {
                if (changed)
                {
                    UpdateCachedRegionScans();
                }

                return this.cachedRects;
            }
        }

        private const int screwUpMax = 10;
#if DEBUG
        private static int[] screwUpCount = new int[screwUpMax];
#endif

        private unsafe void UpdateCachedRegionScans()
        {
            lock (lockObject)
            {
                using (GdiHandleWrapper hRgn = new GdiHandleWrapper(gdiRegion.GetHrgn(NullGraphics)))
                {
                    uint bytes = 0;
                    int countdown = screwUpMax;                    
  
                    while (countdown > 0)
                    {
                        bytes = NativeMethods.GetRegionData(hRgn.HGdiObj, 0, (NativeMethods.RGNDATA *)IntPtr.Zero);

                        if (bytes == 0)
                        {
                            --countdown;
#if DEBUG
                            System.Threading.Interlocked.Increment(ref screwUpCount[countdown]);
#endif
                            System.Threading.Thread.Sleep(5);
                        }
                        else
                        {
                            break;
                        }
                    }

                    // But if we retry several times and it still messes up then we will finally give up.
                    if (bytes == 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new Win32Exception(error, "GetRegionData returned " + bytes.ToString() + ", GetLastError() = " + error.ToString());
                    }

                    if (scansBlock == null || bytes > scansBlock.Length)
                    {
                        if (scansBlock != null)
                        {
                            scansBlock.Dispose();
                            scansBlock = null;
                        }

                        scansBlock = new MemoryBlock((int)bytes);
                    }

                    NativeMethods.RGNDATA *pRgnData = (NativeMethods.RGNDATA *)scansBlock.VoidStar;
                    uint result = NativeMethods.GetRegionData(hRgn.HGdiObj, bytes, pRgnData);

                    if (result != bytes)
                    {
                        throw new OutOfMemoryException("NativeMethods.GetRegionData returned 0");
                    }

                    NativeMethods.RECT *pRects = NativeMethods.RGNDATA.GetRectsPointer(pRgnData);
                    //RectangleF[] ourCachedRectsF = new RectangleF[pRgnData->rdh.nCount];
                    Rectangle[] ourCachedRects = new Rectangle[pRgnData->rdh.nCount];
            
                    for (int i = 0; i < ourCachedRects.Length; ++i)
                    {
                        //ourCachedRectsF[i] = RectangleF.FromLTRB(pRects[i].left, pRects[i].top, pRects[i].right, pRects[i].bottom);
                        ourCachedRects[i] = Rectangle.FromLTRB(pRects[i].left, pRects[i].top, pRects[i].right, pRects[i].bottom);
                    }

                    pRects = null;
                    pRgnData = null;

                    changed = false;

                    //Debug.WriteLine("Uncached call to PdnRegion.GetRegionScans() (" + cachedRectsF.Length.ToString() + " rects)");

                    //cachedRectsF = ourCachedRectsF;
                    cachedRectsF = null;
                    cachedRects = ourCachedRects;
                }
            }
        }
                
        public void Intersect(GraphicsPath path)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Intersect(path);
            }
        }

        public void Intersect(Rectangle rect)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Intersect(rect);
            }
        }

        public void Intersect(RectangleF rectF)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Intersect(rectF);
            }
        }

        public void Intersect(Region region)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Intersect(region);
            }
        }

        public void Intersect(PdnRegion region2)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Intersect(region2.gdiRegion);
            }
        }

        public bool IsEmpty(Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsEmpty(g);
            }
        }

        public bool IsEmpty()
        {
            return GetArea() == 0;
        }

        public bool IsInfinite(Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsInfinite(g);
            }
        }

        public bool IsVisible(Point point)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(point);
            }
        }

        public bool IsVisible(PointF pointF)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(pointF);
            }
        }

        public bool IsVisible(Rectangle rect)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(rect);
            }
        }

        public bool IsVisible(RectangleF rectF)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(rectF);
            }
        }

        public bool IsVisible(Point point, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(point, g);
            }
        }
        
        public bool IsVisible(PointF pointF, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(pointF, g);
            }
        }

        public bool IsVisible(Rectangle rect, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(rect, g);
            }
        }

        public bool IsVisible(RectangleF rectF, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(rectF, g);
            }
        }

        public bool IsVisible(float x, float y)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y);
            }
        }

        public bool IsVisible(int x, int y, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y, g);
            }
        }

        public bool IsVisible(float x, float y, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y, g);
            }
        }

        public bool IsVisible(int x, int y, int width, int height)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y, width, height);
            }
        }

        public bool IsVisible(float x, float y, float width, float height)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y, width, height);
            }
        }

        public bool IsVisible(int x, int y, int width, int height, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y, width, height, g);
            }
        }

        public bool IsVisible(float x, float y, float width, float height, Graphics g)
        {
            lock (lockObject)
            {
                return gdiRegion.IsVisible(x, y, width, height, g);
            }
        }

        public void MakeEmpty()
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.MakeEmpty();
            }
        }

        public void MakeInfinite()
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.MakeInfinite();
            }
        }

        public void Transform(Matrix matrix)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Transform(matrix);
            }
        }

        public void Union(GraphicsPath path)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Union(path);
            }
        }

        public void Union(Rectangle rect)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Union(rect);
            }
        }

        public void Union(RectangleF rectF)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Union(rectF);
            }
        }

        public void Union(RectangleF[] rectsF)
        {
            lock (lockObject)
            {
                Changed();

                using (PdnRegion tempRegion = Utility.RectanglesToRegion(rectsF))
                {
                    this.Union(tempRegion);
                }
            }
        }


        public void Union(Region region)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Union(region);
            }
        }

        public void Union(PdnRegion region2)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Union(region2.gdiRegion);
            }
        }

        public void Xor(Rectangle rect)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Xor(rect);
            }
        }

        public void Xor(RectangleF rectF)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Xor(rectF);
            }
        }

        public void Xor(Region region)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Xor(region);
            }
        }

        public void Xor(PdnRegion region2)
        {
            lock (lockObject)
            {
                Changed();
                gdiRegion.Xor(region2.gdiRegion);
            }
        }
    }
}
