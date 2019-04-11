using System;
namespace MediaBrowser4.DB
{
    public interface ICommandHelper : IDisposable
    {
        new void Dispose();
        int ExecuteNonQuery(string commandText, System.Data.CommandType commandType);
        int ExecuteNonQuery(string commandText);
        System.Data.Common.DbDataReader ExecuteReader(string commandText, System.Data.CommandType commandType);
        System.Data.Common.DbDataReader ExecuteReader(string commandText);
        T ExecuteScalar<T>(string commandText, System.Data.CommandType commandType);
        object ExecuteScalar(string commandText);
        object ExecuteScalar(string commandText, System.Data.CommandType commandType);
        T ExecuteScalar<T>(string commandText);
        System.Data.DataTable GetDataTable(string commandText);
        System.Data.Common.DbParameter GetParameter(string parameterName, bool throwException);
        System.Data.Common.DbParameter GetParameter(string parameterName);
        T GetParameterValue<T>(string parameterName);
        void SetParameter(System.Data.Common.DbParameter parameter);
        void SetParameter(string name, string value);
        void SetParameter(string name, object value, System.Data.DbType type);
    }
}
