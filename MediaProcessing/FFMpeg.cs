using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace MediaProcessing
{
    public class FFMpeg
    {
        public bool ApplicationExists
        {
            get;
            private set;
        }

        public string ApplicationPath
        {
            get;
            private set;
        }

        public FFMpeg()
        {
            this.ApplicationPath = FindFFmpeg();
            this.ApplicationExists = (this.ApplicationPath != null);
        }

        public Bitmap GetFrameFromVideo(string source, string target, double seek)
        {
            Start(source, target, "-f mjpeg -ss " + (int)seek + " -vframes 1", 10);
            return (Bitmap)Image.FromFile(target);
        }

        public void Start(string source, string target, string arguments, int timeout)
        {
            if (!this.ApplicationExists)
            {
                return;
            }

            using (System.Diagnostics.Process p = new System.Diagnostics.Process())
            {
                string cmd = "-i \"" + source + "\" " + arguments + " \"" + target + "\"";

                p.StartInfo.FileName = this.ApplicationPath;
                p.StartInfo.Arguments = cmd;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                p.Start();
                p.WaitForExit(1000 * timeout);
            }
        }

        public string GetInfo(string source)
        {
            string result = "";
            string error = "";
            if (!this.ApplicationExists)
            {
                return result;
            }

            try
            {
                using (System.Diagnostics.Process p = new System.Diagnostics.Process())
                {
                    p.StartInfo.FileName = this.ApplicationPath;
                    p.StartInfo.Arguments = "-i \"" + source + "\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

                    if (p.Start())
                    {
                        result = p.StandardOutput.ReadToEnd();
                        error = p.StandardError.ReadToEnd();
                    }

                    p.WaitForExit(1000 * 10);
                }

                if (error.IndexOf("Input #0") > 0)
                {
                    error = error.Substring(error.IndexOf("Input #0")).Replace("\n", ",").Replace("\r", "");
                }
            }
            catch { }
            return error;
        }

        public double ParseDuration(string ffmpeg)
        {
            double result = 0;
            try
            {
                foreach (string part in ffmpeg.Split(','))
                {
                    if (part.Contains("Duration:"))
                    {
                        string a = part.Replace("Duration:", "").Trim();
                        result += Convert.ToDouble(a.Split(':')[0]) * 60 * 60;
                        result += Convert.ToDouble(a.Split(':')[1]) * 60;
                        result += Convert.ToDouble(a.Split(':')[2].Replace('.', ','));
                    }
                }
            }
            catch { }

            return result;
        }

        public double ParseFPS(string ffmpeg)
        {
            double result = 0;
            try
            {
                foreach (string part in ffmpeg.Split(','))
                {
                    if (part.Contains("tbr"))
                    {
                        string a = part.Replace("tbr", "").Trim();
                        result += Convert.ToDouble(a.Replace('.', ','));
                    }
                }
            }
            catch { }

            return result;
        }

        public System.Diagnostics.Process StartFFPlay(string path)
        {
            if (this.FindFFPlay() != null)
            {
                try
                {
                    return System.Diagnostics.Process.Start(this.FindFFPlay(), "\"" + path + "\"");
                }
                catch
                {
                }
            }

            return null;
        }

        private string FindApplication(string dirStartsWith, string contains)
        {
            foreach (string look in System.IO.Directory.GetDirectories(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), dirStartsWith + "*"))
            {
                if (System.IO.File.Exists(look + "\\" + contains))
                    return look + "\\" + contains;
            }

            return null;
        }

        private string FindFFPlay()
        {
            return FindApplication("ffmpeg", "ffplay.exe");
        }

        private string FindFFmpeg()
        {
            return FindApplication("ffmpeg", "ffmpeg.exe");
        }
    }
}
