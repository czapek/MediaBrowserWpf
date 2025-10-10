using MySqlConnector;

namespace MediaBrowser4.DB.Maria
{
    public class MariaDbProvider : DBProvider
    {
        public override IConnectionManager GetConnection()
        {
            return new SimpleConnection(GetConnectionString());
        }

        private static string GetConnectionString()
        {
            var sb = new MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Database = "mediabrowser",
                UserID = "root",
                Password = ""
            };
            return sb.ConnectionString;
        }

        public override bool DatabaseExists()
        {
            var sb = new MySqlConnectionStringBuilder(GetConnectionString());
            var databaseName = sb.Database;
            sb.Database = null;

            using (var conn = new MySqlConnection(sb.ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{databaseName}'";
                    using (var reader = cmd.ExecuteReader())
                    {
                        return reader.HasRows;
                    }
                }
            }
        }

        public override void CreateDatabase()
        {
            var sb = new MySqlConnectionStringBuilder(GetConnectionString());
            var databaseName = sb.Database;
            sb.Database = null;

            using (var conn = new MySqlConnection(sb.ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE `{databaseName}`";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public override void DropDatabase()
        {
            var sb = new MySqlConnectionStringBuilder(GetConnectionString());
            var databaseName = sb.Database;
            sb.Database = null;

            using (var conn = new MySqlConnection(sb.ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"DROP DATABASE `{databaseName}`";
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}