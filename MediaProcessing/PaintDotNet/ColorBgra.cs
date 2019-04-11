using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PaintDotNet
{
    /// <summary>
    /// This is our pixel format that we will work with. It is always 32-bits / 4-bytes and is
    /// always laid out in BGRA order.
    /// Generally used with the Surface class.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ColorBgra
    {
        [FieldOffset(0)] public byte B;
        [FieldOffset(1)] public byte G;
        [FieldOffset(2)] public byte R;
        [FieldOffset(3)] public byte A;

		/// <summary>
		/// Lets you change b, g, r, and a at the same time.
		/// </summary>
		[FieldOffset(0)] 
      //  [CLSCompliant(false)]
        public uint Bgra;

        public const int BlueChannel = 0;
        public const int GreenChannel = 1;
        public const int RedChannel = 2;
        public const int AlphaChannel = 3;

        public const int SizeOf = 4;

        public unsafe byte this[int channel]
        {
            get
            {
#if DEBUG
                if (channel < 0 || channel > 3)
                {
                    throw new ArgumentOutOfRangeException("channel", "valid range is [0,3]");
                }
#endif

                fixed (byte *p = &B)
                {
                    return p[channel];
                }
            }

            set
            {
#if DEBUG
                if (channel < 0 || channel > 3)
                {
                    throw new ArgumentOutOfRangeException("channel", "valid range is [0,3]");
                }
#endif
                fixed (byte *p = &B)
                {
                    p[channel] = value;
                }
            }
        }

        /// <summary>
        /// Gets the luminance intensity of the pixel based on the values of the red, green, and blue components. Alpha is ignored.
        /// </summary>
        /// <returns>A value in the range 0 to 1 inclusive.</returns>
        public double GetIntensity()
        {
            return ((0.114 * (double)B) + (0.587 * (double)G) + (0.299 * (double)R)) / 255.0;
        }

        /// <summary>
        /// Gets the luminance intensity of the pixel based on the values of the red, green, and blue components. Alpha is ignored.
        /// </summary>
        /// <returns>A value in the range 0 to 255 inclusive.</returns>
        public byte GetIntensityByte()
        {
            return (byte)((0.114 * (double)B) + (0.587 * (double)G) + (0.299 * (double)R));
        }

        public static bool operator == (ColorBgra lhs, ColorBgra rhs)
		{
			return lhs.Bgra == rhs.Bgra;
		}

		public static bool operator != (ColorBgra lhs, ColorBgra rhs)
		{
			return lhs.Bgra != rhs.Bgra;
		}

		public override bool Equals(object obj)
		{
			return (ColorBgra)obj == this; 
		}

    	public override int GetHashCode()
		{
            unchecked
            {
                return (int)Bgra;
            }
		}

        public static PixelFormat PixelFormat
        {
            get
            {
                return PixelFormat.Format32bppArgb;
            }
        }

        public ColorBgra NewAlpha(byte newA)
        {
            return ColorBgra.FromBgra(B, G, R, newA);
        }

        public static ColorBgra FromRgba(byte r, byte g, byte b, byte a)
        {
            ColorBgra color = new ColorBgra();

            color.R = r;
            color.G = g;
            color.B = b;
            color.A = a;

            return color;
        }

        public static ColorBgra FromRgb(byte r, byte g, byte b)
        {
            return FromRgba(r, g, b, 255);
        }

        public static ColorBgra FromBgra(byte b, byte g, byte r, byte a)
        {
            return FromRgba(r, g, b, a);
        }

        public static ColorBgra FromBgr(byte b, byte g, byte r)
        {
            return FromRgb(r, g, b);
        }

     //   [CLSCompliant(false)]
        public static ColorBgra FromUInt32(UInt32 bgra)
        {
            ColorBgra color = new ColorBgra();

            color.Bgra = bgra;
            return color;
        }

        public static ColorBgra FromColor(Color c)
        {
            return FromRgba(c.R, c.G, c.B, c.A);
        }

        public Color ToColor()
        {
            return Color.FromArgb(A, R, G, B);
        }

		public static ColorBgra Lerp(ColorBgra from, ColorBgra to, float frac) 
		{
			ColorBgra ret = new ColorBgra();
			for (int i = 0; i < 4; i++) 
			{
				ret[i] = (byte)Utility.ClampToByte(Utility.Lerp(from[i], to[i], frac));
			}
			return ret;
		}

		public static ColorBgra Lerp(ColorBgra from, ColorBgra to, double frac) 
		{
			ColorBgra ret = new ColorBgra();
			for (int i = 0; i < 4; i++) 
			{
				ret[i] = (byte)Utility.ClampToByte(Utility.Lerp(from[i], to[i], frac));
			}
			return ret;
		}

		public override string ToString()
		{
			return "B: " + B + ", G: " + G + ", R: " + R + ", A: " + A;
		}

    }
}
