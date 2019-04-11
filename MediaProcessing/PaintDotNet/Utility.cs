using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace PaintDotNet
{
    /// <summary>
    /// Defines miscellaneous constants and static functions.
    /// </summary>
    public sealed class Utility
    {
        private Utility()
        {
        }

        private static DateTime startTime = DateTime.Now;

        [Conditional("DEBUG")]
        public static void TraceMe(string message)
        {
            StackTrace trace = new StackTrace();
            StackFrame parentFrame = trace.GetFrame(1);
            MethodBase parentMethod = parentFrame.GetMethod();
            DateTime now = DateTime.Now;
            Debug.WriteLine(((now.Ticks - startTime.Ticks) / 10000).ToString() + ": " + parentMethod.DeclaringType.Name + "." + parentMethod.Name + (message != null ? (": " + message) : ""));
        }

        [Conditional("DEBUG")]
        public static void TraceMe()
        {
            StackTrace trace = new StackTrace();
            StackFrame parentFrame = trace.GetFrame(1);
            MethodBase parentMethod = parentFrame.GetMethod();
            DateTime now = DateTime.Now;
            Debug.WriteLine(((now.Ticks - startTime.Ticks) / 10000).ToString() + ": " + parentMethod.DeclaringType.Name + "." + parentMethod.Name);
        }

        public static void GCFullCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static int defaultSimplificationFactor = 30;
        public static int DefaultSimplificationFactor
        {
            get
            {
                return defaultSimplificationFactor;
            }

            set
            {
                defaultSimplificationFactor = value;
            }
        }




        public static long TicksToMs(long ticks)
        {
            return ticks/10000;
        }

        public static string GetStaticName(Type type)
        {
            PropertyInfo pi = type.GetProperty("StaticName", BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty);
            return (string)pi.GetValue(null, null);
        }


        public static readonly float[][] Identity5x5F = new float[][] { new float[] { 1, 0, 0, 0, 0 },
                                                                        new float[] { 0, 1, 0, 0, 0 },
                                                                        new float[] { 0, 0, 1, 0, 0 },
                                                                        new float[] { 0, 0, 0, 1, 0 },
                                                                        new float[] { 0, 0, 0, 0, 1 } };

        public static readonly ColorMatrix IdentityColorMatrix = new ColorMatrix(Identity5x5F);

        [ThreadStatic]
        private static Matrix identityMatrix = null;
        public static Matrix IdentityMatrix
        {
            get
            {
                if (identityMatrix == null)
                {
                    identityMatrix = new Matrix();
                    identityMatrix.Reset();
                }

                return identityMatrix;
            }
        }

        /// <summary>
        /// Rounds an integer to the smallest power of 2 that is greater
        /// than or equal to it.
        /// </summary>
        public static int Log2RoundUp(int x)
        {
            if (x == 0)
            {
                return 1;
            }

            if (x == 1)
            {
                return 1;
            }

            return 1 << (1 + HighestBit(x - 1));
        }

        private static int HighestBit(int x)
        {
            if (x == 0)
            {
                return 0;
            }

            int b = 0;
            int hi = 0;

            while (b <= 30)
            {
                if ((x & (1 << b)) != 0)
                {
                    hi = b;
                }

                ++b;
            }

            return hi;
        }

        private int CountBits(int x)
        {
            uint y = (uint)x;
            int count = 0;

            for (int bit = 0; bit < 32; ++bit)
            {
                if ((y & ((uint)1 << bit)) != 0)
                {
                    ++count;
                }
            }

            return count;
        }

        /// <summary>
        /// Converts a string name into a more "user friendly" style. Useful for enumeration names.
        /// Example: Converts "TopLeftCenter" to "Top Left Center"
        /// </summary>
        /// <param name="str1"></param>
        /// <returns></returns>
        public static string InsertSpaces(String str1)
        {
            string str2 = string.Copy(str1);
            bool number = false;

            for (int i = 1; i < str2.Length; i++)
            {
                char ch = str2[i];
                if (char.IsUpper(ch))
                {
                    str2 = str2.Insert(i, " ");
                    i++;
                    number = false;
                }
                if (char.IsNumber(ch))
                {
                    if (!number)
                    {
                        str2 = str2.Insert(i, " ");
                        i++;
                    }
                    number = true;
                }
            }

            return str2;
        }

        public static string RemoveSpaces(string s)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in s)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
        
        public static int Max(int[,] array)
        {
            int max = int.MinValue;

            for (int i = array.GetLowerBound(0); i <= array.GetUpperBound(0); ++i)
            {
                for (int j = array.GetLowerBound(1); j <= array.GetUpperBound(1); ++j)
                {
                    if (array[i,j] > max)
                    {
                        max = array[i,j];
                    }
                }
            }

            return max;
        }

        public static int Sum(int[,] array)
        {
            int sum = 0;

            for (int i = array.GetLowerBound(0); i <= array.GetUpperBound(0); ++i)
            {
                for (int j = array.GetLowerBound(1); j <= array.GetUpperBound(1); ++j)
                {
                    sum += array[i,j];
                }
            }

            return sum;
        }





        public static string SizeStringFromBytes(double bytes)
        {
            string returnMe;

            // Gigs
            if (bytes > (1024 * 1024 * 1024))
            {
                bytes /= 1024 * 1024 * 1024;
                returnMe = bytes.ToString("F1") + " GB";
            }
                // Megs
            else if (bytes > (1024 * 1024))
            {
                bytes /= 1024 * 1024;
                returnMe = bytes.ToString("F1") + " MB";
            }
                // K
            else if (bytes > (1024))
            {
                bytes /= 1024;
                returnMe = bytes.ToString("F1") + " KB";
            }
                // Bytes
            else
            {
                returnMe = bytes.ToString("F0") + " Bytes";
            }

            return returnMe;
        }



		public static Icon ImageToIcon(Image image)
		{
			return ImageToIcon(image, Color.FromArgb(192, 192, 192));
		}

		public static Icon ImageToIcon(Image image, bool disposeImage)
		{
			return ImageToIcon(image, Color.FromArgb(192, 192, 192), disposeImage);
		}

		public static Icon ImageToIcon(Image image, Color seeThru)
		{
			return ImageToIcon(image, seeThru, false);
		}

		/// <summary>
		/// Converts an Image to an Icon.
		/// </summary>
		/// <param name="image">The Image to convert to an icon. Must be an appropriate icon size (32x32, 16x16, etc).</param>
		/// <param name="seeThru">The color that will be treated as transparent in the icon.</param>
		/// <param name="disposeImage">Whether or not to dispose the passed-in Image.</param>
		/// <returns>An Icon representation of the Image.</returns>
		public static Icon ImageToIcon(Image image, Color seeThru, bool disposeImage)
        {
			Bitmap bitmap = new Bitmap(image);

			for (int y = 0; y < bitmap.Height; ++y)
			{
				for (int x = 0; x < bitmap.Width; ++x)
				{
					if (bitmap.GetPixel(x, y) == seeThru)
					{
						bitmap.SetPixel(x, y, Color.FromArgb(0));
					}
				}
			}

			Icon icon = Icon.FromHandle(bitmap.GetHicon());
			bitmap.Dispose();

			if (disposeImage)
			{
				image.Dispose();
			}

            return icon;
        }

        public static Image GetImageResource(string fileName)
        {
            StackTrace trace = new StackTrace();
            StackFrame parentFrame = trace.GetFrame(1);
            MethodBase parentMethod = parentFrame.GetMethod();
            Type parentType = parentMethod.DeclaringType;
            Assembly parentAssembly = parentType.Assembly;
            Stream stream = GetResourceStream(parentAssembly, parentType.Namespace, fileName);
            Image image = Image.FromStream(stream);
            return image;
        }

		public static Icon GetIconResource(string fileName) 
		{
			StackTrace trace = new StackTrace();
			StackFrame parentFrame = trace.GetFrame(1);
			MethodBase parentMethod = parentFrame.GetMethod();
			Type parentType = parentMethod.DeclaringType;
			Assembly parentAssembly = parentType.Assembly;
			Stream stream = GetResourceStream(parentAssembly, parentType.Namespace, fileName);
			Image image = Image.FromStream(stream);
			return Icon.FromHandle(((Bitmap)image).GetHicon());
		}
        public static Stream GetResourceStream(string fileName)
        {
            StackTrace trace = new StackTrace();
            StackFrame parentFrame = trace.GetFrame(1);
            MethodBase parentMethod = parentFrame.GetMethod();
            Type parentType = parentMethod.DeclaringType;
            Assembly parentAssembly = parentType.Assembly;
            return GetResourceStream(parentAssembly, parentType.Namespace, fileName);
        }

        public static Stream GetResourceStream(Assembly assembly, string namespaceName, string fileName)
        {
            return assembly.GetManifestResourceStream(namespaceName + "." + fileName);
        }       


        /// <summary>
        /// Found on Google Groups when searching for "Region.Union" while looking
        /// for bugs:
        /// ---
        /// Hello,
        /// 
        /// I did not run your code, but I know Region.Union is flawed in both 1.0 and
        /// 1.1, so I assume it is in the gdi+ unmanged code dll.  The best workaround,
        /// in terms of speed, is to use a PdnGraphicsPath, but it must be a path with
        /// FillMode = FillMode.Winding. You add the rectangles to the path, then you do
        /// union onto an empty region with the path. The important point is to do only
        /// one union call on a given empty region. We created a "super region" object
        /// to hide all these bugs and optimize clipping operations. In fact, it is much
        /// faster to use the path than to call Region.Union for each rectangle.
        /// 
        /// Too bad about Region.Union. A lot of people will hit this bug, as it is
        /// essential in high-performance animation.
        /// 
        /// Regards,
        /// Frank Hileman
        /// Prodige Software Corporation
        /// ---
        /// </summary>
        /// <param name="rectsF"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static PdnRegion RectanglesToRegion(RectangleF[] rectsF, int startIndex, int length)
        {
            PdnRegion region;

            if (rectsF == null || rectsF.Length == 0 || length == 0)
            {
                region = new PdnRegion();
                region.MakeEmpty();
                return region;
            }

            using (PdnGraphicsPath path = new PdnGraphicsPath())
            {
                path.FillMode = FillMode.Winding;
                if (startIndex == 0 && length == rectsF.Length)
                {
                    path.AddRectangles(rectsF);
                }
                else
                {
                    for (int i = startIndex; i < startIndex + length; ++i)
                    {
                        path.AddRectangle(rectsF[i]);
                    }
                }

                region = new PdnRegion(path);
            }

            return region;
        }

        public static PdnRegion RectanglesToRegion(RectangleF[] rectsF)
        {
            return RectanglesToRegion(rectsF, 0, rectsF != null ? rectsF.Length : 0);
        }

        public static PdnRegion RectanglesToRegion(RectangleF[] rectsF1, RectangleF[] rectsF2, params RectangleF[][] rectsFA)
        {
            using (PdnGraphicsPath path = new PdnGraphicsPath())
            {
                path.FillMode = FillMode.Winding;

                if (rectsF1 != null && rectsF1.Length > 0)
                {
                    path.AddRectangles(rectsF1);
                }

                if (rectsF2 != null && rectsF2.Length > 0)
                {
                    path.AddRectangles(rectsF2);
                }
               
                foreach (RectangleF[] rectsF in rectsFA)
                {
                    if (rectsF != null && rectsF.Length > 0)
                    {
                        path.AddRectangles(rectsF);
                    }
                }

                return new PdnRegion(path);
            }
        }

        public static PdnRegion RectanglesToRegion(Rectangle[] rects, int startIndex, int length)
        {
            PdnRegion region;

            if (length == 0)
            {
                region = new PdnRegion();
                region.MakeEmpty();
                return region;
            }

            using (PdnGraphicsPath path = new PdnGraphicsPath())
            {
                path.FillMode = FillMode.Winding;
                if (startIndex == 0 && length == rects.Length)
                {
                    path.AddRectangles(rects);
                }
                else
                {
                    for (int i = startIndex; i < startIndex + length; ++i)
                    {
                        path.AddRectangle(rects[i]);
                    }
                }

                region = new PdnRegion(path);
                path.Dispose();
            }

            return region;
        }

        public static PdnRegion RectanglesToRegion(Rectangle[] rects)
        {
            return RectanglesToRegion(rects, 0, rects.Length);
        }

        public static int RegionArea(RectangleF[] rectsF)
        {
            int area = 0;

            foreach (RectangleF rectF in rectsF)
            {
                Rectangle rect = Rectangle.Truncate(rectF);
                area += rect.Width * rect.Height;
            }

            return area;
        }

		public static RectangleF RectFromCenter(PointF center, float size) 
		{
			RectangleF ret = new RectangleF(center.X, center.Y, 0, 0);
			ret.Inflate(size, size);
			return ret;
		}

        public static Rectangle[] InflateRectangles(Rectangle[] rects, int amount)
        {
            Rectangle[] inflated = new Rectangle[rects.Length];

            for (int i = 0; i < rects.Length; ++i)
            {
                inflated[i] = Rectangle.Inflate(rects[i], amount, amount);
            }

            return inflated;
        }

        public static void InflateRectanglesInPlace(Rectangle[] rects, int amount)
        {
            for (int i = 0; i < rects.Length; ++i)
            {
                rects[i].Inflate(amount, amount);
            }
        }

        public static RectangleF[] InflateRectangles(RectangleF[] rectsF, int amount)
        {
            RectangleF[] inflated = new RectangleF[rectsF.Length];

            for (int i = 0; i < rectsF.Length; ++i)
            {
                inflated[i] = RectangleF.Inflate(rectsF[i], amount, amount);
            }

            return inflated;
        }

        public static void InflateRectanglesInPlace(RectangleF[] rectsF, float amount)
        {
            for (int i = 0; i < rectsF.Length; ++i)
            {
                rectsF[i].Inflate(amount, amount);
            }
        }

        public static Rectangle PointsToConstrainedRectangle(Point a, Point b)
        {
            Rectangle rect = Utility.PointsToRectangle(a, b);
            int minWH = Math.Min(rect.Width, rect.Height);

            rect.Width = minWH;
            rect.Height = minWH;

            if (rect.Y != a.Y)
            {
                rect.Location = new Point(rect.X, a.Y - minWH);
            }

            if (rect.X != a.X)
            {
                rect.Location = new Point(a.X - minWH, rect.Y);
            }

            return rect;
        }

        /// <summary>
        /// Takes two points and creates a bounding rectangle from them.
        /// </summary>
        /// <param name="a">One corner of the rectangle.</param>
        /// <param name="b">The other corner of the rectangle.</param>
        /// <returns>A Rectangle instance that bounds the two points.</returns>
        public static Rectangle PointsToRectangle(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            int width = Math.Abs(a.X - b.X) + 1;
            int height = Math.Abs(a.Y - b.Y) + 1;
 
            return new Rectangle(x, y, width, height);
        }

        public static RectangleF PointsToRectangle(PointF a, PointF b)
        {
            float x = Math.Min(a.X, b.X);
            float y = Math.Min(a.Y, b.Y);
            float width = Math.Abs(a.X - b.X) + 1;
            float height = Math.Abs(a.Y - b.Y) + 1;
 
            return new RectangleF(x, y, width, height);
        }

        public static RectangleF[] PointsToRectangles(PointF[] pointsF)
        {
            if (pointsF.Length == 0)
            {
                return new RectangleF[] { };
            }

            if (pointsF.Length == 1)
            {
                return new RectangleF[] { new RectangleF(pointsF[0].X, pointsF[0].Y, 1, 1) };
            }

            RectangleF[] rectsF = new RectangleF[pointsF.Length - 1];

            for (int i = 0; i < pointsF.Length - 1; ++i)
            {
                rectsF[i] = PointsToRectangle(pointsF[i], pointsF[i + 1]);
            }

            return rectsF;
        }

        public static Rectangle[] PointsToRectangles(Point[] points)
        {
            if (points.Length == 0)
            {
                return new Rectangle[] { };
            }

            if (points.Length == 1)
            {
                return new Rectangle[] { new Rectangle(points[0].X, points[0].Y, 1, 1) };
            }

            Rectangle[] rects = new Rectangle[points.Length - 1];

            for (int i = 0; i < points.Length - 1; ++i)
            {
                rects[i] = PointsToRectangle(points[i], points[i + 1]);
            }

            return rects;
        }

        /// <summary>
        /// Converts a RectangleF to RectangleF by rounding down the Location and rounding
        /// up the Size.
        /// </summary>
        public static Rectangle RoundRectangle(RectangleF rectF)
        {
            float left = (float)Math.Floor(rectF.Left);
            float top = (float)Math.Floor(rectF.Top);
            float right = (float)Math.Ceiling(rectF.Right);
            float bottom = (float)Math.Ceiling(rectF.Bottom);
            
            return Rectangle.Truncate(RectangleF.FromLTRB(left, top, right, bottom));
        }

        public static Stack Reverse(Stack reverseMe)
        {
            Stack reversed = new Stack();

            foreach (object o in reverseMe)
            {
                reversed.Push(o);
            }

            return reversed;
        }

        public static void SerializeObjectToStream(object graph, Stream stream) 
        {
            new BinaryFormatter().Serialize(stream, graph);
        }

        public static object DeserializeObjectFromStream(Stream stream)
        {
            return new BinaryFormatter().Deserialize(stream);
        }

        public static bool IsPointInRectangle(Point p, Rectangle r)
        {
            if ((p.X < r.X) || (p.Y < r.Y) || (p.X >= r.Right) || (p.Y >= r.Bottom))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Disposes an object for you. This function is here just to keep code a little
        /// cleaner so you don't have to test an object for null every time you want to
        /// dispose it.
        /// </summary>
        /// <param name="obj">A reference to the object to dispose.</param>
        /// <returns>true is the object was disposed, false if it wasn't (if obj was null)</returns>
        public static bool Dispose (IDisposable obj)
        {
            if (obj != null)
            {
                obj.Dispose();
                return true;
            }

            return false;
        }

        public static Bitmap FullCloneBitmap(Bitmap cloneMe)
        {
            Bitmap bitmap = new Bitmap(cloneMe.Width, cloneMe.Height, cloneMe.PixelFormat);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(cloneMe, 0, 0, cloneMe.Width, cloneMe.Height);
            }            

            return bitmap;
        }

        [ThreadStatic]
        private static Pen widenPen;

        public static Pen WidenPen
        {
            get
            {
                if (widenPen == null)
                {
                    widenPen = new Pen(Brushes.Black, 2.0f);
                }

                return widenPen;
            }
        }


        /// <summary>
        /// Allows you to find the bounding box for a Region object without requiring
        /// the presence of a Graphics object.
        /// (Region.GetBounds takes a Graphics instance as its only parameter.)
        /// </summary>
        /// <param name="region">The region you want to find a bounding box for.</param>
        /// <returns>A RectangleF structure that surrounds the Region.</returns>
        public static Rectangle GetRegionBounds(PdnRegion region)
        {
            Rectangle[] rects = region.GetRegionScansReadOnlyInt();
            return GetRegionBounds(rects, 0, rects.Length);
        }

        /// <summary>
        /// Allows you to find the bounding box for a "region" that is described as an
        /// array of bounding boxes.
        /// </summary>
        /// <param name="rectsF">The "region" you want to find a bounding box for.</param>
        /// <returns>A RectangleF structure that surrounds the Region.</returns>
        public static RectangleF GetRegionBounds(RectangleF[] rectsF, int startIndex, int length)
        {
            if (rectsF.Length == 0)
            {
                return RectangleF.Empty;
            }

            float left = rectsF[startIndex].Left;
            float top = rectsF[startIndex].Top;
            float right = rectsF[startIndex].Right;
            float bottom = rectsF[startIndex].Bottom;

            for (int i = startIndex + 1; i < startIndex + length; ++i)
            {
                RectangleF rectF = rectsF[i];

                if (rectF.Left < left)
                {
                    left = rectF.Left;
                }

                if (rectF.Top < top)
                {
                    top = rectF.Top;
                }

                if (rectF.Right > right)
                {
                    right = rectF.Right;
                }

                if (rectF.Bottom > bottom)
                {
                    bottom = rectF.Bottom;
                }
            }

            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        public static RectangleF GetTraceBounds(PointF[] pointsF, int startIndex, int length)
        {
            if (pointsF.Length == 0)
            {
                return RectangleF.Empty;
            }

            float left = pointsF[startIndex].X;
            float top = pointsF[startIndex].Y;
            float right = 1 + pointsF[startIndex].X;
            float bottom = 1 + pointsF[startIndex].Y;

            for (int i = startIndex + 1; i < startIndex + length; ++i)
            {
                PointF pointF = pointsF[i];

                if (pointF.X < left)
                {
                    left = pointF.X;
                }

                if (pointF.Y < top)
                {
                    top = pointF.Y;
                }

                if (pointF.X > right)
                {
                    right = pointF.X;
                }

                if (pointF.Y > bottom)
                {
                    bottom = pointF.Y;
                }
            }

            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        public static Rectangle GetTraceBounds(Point[] points, int startIndex, int length)
        {
            if (points.Length == 0)
            {
                return Rectangle.Empty;
            }

            int left = points[startIndex].X;
            int top = points[startIndex].Y;
            int right = 1 + points[startIndex].X;
            int bottom = 1 + points[startIndex].Y;

            for (int i = startIndex + 1; i < startIndex + length; ++i)
            {
                Point point = points[i];

                if (point.X < left)
                {
                    left = point.X;
                }

                if (point.Y < top)
                {
                    top = point.Y;
                }

                if (point.X > right)
                {
                    right = point.X;
                }

                if (point.Y > bottom)
                {
                    bottom = point.Y;
                }
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Allows you to find the bounding box for a "region" that is described as an
        /// array of bounding boxes.
        /// </summary>
        /// <param name="rectsF">The "region" you want to find a bounding box for.</param>
        /// <returns>A RectangleF structure that surrounds the Region.</returns>
        public static Rectangle GetRegionBounds(Rectangle[] rects, int startIndex, int length)
        {
            if (rects.Length == 0)
            {
                return Rectangle.Empty;
            }

            int left = rects[startIndex].Left;
            int top = rects[startIndex].Top;
            int right = rects[startIndex].Right;
            int bottom = rects[startIndex].Bottom;

            for (int i = startIndex + 1; i < startIndex + length; ++i)
            {
                Rectangle rect = rects[i];

                if (rect.Left < left)
                {
                    left = rect.Left;
                }

                if (rect.Top < top)
                {
                    top = rect.Top;
                }

                if (rect.Right > right)
                {
                    right = rect.Right;
                }

                if (rect.Bottom > bottom)
                {
                    bottom = rect.Bottom;
                }
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        public static RectangleF GetRegionBounds(RectangleF[] rectsF)
        {
            return GetRegionBounds(rectsF, 0, rectsF.Length);
        }

        public static Rectangle GetRegionBounds(Rectangle[] rects)
        {
            return GetRegionBounds(rects, 0, rects.Length);
        }

        private static float DistanceSquared(RectangleF[] rectsF, int indexA, int indexB)
        {
            PointF centerA = new PointF(rectsF[indexA].Left + (rectsF[indexA].Width / 2), rectsF[indexA].Top + (rectsF[indexA].Height / 2));
            PointF centerB = new PointF(rectsF[indexB].Left + (rectsF[indexB].Width / 2), rectsF[indexB].Top + (rectsF[indexB].Height / 2));
            
            return ((centerA.X - centerB.X) * (centerA.X - centerB.X)) + 
                   ((centerA.Y - centerB.Y) * (centerA.Y - centerB.Y));
        }
       
        /// <summary>
        /// Simplifies a Region into N number of bounding boxes.
        /// </summary>
        /// <param name="region">The Region to simplify.</param>
        /// <param name="complexity">The maximum number of bounding boxes to return, or 0 for however many are necessary (equivalent to using Region.GetRegionScans).</param>
        /// <returns></returns>
        public static Rectangle[] SimplifyRegion(PdnRegion region, int complexity)
        {
            Rectangle[] rects = region.GetRegionScansReadOnlyInt();
            return SimplifyRegion(rects, complexity);
        }

        public static Rectangle[] SimplifyRegion(Rectangle[] rects, int complexity)
        {
            if (complexity == 0 ||
                rects.Length < complexity)
            {
                return (Rectangle[])rects.Clone();
            }

            Rectangle[] boxes = new Rectangle[complexity];

            for (int i = 0; i < complexity; ++i)
            {
                int startIndex = (i * rects.Length) / complexity;
                int length = Math.Min(rects.Length, ((i + 1) * rects.Length) / complexity) - startIndex;
                boxes[i] = GetRegionBounds(rects, startIndex, length);
            }

            return boxes;
        }


        public static RectangleF[] SimplifyTrace(PointF[] pointsF, int complexity)
        {
            if (complexity == 0 || 
                (pointsF.Length - 1) < complexity)
            {
                return PointsToRectangles(pointsF);
            }

            RectangleF[] boxes = new RectangleF[complexity];
            int parLength = pointsF.Length - 1; // "(points as Rectangles).Length"
            
            for (int i = 0; i < complexity; ++i)
            {
                int startIndex = (i * parLength) / complexity;
                int length = Math.Min(parLength, ((i + 1) * parLength) / complexity) - startIndex;
                boxes[i] = GetTraceBounds(pointsF, startIndex, length + 1);
            }

            return boxes;
        }

        public static Rectangle[] SimplifyTrace(PdnGraphicsPath trace, int complexity)
        {
            return SimplifyRegion(TraceToRectangles(trace), complexity);
        }

        public static Rectangle[] SimplifyTrace(PdnGraphicsPath trace)
        {
            return SimplifyTrace(trace, DefaultSimplificationFactor);
        }

        public static Rectangle[] TraceToRectangles(PdnGraphicsPath trace, int complexity)
        {
            int pointCount = trace.PointCount;

            if (pointCount == 0)
            {
                return new Rectangle[0];
            }

            PointF[] pathPoints = trace.PathPoints;
            byte[] pathTypes = trace.PathTypes;
            int figureStart = 0;

            // first get count of rectangles we'll need
            Rectangle[] rects = new Rectangle[pointCount];

            for (int i = 0; i < pointCount; ++i)
            {
                byte type = pathTypes[i];

                Point a = Point.Truncate(pathPoints[i]);
                Point b;
            
                if ((type & (byte)PathPointType.CloseSubpath) != 0)
                {
                    b = Point.Truncate(pathPoints[figureStart]);
                    figureStart = i + 1;
                }
                else
                {
                    b = Point.Truncate(pathPoints[i + 1]);
                }

                rects[i] = Utility.PointsToRectangle(a, b);
            }

            return rects;
        }

        public static Rectangle[] TraceToRectangles(PdnGraphicsPath trace)
        {
            return TraceToRectangles(trace, DefaultSimplificationFactor);
        }

        public static RectangleF[] SimplifyTrace(PointF[] pointsF)
        {
            return SimplifyTrace(pointsF, defaultSimplificationFactor);
        }

        public static Rectangle[] SimplifyAndInflateRegion(Rectangle[] rects, int complexity, int inflationAmount)
        {
            Rectangle[] simplified = SimplifyRegion(rects, complexity);

            for (int i = 0; i < simplified.Length; ++i)
            {
                simplified[i].Inflate(inflationAmount, inflationAmount);
            }

            return simplified;
        }

        public static Rectangle[] SimplifyAndInflateRegion(Rectangle[] rects)
        {
            return SimplifyAndInflateRegion(rects, defaultSimplificationFactor, 1);
        }

        public static PdnRegion SimplifyAndInflateRegion(PdnRegion region, int complexity, int inflationAmount)
        {
            Rectangle[] rectRegion = SimplifyRegion(region, complexity);
            
            for (int i = 0; i < rectRegion.Length; ++i)
            {
                rectRegion[i].Inflate(inflationAmount, inflationAmount);
            }

            return RectanglesToRegion(rectRegion);
        }

        public static PdnRegion SimplifyAndInflateRegion(PdnRegion region)
        {
            return SimplifyAndInflateRegion(region, defaultSimplificationFactor, 1);
        }

        public static RectangleF[] TranslateRectangles(RectangleF[] rectsF, PointF offset)
        {
            RectangleF[] retRectsF = new RectangleF[rectsF.Length];
            int i = 0;

            foreach (RectangleF rectF in rectsF)
            {
                retRectsF[i] = new RectangleF(rectF.X + offset.X, rectF.Y + offset.Y, rectF.Width, rectF.Height);
                ++i;
            }

            return retRectsF;
        }

        public static Rectangle[] TranslateRectangles(Rectangle[] rects, int dx, int dy)
        {
            Rectangle[] retRects = new Rectangle[rects.Length];

            for (int i = 0; i < rects.Length; ++i)
            {
                retRects[i] = new Rectangle(rects[i].X + dx, rects[i].Y + dy, rects[i].Width, rects[i].Height);
            }

            return retRects;
        }

        public static Rectangle[] TruncateRectangles(RectangleF[] rectsF)
        {
            Rectangle[] rects = new Rectangle[rectsF.Length];

            for (int i = 0; i < rectsF.Length; ++i)
            {
                rects[i] = Rectangle.Truncate(rectsF[i]);
            }

            return rects;
        }

        /// <summary>
        /// The Sutherland-Hodgman clipping alrogithm.
        /// http://ezekiel.vancouver.wsu.edu/~cs442/lectures/clip/clip/index.html
        /// 
        /// # Clipping a convex polygon to a convex region (e.g., rectangle) will always produce a convex polygon (or no polygon if completely outside the clipping region).
        /// # Clipping a concave polygon to a rectangle may produce several polygons (see figure above) or, as the following algorithm does, produce a single, possibly degenerate, polygon.
        /// # Divide and conquer: Clip entire polygon against a single edge (i.e., half-plane). Repeat for each edge in the clipping region.
        ///
        /// The input is a sequence of vertices: {v0, v1, ... vn} given as an array of Points
        /// the result is a sequence of vertices, given as an array of Points. This result may have
        /// less than, equal, more than, or 0 vertices.
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static Point[] SutherlandHodgman(Rectangle bounds, Point[] v)
        {
            Point[] p1 = SutherlandHodgmanOneAxis(bounds, RectangleEdge.Left, v);
            Point[] p2 = SutherlandHodgmanOneAxis(bounds, RectangleEdge.Right, p1);
            Point[] p3 = SutherlandHodgmanOneAxis(bounds, RectangleEdge.Top, p2);
            Point[] p4 = SutherlandHodgmanOneAxis(bounds, RectangleEdge.Bottom, p3);

            return p4;
        }

        public static Point[] SutherlandHodgman(Rectangle bounds, ArrayList v)
        {
            return SutherlandHodgman(bounds, (Point[])v.ToArray(typeof(Point)));
        }

        private enum RectangleEdge
        {
            Left,
            Right,
            Top,
            Bottom
        }

        private static Point[] SutherlandHodgmanOneAxis(Rectangle bounds, RectangleEdge edge, Point[] v)
        {
            if (v.Length == 0)
            {
                return new Point[0];
            }

            ArrayList polygon = new ArrayList();
            
            Point s = v[v.Length - 1];

            for (int i = 0; i < v.Length; ++i)
            {
                Point p = v[i];
                bool pIn = IsInside(bounds, edge, p);
                bool sIn = IsInside(bounds, edge, s);

                if (sIn && pIn)
                {   // case 1: inside -> inside
                    polygon.Add(p);
                }
                else if (sIn && !pIn)
                {   // case 2: inside -> outside
                    polygon.Add(LineIntercept(bounds, edge, s, p));
                }
                else if (!sIn && !pIn)
                {   // case 3: outside -> outside
                    // emit nothing
                }
                else if (!sIn && pIn)
                {   // case 4: outside -> inside
                    polygon.Add(LineIntercept(bounds, edge, s, p));
                    polygon.Add(p);
                }

                s = p;
            }

            return (Point[])polygon.ToArray(typeof(Point));
        }

        private static bool IsInside(Rectangle bounds, RectangleEdge edge, Point p)
        {
            switch (edge)
            {
                case RectangleEdge.Left:
                    if (p.X < bounds.Left)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                        
                case RectangleEdge.Right:
                    if (p.X >= bounds.Right)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }

                case RectangleEdge.Top:
                    if (p.Y < bounds.Top)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }

                case RectangleEdge.Bottom:
                    if (p.Y >= bounds.Bottom)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }

                default:
                    throw new InvalidEnumArgumentException("edge");
            }
        }

        private static Point LineIntercept(Rectangle bounds, RectangleEdge edge, Point a, Point b)
        {
            if (a == b)
            {
                return a;
            }

            switch (edge)
            {
                case RectangleEdge.Bottom:
                    if (b.Y == a.Y)
                    {
                        throw new ArgumentException("no intercept found");
                    }

                    return new Point(a.X + (((b.X - a.X) * (bounds.Bottom - a.Y)) / (b.Y - a.Y)), bounds.Bottom);

                case RectangleEdge.Left:
                    if (b.X == a.X)
                    {
                        throw new ArgumentException("no intercept found");
                    }

                    return new Point(bounds.Left, a.Y + (((b.Y - a.Y) * (bounds.Left - a.X)) / (b.X - a.X)));

                case RectangleEdge.Right:
                    if (b.X == a.X)
                    {
                        throw new ArgumentException("no intercept found");
                    }

                    return new Point(bounds.Right, a.Y + (((b.Y - a.Y) * (bounds.Right - a.X)) / (b.X - a.X)));

                case RectangleEdge.Top:
                    if (b.Y == a.Y)
                    {
                        throw new ArgumentException("no intercept found");
                    }

                    return new Point(a.X + (((b.X - a.X) * (bounds.Top - a.Y)) / (b.Y - a.Y)), bounds.Top);                                    
            }

            throw new ArgumentException("no intercept found");
        }

        public static Point[] GetLinePoints(Point first, Point second)
        {
            Point[] coords = null;

            int x1 = first.X;
            int y1 = first.Y;
            int x2 = second.X;
            int y2 = second.Y;
            int dx = x2 - x1;
            int dy = y2 - y1;
            int dxabs = Math.Abs(dx);
            int dyabs = Math.Abs(dy);
            int px = x1;
            int py = y1;
            int sdx = Math.Sign(dx);
            int sdy = Math.Sign(dy);
            int x = 0;
            int y = 0;

            if (dxabs > dyabs)
            {
                coords = new Point[dxabs + 1];

                for (int i = 0; i <= dxabs; i++)
                {
                    y += dyabs;

                    if (y >= dxabs)
                    {
                        y -= dxabs;
                        py += sdy;
                    }

                    coords[i] = new Point(px, py);
                    px += sdx;
                }
            }
            else 
            // had to add in this cludge for slopes of 1 ... wasn't drawing half the line
            if (dxabs == dyabs)
            {
                coords = new Point[dxabs + 1];

                for (int i = 0; i <= dxabs; i++)
                {
                    coords[i] = new Point(px, py);
                    px += sdx;
                    py += sdy;
                }
            }
            else
            {
                coords = new Point[dyabs + 1];

                for (int i = 0; i <= dyabs; i++)
                {
                    x += dxabs;

                    if (x >= dyabs)
                    {
                        x -= dyabs;
                        px += sdx;
                    }

                    coords[i] = new Point(px, py);
                    py += sdy;
                }
            }

            return coords;
        }

        public static long GetTimeMs()
        {
            return Utility.TicksToMs(DateTime.Now.Ticks);        
        }

		/// <summary>
		/// Returns the Distance between two points
		/// </summary>
		public static float Distance(PointF a, PointF b)
		{
			return Magnitude(new PointF(a.X - b.X, a.Y - b.Y));
		}

		/// <summary>
		/// Returns the Magnitude (distance to origin) of a point
		/// </summary>
		public static float Magnitude(PointF p)
		{
			return (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);
		}

		public static double Clamp(double x, double min, double max) 
		{
			if (x < min)
				return min;
			else if (x > max)
				return max;
			else
				return x;
		}

		public static float Clamp(float x, float min, float max) 
		{
			if (x < min)
				return min;
			else if (x > max)
				return max;
			else
				return x;
		}

        public static int Clamp(int x, int min, int max)
        {
            if (x < min)
            {
                return min;
            }
            else if (x > max)
            {
                return max;
            }
            else
            {
                return x;
            }
		}
        
		public static byte ClampToByte(double x) 
		{
			if (x > 255)
				return 255;
			else if (x < 0)
				return 0;
			else
				return (byte)x;
		}
        
		public static byte ClampToByte(float x) 
		{
			if (x > 255)
				return 255;
			else if (x < 0)
				return 0;
			else
				return (byte)x;
		}
        
		public static byte ClampToByte(int x) 
		{
			if (x > 255)
				return 255;
			else if (x < 0)
				return 0;
			else
				return (byte)x;
		}

		public static float Lerp(float from, float to, float frac) 
		{
			return (from * (1 - frac) + to * frac);
		}

		public static double Lerp(double from, double to, double frac) 
		{
			return (from * (1 - frac) + to * frac);
		}

		public static int ColorDifference(ColorBgra a, ColorBgra b) 
		{
			return (int)Math.Ceiling(Math.Sqrt(ColorDifferenceSquared(a, b)));
		}

		public static int ColorDifferenceSquared(ColorBgra a, ColorBgra b) 
		{
			int diffSq = 0, tmp;

			tmp = a.R - b.R;
			diffSq += tmp * tmp;
			tmp = a.G - b.G;
			diffSq += tmp * tmp;
			tmp = a.B - b.B;
			diffSq += tmp * tmp;

			return diffSq / 3;
		}

		public static Rectangle [] MakeRectangleOutline(Rectangle roi) 
		{
			Rectangle [] ret = new Rectangle[4];

			//Left
			ret[0] = new Rectangle(roi.Left - 2, roi.Top - 2, 5, roi.Height + 5);
			//Right
			ret[1] = new Rectangle(roi.Right - 2, roi.Top - 2, 5, roi.Height + 5);
			//Top
			ret[2] = new Rectangle(roi.Left - 2, roi.Top - 2, roi.Width + 5, 5);
			//Bottom
			ret[2] = new Rectangle(roi.Left - 2, roi.Bottom - 2, roi.Width + 5, 5);

			return ret;
		}
    }
}
