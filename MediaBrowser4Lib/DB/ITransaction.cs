using System;
namespace MediaBrowser4.DB
{
    public interface ITransaction :ICommandHelper, IDisposable
    {
        new int ExecuteNonQuery(string commandText, System.Data.CommandType commandType);
        new System.Data.Common.DbDataReader ExecuteReader(string commandText, System.Data.CommandType commandType);
        new T ExecuteScalar<T>(string commandText, System.Data.CommandType commandType);
        new object ExecuteScalar(string commandText, System.Data.CommandType commandType);
        void Rollback();
    }
}
