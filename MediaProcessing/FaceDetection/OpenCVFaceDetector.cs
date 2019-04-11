using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaProcessing.FaceDetection;


namespace OpenCVFaceDetector
{
    class DllPaths
    {
        public const string HaarcascadeString = @"Resources\haarcascade_frontalface_alt2.xml";//"haarcascade_frontalface_default.xml"
        public const string KERNEL32_DLL_PATH = "kernel32.dll";
        public const string OPENCV_CORE_DLL_PATH = @"opencv_core249.dll";
        public const string OPENCV_OBJDETECT_DLL_PATH = @"opencv_objdetect249.dll";
    }

    public class SimpleFaceDetector
    {
        private HaarClassifier _haarClassifier;

        public SimpleFaceDetector()
        {
            _haarClassifier = new HaarClassifier(DllPaths.HaarcascadeString);
        }

        public Faces DetectFace(string imagePath)
        {
            Faces faces = new Faces();
            faces.Facelist = new List<System.Drawing.Rectangle>();

            var image = CreateIntelImageFromFile(imagePath);
            var facesHaar = _haarClassifier.DetectObjects(image);

            foreach (var faceRect in facesHaar)
            {
                faces.Facelist.Add(new System.Drawing.Rectangle(faceRect.x, faceRect.y, faceRect.width, faceRect.height));
            }

            return faces;
        }

