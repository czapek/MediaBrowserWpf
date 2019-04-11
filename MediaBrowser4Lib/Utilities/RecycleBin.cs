using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4.Utilities
{
    /// <summary>
    /// Summary description for DeleteFileRecBin.
    /// </summary>    
    public class RecycleBin
    {
        /// <summary>
        /// Type of an operation above file
        /// </summary>
        private enum FILEOP_ENUM : uint
        {
            FO_MOVE = 0x0001,
            FO_COPY = 0x0002,
            FO_DELETE = 0x0003,
            FO_RENAME = 0x0004
        }

        /// <summary>
        /// Flags controlling SHFileOperation
        /// </summary>
        /// <remarks>
        /// <LinkToMSDN>
        /// </remarks>
        private enum FILEOP_FLAGS_ENUM : ushort
        {
            FOF_MULTIDESTFILES = 0x0001,
            FOF_CONFIRMMOUSE = 0x0002,
            FOF_SILENT = 0x0004,
            FOF_RENAMEONCOLLISION = 0x0008,
            FOF_NOCONFIRMATION = 0x0010,
            FOF_WANTMAPPINGHANDLE = 0x0020,
            FOF_ALLOWUNDO = 0x0040,
            FOF_FILESONLY = 0x0080,
            FOF_SIMPLEPROGRESS = 0x0100,
            FOF_NOCONFIRMMKDIR = 0x0200,
            FOF_NOERRORUI = 0x0400,
            FOF_NOCOPYSECURITYATTRIBS = 0x0800,
            FOF_NORECURSION = 0x1000,
            FOF_NO_CONNECTED_ELEMENTS = 0x2000,
            FOF_WANTNUKEWARNING = 0x4000,
            FOF_NORECURSEREPARSE = 0x8000
        }

        /// <summary>
        /// SHFILEOPSTRUCT is required for SHFileOperation function
        /// </summary>
        /// <remarks>
        ///  <LinkToMSDN>
        /// </remarks>
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public FILEOP_ENUM wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public FILEOP_FLAGS_ENUM fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        private SHFILEOPSTRUCT _fileOPStruct;

        public static void Recycle(string filename, bool ask)
        {
            int ret = 1;

            RecycleBin delFile = new RecycleBin();
            delFile._fileOPStruct = new SHFILEOPSTRUCT();

            delFile._fileOPStruct.wFunc = FILEOP_ENUM.FO_DELETE;
            // full path must be specified (an extra NULL must be appended)
            delFile._fileOPStruct.pFrom = filename + "\0"; ;
            // must be set to null
            delFile._fileOPStruct.pTo = null;
            // FOF_ALLOWUNDO must be set to move file to recycle bin
            delFile._fileOPStruct.fFlags = FILEOP_FLAGS_ENUM.FOF_ALLOWUNDO;
            // use this, if you don't want the confirmation dialog to appear
            if (!ask) delFile._fileOPStruct.fFlags =  FILEOP_FLAGS_ENUM.FOF_ALLOWUNDO | FILEOP_FLAGS_ENUM.FOF_NOCONFIRMATION;

            ret = SHFileOperation(ref delFile._fileOPStruct);         

            if (ret != 0)
            {
                object handle = FileLock.UnsafeGetHandkesLockedBy(Process.GetCurrentProcess(), filename);

                if (handle == null)
                {
                    Process []  x = Process.GetProcessesByName("WpfApplication1.vshost.exe");

                    if(x.Length > 0)
                        handle = FileLock.UnsafeGetHandkesLockedBy(x[0], filename);
                }

                if (handle != null)
                {
                    FileLock.Win32API.SYSTEM_HANDLE_INFORMATION lockHandle = (FileLock.Win32API.SYSTEM_HANDLE_INFORMATION)handle;

                    System.Diagnostics.ProcessStartInfo procStIfo =
                           new System.Diagnostics.ProcessStartInfo("handle.exe", System.String.Format("-c {0:x} -p {1} -y",
                        lockHandle.Handle, lockHandle.ProcessID));

                    procStIfo.RedirectStandardOutput = true;
                    procStIfo.UseShellExecute = false;
                    procStIfo.CreateNoWindow = true;
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.StartInfo.Verb = "runas";
                    proc.StartInfo = procStIfo;
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();            

                    throw new Exception(System.String.Format("Datei gesperrt von PID: {1}, Handle: {0:x} ({2})",
                        lockHandle.Handle, lockHandle.ProcessID, filename) + "\r\n\r\n" + output);
                }
                else
                {
                    throw new Exception("Datei kann aus unbekannten Gründen nicht gelöscht werden.");
                }
            }
        }      
    }
}
