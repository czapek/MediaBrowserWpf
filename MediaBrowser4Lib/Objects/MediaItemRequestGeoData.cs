using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class MediaItemRequestGeoData : MediaItemRequest
    {
        public string DisplayName { get; set; }
        public double Latitude { get; set; }
        public double Longitute { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }


        override public string Header
        {
            get
            {
                return this.DisplayName.Split(',')[0].Trim();
            }
        }

        override public string Description
        {
            get
            {
                return this.DisplayName + " " + String.Format("(Breite: {0} Länge: {1})", Latitude, Longitute);
            }
        }

        public MediaItemRequestGeoData(double longitute, double width, double latitude, double height, String displayName)
        {
            DisplayName = displayName;
            Latitude = latitude;
            Longitute = longitute;
            Height = height;
            Width = width;
        }

        public override bool Equals(object obj)
        {
            MediaItemRequestGeoData other = obj as MediaItemRequestGeoData;

            if (other == null)
                return false;

            return this.Description == other.Description;
        }

        public override int GetHashCode()
        {
            return Description.GetHashCode();
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }
    }
}
