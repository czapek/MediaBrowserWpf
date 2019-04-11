using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using System.Data.SQLite;
using System.Data;

namespace MediaBrowser4.DB.SQLite
{
    public class SearchTokenSql
    {
        public List<SQLiteParameter> ParameterList { get; private set; }
        public string WhereSql { get; private set; }
        public bool SearchCategories { get; private set; }
        public bool SearchMetaData { get; private set; }
        public bool SearchMetaName { get; private set; }
        public bool SearchDescription { get; private set; }
        public bool IsValid { get; private set; }

        private SearchTokenSql()
        {

        }

        public SearchTokenSql(SearchToken searchToken)
        {
            this.WhereSql = "";
            if (searchToken != null && searchToken.IsValid)
            {
                this.IsValid = true;
                this.Init(searchToken);
            }
        }

        public SearchTokenSql(MediaItemRequest mediaItemRequest)
        {
            this.WhereSql = "";
            SearchToken searchToken = null;

            if (mediaItemRequest != null)
            {
                searchToken = mediaItemRequest.SearchTokenCombined;
            }

            if (searchToken != null && searchToken.IsValid)
            {
                this.IsValid = true;
                this.Init(searchToken);
            }
        }

        public string JoinSql
        {
            get
            {
                if (this.IsValid)
                {
                    return (this.SearchDescription ? "LEFT OUTER JOIN DESCRIPTION ON DESCRIPTION.ID=MEDIAFILES.DESCRIPTION_FK LEFT OUTER JOIN ATTACHED ON ATTACHED.MEDIAFILES_FK=MEDIAFILES.ID LEFT OUTER JOIN ATTACHMENTS ON ATTACHMENTS.ID=ATTACHED.ATTACHMENTS_FK " : "")
                      + (this.SearchCategories ? "LEFT OUTER JOIN CATEGORIZE ON VARIATIONS_FK=CURRENTVARIATION LEFT OUTER JOIN CATEGORY ON CATEGORY_FK=CATEGORY.ID " : "")
                      + (this.SearchMetaData || this.SearchMetaName ? "INNER JOIN METADATA ON METADATA.MEDIAFILES_FK=MEDIAFILES.ID " : "")
                      + (this.SearchMetaName ? "INNER JOIN METADATANAME ON METADATA.METANAME_FK=METADATANAME.ID " : "");
                }
                else
                {
                    return "";
                }
            }
        }

        public string JoinSqlCategory
        {
            get
            {
                if (this.IsValid)
                {
                    return (this.SearchCategories ? "LEFT OUTER JOIN CATEGORY ON CATEGORY_FK=CATEGORY.ID " : "")
                       + (this.SearchDescription ? "LEFT OUTER JOIN DESCRIPTION ON DESCRIPTION.ID=MEDIAFILES.DESCRIPTION_FK LEFT OUTER JOIN ATTACHED ON ATTACHED.MEDIAFILES_FK=MEDIAFILES.ID LEFT OUTER JOIN ATTACHMENTS ON ATTACHMENTS.ID=ATTACHED.ATTACHMENTS_FK " : "")
                       + (this.SearchMetaData || this.SearchMetaName ? "INNER JOIN METADATA ON METADATA.MEDIAFILES_FK=MEDIAFILES.ID " : "")
                       + (this.SearchMetaName ? "INNER JOIN METADATANAME ON METADATA.METANAME_FK=METADATANAME.ID " : "");
                }
                else
                {
                    return "";
                }
            }
        }

