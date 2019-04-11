using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class MediaItemSearchRequest : MediaItemRequest
    {
        public SearchToken SearchToken { get; private set; }
        public int WindowIdentifier { get; set; }

        override public string Header
        {
            get
            {
                string header = "Suche " + this.WindowIdentifier;
                return this.UserDefinedName == null ? header : this.UserDefinedName;
            }
        }

        override public string Description
        {
            get
            {
                return this.SearchToken.ToString();
            }
        }

        public MediaItemSearchRequest(SearchToken searchToken, int windowIdentifier)
        {
            this.SearchToken = searchToken;
            this.IsValid = searchToken.IsValid;
            this.WindowIdentifier = windowIdentifier;
        }

        public override bool Equals(object obj)
        {
            MediaItemSearchRequest other = obj as MediaItemSearchRequest;

            if (other == null)
                return false;

            if (other.UserDefinedId > 0 && this.UserDefinedId > 0)
            {
                return other.UserDefinedId == this.UserDefinedId;
            }
            else if (!(other.UserDefinedId == 0 && this.UserDefinedId == 0))
            {
                return false;
            }

            return this.GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            return this.WindowIdentifier;
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }
    }
}
