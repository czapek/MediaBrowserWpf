using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security;

namespace PaintDotNet
{
    /// <summary>
    /// Manages an arbitrarily sized block of memory. You can also create child MemoryBlocks
    /// which reference a portion of the memory allocated by a parent MemoryBlock. If the parent
    /// is disposed, the children will not be valid.
    /// </summary>
    [Serializable]
    public unsafe class MemoryBlock
        : IDisposable,
          ISerializable
    {
        private byte[] array;
        private int cachedLength;
        private IntPtr cachedPointer;
        private void *cachedVoidStar;
        private GCHandle gcHandle;
        private MemoryBlock parentBlock;

        public int Length
        {
            get
            {
                return cachedLength;
            }
        }

        public unsafe IntPtr Pointer
        {
            get
            {
                return cachedPointer;
            }
        }

      //  [CLSCompliant(false)]
        public unsafe void *VoidStar
        {
            get
            {
                return cachedVoidStar;
            }
        }

        public unsafe byte this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                {
                    throw new ArgumentOutOfRangeException("index must be positive and less than Length");
                }

                return ((byte *)cachedVoidStar)[index];
            }

            set
            {
                if (index < 0 || index >= Length)
                {
                    throw new ArgumentOutOfRangeException("index must be positive and less than Length");
                }

                ((byte *)cachedVoidStar)[index] = value;
            }
        }

        /// <summary>
        /// Copies bytes from one area of memory to another. Since this function only
        /// takes pointers, it can not do any bounds checking.
        /// </summary>
        /// <param name="dst">The starting address of where to copy bytes to.</param>
        /// <param name="src">The starting address of where to copy bytes from.</param>
        /// <param name="length">The number of bytes to copy</param>
       // [CLSCompliant(false)]
        public static void CopyMemory(void *dst, void *src, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length must be greater than or equal to zero");
            }

            // Note: This accomplishes essentially the same thing as memcpy() from C.
            //       The loop is unrolled to 16 bytes
            int outerCount = length / 16;
            int innerCount = length & 15;

            int *uintDst = (int *)dst;
            int *uintSrc = (int *)src;

            while (outerCount > 0)
            {
                *uintDst = *uintSrc;
                *(uintDst + 1) = *(uintSrc + 1);
                *(uintDst + 2) = *(uintSrc + 2);
                *(uintDst + 3) = *(uintSrc + 3);

                uintDst += 4;
                uintSrc += 4;
                
                --outerCount;
            }

            byte *byteDst = (byte *)uintDst;
            byte *byteSrc = (byte *)uintSrc;

            while (innerCount > 0)
            {
                *byteDst = *byteSrc;
                ++byteDst;
                ++byteSrc;
                --innerCount;
            }

            return;
        }

        /// <summary>
        /// Copies bytes from one area of memory to another. Since this function works
        /// with MemoryBlock instances, it does bounds checking.
        /// </summary>
        /// <param name="dst">The MemoryBlock to copy bytes to.</param>
        /// <param name="dstOffset">The offset within dst to copy bytes to.</param>
        /// <param name="src">The MemoryBlock to copy bytes from.</param>
        /// <param name="srcOffset">The offset within src to copy bytes from.</param>
        /// <param name="length">The number of bytes to copy.</param>
        public static void CopyBlock(MemoryBlock dst, int dstOffset, MemoryBlock src, int srcOffset, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length must be greater than or equal to 0");
            }

            if ((dstOffset + length > dst.Length) || (srcOffset + length > src.Length))
            {
                throw new ArgumentOutOfRangeException("copy ranges were out of bounds");
            }

            void *dstPtr = ((byte *)dst.VoidStar) + dstOffset;
            void *srcPtr = ((byte *)src.VoidStar) + srcOffset;
            CopyMemory (dstPtr, srcPtr, length);
        }

        /// <summary>
        /// Creates a new MemoryBlock instance that refers to part of another MemoryBlock.
        /// The other MemoryBlock is the parent, and this new instance is the child.
        /// </summary>
        public unsafe MemoryBlock(MemoryBlock parentBlock, int offset, int length)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", offset, "Offset must be greater than or equal to zero");
            }

            if (offset >= parentBlock.Length)
            {
                throw new ArgumentOutOfRangeException("offset", offset, "Offset must be less than the parent block's length");
            }

            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length must be greater than zero");
            }

            if (offset + length > parentBlock.Length)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length must be less than or equal to parentBlock.Length - offset");
            }

            this.array = null;
            this.cachedLength = length;
            this.cachedVoidStar = (byte *)parentBlock.VoidStar + offset;
            this.cachedPointer = new IntPtr(cachedVoidStar);
            this.parentBlock = parentBlock;
        }

        /// <summary>
        /// Creates a new MemoryBlock instance and allocates the requested number of bytes.
        /// </summary>
        /// <param name="bytes"></param>
        public unsafe MemoryBlock(int bytes)
        {
            if (bytes <= 0)
            {
                throw new ArgumentOutOfRangeException("bytes", bytes, "Bytes must be greater than zero");
            }

            this.array = new byte[bytes];
            this.gcHandle = GCHandle.Alloc(array, System.Runtime.InteropServices.GCHandleType.Pinned);
            
            fixed(void *pointer = this.array)
            {
                this.cachedVoidStar = pointer;
                this.cachedPointer = new IntPtr(cachedVoidStar);
            }

            this.cachedLength = array.Length;
        }


        ~MemoryBlock()
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
                disposed = true;

                if (disposing)
                {
                }

                if (array != null)
                {
                    cachedPointer = IntPtr.Zero;
                    cachedVoidStar = null;
                    gcHandle.Free();
                    array = null;
                }
            }
        }

        public byte[] ToByteArray()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("MemoryBlock");
            }

            return ToByteArray(0, this.Length);
        }

        public byte[] ToByteArray(int startOffset, int length)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("MemoryBlock");
            }

            if (startOffset + length > this.Length)
            {
                throw new ArgumentOutOfRangeException("startOffset + length must be less than Length");
            }

            byte[] array = new byte[length];
            Marshal.Copy(new IntPtr((byte *)this.cachedVoidStar + startOffset), array, 0, length);
            return array;
        }

        #region ISerializable Members
        protected MemoryBlock(SerializationInfo info, StreamingContext context)
        {
            this.disposed = false;
            this.cachedLength = info.GetInt32("length");
            bool hasParent = info.GetBoolean("hasParent");

            if (hasParent)
            {
                this.parentBlock = (MemoryBlock)info.GetValue("parentBlock", typeof(MemoryBlock));
                this.cachedPointer = new IntPtr((byte *)parentBlock.cachedVoidStar + info.GetInt32("parentOffset"));
                this.cachedVoidStar = cachedPointer.ToPointer();
                this.array = null;
            }
            else
            {
                this.parentBlock = null;
                this.array = (byte[])info.GetValue("pointerData", typeof(byte[]));
                this.gcHandle = GCHandle.Alloc(this.array, GCHandleType.Pinned);

                fixed (void *ptr = this.array)
                {
                    this.cachedVoidStar = ptr;
                    this.cachedPointer = new IntPtr(ptr);
                }
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("MemoryBlock");
            }

            info.AddValue("length", cachedLength);
            info.AddValue("hasParent", array == null);

            if (array != null)
            {
                info.AddValue("pointerData", array, typeof(byte[]));
            }
            else
            {
                info.AddValue("parentBlock", parentBlock, typeof(MemoryBlock));
                info.AddValue("parentOffset", ((byte *)cachedVoidStar - (byte *)parentBlock.cachedVoidStar));
            }
        }

        #endregion
    }
}
