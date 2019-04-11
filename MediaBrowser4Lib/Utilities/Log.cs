using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;

namespace MediaBrowser4.Utilities
{
    public static class Log
    {
        private static List<Exception> errorList = new List<Exception>();
        private static string LogFolder = Path.Combine(Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "MediaBrowserWpf\\logfiles");

        public static Exception[] ErrorList
        {
            get
            {
                return errorList.ToArray();
            }
        }

        public static void ErrorListClear()
        {
            errorList.Clear();
        }

        public static void Info(string info)
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }

            File.AppendAllText(LogFolder + "\\info_" + DateTime.Now.ToString("yyyy_MM_dd") + ".log", "\r\n" + DateTime.Now.ToString("yy/MM/dd HH:mm:ss") + " "
                + info, System.Text.Encoding.Default);
        }

        public static void Exception(Exception ex)
        {
            Exception(ex, null);
        }

        public static void Exception(Exception ex, string comment)
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }

            errorList.Add(ex);
            File.AppendAllText(LogFolder + "\\error_" + DateTime.Now.ToString("yyyy_MM_dd") + ".log", "\r\n" + DateTime.Now.ToString("yy/MM/dd HH:mm:ss") + " "
                + ex.Message + (comment != null ? " [" + comment + "]" : ""), System.Text.Encoding.Default);
            File.AppendAllText(LogFolder + "\\errorStackTrace_" + DateTime.Now.ToString("yyyy_MM_dd") + ".log", "\r\n\r\n" + DateTime.Now.ToString("yy/MM/dd HH:mm:ss") + "\r\n" + ex.ToString(), System.Text.Encoding.Default);
        }
    }
}