        private void Init(SearchToken searchToken)
        {
            this.ParameterList = new List<SQLiteParameter>();
            List<string> sqlWhere = new List<string>();

            if (!String.IsNullOrWhiteSpace(searchToken.SearchText1)
                && (searchToken.SearchText1Category
                || searchToken.SearchText1Description
                || searchToken.SearchText1Filename
                || searchToken.SearchText1Folder
                || searchToken.SearchText1Md5))
            {
                string name = searchToken.SearchText1.Trim().Replace('*', '%');
                if (!name.StartsWith("%"))
                {
                    name = "%" + name;
                }

                if (!name.EndsWith("%"))
                {
                    name += "%";
                }

                string textParameter = "@Searchtext1";
                SQLiteParameter param = new SQLiteParameter(textParameter, DbType.String);

                param.Value = name;
                this.ParameterList.Add(param);

                List<string> joinSql = new List<string>();

                if (searchToken.SearchText1Filename)
                {
                    joinSql.Add("FILENAME LIKE " + textParameter);
                }

                if (searchToken.SearchText1Folder)
                {
                    joinSql.Add("FOLDERNAME LIKE " + textParameter);
                }

                if (searchToken.SearchText1Md5)
                {
                    joinSql.Add("MD5VALUE LIKE " + textParameter);
                }

                if (searchToken.SearchText1Description)
                {
                    joinSql.Add("DESCRIPTION.VALUE LIKE " + textParameter);
                    joinSql.Add("ATTACHMENTS.PATH LIKE " + textParameter);

                    this.SearchDescription = true;
                }

                if (searchToken.SearchText1Category)
                {
                    if (searchToken.SearchText1Not)
                    {
                        sqlWhere.Add("VARIATIONS.ID NOT IN (SELECT CATEGORIZE.VARIATIONS_FK FROM CATEGORY INNER JOIN CATEGORIZE ON CATEGORY.ID=CATEGORIZE.CATEGORY_FK WHERE FULLPATH LIKE " + textParameter + ")");
                    }
                    else
                    {
                        joinSql.Add("CATEGORY.FULLPATH NOT LIKE '" + MediaBrowserContext.CategoryHistoryName + "%' AND CATEGORY.FULLPATH LIKE " + textParameter);
                    }
                    this.SearchCategories = true;
                }

                if (joinSql.Count > 0)
                    sqlWhere.Add((searchToken.SearchText1Not ? "NOT " : "") + "(" + String.Join(" OR ", joinSql) + ")");
            }

            if (!String.IsNullOrWhiteSpace(searchToken.SearchText2)
                && (searchToken.SearchText2Category
                || searchToken.SearchText2Description
                || searchToken.SearchText2Filename
                || searchToken.SearchText2Folder
                || searchToken.SearchText2Md5))
            {
                string name = searchToken.SearchText2.Trim().Replace('*', '%');
                if (!name.StartsWith("%"))
                {
                    name = "%" + name;
                }

                if (!name.EndsWith("%"))
                {
                    name += "%";
                }

                string textParameter = "@Searchtext2";
                SQLiteParameter param = new SQLiteParameter(textParameter, DbType.String);

                param.Value = name;
                this.ParameterList.Add(param);

                List<string> joinSql = new List<string>();

                if (searchToken.SearchText2Filename)
                {
                    joinSql.Add("FILENAME LIKE " + textParameter);
                }

                if (searchToken.SearchText2Folder)
                {
                    joinSql.Add("FOLDERNAME LIKE " + textParameter);
                }

                if (searchToken.SearchText2Md5)
                {
                    joinSql.Add("MD5VALUE LIKE " + textParameter);
                }

                if (searchToken.SearchText2Description)
                {
                    joinSql.Add("DESCRIPTION.VALUE LIKE " + textParameter);
                    joinSql.Add("ATTACHMENTS.PATH LIKE " + textParameter);

                    this.SearchDescription = true;
                }

                if (searchToken.SearchText2Category)
                {
                    if (searchToken.SearchText2Not)
                    {
                        sqlWhere.Add("VARIATIONS.ID NOT IN (SELECT CATEGORIZE.VARIATIONS_FK FROM CATEGORY INNER JOIN CATEGORIZE ON CATEGORY.ID=CATEGORIZE.CATEGORY_FK WHERE FULLPATH LIKE " + textParameter + ")");
                    }
                    else
                    {
                        joinSql.Add("CATEGORY.FULLPATH NOT LIKE '" + MediaBrowserContext.CategoryHistoryName + "%' AND CATEGORY.FULLPATH LIKE " + textParameter);
                    }

                    this.SearchCategories = true;
                }

                if (joinSql.Count > 0)
                    sqlWhere.Add((searchToken.SearchText2Not ? "NOT " : "") + "(" + String.Join(" OR ", joinSql) + ")");
            }

            if (searchToken.DateFromEnabled)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchdateFrom", DbType.DateTime);
                param.Value = searchToken.DateFrom;
                sqlWhere.Add("MEDIADATE >= @SearchdateFrom");
                this.ParameterList.Add(param);
            }

            if (searchToken.DateToEnabled)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchdateTo", DbType.DateTime);
                param.Value = searchToken.DateTo;
                sqlWhere.Add("MEDIADATE <= @SearchdateTo");
                this.ParameterList.Add(param);
            }

