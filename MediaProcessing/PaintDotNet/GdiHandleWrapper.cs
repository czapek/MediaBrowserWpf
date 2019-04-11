using System;
using System.Runtime.InteropServices;
using System.Security;

namespace PaintDotNet
{
    /// <summary>
    /// Wraps a GDI handle and calls DeleteObject upon finalization or disposal.
    /// </summary>
    public sealed class GdiHandleWrapper :
        IDisposable
    {
        [SuppressUnmanagedCodeSecurity]
        private sealed class SafeNativeMethods
        {
            private SafeNativeMethods()
            {
            }

            [SuppressUnmanagedCodeSecurity]
            [DllImport("Gdi32.dll")]
            internal static extern uint DeleteObject(
                IntPtr hObject   // handle to graphic object
                );
        }

        private IntPtr hGdiObj;

        public IntPtr HGdiObj
        {
            get
            {
                return hGdiObj;
            }
        }

        public GdiHandleWrapper(IntPtr hGdiObj)
        {
            this.hGdiObj = hGdiObj;
        }

        ~GdiHandleWrapper()
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
                if (hGdiObj != IntPtr.Zero)
                {
                    SafeNativeMethods.DeleteObject(hGdiObj);
                    hGdiObj = IntPtr.Zero;
                }

                disposed = true;
            }
        }
    }
}
