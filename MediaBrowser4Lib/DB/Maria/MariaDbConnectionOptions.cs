using MySqlConnector;

namespace MediaBrowser4.DB.Maria
{
    public class MariaDbConnectionOptions
    {
        public string Host { get; set; }
        public uint Port { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public MySqlSslMode SslMode { get; set; }
        public uint ConnectionTimeout { get; set; }

        public MariaDbConnectionOptions()
        {
            this.Port = 3306;
            this.SslMode = MySqlSslMode.Preferred;
            this.ConnectionTimeout = 15;
        }

        public string BuildConnectionString()
        {
            return this.BuildConnectionString(this.Database);
        }

        public string BuildConnectionString(string databaseName)
        {
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = this.Host,
                Port = this.Port,
                Database = databaseName,
                UserID = this.User,
                Password = this.Password,
                SslMode = this.SslMode,
                ConnectionTimeout = this.ConnectionTimeout,
                ConvertZeroDateTime = true,
                TreatTinyAsBoolean = true,
                AllowUserVariables = false
            };

            return builder.ConnectionString;
        }
    }
}
