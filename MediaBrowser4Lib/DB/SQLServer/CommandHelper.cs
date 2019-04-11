using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Web;
using System.Collections;

namespace MediaBrowser4.DB.SqlServer
{
    /// <summary>
    /// Helper Class for a transaction. Opens a connection, creates a command
    /// and a transaction. Commit() is executed and the connection is closed when
    /// Disposed() is called. If an error occured, no more SQL commands will be 
    /// send to the SQL Server and the error will be thrown after Disposed() is called. 
    /// </summary>
    public class CommandHelper : IDisposable, MediaBrowser4.DB.ICommandHelper
    {
        #region members

        internal SqlCommand command;
        private Exception lastError;

        protected Exception LastError
        {
            get
            {
                return lastError;
            }
            set
            {
                lastError = value;
            }
        }
        internal IConnectionManager conManger = null;

        #endregion

        #region constructors and initialization
        /// <summary>
        /// Creates a new transaction using the given connection manager.
        /// </summary>
        /// <param name="conManger">Connection manager to be used for the transaction</param>
        public CommandHelper( IConnectionManager conManger )
        {
            if ( conManger != null )
            {
                this.conManger = conManger;

                SqlConnection conn = (SqlConnection)this.conManger.Connection;
                this.command = conn.CreateCommand();
                this.command.CommandTimeout = 90;
            }
            else
            {
                throw new ArgumentNullException( "conManger", "Connection manager is NULL." );
            }
        }

        #endregion

        #region parameter getting / setting

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameter"></param>
        public void SetParameter(System.Data.Common.DbParameter parameter)
        {
            if ( this.command.Parameters.Contains( parameter ) )
            {
                this.command.Parameters.Remove( parameter );
            }
            this.command.Parameters.Add( parameter );
        }

        /// <summary>
        /// Adds a new parameter of type SqlDbType.NVarChar to the transaction parameters if there is not 
        /// already a parameter with the same name and type. If there is not a parameter with the same name, 
        /// a new one will be created. In case of update if old and new parameter have different types, an 
        /// exception will be raised.
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Parameter value</param>
        public void SetParameter( string name, string value )
        {
            this.SetParameter( name, value, DbType.String );
        }

        /// <summary>
        /// Adds a new parameter to the transaction parameters if there is not already a parameter with the 
        /// same name and type. If there is not a parameter with the same name, a new one will be created.
        /// In case of update if old and new parameter have different types, an  exception will be raised.
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Parameter value</param>
        /// <param name="type">Database type of parameter</param>
        public void SetParameter(string name, object value, DbType type)
        {
            SqlParameter parameter;
            if ( this.command.Parameters.Contains( name ) )
            {
                parameter = this.command.Parameters[name];
                if ( !parameter.SqlDbType.Equals( type ) )
                    throw new FormatException( "Parameter type is different in the existing parameter: " + name );
            }
            else
            {
                parameter = this.command.Parameters.Add( new SqlParameter( name, type ) );
            }
            parameter.Value = value;
        }

        /// <summary>
        /// Returns the parameter with the given parameter name.
        /// </summary>
        /// <param name="parameterName">Parameter name for lookup</param>
        /// <returns>Found SqlParameter</returns>
        /// <exception cref="IndexOutOfRangeException">Will be thrown if a parameter with the given name does not exist.</exception>
        public System.Data.Common.DbParameter GetParameter(string parameterName)
        {
            return this.GetParameter( parameterName, true );
        }

        /// <summary>
        /// Returns the parameter with the given parameter name.
        /// </summary>
        /// <param name="parameterName">Parameter name for lookup</param>
        /// <param name="throwException">Defines if an exception will be raised if parameter does not exist. 
        /// If set to false an no parameter has been found, null will be returned.</param>
        /// <returns>Found SqlParameter</returns>
        /// <exception cref="IndexOutOfRangeException">Will be thrown if a parameter with the given name does not exist
        /// and throwException is set to true.</exception>
        public System.Data.Common.DbParameter GetParameter(string parameterName, bool throwException)
        {
            if ( this.command.Parameters.Contains( parameterName ) )
                return this.command.Parameters[parameterName];
            else if ( throwException )
                throw new ArgumentOutOfRangeException( "parameterName",
                    String.Format( "The current SqlCommand has no parameter with the name '{0}'.", parameterName ) );
            else
                return null;
        }

        /// <summary>
        /// Returns the value of the parameter with the given name.
        /// </summary>
        /// <typeparam name="T">Type of result value</typeparam>
        /// <param name="parameterName">Parameter name for lookup</param>
        /// <returns>Found parameter casted to the given type or default(T) if parameter does not exist,
        /// the parameter value is null or the parameter value is DBNull.</returns>
        /// <exception cref="InvalidCastException">Will be thrown if parameter value cannot be casted to T.</exception>
        public T GetParameterValue<T>( string parameterName )
        {
            System.Data.Common.DbParameter parameter = this.GetParameter(parameterName, false);
            if ( parameter == null || parameter.Value == null || parameter.Value == DBNull.Value )
            {
                return default( T );
            }
            else if ( parameter.Value is T )
            {
                return ( T )parameter.Value;
            }
            throw new InvalidCastException(
                String.Format( "The parameter with the name '{0}' is of type '{1}' and cannot be casted to '{2}'",
                    parameterName, parameter.Value.GetType(), typeof( T ) ) );
        }

