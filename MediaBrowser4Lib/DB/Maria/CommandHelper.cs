using MySqlConnector;
using System.Data;
using System.Data.Common;

namespace MediaBrowser4.DB.Maria
{
    public class CommandHelper : ICommandHelper
    {
        private readonly IConnectionManager _connectionManager;

        public CommandHelper(IConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public IDbCommand CreateCommand(string sql)
        {
            return new MySqlCommand(sql, (MySqlConnection)_connectionManager.Connection);
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public int ExecuteNonQuery(string commandText, CommandType commandType)
        {
            throw new System.NotImplementedException();
        }

        public int ExecuteNonQuery(string commandText)
        {
            throw new System.NotImplementedException();
        }

        public DbDataReader ExecuteReader(string commandText, CommandType commandType)
        {
            throw new System.NotImplementedException();
        }

        public DbDataReader ExecuteReader(string commandText)
        {
            throw new System.NotImplementedException();
        }

        public T ExecuteScalar<T>(string commandText, CommandType commandType)
        {
            throw new System.NotImplementedException();
        }

        public object ExecuteScalar(string commandText)
        {
            throw new System.NotImplementedException();
        }

        public object ExecuteScalar(string commandText, CommandType commandType)
        {
            throw new System.NotImplementedException();
        }

        public T ExecuteScalar<T>(string commandText)
        {
            throw new System.NotImplementedException();
        }

        public DataTable GetDataTable(string commandText)
        {
            throw new System.NotImplementedException();
        }

        public DbParameter GetParameter(string parameterName, bool throwException)
        {
            throw new System.NotImplementedException();
        }

        public DbParameter GetParameter(string parameterName)
        {
            throw new System.NotImplementedException();
        }

        public T GetParameterValue<T>(string parameterName)
        {
            throw new System.NotImplementedException();
        }

        public void SetParameter(DbParameter parameter)
        {
            throw new System.NotImplementedException();
        }

        public void SetParameter(string name, string value)
        {
            throw new System.NotImplementedException();
        }

        public void SetParameter(string name, object value, DbType type)
        {
            throw new System.NotImplementedException();
        }
    }
}