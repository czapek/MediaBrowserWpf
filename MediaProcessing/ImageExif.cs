using System;
using com.drew.metadata;
using com.drew.imaging.jpg;
using System.IO;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using com.drew.metadata.exif;

namespace MediaProcessing
{
    public class ImageExif
    {
        private static bool IsValidMetaData(string metadata)
        {
            string[] split = metadata.Split(' ');
            if (split.Length > 1)
            {
                int b;
                foreach (string a in split)
                {
                    if (!Int32.TryParse(a, out b))
                        return true;
                }
                return false;
            }

            return true;
        }

        Metadata metadata;
        public static ImageExif ImageExifFactory(string aFileName)
        {
            if (!File.Exists(aFileName))
                return null;

            try
            {
                return new ImageExif() { metadata = JpegMetadataReader.ReadMetadata(new FileInfo(aFileName)) };
            }
            catch
            { }

            return null;
        }

        public int[] GetThumbnailData()
        {
            IEnumerator<AbstractDirectory> lcDirectoryEnum = this.metadata.GetDirectoryIterator();

            while (lcDirectoryEnum.MoveNext())
            {
                AbstractDirectory lcDirectory = lcDirectoryEnum.Current;

                if (lcDirectory.ContainsTag(ExifDirectory.TAG_THUMBNAIL_DATA))
                {
                    return lcDirectory.GetIntArray(ExifDirectory.TAG_THUMBNAIL_DATA);
                }
            }

            return new int[] { };
        }

        public DateTime? GetCreateDate()
        {
            IEnumerator<AbstractDirectory> lcDirectoryEnum = this.metadata.GetDirectoryIterator();

            while (lcDirectoryEnum.MoveNext())
            {
                AbstractDirectory lcDirectory = lcDirectoryEnum.Current;

                if (lcDirectory.ContainsTag(ExifDirectory.TAG_DATETIME_DIGITIZED))
                {
                    return lcDirectory.GetDate(ExifDirectory.TAG_DATETIME_DIGITIZED);
                }

                if (lcDirectory.ContainsTag(ExifDirectory.TAG_DATETIME))
                {
                    return lcDirectory.GetDate(ExifDirectory.TAG_DATETIME);
                }

                if (lcDirectory.ContainsTag(ExifDirectory.TAG_DATETIME_ORIGINAL))
                {
                    return lcDirectory.GetDate(ExifDirectory.TAG_DATETIME_ORIGINAL);
                }
            }

            return null;      
        }

        public static Dictionary<string, Dictionary<string, string>> GetAllTags(string aFileName)
        {
            if (!File.Exists(aFileName))
                return null;

            Dictionary<string, Dictionary<string, string>> metaList
                = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, string> subList = null;

            Metadata lcMetadata = null;
            FileInfo lcImgFile = new FileInfo(aFileName);
            try
            {
                lcMetadata = JpegMetadataReader.ReadMetadata(lcImgFile);
            }
            catch
            {
                return null;
            }

            IEnumerator<AbstractDirectory> lcDirectoryEnum = lcMetadata.GetDirectoryIterator();
            while (lcDirectoryEnum.MoveNext())
            {
                string lcTagDescription = null;
                Tag lcTag = null;
                string lcTagName = null;
                AbstractDirectory lcDirectory = lcDirectoryEnum.Current;

                if (lcDirectory.HasError)
                {
                    //Console.Error.WriteLine("Some errors were found, activate trace using /d:TRACE option with the compiler");
                }

                if (metaList.ContainsKey(lcDirectory.GetName()))
                {
                    subList = metaList[lcDirectory.GetName()];
                }
                else
                {
                    subList = new Dictionary<string, string>();
                    metaList.Add(lcDirectory.GetName(), subList);
                }

                IEnumerator<Tag> lcTagsIterator = lcDirectory.GetTagIterator();
                while (lcTagsIterator.MoveNext())
                {
                    lcTag = lcTagsIterator.Current;
                    lcTagDescription = null;
                    try
                    {
                        lcTagDescription = lcTag.GetDescription();
                    }
                    catch (MetadataException e)
                    {
                        Console.Error.WriteLine(e.Message);
                    }
                    lcTagName = lcTag.GetTagName();
                    if (lcTagName != null && lcTagDescription != null
                        && !lcTagName.StartsWith("Unknown") && !lcTagName.StartsWith("Component 1")
                        && !lcTagName.StartsWith("Component 2")
                        && !lcTagName.StartsWith("Component 3")
                        && !lcTagName.StartsWith("Thumbnail")
                        && !lcTagDescription.StartsWith("Unknown")
                        && IsValidMetaData(lcTagDescription) && !subList.ContainsKey(lcTagName))
                        subList.Add(lcTagName, lcTagDescription);

                    lcTag = null;
                    lcTagDescription = null;
                    lcTagName = null;
                }
                lcDirectory = null;
                lcTagsIterator = null;
            }
            lcDirectoryEnum = null;
            lcMetadata = null;

            return metaList;
        }

        public static int GetExifOrientation(Dictionary<string, Dictionary<string, string>> metaDic)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> sublist in metaDic)
            {
                if (sublist.Value.ContainsKey("Orientation"))
                {
                    string data = sublist.Value["Orientation"].ToString().ToLower();

                    if (data.StartsWith("right"))
                        return 1;
                    if (data.StartsWith("bottom"))
                        return 2;
                    if (data.StartsWith("left"))
                        return 3;

                    break;
                }
            }

            return 0;
        }
    }
}
