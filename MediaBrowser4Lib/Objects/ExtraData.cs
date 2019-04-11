using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MediaBrowser4.Objects
{
    public class ExtraData
    {
        public static List<string> GetLocalAudioFiles(List<string> extraFileList)
        {
            List<string> list = new List<string>();
            foreach (string extrafile in extraFileList)
            {
                if (MediaBrowserContext.audioExtraFiles.Contains(Path.GetExtension(extrafile).ToLower()))
                    list.Add(extrafile);
            }
            return list;
        }

        public static List<string> GetLocalStickyFiles(MediaItem mItem)
        {
            return GetLocalStickyFiles(mItem, null);
        }

        public static List<string> GetLocalStickyFiles(MediaItem mItem, string exclude)
        {
            if (exclude == null)
                exclude = "";

            List<string> extraFileList = new List<string>();
            if (File.Exists(mItem.FileObject.FullName))
            {
                string[] files = Directory.GetFiles(mItem.FileObject.Directory.FullName, Path.GetFileNameWithoutExtension(mItem.FileObject.FullName) + ".*");

                foreach (string extrafile in files)
                {
                    if (mItem.FileObject.FullName != extrafile
                       && Path.GetExtension(extrafile).ToLower() != exclude)
                    {
                        extraFileList.Add(extrafile);
                    }
                }


                if (mItem.FileObject.FullName.StartsWith("IMG_") && mItem.FileObject.FullName.EndsWith(".WAV"))
                {
                    string canonSND = mItem.FileObject.FullName.Replace("IMG_", "SND_").Replace("JPG", "WAV");

                    if (File.Exists(canonSND))
                    {
                        extraFileList.Add(canonSND);
                    }
                }
            }
            return extraFileList;
        }
    }
}
