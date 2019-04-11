using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class SearchToken
    {
        public SearchToken()
        {
            this.SearchText1 = "";
            this.SearchText1Not = false;
            this.SearchText1Description = true;
            this.SearchText1Category = true;
            this.SearchText1Filename = true;
            this.SearchText1Md5 = false;
            this.SearchText1Folder = false;

            this.SearchText2 = "";
            this.SearchText2Not = false;
            this.SearchText2Description = true;
            this.SearchText2Category = true;
            this.SearchText2Filename = true;
            this.SearchText2Md5 = false;
            this.SearchText2Folder = false;

            this.MetaDataKey = "";
            this.MetaDataValue = "";

            this.DateFrom = DateTime.Now;
            this.DateTo = DateTime.Now;
            this.DateFromEnabled = false;
            this.DateToEnabled = false;

            this.LengthFrom = 0;
            this.LengthTo = 0;

            this.DurationFrom = 0;
            this.DurationTo = 0;

            this.PriorityFrom = 1;
            this.PriorityTo = 9;

            this.DimensionFrom = 0;
            this.DimensionTo = 0;

            this.Relation = new SearchTokenRelation() { RelationType = SearchTokenRelationType.ALL };
       
            this.MediaTypeDirectShow = true;
            this.MediaTypeRgb = true;
        }

        public string SearchText1 { get; set; }
        public bool SearchText1Not { get; set; }
        public bool SearchText1Description { get; set; }
        public bool SearchText1Category { get; set; }
        public bool SearchText1Filename { get; set; }
        public bool SearchText1Md5 { get; set; }
        public bool SearchText1Folder { get; set; }

        public string SearchText2 { get; set; }
        public bool SearchText2Not { get; set; }
        public bool SearchText2Description { get; set; }
        public bool SearchText2Category { get; set; }
        public bool SearchText2Filename { get; set; }
        public bool SearchText2Md5 { get; set; }
        public bool SearchText2Folder { get; set; }

        public string MetaDataKey { get; set; }
        public string MetaDataValue { get; set; }

        public bool DateFromEnabled { get; set; }
        public DateTime DateFrom { get; set; }
        public bool DateToEnabled { get; set; }
        public DateTime DateTo { get; set; }

        public double LengthFrom { get; set; }
        public double LengthTo { get; set; }

        public double DurationFrom { get; set; }
        public double DurationTo { get; set; }

        public int PriorityFrom { get; set; }
        public int PriorityTo { get; set; }

        public int DimensionFrom { get; set; }
        public int DimensionTo { get; set; }

        public SearchTokenRelation Relation { get; set; }

        public bool MediaTypeRgb { get; set; }
        public bool MediaTypeDirectShow { get; set; }

        public bool IsValid
        {
            get
            {
                return this.InfoList().Count > 0;
            }
        }

        private List<string> InfoList()
        {
            List<string> list = new List<string>();

            if (!String.IsNullOrWhiteSpace(this.SearchText1) &&
                (this.SearchText1Description
                || this.SearchText1Category
                || this.SearchText1Filename
                || this.SearchText1Folder
                || this.SearchText1Md5))
            {
                list.Add("Suchtext 1: " + this.SearchText1
                 + " (" + ((this.SearchText1Not ? "nicht, " : "")
                 + (this.SearchText1Description ? "Beschreibung, " : "")
                 + (this.SearchText1Category ? "Kategorie, " : "")
                 + (this.SearchText1Filename ? "Dateiname, " : "")
                 + (this.SearchText1Folder ? "Dateipfad, " : "")
                 + (this.SearchText1Md5 ? "Prüfsumme" : "")).TrimEnd().TrimEnd(',') + ")");
            }

            if (!String.IsNullOrWhiteSpace(this.SearchText2) &&
              (this.SearchText2Description
              || this.SearchText2Category
              || this.SearchText2Filename
              || this.SearchText2Folder
              || this.SearchText2Md5))
            {
                list.Add("Suchtext 2: " + this.SearchText2
                 + " (" + ((this.SearchText2Not ? "nicht, " : "")
                 + (this.SearchText2Description ? "Beschreibung, " : "")
                 + (this.SearchText2Category ? "Kategorie, " : "")
                 + (this.SearchText2Filename ? "Dateiname, " : "")
                 + (this.SearchText2Folder ? "Dateipfad, " : "")
                 + (this.SearchText2Md5 ? "Prüfsumme" : "")).TrimEnd().TrimEnd(',') + ")");
            }

            if (this.MediaTypeRgb && !this.MediaTypeDirectShow)
            {
                list.Add("nur Bilder");
            }

            if (!this.MediaTypeRgb && this.MediaTypeDirectShow)
            {
                list.Add("nur Videos");
            }

            if (this.DateFromEnabled)
            {
                list.Add("nach dem " + this.DateFrom.ToShortDateString());
            }

            if (this.DateToEnabled)
            {
                list.Add("vor dem " + this.DateTo.ToShortDateString());
            }

            if (this.Relation.RelationType != SearchTokenRelationType.ALL)
            {
                list.Add(this.Relation.Caption);
            }

            if (this.MetaDataKey.Trim().Length > 0
                && this.MetaDataValue.Trim().Length > 0)
            {
                list.Add(this.MetaDataKey.Trim() + ": " + this.MetaDataValue.Trim());
            }

            if (this.DimensionFrom > 0 || this.DimensionTo > 0
                && this.DimensionFrom < this.DimensionTo)
            {
                list.Add("Abmessung (pix): " + this.DimensionFrom + " <-> " + this.DimensionTo);
            }

            if (this.LengthFrom > 0 || this.LengthTo > 0
                && this.LengthFrom < this.LengthTo)
            {
                list.Add("Datei-Größe (KB): " + this.LengthFrom + " <-> " + this.LengthTo);
            }
            
            if (this.DurationFrom > 0 || this.DurationTo > 0
                && this.DurationFrom < this.DurationTo)
            {
                list.Add("Dauer (s): " + this.DurationFrom + " <-> " + this.DurationTo);
            }

            if (this.PriorityFrom > 1 || this.PriorityTo < 9
               && this.PriorityFrom <= this.PriorityTo)
            {
                list.Add("Priorität: " + this.PriorityFrom + " <-> " + this.PriorityTo);
            }

            return list;
        }

        public override string ToString()
        {
            return String.Join(";\n", this.InfoList());
        }
    }
}