        static BitmapSource CreateBitmapSourceFromFile(string filePath, PixelFormat targetPixelFormat)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return new FormatConvertedBitmap(decoder.Frames[0], targetPixelFormat, null, 0);
            }
        }

        static IntelImage CreateIntelImageFromBitmapSource(BitmapSource bitmapSource)
        {
            var intelImage = new IntelImage(bitmapSource.PixelWidth, bitmapSource.PixelHeight, bitmapSource.Format != PixelFormats.Gray8);
            byte[] pixelBuffer = new byte[bitmapSource.PixelHeight * intelImage.Stride];
            bitmapSource.CopyPixels(pixelBuffer, intelImage.Stride, 0);
            intelImage.CopyPixels(pixelBuffer);
            return intelImage;
        }

        static IntelImage CreateIntelImageFromFile(string filePath, bool convertToGrayscale = false)
        {
            var bitmapSource = CreateBitmapSourceFromFile(filePath, convertToGrayscale ? PixelFormats.Gray8 : PixelFormats.Bgr24);
            return CreateIntelImageFromBitmapSource(bitmapSource);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct IplImage
    {
        public int nSize;
        public int ID;
        public int nChannels;
        public int alphaChannel;
        public int depth;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string colorModel;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string channelSeq;
        public int dataOrder;
        public int origin;
        public int align;
        public int width;
        public int height;
        public System.IntPtr roi;
        public System.IntPtr maskROI;
        public System.IntPtr imageId;
        public System.IntPtr tileInfo;
        public int imageSize;
        public System.IntPtr imageData;
        public int widthStep;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
        public int[] BorderMode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
        public int[] BorderConst;
        public System.IntPtr imageDataOrigin;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CvSize
    {
        public int width;
        public int height;

        public CvSize(int width, int height) { this.width = width; this.height = height; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CvRect
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    public class NativeMethods
    {
        [DllImport(DllPaths.KERNEL32_DLL_PATH, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport(DllPaths.KERNEL32_DLL_PATH, SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvCreateImage", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvCreateImage(CvSize size, int depth, int channels);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvReleaseImage", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cvReleaseImage(ref IntPtr image);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvLoad", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvLoad([In][MarshalAs(UnmanagedType.LPStr)]string filename, IntPtr storage, IntPtr name,
        IntPtr real_name);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvCreateMemStorage", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvCreateMemStorage(int block_size = 0);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvClearMemStorage", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvClearMemStorage(IntPtr memory_storage);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvReleaseMemStorage", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cvReleaseMemStorage(ref IntPtr storage);

        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvGetSeqElem", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvGetSeqElem(IntPtr sequence, int index);
        [DllImport(DllPaths.OPENCV_CORE_DLL_PATH, EntryPoint = "cvSeqPopFront", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvSeqPopFront(IntPtr sequence, IntPtr dest);

        [DllImport(DllPaths.OPENCV_OBJDETECT_DLL_PATH, EntryPoint = "cvReleaseHaarClassifierCascade", CallingConvention =
        CallingConvention.Cdecl)]
        public static extern void cvReleaseHaarClassifierCascade(ref IntPtr cascade);

        [DllImport(DllPaths.OPENCV_OBJDETECT_DLL_PATH, EntryPoint = "cvHaarDetectObjects", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cvHaarDetectObjects(IntPtr image, IntPtr cascade, IntPtr mem_storage,
        double scale_factor, int min_neighbours, int flags,
        CvSize min_size, CvSize max_size);
    }

    class IntelImage : IDisposable
    {
        IntPtr iplImagePointer;
        IplImage iplImageStruct;
        public IntelImage(int width, int height, bool color = true)
        {
            iplImagePointer = NativeMethods.cvCreateImage(new CvSize() { width = width, height = height }, 8, color ? 3 : 1);
            iplImageStruct = (IplImage)Marshal.PtrToStructure(iplImagePointer, typeof(IplImage));
        }

        public void CopyPixels(byte[] sourcePixelBuffer, int startIndex = 0)
        {
            Marshal.Copy(sourcePixelBuffer, startIndex, iplImageStruct.imageData, sourcePixelBuffer.Length);
        }

        public int Stride { get { return iplImageStruct.widthStep; } }

        public IntPtr IplImage()
        {
            return iplImagePointer;
        }

        public void Dispose()
        {
            NativeMethods.cvReleaseImage(ref iplImagePointer);
            GC.SuppressFinalize(this);
        }

        ~IntelImage()
        {
            Dispose();
        }
    }

    class HaarClassifier : IDisposable
    {
        IntPtr dllHandle, haarCascade, memoryStorage;
        void LoadObjDetectDll()
        {
            dllHandle = NativeMethods.LoadLibrary(DllPaths.OPENCV_OBJDETECT_DLL_PATH);
            if (dllHandle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        public HaarClassifier(string cascadeFilePath)
        {
            LoadObjDetectDll(); // this is a workaround, leave it be
            haarCascade = NativeMethods.cvLoad(cascadeFilePath, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            memoryStorage = NativeMethods.cvCreateMemStorage();
        }

        [Flags]
        public enum DetectionFlags : int
        {
            None = 0,
            DoCannyPruning = 1,
            ScaleImage = 2,
            FindBiggestObject = 4,
            DoRoughSearch = 8
        }

        public LinkedList<CvRect> DetectObjects(IntelImage sourceImage, double scaleFactor = 1.1, int minNeighbours = 3,
        DetectionFlags flags = DetectionFlags.None, CvSize minSize = default(CvSize), CvSize maxSize = default(CvSize))
        {
            NativeMethods.cvClearMemStorage(memoryStorage);
            LinkedList<CvRect> result = new LinkedList<CvRect>();
            IntPtr faceSequence = NativeMethods.cvHaarDetectObjects(sourceImage.IplImage(), haarCascade, memoryStorage,
            scaleFactor, minNeighbours, (int)flags, minSize, maxSize);
            for (; ; )
            {
                IntPtr faceRectPointer = NativeMethods.cvGetSeqElem(faceSequence, 0);
                if (faceRectPointer == IntPtr.Zero) break;
                NativeMethods.cvSeqPopFront(faceSequence, IntPtr.Zero); // TODO: merge with cvGetSeqElem
                result.AddFirst((CvRect)Marshal.PtrToStructure(faceRectPointer, typeof(CvRect)));
            }
            return result;
        }

        public void Dispose()
        {
            NativeMethods.cvReleaseMemStorage(ref memoryStorage);
            NativeMethods.cvReleaseHaarClassifierCascade(ref haarCascade);
            NativeMethods.FreeLibrary(dllHandle);
            GC.SuppressFinalize(this);
        }

        ~HaarClassifier()
        {
            Dispose();
        }
    }
}