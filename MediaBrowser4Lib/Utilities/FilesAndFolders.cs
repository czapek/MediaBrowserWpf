using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MediaBrowser4.Objects;

namespace MediaBrowser4.Utilities
{
    public class FilesAndFolders
    {
        public static List<Attachment> GetAttachments(MediaItem mItem)
        {
            List<Attachment> attachments = new List<Attachment>();
            if (!mItem.FileObject.Exists)
                return attachments;

            bool isShared = false;

            string pattern = System.IO.Path.GetFileNameWithoutExtension(mItem.Filename);

            foreach (string name in Directory.GetFiles(mItem.FileObject.DirectoryName, pattern + ".*"))
            {
                if (!name.Equals(mItem.FullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (MediaBrowserContext.HasKnownMediaExtension(name))
                    {
                        isShared = true;
                    }
                    else
                    {
                        attachments.Add(new Attachment(mItem, name));
                    }
                }
            }

            foreach (Attachment attachment in attachments)
                attachment.IsShared = isShared;

            return attachments;
        }

        public static Exception MoveMediaItem(MediaItem mItem, string drainFolder)
        {
            if (drainFolder.EndsWith(":"))
                drainFolder += "\\";

            if (!File.Exists(mItem.FileObject.FullName))
                return new Exception("Datei nicht gefunden: " + mItem.FullName);

            if (drainFolder.Equals(mItem.FileObject.DirectoryName, StringComparison.InvariantCultureIgnoreCase))
                return null;

            if (File.Exists(System.IO.Path.Combine(drainFolder, mItem.Filename)))
                return new Exception("Datei existiert bereits: " + System.IO.Path.Combine(drainFolder, mItem.Filename));

            Exception exResult = null;
            List<Tuple<string, string, bool>> rollbackList = new List<Tuple<string, string, bool>>();

            try
            {
                foreach (Attachment attachment in GetAttachments(mItem))
                {

                    if (!File.Exists(attachment.FullName))
                        throw new Exception("Datei nicht gefunden: " + attachment.FullName);

                    if (File.Exists(System.IO.Path.Combine(drainFolder,
                        System.IO.Path.GetFileName(attachment.Name))))
                    {
                        throw new Exception("Datei existiert bereits: " + System.IO.Path.Combine(drainFolder,
                            System.IO.Path.GetFileName(attachment.Name)));
                    }


                    if (attachment.IsShared)
                    {
                        File.Copy(attachment.FullName, System.IO.Path.Combine(drainFolder,
                        System.IO.Path.GetFileName(attachment.Name)));
                    }
                    else
                    {
                        File.Move(attachment.FullName, System.IO.Path.Combine(drainFolder,
                        System.IO.Path.GetFileName(attachment.Name)));
                    }

                    rollbackList.Add(Tuple.Create<string, string, bool>(System.IO.Path.Combine(drainFolder,
                        System.IO.Path.GetFileName(attachment.Name)), attachment.FullName, attachment.IsShared));
                }

                mItem.FileObject.MoveTo(System.IO.Path.Combine(drainFolder, mItem.Filename));
            }
            catch (Exception ex)
            {
                exResult = ex;
            }

            if (exResult != null)
            {
                foreach (Tuple<string, string, bool> fileTupel in rollbackList)
                {
                    try
                    {
                        if (fileTupel.Item3)
                            File.Delete(fileTupel.Item1);
                        else
                            File.Move(fileTupel.Item1, fileTupel.Item2);
                    }
                    catch { }
                }
            }

            return exResult;
        }

        public static string FormatByte(long value)
        {
            if (value > 1024)
            {
                return string.Format("{0:0,0}", value / 1024) + " KB";
            }
            else
            {
                return value + " Byte";
            }
        }

        /// <summary>
        /// Returns true if the protection was changed
        /// </summary>
        /// <param name="fullName"></param>
        /// <param name="readOnly"></param>
        public static bool SetReadOnlyAttribute(string fullName, bool readOnly)
        {
            bool result = false;
            try
            {
                FileInfo filePath = new FileInfo(fullName);
                FileAttributes attribute;

                if ((filePath.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly && readOnly)
                    return false;

                if ((filePath.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly && !readOnly)
                    return false;

                if (readOnly)
                    attribute = filePath.Attributes | FileAttributes.ReadOnly;
                else
                    attribute = (FileAttributes)(filePath.Attributes - FileAttributes.ReadOnly);

                File.SetAttributes(filePath.FullName, attribute);
                result = true;
            }
            catch { }
            return result;
        }


        public static string CleanPath(string path)
        {
            path = path.Trim().Replace('/', '\\');

            if (path.StartsWith("\\\\"))
            {
                return "\\\\" + path.Replace("\\\\", "\\").Trim('\\');
            }
            else
            {
                return path.Replace("\\\\", "\\").Trim('\\');
            }
        }
    }
}