            if (searchToken.LengthFrom > 0)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchlengthA", DbType.Double);
                param.Value = searchToken.LengthFrom * 1024;
                sqlWhere.Add("LENGTH >= @SearchlengthA");
                this.ParameterList.Add(param);
            }

            if (searchToken.LengthTo > 0)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchlengthB", DbType.Double);
                param.Value = searchToken.LengthTo * 1024;
                sqlWhere.Add("LENGTH <= @SearchlengthB");
                this.ParameterList.Add(param);
            }

            if (searchToken.DurationFrom > 0)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchdurationA", DbType.Double);
                param.Value = searchToken.DurationFrom;
                sqlWhere.Add("DURATION >= @SearchdurationA"); // AND MEDIAFILES.TYPE='dsh'");
                this.ParameterList.Add(param);
            }

            if (searchToken.DurationTo > 0)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchdurationB", DbType.Double);
                param.Value = searchToken.DurationTo;
                sqlWhere.Add("DURATION <= @SearchdurationB"); //  AND MEDIAFILES.TYPE='dsh'");
                this.ParameterList.Add(param);
            }

            if (searchToken.PriorityFrom > 1)
            {
                sqlWhere.Add("PRIORITY >= " + searchToken.PriorityFrom);
            }

            if (searchToken.PriorityTo < 9)
            {
                sqlWhere.Add("PRIORITY <= " + searchToken.PriorityTo);
            }

            if (searchToken.Relation.From != searchToken.Relation.To)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchrelationA", DbType.Double);
                param.Value = searchToken.Relation.From;

                sqlWhere.Add("(CASE WHEN (ORIENTATION = 0 OR ORIENTATION = 2) THEN (CAST(WIDTH AS float) / CAST(HEIGHT AS float)) "
                        + "ELSE (CAST(HEIGHT AS float) / CAST(WIDTH AS float)) END) >= @SearchrelationA");

                this.ParameterList.Add(param);
            }

            if (searchToken.Relation.From != searchToken.Relation.To &&
                searchToken.Relation.From < searchToken.Relation.To)
            {
                SQLiteParameter param = new SQLiteParameter("@SearchrelationB", DbType.Double);
                param.Value = searchToken.Relation.To;

                sqlWhere.Add("(CASE WHEN (ORIENTATION = 0 OR ORIENTATION = 2) THEN (CAST(WIDTH AS float) / CAST(HEIGHT AS float)) "
                        + "ELSE (CAST(HEIGHT AS float) / CAST(WIDTH AS float)) END) <= @SearchrelationB");

                this.ParameterList.Add(param);
            }

            if (searchToken.DimensionFrom > 0)
            {
                sqlWhere.Add("(WIDTH * HEIGHT) >= " + (searchToken.DimensionFrom*searchToken.DimensionFrom));
            }

            if (searchToken.DimensionTo > 0)
            {
                sqlWhere.Add("(WIDTH * HEIGHT) <= " + (searchToken.DimensionTo*searchToken.DimensionTo));
            }

            if (searchToken.MediaTypeRgb && !searchToken.MediaTypeDirectShow)
            {
                sqlWhere.Add("MEDIAFILES.TYPE='rgb'");
            }
            else if (!searchToken.MediaTypeRgb && searchToken.MediaTypeDirectShow)
            {
                sqlWhere.Add("MEDIAFILES.TYPE='dsh'");
            }

            if (!String.IsNullOrWhiteSpace(searchToken.MetaDataKey))
            {
                SQLiteParameter param = new SQLiteParameter("@SearchMetaName", DbType.String);
                string name = searchToken.MetaDataKey.Replace('*', '%').ToLower();
                param.Value = name;
                this.ParameterList.Add(param);
                sqlWhere.Add("LOWER(METADATANAME.NAME) LIKE @SearchMetaName");

                this.SearchMetaName = true;
            }

            if (!String.IsNullOrWhiteSpace(searchToken.MetaDataValue))
            {
                SQLiteParameter param = new SQLiteParameter("@SearchMetaData", DbType.String);
                string name = searchToken.MetaDataValue.Replace('*', '%').ToLower();
                param.Value = name;
                this.ParameterList.Add(param);
                sqlWhere.Add("LOWER(METADATA.VALUE) LIKE @SearchMetaData");

                this.SearchMetaData = true;
            }

            this.WhereSql = String.Join(" AND ", sqlWhere);
        }
    }
}
