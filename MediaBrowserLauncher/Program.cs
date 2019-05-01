using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowserLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), @"SynologyDrive\Programme\MediaBrowserWpf\MediaBrowserWPF.exe");

            if (!File.Exists(path)) 
                path = Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), @"Shared with me\MediaBrowserWpf\MediaBrowserWPF.exe");

            if (File.Exists(path))
            {
                Process process = new Process();
                process.StartInfo.FileName = path;

                if (args.Length > 0)
                    process.StartInfo.Arguments = String.Join(" ", args);
                process.Start();
            }
        }
    }
}
