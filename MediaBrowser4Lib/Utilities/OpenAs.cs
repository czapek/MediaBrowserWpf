using System;
using System.Text;
using System.Runtime.InteropServices;


namespace MediaBrowser4.Utilities
{

    [Serializable]
    public struct ShellExecuteInfo
    {
        public int Size;
        public uint Mask;
        public IntPtr hwnd;
        public string Verb;
        public string File;
        public string Parameters;
        public string Directory;
        public uint Show;
        public IntPtr InstApp;
        public IntPtr IDList;
        public string Class;
        public IntPtr hkeyClass;
        public uint HotKey;
        public IntPtr Icon;
        public IntPtr Monitor;
    }

    public class OpenAs
    {    
        // Code For OpenWithDialog Box
        [DllImport("shell32.dll", SetLastError = true)]
        extern public static bool
               ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

        [DllImport("shell32.dll", EntryPoint = "FindExecutable")]
        public static extern long FindExecutableA(
          string lpFile, string lpDirectory, StringBuilder lpResult);

        public const uint SW_NORMAL = 1;

        public static void Open(string file)
        {
            ShellExecuteInfo sei = new ShellExecuteInfo();
            sei.Size = Marshal.SizeOf(sei);
            sei.Verb = "openas";
            sei.File = file;
            sei.Show = SW_NORMAL;
            if (!ShellExecuteEx(ref sei))
                throw new System.ComponentModel.Win32Exception();
        }

        public static string FindExe(string Path)
        {
            StringBuilder objResult = new StringBuilder(1024);
            long lngResult = 0;

            lngResult = FindExecutableA(Path, "", objResult);

            if (lngResult >= 32)
            {
                return objResult.ToString();
            }

            return "";
        }

    }
}
