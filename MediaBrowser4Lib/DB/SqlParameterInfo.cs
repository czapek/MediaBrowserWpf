using System;
using System.Data;

namespace MediaBrowser4.DB
{
    public class SqlParameterInfo
    {
        public string ParameterName { get; set; }
        public object Value { get; set; }
        public DbType? DbType { get; set; }

        public SqlParameterInfo()
        {
        }

        public SqlParameterInfo(string parameterName)
        {
            this.ParameterName = parameterName;
        }

        public SqlParameterInfo(string parameterName, DbType dbType)
        {
            this.ParameterName = parameterName;
            this.DbType = dbType;
        }

        public SqlParameterInfo(string parameterName, object value)
        {
            this.ParameterName = parameterName;
            this.Value = value;
        }

        public SqlParameterInfo(string parameterName, object value, DbType dbType)
        {
            this.ParameterName = parameterName;
            this.Value = value;
            this.DbType = dbType;
        }

        public DbType EffectiveDbType
        {
            get
            {
                if (this.DbType.HasValue)
                {
                    return this.DbType.Value;
                }

                object value = this.Value;

                if (value == null || value == DBNull.Value)
                {
                    return System.Data.DbType.String;
                }

                if (value is DateTime)
                {
                    return System.Data.DbType.DateTime;
                }

                if (value is byte[])
                {
                    return System.Data.DbType.Binary;
                }

                if (value is bool)
                {
                    return System.Data.DbType.Boolean;
                }

                if (value is Guid)
                {
                    return System.Data.DbType.Guid;
                }

                if (value is long)
                {
                    return System.Data.DbType.Int64;
                }

                if (value is int || value is short || value is byte || value is sbyte)
                {
                    return System.Data.DbType.Int32;
                }

                if (value is decimal)
                {
                    return System.Data.DbType.Decimal;
                }

                if (value is double)
                {
                    return System.Data.DbType.Double;
                }

                if (value is float)
                {
                    return System.Data.DbType.Single;
                }

                return System.Data.DbType.String;
            }
        }
    }
}
