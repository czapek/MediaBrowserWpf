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
            string path = Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), @"SynologyDriveDeployment\MediaBrowserWpf\MediaBrowserWPF.exe");

            if (File.Exists(path))
            {
                Process process = new Process();
                process.StartInfo.FileName = path;

                if (args.Length > 0)
                    process.StartInfo.Arguments = String.Join(" ", args.Select(QuoteArgument));
                process.Start();
            }
        }

        private static string QuoteArgument(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            if (value.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) < 0)
            {
                return value;
            }

            StringBuilder result = new StringBuilder();
            result.Append('"');
            int backslashes = 0;

            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                }
                else
                {
                    result.Append('\\', backslashes);
                    result.Append(character);
                    backslashes = 0;
                }
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }
}
