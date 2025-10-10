using MySqlConnector;
using System;
using System.Data;
using System.Data.Common;
using System.Threading;

namespace MediaBrowser4.DB.Maria
{
    public class SimpleConnection : IConnectionManager
    {
        private readonly string _connectionString;
        private MySqlConnection _connection;

        public SimpleConnection(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection Connection
        {
            get
            {
                if (_connection == null)
                {
                    _connection = new MySqlConnection(_connectionString);
                }
                return _connection;
            }
        }

        DbConnection IConnectionManager.Connection => throw new NotImplementedException();

        public void Open()
        {
            int retries = 5;
            while (true)
            {
                try
                {
                    if (_connection.State != ConnectionState.Open)
                        _connection.Open();
                    return;
                }
                catch (Exception e)
                {
                    if (retries-- == 0)
                        throw new Exception("Failed to connect to the database.", e);
                    Thread.Sleep(100);
                }
            }
        }

        public void Dispose()
        {
            if (_connection == null)
                return;

            try
            {
                if (_connection.State != ConnectionState.Closed)
                    _connection.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                _connection.Dispose();
                _connection = null;
            }
        }

        public ITransaction BeginTransaction()
        {
            return new Transaction(this);
        }

        public ICommandHelper GetCommandHelper()
        {
            return new CommandHelper(this);
        }
    }
}