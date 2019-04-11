using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public enum SearchTokenRelationType { ALL, LANDSCAPE, LANDSCAPE43, LANDSCAPE21, PORTRAIT, PORTRAIT43, PORTRAIT21 }

    [Serializable]
    public class SearchTokenRelation
    {
        public SearchTokenRelationType RelationType { get; set; }
       
        public string Caption        
        {
            get
            {
                switch (this.RelationType)
                {
                    case SearchTokenRelationType.LANDSCAPE:
                        return "Querformat";
                    case SearchTokenRelationType.LANDSCAPE21:
                        return "Querformat > 4:3";
                    case SearchTokenRelationType.LANDSCAPE43:
                        return "Querformat > 2:1";
                    case SearchTokenRelationType.PORTRAIT:
                        return "Hochformat";
                    case SearchTokenRelationType.PORTRAIT21:
                        return "Hochformat > 4:3";
                    case SearchTokenRelationType.PORTRAIT43:
                        return "Hochformat > 2:1";
                    default:
                        return "Alle";
                }
            }
        }
        
        public double From
        {
            get
            {
                switch (this.RelationType)
                {
                    case SearchTokenRelationType.LANDSCAPE:
                        return 1.0;
                    case SearchTokenRelationType.LANDSCAPE21:
                        return 1.333;
                    case SearchTokenRelationType.LANDSCAPE43:
                        return 2.0;
                    case SearchTokenRelationType.PORTRAIT:
                        return 0;
                    case SearchTokenRelationType.PORTRAIT21:
                        return 0;
                    case SearchTokenRelationType.PORTRAIT43:
                        return 0;
                    default:
                        return 0;
                }
            }
        }

        public double To
        {
            get
            {
                switch (this.RelationType)
                {
                    case SearchTokenRelationType.LANDSCAPE:
                        return 0;
                    case SearchTokenRelationType.LANDSCAPE21:
                        return 0;
                    case SearchTokenRelationType.LANDSCAPE43:
                        return 0;
                    case SearchTokenRelationType.PORTRAIT:
                        return 1;
                    case SearchTokenRelationType.PORTRAIT21:
                        return 1.333;
                    case SearchTokenRelationType.PORTRAIT43:
                        return 2.0;
                    default:
                        return 0;
                }
            }
        }

        public override int GetHashCode()
        {
            return (int)this.RelationType;
        }

        public override string ToString()
        {
            return this.Caption;
        }

        public override bool Equals(object obj)
        {
            if (obj is SearchTokenRelation)
            {
                return this.RelationType == ((SearchTokenRelation)obj).RelationType;
            }
            else
            {
                return false;
            }        
        }
    }

    public class SearchTokenRelationList : List<SearchTokenRelation>
    {
    }
}
