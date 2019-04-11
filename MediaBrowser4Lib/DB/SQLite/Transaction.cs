using System;
using System.Data;
using System.Data.SQLite;

namespace MediaBrowser4.DB.SQLite
{
    /// <summary>
    /// Helper Class for a transaction. Opens a connection, creates a command
    /// and a transaction. Commit() is executed and the connection is closed when
    /// Disposed() is called. If an error occured, no more SQL commands will be 
    /// send to the SQL Server and the error will be thrown after Disposed() is called. 
    /// </summary>
    public sealed class Transaction : CommandHelper, IDisposable, MediaBrowser4.DB.ITransaction
    {
        #region members

        private bool doRollback;

        #endregion

        #region constructors and initialization   
        /// <summary>
        /// Creates a new transaction using the given connection manager.
        /// </summary>
        /// <param name="conManger">Connection manager to be used for the transaction</param>
        public Transaction( IConnectionManager connectionManager )
            : base( connectionManager )
        {
            this.command.Transaction = this.command.Connection.BeginTransaction();
        }

        #endregion

        #region execution methods

        /// <summary>
        /// Executes the given SQL statement against the database and returns the number of affected rows.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="sql">A SQL Statement.</param>
        /// <returns>Number of affected rows.</returns>
        public override int ExecuteNonQuery( string commandText, CommandType commandType )
        {
            try
            {
                if ( !this.doRollback )
                {
                    return base.ExecuteNonQuery( commandText, commandType );
                }
            }
            catch ( SQLiteException exc )
            {
                this.LastError = exc;
                this.Rollback();
            }
            return 0;
        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns the result object 
        /// in case of success.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">A SQL Statement.</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>An result object.</returns>
        public override object ExecuteScalar( string commandText, CommandType commandType )
        {
            try
            {
                if ( !this.doRollback )
                {
                    return base.ExecuteScalar( commandText, commandType );
                }
            }
            catch (SQLiteException exc)
            {
                this.LastError = exc;
                this.Rollback();
            }

            return System.DBNull.Value;
        }

        /// <summary>
        /// Sends a SQL statement to the database server using SqlCommand.ExecuteScalar.
        /// </summary>
        /// <typeparam name="T">Type of method result</typeparam>
        /// <param name="commandText">SQL statement or name of a stored procedure</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>Result of SQL query casted to T or default(T) if query result is null or DBNull.Value</returns>
        public override T ExecuteScalar<T>( string commandText, CommandType commandType )
        {
            try
            {
                if ( !this.doRollback )
                {
                    return base.ExecuteScalar<T>( commandText, commandType );
                }
            }
            catch (SQLiteException exc)
            {
                this.LastError = exc;
                this.Rollback();
            }
            return default( T );
        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns a SqlDataReader with the
        /// result set in case of success.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">SQL statement or name of a stored procedure</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>An result Data Reader.</returns>
        public override System.Data.Common.DbDataReader ExecuteReader(string commandText, CommandType commandType)
        {
            try
            {
                if ( !this.doRollback )
                {
                    return base.ExecuteReader( commandText, commandType );
                }
            }
            catch (SQLiteException ex)
            {
                this.LastError = ex;
                this.Rollback();
            }
            return null;
        }
        #endregion

        #region rollback and disposal

        /// <summary>
        /// Sets a flag to rollback the transaction when the current object will be disposed.
        /// After Rollback() has been called, no other statements will be executed against the database.
        /// </summary>
        public void Rollback()
        {
            this.doRollback = true;
        }

        /// <summary>
        /// Closes the connection and commits the transaction if no error has occured. 
        /// Rolls back the transaction and rethrows an error, if one has ocurred.
        /// </summary>
        //public override void Dispose()       {


        protected override void Dispose( bool doDispose )
        {
            if ( !this.doRollback )
            {
                this.command.Transaction.Commit();
            }
            else
            {
                try
                {
                    if ( this.command != null && this.command.Transaction != null )
                        this.command.Transaction.Rollback();
                }
                catch ( System.InvalidOperationException )
                {
                }
            }


            base.Dispose(doDispose);
        }

        #endregion
    }
}
