using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PaintDotNet
{ 
    /// <summary>
    /// Summary description for PdnGraphicsPath.
    /// </summary>
    public class PdnGraphicsPath
        : MarshalByRefObject,
          ICloneable,
          IDisposable
    { 
        private GraphicsPath gdiPath;

        internal PdnRegion regionCache = null;

        public static implicit operator GraphicsPath(PdnGraphicsPath convert)
        {
            return convert.gdiPath;
        }

        internal PdnRegion GetRegionCache()
        {
            if (regionCache == null)
            {
                regionCache = new PdnRegion(this.gdiPath);
            }

            return regionCache;
        }

        public GraphicsPath GdiPath
        {
            get 
            { 
                return gdiPath; 
            }
        }

        private void Changed()
        {
            if (regionCache != null)
            {
                lock (regionCache.SyncRoot)
                {
                    regionCache.Dispose();
                    regionCache = null;
                }
            }
        }

        public PdnGraphicsPath()
        {
            Changed();
            gdiPath = new GraphicsPath();
        }

        public PdnGraphicsPath(GraphicsPath wrapMe)
        {
            Changed();
            gdiPath = wrapMe;
        }

        public PdnGraphicsPath(FillMode fillMode)
        {
            Changed();
            gdiPath = new GraphicsPath(fillMode);
        }

        public PdnGraphicsPath(Point[] pts, byte[] types)
        {
            Changed();
            gdiPath = new GraphicsPath(pts, types);
        }

        public PdnGraphicsPath(PointF[] pts, byte[] types)
        {
            Changed();
            gdiPath = new GraphicsPath(pts, types);
        }

        public PdnGraphicsPath(Point[] pts, byte[] types, FillMode fillMode)
        {
            Changed();
            gdiPath = new GraphicsPath(pts, types, fillMode);
        }

        public PdnGraphicsPath(PointF[] pts, byte[] types, FillMode fillMode)
        {
            Changed();
            gdiPath = new GraphicsPath(pts, types, fillMode);
        }

        ~PdnGraphicsPath()
        {
            Changed();
            Dispose(false);
        }

        public FillMode FillMode
        { 
            get { return gdiPath.FillMode; }

            set 
            { 
                Changed(); 
                gdiPath.FillMode = value; 
            }
        }

        public PathData PathData
        { 
            get { return gdiPath.PathData; }
        }

        public PointF[] PathPoints
        { 
            get { return gdiPath.PathPoints; }
        }

        public byte[] PathTypes
        { 
            get { return gdiPath.PathTypes; }
        }

        public int PointCount
        { 
            get { return gdiPath.PointCount; }
        }

        public void AddArc(Rectangle rect, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddArc(rect, startAngle, sweepAngle);
        }

        public void AddArc(RectangleF rectF, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddArc(rectF, startAngle, sweepAngle);
        }

        public void AddArc(int x, int y, int width, int height, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddArc(x, y, width, height, startAngle, sweepAngle);
        }

        public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddArc(x, y, width, height, startAngle, sweepAngle);
        }

        public void AddBezier(Point pt1, Point pt2, Point pt3, Point pt4)
        {
            Changed();
            gdiPath.AddBezier(pt1, pt2, pt3, pt4);
        }

        public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            Changed();
            gdiPath.AddBezier(pt1, pt2, pt3, pt4);
        }

        public void AddBezier(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4)
        {
            Changed();
            gdiPath.AddBezier(x1, y1, x2, y2, x3, y3, x4, y4);
        }

        public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        {
            Changed();
            gdiPath.AddBezier(x1, y1, x2, y2, x3, y3, x4, y4);
        }

        public void AddBeziers(Point[] points)
        {
            Changed();
            gdiPath.AddBeziers(points);
        }

        public void AddBeziers(PointF[] points)
        {
            Changed();
            gdiPath.AddBeziers(points);
        }

        public void AddClosedCurve(Point[] points)
        {
            Changed();
            gdiPath.AddClosedCurve(points);
        }

        public void AddClosedCurve(PointF[] points)
        {
            Changed();
            gdiPath.AddClosedCurve(points);
        }

        public void AddClosedCurve(Point[] points, float tension)
        {
            Changed();
            gdiPath.AddClosedCurve(points, tension);
        }

        public void AddClosedCurve(PointF[] points, float tension)
        {
            Changed();
            gdiPath.AddClosedCurve(points, tension);
        }

        public void AddCurve(Point[] points)
        {
            Changed();
            gdiPath.AddCurve(points);
        }

        public void AddCurve(PointF[] points)
        {
            Changed();
            gdiPath.AddCurve(points);
        }

        public void AddCurve(Point[] points, float tension)
        {
            Changed();
            gdiPath.AddCurve(points, tension);
        }

        public void AddCurve(PointF[] points, float tension)
        {
            Changed();
            gdiPath.AddCurve(points, tension);
        }

        public void AddCurve(Point[] points, int offset, int numberOfSegments, float tension)
        {
            Changed();
            gdiPath.AddCurve(points, offset, numberOfSegments, tension);
        }

        public void AddCurve(PointF[] points, int offset, int numberOfSegments, float tension)
        {
            Changed();
            gdiPath.AddCurve(points, offset, numberOfSegments, tension);
        }

        public void AddEllipse(Rectangle rect)
        {
            Changed();
            gdiPath.AddEllipse(rect);
        }

        public void AddEllipse(RectangleF rectF)
        {
            Changed();
            gdiPath.AddEllipse(rectF);
        }

        public void AddEllipse(int x, int y, int width, int height)
        {
            Changed();
            gdiPath.AddEllipse(x, y, width, height);
        }

        public void AddEllipse(float x, float y, float width, float height)
        {
            Changed();
            gdiPath.AddEllipse(x, y, width, height);
        }

        public void AddLine(Point pt1, Point pt2)
        {
            Changed();
            gdiPath.AddLine(pt1, pt2);
        }

        public void AddLine(PointF pt1, PointF pt2)
        {
            Changed();
            gdiPath.AddLine(pt1, pt2);
        }

        public void AddLine(int x1, int y1, int x2, int y2)
        {
            Changed();
            gdiPath.AddLine(x1, y1, x2, y2);
        }

        public void AddLine(float x1, float y1, float x2, float y2)
        {
            Changed();
            gdiPath.AddLine(x1, y1, x2, y2);
        }

        public void AddLines(Point[] points)
        {
            Changed();
            gdiPath.AddLines(points);
        }

        public void AddLines(PointF[] points)
        {
            Changed();
            gdiPath.AddLines(points);
        }

        public void AddPath(GraphicsPath addingPath, bool connect)
        {
            Changed();
            gdiPath.AddPath(addingPath, connect);
        }

        public void AddPie(Rectangle rect, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddPie(rect, startAngle, sweepAngle);
        }

        public void AddPie(int x, int y, int width, int height, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddPie(x, y, width, height, startAngle, sweepAngle);
        }

        public void AddPie(float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            Changed();
            gdiPath.AddPie(x, y, width, height, startAngle, sweepAngle);
        }

        public void AddPolygon(Point[] points)
        {
            Changed();
            gdiPath.AddPolygon(points);
        }

        public void AddPolygon(PointF[] points)
        {
            Changed();
            gdiPath.AddPolygon(points);
        }

        public void AddRectangle(Rectangle rect)
        {
            Changed();
            gdiPath.AddRectangle(rect);
        }

        public void AddRectangle(RectangleF rectF)
        {
            Changed();
            gdiPath.AddRectangle(rectF);
        }

        public void AddRectangles(Rectangle[] rects)
        {
            Changed();
            gdiPath.AddRectangles(rects);
        }

        public void AddRectangles(RectangleF[] rectsF)
        {
            Changed();
            gdiPath.AddRectangles(rectsF);
        }

        public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat format)
        {
            Changed();
            gdiPath.AddString(s, family, style, emSize, origin, format);
        }


        public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat format)
        {
            Changed();
            gdiPath.AddString(s, family, style, emSize, origin, format);
        }


        public void AddString(string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat format)
        {
            Changed();
            gdiPath.AddString(s, family, style, emSize, layoutRect, format);
        }


        public void AddString(string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat format)
        {
            Changed();
            gdiPath.AddString(s, family, style, emSize, layoutRect, format);
        }

        public void ClearMarkers()
        {
            Changed();
            gdiPath.ClearMarkers();
        }

        public virtual object Clone()
        {
            return new PdnGraphicsPath((GraphicsPath)gdiPath.Clone());
        }

        public void CloseAllFigures()
        {
            Changed();
            gdiPath.CloseAllFigures();
        }

        public void CloseFigure()
        {
            Changed();
            gdiPath.CloseFigure();
        }

        private bool disposed = false;
        public virtual void Dispose()
        {
            Changed();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        { 
            if (!disposed)
            { 
                if (disposing)
                { 
                    gdiPath.Dispose();
                    gdiPath = null;
                }

                disposed = true;
            }
        }

        public void Flatten()
        {
            Changed();
            gdiPath.Flatten();
        }

        public void Flatten(Matrix matrix)
        {
            Changed();
            gdiPath.Flatten(matrix);
        }

        public void Flatten(Matrix matrix, float flatness)
        {
            Changed();
            gdiPath.Flatten(matrix, flatness);
        }

        public RectangleF GetBounds()
        {
            return gdiPath.GetBounds();
        }

        public RectangleF GetBounds(Matrix matrix)
        {
            return gdiPath.GetBounds(matrix);
        }

        public RectangleF GetBounds(Matrix matrix, Pen pen)
        {
            return gdiPath.GetBounds(matrix, pen);
        }

        public PointF GetLastPoint()
        {
            return gdiPath.GetLastPoint();
        }

        public bool IsOutlineVisible(Point point, Pen pen)
        {
            return gdiPath.IsOutlineVisible(point, pen);
        }

        public bool IsOutlineVisible(PointF point, Pen pen)
        {
            return gdiPath.IsOutlineVisible(point, pen);
        }

        public bool IsOutlineVisible(int x, int y, Pen pen)
        {
            return gdiPath.IsOutlineVisible(x, y, pen);
        }

        public bool IsOutlineVisible(Point point, Pen pen, Graphics g)
        {
            return gdiPath.IsOutlineVisible(point, pen, g);
        }

        public bool IsOutlineVisible(PointF point, Pen pen, Graphics g)
        {
            return gdiPath.IsOutlineVisible(point, pen, g);
        }

        public bool IsOutlineVisible(float x, float y, Pen pen)
        {
            return gdiPath.IsOutlineVisible(x, y, pen);
        }

        public bool IsOutlineVisible(int x, int y, Pen pen, Graphics g)
        {
            return gdiPath.IsOutlineVisible(x, y, pen, g);
        }

        public bool IsOutlineVisible(float x, float y, Pen pen, Graphics g)
        {
            return gdiPath.IsOutlineVisible(x, y, pen, g);
        }

        public bool IsVisible(Point point)
        {
            return gdiPath.IsVisible(point);
        }

        public bool IsVisible(PointF point)
        {
            return gdiPath.IsVisible(point);
        }

        public bool IsVisible(int x, int y)
        {
            return gdiPath.IsVisible(x, y);
        }

        public bool IsVisible(Point point, Graphics g)
        {
            return gdiPath.IsVisible(point, g);
        }

        public bool IsVisible(PointF point, Graphics g)
        {
            return gdiPath.IsVisible(point, g);
        }

        public bool IsVisible(float x, float y)
        {
            return gdiPath.IsVisible(x, y);
        }

        public bool IsVisible(int x, int y, Graphics g)
        {
            return gdiPath.IsVisible(x, y, g);
        }

        public bool IsVisible(float x, float y, Graphics g)
        {
            return gdiPath.IsVisible(x, y, g);
        }
        
        public void Reset()
        {
            Changed();
            gdiPath.Reset();
        }

        public void Reverse()
        {
            Changed();
            gdiPath.Reverse();
        }

        public void SetMarkers()
        {
            Changed();
            gdiPath.SetMarkers();
        }

        public void StartFigure()
        {
            Changed();
            gdiPath.StartFigure();
        }

        public void Transform(Matrix matrix)
        {
            Changed();
            gdiPath.Transform(matrix);
        }

        public void Warp(PointF[] destPoints, RectangleF srcRect)
        {
            Changed();
            gdiPath.Warp(destPoints, srcRect);
        }

        public void Warp(PointF[] destPoints, RectangleF srcRect, Matrix matrix)
        {
            Changed();
            gdiPath.Warp(destPoints, srcRect, matrix);
        }

        public void Warp(PointF[] destPoints, RectangleF srcRect, Matrix matrix, WarpMode warpMode)
        {
            Changed();
            gdiPath.Warp(destPoints, srcRect, matrix, warpMode);
        }

        public void Warp(PointF[] destPoints, RectangleF srcRect, Matrix matrix, WarpMode warpMode, float flatness)
        {
            Changed();
            gdiPath.Warp(destPoints, srcRect, matrix, warpMode, flatness);
        }

        public void Widen(Pen pen)
        {
            Changed();
            gdiPath.Widen(pen);
        }

        public void Widen(Pen pen, Matrix matrix)
        {
            Changed();
            gdiPath.Widen(pen, matrix);
        }

        public void Widen(Pen pen, Matrix matrix, float flatness)
        {
            Changed();
            gdiPath.Widen(pen, matrix, flatness);
        }
    }
}
