using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class MediaItemDublicatesRequest : MediaItemRequest
    {
        override public string Header
        {
            get
            {
                return "Alle Dubletten";
            }
        }

        override public string Description
        {
            get
            {
                return "Zeigt alle Medien an, die entsprechend ihrer MD5-Prüfsumme mehrfach in der Datenbank vorhanden sind.";
            }
        }

        public MediaItemDublicatesRequest()
        {
            this.IsValid = true;
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            return base.GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
