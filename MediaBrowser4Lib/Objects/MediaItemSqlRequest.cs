using System;
using System.Collections.Generic;
using MediaBrowser4.DB;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class MediaItemSqlRequest : MediaItemRequest
    {
        public override string Description
        {
            get
            {
                return this.Sql;
            }
        }

        override public string Header
        {
            get
            {
                return this.UserDefinedName == null ? "Sql Request" : this.UserDefinedName;
            }
        }

        public String Sql
        {
            get; set;
        }

        private List<Tuple<String, object>> TupelParameterList { get; set; }

        public List<SqlParameterInfo> ParameterList
        {
            set
            {
                if (value != null)
                {
                    TupelParameterList = new List<Tuple<string, object>>();
                    foreach (SqlParameterInfo parameter in value)
                    {
                        Tuple<string, object> tupel = new Tuple<string, object>(parameter.ParameterName, parameter.Value);
                        TupelParameterList.Add(tupel);
                    }
                }
            }

            get
            {
                List<SqlParameterInfo> parameterList = new List<SqlParameterInfo>();
                TupelParameterList = TupelParameterList ?? new List<Tuple<string, object>>();
                foreach (Tuple<string, object> tupel in TupelParameterList)
                {
                    SqlParameterInfo parameter = new SqlParameterInfo()
                    {
                        ParameterName = tupel.Item1,
                        Value = tupel.Item2
                    };

                    parameterList.Add(parameter);
                }
                return parameterList;
            }
        }

        public string SortString
        {
            get; set;
        }

        public MediaItemSqlRequest(string sql, List<SqlParameterInfo> parameterList, string sortString, int limtRequest)
        {
            this.Sql = sql;
            this.ParameterList = parameterList;
            this.SortString = sortString;
            this.LimitRequest = limtRequest;
            this.IsValid = true;
        }

        public override bool Equals(object obj)
        {
            MediaItemSqlRequest other = obj as MediaItemSqlRequest;

            if (other == null)
                return false;

            return this.Sql == other.Sql;
        }

        public override int GetHashCode()
        {
            return Sql.GetHashCode();
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }
    }
}
