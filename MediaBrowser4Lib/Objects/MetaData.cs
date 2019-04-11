using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4.Objects
{
    public struct MetaData : IComparable<MetaData>
    {
        public int Id;
        public string Name;
        public string GroupName;
        public string Value;
        public string Type;
        public bool Null;
        public bool IsVisible;

        public MetaData(string name, string groupName, string value, string type, bool IsVisible)
        {
            this.Type = type.Trim();
            this.Name = name.Trim();
            this.Value = value.Trim();
            this.IsVisible = IsVisible;
            this.GroupName = groupName.Trim();
            this.Id = -1;
            Null = false;
        }

        public override string ToString()
        {
            return this.GroupName + " /// " + this.Name + " /// " + this.Value;
        }

        #region IComparable<MetaData> Member

        int IComparable<MetaData>.CompareTo(MetaData other)
        {
            return this.GroupName.CompareTo(other.GroupName) == 0 ?
                this.Name.CompareTo(other.Name) : this.GroupName.CompareTo(other.GroupName);
        }

        #endregion
    }

    public class MetaDataList : List<MetaData>
    {
        public MetaData Find(string name, string type)
        {
            return this.Find(name, type, null);
        }

        public MetaData Find(string name, string type, string groupName)
        {
            foreach (MetaData mt in this)
            {
                if ((type == null || mt.Type == type) && (groupName == null || mt.GroupName == groupName)
                    && mt.Name.ToLower().Replace(" ", "") == name.ToLower().Replace(" ", ""))
                {
                    return mt;
                }
            }

            MetaData m = new MetaData();
            m.Null = true;
            return m;
        }

        public MetaData FindSoft(string name)
        {
            return this.FindSoft(name, null);
        }

        public MetaData FindSoft(string name, string groupName)
        {
            name = name.Trim().ToLower();
            if (groupName != null)
                groupName = groupName.Trim().ToLower();

            foreach (MetaData mt in this)
            {
                if ((groupName == null || mt.GroupName.ToLower().Contains(groupName)) && mt.Name.ToLower().Contains(name))
                {
                    return mt;
                }
            }

            MetaData m = new MetaData();
            m.Null = true;
            return m;
        }

        public static MetaDataList GetList(Dictionary<string, Dictionary<string, string>> metaDic, string type)
        {
            MetaDataList mList = null;
            if (metaDic != null)
            {
                mList = new MetaDataList();
                foreach (KeyValuePair<string, Dictionary<string, string>> sublist in metaDic)
                {
                    foreach (KeyValuePair<string, string> kv in sublist.Value)
                    {
                        mList.Add(new MetaData(kv.Key, sublist.Key, kv.Value, type, true));
                    }
                }
            }
            return mList;
        }

        public static MetaDataList GetList(SortedDictionary<string, string> metaDic, string groupName, string type)
        {
            MetaDataList mList = null;
            if (metaDic != null)
            {
                mList = new MetaDataList();

                foreach (KeyValuePair<string, string> kv in metaDic)
                {
                    mList.Add(new MetaData(kv.Key, groupName, kv.Value, type, true));
                }
            }

            return mList;
        }

        string model;
        public string Model
        {
            get
            {
                if (model == null)
                {
                    MetaData m1 = this.Find("Make", null, "Exif Makernote");
                    MetaData m2 = this.Find("Model", null, "Exif Makernote");

                    if (!m2.Null && !m1.Null && m2.Value.Contains(m1.Value))
                    {
                        model = m2.Value;
                    }
                    else if (!m1.Null)
                    {
                        model = m1.Value + " " + m2.Value;
                    }
                }

                return model;
            }
        }

        string whiteBalance;
        public string WhiteBalance
        {
            get
            {
                if (whiteBalance == null)
                {
                    MetaData m = this.FindSoft("white balance");

                    if (!m.Null)
                    {
                        whiteBalance = m.Value;
                    }
                }

                return whiteBalance;
            }
        }

        string focalLength;
        public string FocalLength
        {
            get
            {
                if (focalLength == null)
                {
                    MetaData m = this.FindSoft("focal length", "Exif Makernote");

                    if (!m.Null)
                    {
                        focalLength = m.Value;
                    }
                    else
                    {
                        m = this.FindSoft("focal length");

                        if (!m.Null)
                        {
                            aperture = m.Value;
                        }
                    }
                }

                return focalLength;
            }
        }

        string aperture;
        public string Aperture
        {
            get
            {
                if (aperture == null)
                {
                    MetaData m = this.FindSoft("f-number", "Exif Makernote");

                    if (!m.Null)
                    {
                        aperture = m.Value;
                    }
                    else
                    {
                        m = this.FindSoft("aperture", "Exif Makernote");

                        if (!m.Null)
                        {
                            aperture = m.Value;
                        }
                    }
                }

                return aperture;
            }
        }

        string iso;
        public string Iso
        {
            get
            {
                if (iso == null)
                {
                    MetaData m = this.Find("ISO", null, "Exif Makernote");

                    if (m.Null)
                    {
                        m = this.Find("ISO", null, null);
                    }

                    if (m.Null)
                    {
                        m = this.FindSoft("iso", "Exif Makernote");
                    }

                    if (m.Null)
                    {
                        m = this.FindSoft("iso");
                    }

                    if (!m.Null)
                    {
                        iso = m.Value;

                        if (!iso.ToLower().Contains("iso"))
                        {
                            iso = "ISO " + iso;
                        }
                    }    
                }

                return iso;
            }
        }

        string exposureTime;
        public string ExposureTime
        {
            get
            {
                if (exposureTime == null)
                {
                    MetaData m = this.FindSoft("exposure time", "Exif Makernote");

                    if (!m.Null)
                    {
                        exposureTime = m.Value;
                    }
                    else
                    {
                        m = this.FindSoft("exposure time");
                    }

                    if (!m.Null)
                    {
                        exposureTime = m.Value;
                    }
                }

                return exposureTime;
            }
        }

        string audioFormat;
        public string AudioFormat
        {
            get
            {
                if (audioFormat == null)
                {
                    audioFormat = String.Join(" ", this.FindAll(x => x.GroupName == "MediaInfoLib Audio" && x.Name.StartsWith("Format")).Select(x => x.Value));
                }
                return audioFormat;
            }
        }

        string videoCodec;
        public string VideoCodec
        {
            get
            {
                if (videoCodec == null)
                {
                    MetaData m = this.Find("Codec Id", null, "MediaInfoLib Video");

                    if (!m.Null)
                    {
                        videoCodec = m.Value;
                    }

                    m = this.Find("Format", null, "MediaInfoLib Video");

                    if (!m.Null)
                    {
                        videoCodec += " " + m.Value;
                    }
                }

                return videoCodec;
            }
        }

        string videoformat;
        public string VideoFormat
        {
            get
            {
                if (videoformat == null)
                {
                    MetaData m = this.Find("Format", null, "MediaInfoLib General");
                    if (m.Null)
                    {
                        m = this.FindSoft("format", "MediaInfoLib General");
                    }

                    if (!m.Null)
                    {
                        videoformat = m.Value;
                    }
                }

                return videoformat;
            }
        }

        string frameRate;
        public string FrameRate
        {
            get
            {
                if (frameRate == null)
                {
                MetaData m = this.FindSoft("Frame rate", "MediaInfoLib Video");

                    if (!m.Null)
                    {
                        frameRate = m.Value;
                    }
                }

                return frameRate;
            }
        }
    }
}
