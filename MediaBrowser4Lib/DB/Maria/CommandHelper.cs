using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using MediaBrowser4.Utilities;
using MySqlConnector;

namespace MediaBrowser4.DB.Maria
{
    public class CommandHelper : ICommandHelper
    {
        protected MySqlCommand command;
        protected Exception lastError;
        protected IConnectionManager conManager;

        public CommandHelper(IConnectionManager connectionManager)
        {
            if (connectionManager == null)
            {
                throw new ArgumentNullException("connectionManager");
            }

            this.conManager = connectionManager;

            MySqlConnection connection = (MySqlConnection)this.conManager.Connection;
            this.command = connection.CreateCommand();
            this.command.CommandTimeout = 90;
        }

        public IDbCommand CreateCommand(string sql)
        {
            MySqlCommand result = this.command.Connection.CreateCommand();
            result.CommandText = sql;
            result.CommandTimeout = 90;
            result.Transaction = this.command.Transaction;
            return result;
        }

        public void SetParameter(DbParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            if (this.command.Parameters.Contains(parameter.ParameterName))
            {
                this.command.Parameters.Remove(this.command.Parameters[parameter.ParameterName]);
            }

            MySqlParameter mySqlParameter = new MySqlParameter(parameter.ParameterName, parameter.Value ?? DBNull.Value)
            {
                DbType = parameter.DbType
            };

            this.command.Parameters.Add(mySqlParameter);
        }

        public void SetParameter(string name, string value)
        {
            this.SetParameter(name, value, DbType.String);
        }

        public void SetParameter(string name, object value, DbType type)
        {
            string parameterName = NormalizeParameterName(name);
            MySqlParameter parameter;

            if (this.command.Parameters.Contains(parameterName))
            {
                parameter = this.command.Parameters[parameterName];

                if (parameter.DbType != type)
                {
                    Log.Info("MariaDB parameter " + parameterName + ": " + parameter.DbType
                        + " -> " + type);
                    this.command.Parameters.Remove(parameter);
                    parameter = new MySqlParameter(parameterName, type);
                    this.command.Parameters.Add(parameter);
                }
            }
            else
            {
                parameter = new MySqlParameter(parameterName, type);
                this.command.Parameters.Add(parameter);
            }

            parameter.Value = NormalizeValue(value);
        }

        public DbParameter GetParameter(string parameterName)
        {
            return this.GetParameter(parameterName, true);
        }

        public DbParameter GetParameter(string parameterName, bool throwException)
        {
            string normalizedName = NormalizeParameterName(parameterName);

            if (this.command.Parameters.Contains(normalizedName))
            {
                return this.command.Parameters[normalizedName];
            }

            if (throwException)
            {
                throw new ArgumentOutOfRangeException("parameterName",
                    String.Format("The current command has no parameter with the name '{0}'.", parameterName));
            }

            return null;
        }

        public T GetParameterValue<T>(string parameterName)
        {
            DbParameter parameter = this.GetParameter(parameterName, false);

            if (parameter == null)
            {
                return default(T);
            }

            return ConvertScalar<T>(parameter.Value);
        }

        public virtual int ExecuteNonQuery(string commandText, CommandType commandType)
        {
            this.command.CommandText = commandText;
            this.command.CommandType = commandType;
            return this.command.ExecuteNonQuery();
        }

        public virtual int ExecuteNonQuery(string commandText)
        {
            return this.ExecuteNonQuery(commandText, CommandType.Text);
        }

        public DataTable GetDataTable(string commandText)
        {
            this.command.CommandText = commandText;
            this.command.CommandType = CommandType.Text;

            using (MySqlDataAdapter adapter = new MySqlDataAdapter(this.command))
            {
                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet);
                return dataSet.Tables[0];
            }
        }

        public virtual object ExecuteScalar(string commandText, CommandType commandType)
        {
            this.command.CommandText = commandText;
            this.command.CommandType = commandType;
            return this.command.ExecuteScalar();
        }

        public virtual object ExecuteScalar(string commandText)
        {
            return this.ExecuteScalar(commandText, CommandType.Text);
        }

        public virtual T ExecuteScalar<T>(string commandText, CommandType commandType)
        {
            return ConvertScalar<T>(this.ExecuteScalar(commandText, commandType));
        }

        public virtual T ExecuteScalar<T>(string commandText)
        {
            return this.ExecuteScalar<T>(commandText, CommandType.Text);
        }

        public virtual DbDataReader ExecuteReader(string commandText, CommandType commandType)
        {
            this.command.CommandText = commandText;
            this.command.CommandType = commandType;
            return this.command.ExecuteReader();
        }

        public virtual DbDataReader ExecuteReader(string commandText)
        {
            return this.ExecuteReader(commandText, CommandType.Text);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (this.command != null)
            {
                if (this.command.Connection != null && this.command.Connection.State != ConnectionState.Closed)
                {
                    this.command.Connection.Close();
                }

                this.command.Dispose();
                this.command = null;
            }

            if (this.lastError != null)
            {
                throw this.lastError;
            }
        }

        protected static object NormalizeValue(object value)
        {
            if (value == null)
            {
                return DBNull.Value;
            }

            if (value is DateTime)
            {
                DateTime date = (DateTime)value;

                if (date == DateTime.MinValue || date.Year < 1000)
                {
                    return DBNull.Value;
                }
            }

            return value;
        }

        protected static string NormalizeParameterName(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return name.StartsWith("@", StringComparison.Ordinal) ? name : "@" + name;
        }

        protected static T ConvertScalar<T>(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return default(T);
            }

            if (value is T)
            {
                return (T)value;
            }

            Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (targetType == typeof(byte[]))
            {
                throw new InvalidCastException(
                    String.Format("Result of query is of type '{0}' and cannot be casted to '{1}'", value.GetType(), typeof(T)));
            }

            if (targetType == typeof(string))
            {
                return (T)(object)Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(bool))
            {
                return (T)(object)Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(DateTime))
            {
                if (value is string)
                {
                    return (T)(object)DateTime.Parse((string)value, CultureInfo.InvariantCulture);
                }

                return (T)(object)Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(long))
            {
                return (T)(object)Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return (T)(object)Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return (T)(object)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(decimal))
            {
                return (T)(object)Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return (T)(object)Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            try
            {
                return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                throw new InvalidCastException(
                    String.Format("Result of query is of type '{0}' and cannot be casted to '{1}'",
                        value.GetType(), typeof(T)), exception);
            }
        }
    }
}
