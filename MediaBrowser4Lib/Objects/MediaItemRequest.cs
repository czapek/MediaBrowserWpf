using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public abstract class MediaItemRequest
    {
        public static string[] MediaItemRequestSortTypeGui = new string[] { "Ordnername", "Dateiname", "Medientyp",
         "Datum", "Dateigröße", "Abspieldauer", "Betrachtungsdauer", "Bildfläche", "Seitenverhältnis", "Keine"};
        public static string[] MediaItemRequestShuffleTypeGui = new string[] { "Keine", "Zufall", "Zufällige 5er-Gruppe", "Zufällig nach Medien" };
        public static string[] MediaItemRequestSortDirectionGui = new string[] { "Absteigend", "Keine", "Aufsteigend" };

        public int UserDefinedId { get; set; }
        public SearchToken UserDefinedSearchToken { get; set; }
         
        public string UserDefinedName { get; set; }

        [NonSerialized]
        public MediaItem SelectedMediaItem;

        public SearchToken SearchTokenCombined
        {
            get
            {
                return (this.UserDefinedSearchToken == null
                        ? (MediaBrowserContext.SearchTokenGlobal == null
                        ? (SearchToken)null : MediaBrowserContext.SearchTokenGlobal) : this.UserDefinedSearchToken);
            }
        }

        public virtual bool IsValid
        {
            get;
            protected set;
        }

        public abstract string Header
        {
            get;
        }

        public abstract string Description
        {
            get;
        }        

        public int LimitRequest = MediaBrowserContext.LimitRequest;
        public MediaItemSqlRequest MediaItemSqlRequest { get; set;}

        public MediaItemRequestShuffleType ShuffleType = MediaItemRequestShuffleType.NONE;
        public MediaItemRequestType RequestType = MediaItemRequestType.UNION;
        public List<Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection>> SortTypeList = new List<Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection>>();

        public virtual void Refresh()
        {
        }

        public string BaseInfo
        {
            get
            {
                string sortInfo = null;
                if (this.ShuffleType == MediaItemRequestShuffleType.NONE)
                {
                    if (this.SortTypeList.Count > 0)
                    {
                        sortInfo = string.Join(", ", this.SortTypeList.Select(x => MediaItemRequestSortTypeGui[(int)x.Item1]))
                            + " (" + MediaItemRequestSortDirectionGui[((int)this.SortTypeList[0].Item2) + 1].ToLower() + ")";
                    }
                }
                else
                {
                    sortInfo = MediaItemRequestShuffleTypeGui[(int)this.ShuffleType];
                }

                return (sortInfo != null ? "Sortierung: " + sortInfo + "\n" : "") + "Maximal " + String.Format("{0:0,0}", this.LimitRequest) + " Ergebnisse";
            }
        }

        public override bool Equals(object obj)
        {
            MediaItemObservableCollectionRequest sortRequest = obj as MediaItemObservableCollectionRequest;

            if (sortRequest == null)
            {
                return false;
            }
            else
            {
                return this.Header.Equals(sortRequest.Header);
            }
        }

        public override int GetHashCode()
        {
            return this.Header.GetHashCode();
        }

        public abstract MediaItemRequest Clone();
    }
}