        #endregion

        #region execution methods

        /// <summary>
        /// Executes the given SQL statement against the database and returns the number of affected rows.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="sql">A SQL Statement.</param>
        /// <returns>Number of affected rows.</returns>
        public virtual int ExecuteNonQuery( string commandText, CommandType commandType )
        {
            this.command.CommandText = commandText;
            this.command.CommandType = commandType;
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns the number of affected rows.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">A SQL Statement.</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>Number of affected rows.</returns>
        public virtual int ExecuteNonQuery( string commandText )
        {
            return this.ExecuteNonQuery( commandText, CommandType.Text );
        }

        /// <summary>
        /// Returns an filled DataView for the submitted SQL statement.
        /// </summary>
        /// <param name="sql">The SQL statement</param>
        /// <returns>A DataView object</returns>
        public DataTable GetDataTable( string commandText )
        {
            this.command.CommandText = commandText;
            SqlDataAdapter dAdapter = new SqlDataAdapter( command );
            DataSet ds = new DataSet();
            dAdapter.Fill( ds );
            return ds.Tables[0];

        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns the result object 
        /// in case of success.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">A SQL Statement.</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>An result object.</returns>
        public virtual object ExecuteScalar( string commandText, CommandType commandType )
        {
            this.command.CommandText = commandText;
            this.command.CommandType = commandType;
            return command.ExecuteScalar();
        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns the result object 
        /// in case of success.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">A SQL Statement.</param>
        /// <returns>An result object.</returns>
        public virtual object ExecuteScalar( string commandText )
        {
            return this.ExecuteScalar( commandText, CommandType.Text );
        }

        /// <summary>
        /// Sends a SQL statement to the database server using SqlCommand.ExecuteScalar.
        /// </summary>
        /// <typeparam name="T">Type of method result</typeparam>
        /// <param name="commandText">SQL statement or name of a stored procedure</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>Result of SQL query casted to T or default(T) if query result is null or DBNull.Value</returns>
        public virtual T ExecuteScalar<T>( string commandText, CommandType commandType )
        {
            object o = this.ExecuteScalar( commandText, commandType );

            if ( o == null || o == DBNull.Value )
            {
                return default( T );
            }

            if ( !( o is T ) )
            {
                throw new InvalidCastException(
                    String.Format( "Result of query is of type '{0}' and cannot be casted to '{1}'",
                        o.GetType(), typeof( T ) ) );
            }

            return ( T )o;
        }

        /// <summary>
        /// Sends a SQL statement to the database server using SqlCommand.ExecuteScalar.
        /// The given commandText will be interpreted as a SQL statement.
        /// </summary>
        /// <typeparam name="T">Type of method result</typeparam>
        /// <param name="commandText">SQL statement or name of a stored procedure</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>Result of SQL query casted to T or default(T) if query result is null or DBNull.Value</returns>
        public virtual T ExecuteScalar<T>( string commandText )
        {
            return this.ExecuteScalar<T>( commandText, CommandType.Text );
        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns a SqlDataReader with the
        /// result set in case of success.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">SQL statement or name of a stored procedure</param>
        /// <param name="commandType">Command type (text or stored procedure)</param>
        /// <returns>An result Data Reader.</returns>
        public virtual System.Data.Common.DbDataReader ExecuteReader( string commandText, CommandType commandType )
        {
            this.command.CommandText = commandText;
            this.command.CommandType = commandType;
            return this.command.ExecuteReader();
        }

        /// <summary>
        /// Executes the given SQL statement against the database and returns a SqlDataReader with the
        /// result set in case of success. The given commandText will be interpreted as a SQL statement.
        /// Statement will not be executed if an error has been occured or Rollback() has been called.
        /// </summary>
        /// <param name="commandText">SQL Statement.</param>
        /// <returns>An result Data Reader.</returns>
        public virtual System.Data.Common.DbDataReader ExecuteReader(string commandText)
        {
            return this.ExecuteReader( commandText, CommandType.Text );
        }


        #endregion

        #region rollback and disposal

        /// <summary>
        /// Closes the connection and commits the transaction if no error has occured. 
        /// Rolls back the transaction and rethrows an error, if one has ocurred.
        /// </summary>
        public void Dispose()
        {
            /*
            this.command.Connection.Close();
            this.command.Dispose();

            if ( this.LastError != null )
                throw this.LastError;
             */
            this.Dispose(true);

            GC.SuppressFinalize( this );
        }

        protected  virtual void Dispose( bool disposing)
        {
            if ( disposing )
            {

                this.command.Connection.Close();
                this.command.Dispose();

                if ( this.LastError != null )
                    throw this.LastError;
            }
        }


        #endregion
    }
}