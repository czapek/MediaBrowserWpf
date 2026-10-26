using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using MySqlConnector;

namespace MediaBrowser4.DB.Maria
{
    public class SimpleConnection : IConnectionManager
    {
        private readonly string connectionString;
        private MySqlConnection connection;

        public SimpleConnection(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public DbConnection Connection
        {
            get
            {
                if (this.connection == null)
                {
                    this.connection = new MySqlConnection(this.connectionString);
                }

                if (this.connection.State != ConnectionState.Open)
                {
                    this.connection.Open();
                }

                return this.connection;
            }
        }

        public void Open()
        {
            int retries = 5;

            while (true)
            {
                try
                {
                    this.Connection.ToString();
                    return;
                }
                catch (Exception exception)
                {
                    if (retries-- == 0)
                    {
                        throw new Exception("Failed to connect to the MariaDB database.", exception);
                    }

                    Thread.Sleep(100);
                }
            }
        }

        public void Dispose()
        {
            if (this.connection == null)
            {
                return;
            }

            try
            {
                if (this.connection.State != ConnectionState.Closed)
                {
                    this.connection.Close();
                }
            }
            finally
            {
                this.connection.Dispose();
                this.connection = null;
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
