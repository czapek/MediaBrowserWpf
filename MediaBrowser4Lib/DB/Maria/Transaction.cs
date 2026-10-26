using System;
using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace MediaBrowser4.DB.Maria
{
    public sealed class Transaction : CommandHelper, ITransaction
    {
        private bool doRollback;

        public Transaction(IConnectionManager connectionManager)
            : base(connectionManager)
        {
            this.command.Transaction = this.command.Connection.BeginTransaction();
        }

        public override int ExecuteNonQuery(string commandText, CommandType commandType)
        {
            try
            {
                if (!this.doRollback)
                {
                    return base.ExecuteNonQuery(commandText, commandType);
                }
            }
            catch (MySqlException exception)
            {
                this.lastError = exception;
                this.Rollback();
            }

            return 0;
        }

        public override object ExecuteScalar(string commandText, CommandType commandType)
        {
            try
            {
                if (!this.doRollback)
                {
                    return base.ExecuteScalar(commandText, commandType);
                }
            }
            catch (MySqlException exception)
            {
                this.lastError = exception;
                this.Rollback();
            }

            return DBNull.Value;
        }

        public override T ExecuteScalar<T>(string commandText, CommandType commandType)
        {
            try
            {
                if (!this.doRollback)
                {
                    return base.ExecuteScalar<T>(commandText, commandType);
                }
            }
            catch (MySqlException exception)
            {
                this.lastError = exception;
                this.Rollback();
            }

            return default(T);
        }

        public override DbDataReader ExecuteReader(string commandText, CommandType commandType)
        {
            try
            {
                if (!this.doRollback)
                {
                    return base.ExecuteReader(commandText, commandType);
                }
            }
            catch (MySqlException exception)
            {
                this.lastError = exception;
                this.Rollback();
            }

            return null;
        }

        public void Rollback()
        {
            this.doRollback = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(false);
                return;
            }

            if (this.command != null && this.command.Transaction != null)
            {
                try
                {
                    if (this.doRollback || this.lastError != null)
                    {
                        this.command.Transaction.Rollback();
                    }
                    else
                    {
                        this.command.Transaction.Commit();
                    }
                }
                catch (Exception exception)
                {
                    if (this.lastError == null)
                    {
                        this.lastError = exception;
                    }
                }
            }

            base.Dispose(true);
        }
    }
}
