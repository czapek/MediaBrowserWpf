using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MySqlConnector;

namespace MediaBrowser4.DB.Maria
{
    public class MariaDbAdministration
    {
        private const string TableOptions = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

        private readonly MariaDbConnectionOptions options;
        private readonly Action<string> progress;

        public MariaDbAdministration(MariaDbConnectionOptions options)
            : this(options, null)
        {
        }

        public MariaDbAdministration(MariaDbConnectionOptions options, Action<string> progress)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            this.options = options;
            this.progress = progress;
        }

        public void ValidateSchema()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.options.BuildConnectionString(this.options.Database)))
                {
                    connection.Open();

                    foreach (string statement in this.GetTableStatements())
                    {
                        string tableName = GetTableName(statement);

                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
                                "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";
                            command.Parameters.AddWithValue("@schema", this.options.Database);
                            command.Parameters.AddWithValue("@table", tableName);

                            if (Convert.ToInt64(command.ExecuteScalar()) == 0)
                            {
                                throw new UnvalidDBException(
                                    "Die MariaDB-Datenbank ist unvollständig. Tabelle fehlt: "
                                    + tableName + ". Starten Sie mit --update.");
                            }
                        }
                    }

                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT `VALUE` FROM DBPROPERTIES WHERE `KEY` = 'DBVersion'";
                        object version = command.ExecuteScalar();

                        if (version == null || version == DBNull.Value
                            || !String.Equals(version.ToString(), UpdateDb.DbVersion, StringComparison.Ordinal))
                        {
                            throw new UnvalidDBException(
                                "Die MariaDB-Datenbank hat eine unerwartete Version. Starten Sie mit --update.");
                        }
                    }
                }
            }
            catch (UnvalidDBException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UnvalidDBException(
                    "Die MariaDB-Datenbank konnte nicht validiert werden. Prüfen Sie die Verbindungsdaten "
                    + "und starten Sie bei einer neuen Datenbank mit --update. " + ex.Message);
            }
        }

        public void CreateDatabaseAndSchema()
        {
            this.Report("Erstelle Datenbank (falls nicht vorhanden)...");

            using (MySqlConnection connection = new MySqlConnection(this.options.BuildConnectionString(String.Empty)))
            {
                connection.Open();
                this.Execute(connection, "CREATE DATABASE IF NOT EXISTS " + QuoteIdentifier(this.options.Database)
                    + " CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            }

            using (MySqlConnection connection = new MySqlConnection(this.options.BuildConnectionString(this.options.Database)))
            {
                connection.Open();
                this.Report("Lege Tabellen an...");
                this.CreateTables(connection);
                this.Report("Pruefe Spalten und Indizes...");
                this.EnsureColumnsAndIndexes(connection);
                this.Report("Pruefe Spaltenbreiten...");
                this.EnsureColumnCapacities(connection);
                this.Report("Erstelle Trigger...");
                this.CreateTriggers(connection);
                this.Report("Pruefe Standarddaten...");
                this.Seed(connection);
            }

            this.Report("Schema-Update abgeschlossen.");
        }

        private void CreateTables(MySqlConnection connection)
        {
            List<string> statements = new List<string>(this.GetTableStatements());
            int index = 0;

            foreach (string statement in statements)
            {
                index++;
                this.Report("  Tabelle " + index + "/" + statements.Count + ": "
                    + GetTableName(statement) + "...");
                this.Execute(connection, statement);
            }
        }

        private void EnsureColumnsAndIndexes(MySqlConnection connection)
        {
            List<string> statements = new List<string>(this.GetTableStatements());
            int index = 0;

            foreach (string statement in statements)
            {
                index++;
                string tableName = GetTableName(statement);
                this.Report("  Pruefe " + index + "/" + statements.Count + ": " + tableName + "...");
                HashSet<string> existingColumns = this.GetExistingColumns(connection, tableName);
                HashSet<string> existingIndexes = this.GetExistingIndexes(connection, tableName);

                foreach (string definition in SplitSqlList(GetTableBody(statement)))
                {
                    string segment = definition.Trim();

                    if (segment.Length == 0 || segment.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (IsIndexDefinition(segment))
                    {
                        string indexName = GetIndexName(segment);

                        if (indexName != null && existingIndexes.Add(indexName))
                        {
                            this.Execute(connection, "ALTER TABLE " + QuoteIdentifier(tableName) + " ADD " + segment);
                        }
                    }
                    else
                    {
                        string columnName = GetColumnName(segment);

                        if (columnName != null && existingColumns.Add(columnName))
                        {
                            this.Execute(connection, "ALTER TABLE " + QuoteIdentifier(tableName)
                                + " ADD COLUMN " + segment);
                        }
                    }
                }
            }
        }

        private void EnsureColumnCapacities(MySqlConnection connection)
        {
            string dataType;
            long maxLength;

            if (TryGetColumnInfo(connection, "LAYERS", "EDIT", out dataType, out maxLength)
                && !IsTextType(dataType)
                && !((String.Equals(dataType, "varchar", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(dataType, "char", StringComparison.OrdinalIgnoreCase))
                    && maxLength >= 64))
            {
                this.Execute(connection,
                    "ALTER TABLE " + QuoteIdentifier("LAYERS") + " MODIFY COLUMN "
                    + QuoteIdentifier("EDIT") + " VARCHAR(64) NULL");
            }

            if (TryGetColumnInfo(connection, "METADATA", "VALUE", out dataType, out maxLength)
                && !IsTextType(dataType))
            {
                this.Execute(connection,
                    "ALTER TABLE " + QuoteIdentifier("METADATA") + " MODIFY COLUMN "
                    + QuoteIdentifier("VALUE") + " TEXT NULL");
            }
        }

        private bool TryGetColumnInfo(MySqlConnection connection, string tableName, string columnName,
            out string dataType, out long maxLength)
        {
            dataType = null;
            maxLength = 0;

            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND COLUMN_NAME = @column";
                command.Parameters.AddWithValue("@schema", this.options.Database);
                command.Parameters.AddWithValue("@table", tableName);
                command.Parameters.AddWithValue("@column", columnName);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return false;

                    dataType = reader.GetString(0);

                    if (!reader.IsDBNull(1))
                        maxLength = Convert.ToInt64(reader.GetValue(1));

                    return true;
                }
            }
        }

        private static bool IsTextType(string dataType)
        {
            return String.Equals(dataType, "tinytext", StringComparison.OrdinalIgnoreCase)
                || String.Equals(dataType, "text", StringComparison.OrdinalIgnoreCase)
                || String.Equals(dataType, "mediumtext", StringComparison.OrdinalIgnoreCase)
                || String.Equals(dataType, "longtext", StringComparison.OrdinalIgnoreCase);
        }

        private HashSet<string> GetExistingColumns(MySqlConnection connection, string tableName)
        {            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";
                command.Parameters.AddWithValue("@schema", this.options.Database);
                command.Parameters.AddWithValue("@table", tableName);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(reader.GetString(0));
                    }
                }
            }

            return result;
        }

        private HashSet<string> GetExistingIndexes(MySqlConnection connection, string tableName)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT DISTINCT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS " +
                    "WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";
                command.Parameters.AddWithValue("@schema", this.options.Database);
                command.Parameters.AddWithValue("@table", tableName);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(reader.GetString(0));
                    }
                }
            }

            return result;
        }

        private static string GetTableName(string statement)
        {
            const string prefix = "CREATE TABLE IF NOT EXISTS ";
            int start = statement.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) + prefix.Length;
            int end = start;

            while (end < statement.Length && !Char.IsWhiteSpace(statement[end]) && statement[end] != '(')
            {
                end++;
            }

            return statement.Substring(start, end - start);
        }

        private static string GetTableBody(string statement)
        {
            int start = statement.IndexOf('(');
            int end = statement.LastIndexOf(')');

            if (start < 0 || end <= start)
            {
                throw new InvalidOperationException("Ungültige CREATE TABLE-Definition.");
            }

            return statement.Substring(start + 1, end - start - 1);
        }

        private static List<string> SplitSqlList(string value)
        {
            List<string> result = new List<string>();
            StringBuilder current = new StringBuilder();
            int depth = 0;
            char quote = '\0';

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (quote != '\0')
                {
                    current.Append(character);

                    if (character == '\\' && i + 1 < value.Length)
                    {
                        current.Append(value[++i]);
                    }
                    else if (character == quote)
                    {
                        if (i + 1 < value.Length && value[i + 1] == quote)
                        {
                            current.Append(value[++i]);
                        }
                        else
                        {
                            quote = '\0';
                        }
                    }
                }
                else if (character == '`' || character == '\'' || character == '"')
                {
                    quote = character;
                    current.Append(character);
                }
                else
                {
                    if (character == '(')
                    {
                        depth++;
                    }
                    else if (character == ')')
                    {
                        depth--;
                    }

                    if (character == ',' && depth == 0)
                    {
                        result.Add(current.ToString().Trim());
                        current.Length = 0;
                    }
                    else
                    {
                        current.Append(character);
                    }
                }
            }

            if (current.ToString().Trim().Length > 0)
            {
                result.Add(current.ToString().Trim());
            }

            return result;
        }

        private static bool IsIndexDefinition(string segment)
        {
            return segment.StartsWith("KEY ", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("UNIQUE KEY ", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("INDEX ", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("UNIQUE INDEX ", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetColumnName(string definition)
        {
            string value = definition.Trim();

            if (value.Length == 0)
            {
                return null;
            }

            if (value[0] == '`')
            {
                int end = value.IndexOf('`', 1);
                return end > 1 ? value.Substring(1, end - 1).Replace("``", "`") : null;
            }

            int length = 0;

            while (length < value.Length && !Char.IsWhiteSpace(value[length]))
            {
                length++;
            }

            return length > 0 ? value.Substring(0, length) : null;
        }

        private static string GetIndexName(string definition)
        {
            string value = definition.Trim();

            if (value.StartsWith("UNIQUE KEY ", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("UNIQUE KEY ".Length).Trim();
            }
            else if (value.StartsWith("UNIQUE INDEX ", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("UNIQUE INDEX ".Length).Trim();
            }
            else if (value.StartsWith("KEY ", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("KEY ".Length).Trim();
            }
            else if (value.StartsWith("INDEX ", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("INDEX ".Length).Trim();
            }
            else
            {
                return null;
            }

            if (value.Length == 0)
            {
                return null;
            }

            if (value[0] == '`')
            {
                int end = value.IndexOf('`', 1);
                return end > 1 ? value.Substring(1, end - 1).Replace("``", "`") : null;
            }

            int length = 0;

            while (length < value.Length && !Char.IsWhiteSpace(value[length]) && value[length] != '(')
            {
                length++;
            }

            return length > 0 ? value.Substring(0, length) : null;
        }

        private IEnumerable<string> GetTableStatements()
        {
            List<string> statements = new List<string>();

            statements.Add(
                "CREATE TABLE IF NOT EXISTS DBPROPERTIES (" +
                "`KEY` VARCHAR(50) NOT NULL, " +
                "`VALUE` VARCHAR(500) NOT NULL, " +
                "PRIMARY KEY (`KEY`)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS ATTACHMENTS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "PATH VARCHAR(8000) NOT NULL, " +
                "PRIMARY KEY (ID)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS ATTACHED (" +
                "MEDIAFILES_FK BIGINT NOT NULL, " +
                "ATTACHMENTS_FK BIGINT NOT NULL, " +
                "PRIMARY KEY (MEDIAFILES_FK, ATTACHMENTS_FK), " +
                "KEY idx_attached_mediafiles (MEDIAFILES_FK), " +
                "KEY idx_attached_attachments (ATTACHMENTS_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS MEDIAFILES (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "FILENAME VARCHAR(255) NOT NULL, " +
                "SORTORDER VARCHAR(255) NOT NULL, " +
                "FOLDERS_FK BIGINT NOT NULL, " +
                "MD5VALUE CHAR(32) NOT NULL, " +
                "LENGTH BIGINT NULL, " +
                "VIEWED INT NULL, " +
                "ISDELETED TINYINT(1) NOT NULL DEFAULT 0, " +
                "DELETEDATE DATETIME(6) NULL, " +
                "ISBOOKMARKED TINYINT(1) NOT NULL DEFAULT 0, " +
                "CREATIONDATE DATETIME(6) NOT NULL, " +
                "MEDIADATE DATETIME(6) NOT NULL, " +
                "EDITDATE DATETIME(6) NOT NULL, " +
                "INSERTDATE DATETIME(6) NOT NULL, " +
                "ORIENTATION INT NULL, " +
                "`TYPE` CHAR(3) NULL, " +
                "PRIVACY INT NULL, " +
                "CURRENTVARIATION BIGINT NULL, " +
                "DESCRIPTION_FK BIGINT NULL, " +
                "DURATION DOUBLE NULL, " +
                "FRAMES INT NULL, " +
                "PRIORITY INT NULL, " +
                "ROLES_FK BIGINT NOT NULL, " +
                "HISTORYVERSION INT NOT NULL DEFAULT 0, " +
                "ISDUBLICATE TINYINT(1) NOT NULL DEFAULT 0, " +
                "WIDTH INT NULL, " +
                "HEIGHT INT NULL, " +
                "LONGITUDE DOUBLE NULL, " +
                "LATITUDE DOUBLE NULL, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_mediafiles_filename (FILENAME, FOLDERS_FK), " +
                "KEY idx_mediafiles_folders (FOLDERS_FK), " +
                "KEY idx_mediafiles_md5value (MD5VALUE), " +
                "KEY idx_mediafiles_currentvariation (CURRENTVARIATION), " +
                "KEY idx_mediafiles_isdeleted (ISDELETED), " +
                "KEY idx_mediafiles_mediadate (MEDIADATE), " +
                "KEY idx_mediafiles_description_fk (DESCRIPTION_FK), " +
                "KEY idx_mediafiles_longitude (LONGITUDE), " +
                "KEY idx_mediafiles_latitude (LATITUDE)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS VARIATIONS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "MEDIAFILES_FK BIGINT NOT NULL, " +
                "`POSITION` INT NOT NULL DEFAULT 1, " +
                "NAME VARCHAR(50) NULL, " +
                "DESCRIPTION VARCHAR(512) NULL, " +
                "THUMB LONGBLOB NULL, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_variations_mediafiles (MEDIAFILES_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS DESCRIPTION (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "`VALUE` LONGTEXT NULL, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_description_value (`VALUE`(191))" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS LAYERS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "VARIATIONS_FK BIGINT NOT NULL, " +
                "`POSITION` INT NOT NULL, " +
                "EDIT VARCHAR(64) NULL, " +
                "ACTION VARCHAR(512) NULL, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_layers_variations (VARIATIONS_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS FOLDERS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "FOLDERNAME VARCHAR(500) NOT NULL, " +
                "PRIMARY KEY (ID), " +
                "UNIQUE KEY uk_folders_foldername (FOLDERNAME)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS ROLES (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "`ROLE` CHAR(30) NOT NULL, " +
                "`PASSWORD` CHAR(32) NULL, " +
                "DESCRIPTION VARCHAR(500) NULL, " +
                "PRIMARY KEY (ID), " +
                "UNIQUE KEY uk_roles_role (`ROLE`)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS FAVORITES (" +
                "NAME VARCHAR(255) NOT NULL, " +
                "`TYPE` CHAR(3) NOT NULL, " +
                "ACCESS DATETIME(6) NOT NULL" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS CATEGORY (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "PARENT BIGINT NULL, " +
                "NAME VARCHAR(50) NOT NULL, " +
                "SORTORDER VARCHAR(50) NOT NULL, " +
                "DESCRIPTION VARCHAR(255) NULL, " +
                "ISUNIQUE TINYINT(1) NOT NULL DEFAULT 0, " +
                "ISLOCATION TINYINT(1) NOT NULL DEFAULT 0, " +
                "ISDATE TINYINT(1) NOT NULL DEFAULT 0, " +
                "LATITUDE DOUBLE NULL, " +
                "LONGITUDE DOUBLE NULL, " +
                "`DATE` DATETIME(6) NULL, " +
                "FULLPATH VARCHAR(8000) NULL, " +
                "GUID CHAR(32) NOT NULL, " +
                "PRIMARY KEY (ID), " +
                "UNIQUE KEY uk_category_guid (GUID), " +
                "KEY idx_category_fullpath (FULLPATH(191)), " +
                "KEY idx_category_parent (PARENT), " +
                "KEY idx_category_name (NAME)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS CATEGORIZE (" +
                "CATEGORY_FK BIGINT NOT NULL, " +
                "VARIATIONS_FK BIGINT NOT NULL, " +
                "USERDEFINED BOOLEAN NOT NULL DEFAULT TRUE, " +
                "PRIMARY KEY (CATEGORY_FK, VARIATIONS_FK), " +
                "KEY idx_categorize_variations (VARIATIONS_FK), " +
                "KEY idx_categorize_category (CATEGORY_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS REMOTEMAPPING (" +
                "DBGUID CHAR(32) NOT NULL, " +
                "OBJECTGUID_LOCAL CHAR(32) NOT NULL, " +
                "OBJECTGUID_REMOTE CHAR(32) NOT NULL, " +
                "`TYPE` CHAR(3) NOT NULL, " +
                "KEY idx_remotemapping_dbguid (DBGUID), " +
                "KEY idx_remotemapping_objectguid_local (OBJECTGUID_LOCAL), " +
                "KEY idx_remotemapping_objectguid_remote (OBJECTGUID_REMOTE)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS METADATA (" +
                "MEDIAFILES_FK BIGINT NOT NULL, " +
                "METANAME_FK BIGINT NOT NULL, " +
                "`VALUE` TEXT NULL, " +
                "KEY idx_metadata_mediafiles (MEDIAFILES_FK), " +
                "KEY idx_metadata_metaname (METANAME_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS METADATANAME (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "NAME VARCHAR(50) NOT NULL, " +
                "GROUPNAME VARCHAR(50) NOT NULL, " +
                "`TYPE` CHAR(4) NOT NULL, " +
                "ISVISIBLE TINYINT(1) NOT NULL DEFAULT 1, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_metadataname_name (NAME)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS EXTRADATA (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "NAME VARCHAR(255) NOT NULL, " +
                "TYP VARCHAR(3) NOT NULL, " +
                "FOLDER VARCHAR(50) NULL, " +
                "CATEGORY VARCHAR(50) NOT NULL DEFAULT 'Default', " +
                "DATA LONGBLOB NULL, " +
                "PRIMARY KEY (ID)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS EXTRADATAS (" +
                "EXTRADATA_FK BIGINT NOT NULL, " +
                "MEDIAFILES_FK BIGINT NOT NULL, " +
                "PRIMARY KEY (EXTRADATA_FK, MEDIAFILES_FK), " +
                "KEY idx_extradatas_extradata (EXTRADATA_FK), " +
                "KEY idx_extradatas_mediafiles (MEDIAFILES_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS USERDEFINEDREQUESTS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "REQUEST LONGBLOB NOT NULL, " +
                "PRIMARY KEY (ID)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS DATALOGGER_METEOROLOGY (" +
                "LOGTIME DATETIME(6) NOT NULL, " +
                "TEMPERATURE DOUBLE NOT NULL, " +
                "HUMIDITY DOUBLE NOT NULL, " +
                "PRIMARY KEY (LOGTIME)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS DATALOGGER_GPS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "FILETIME DATETIME(6) NOT NULL, " +
                "FILESIZE BIGINT NOT NULL, " +
                "UTCTIME DATETIME(6) NOT NULL, " +
                "`LOCALTIME` DATETIME(6) NOT NULL, " +
                "LONGITUDE DOUBLE NOT NULL, " +
                "LATITUDE DOUBLE NOT NULL, " +
                "ALTITUDE DOUBLE NULL, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_datalogger_gps_localtime (`LOCALTIME`), " +
                "KEY idx_datalogger_gps_filetime (FILETIME), " +
                "KEY idx_datalogger_gps_longitude (LONGITUDE), " +
                "KEY idx_datalogger_gps_latitude (LATITUDE)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS THUMBS (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "VARIATIONS_FK BIGINT NOT NULL, " +
                "PREVIEW_WIDTH INT NOT NULL DEFAULT 0, " +
                "PREVIEW_HEIGHT INT NOT NULL DEFAULT 0, " +
                "ISOUTDATED TINYINT(1) NOT NULL DEFAULT 0, " +
                "THUMB LONGBLOB NOT NULL, " +
                "EXTENSION VARCHAR(10) NOT NULL DEFAULT 'jpg', " +
                "WIDTH INT NULL, " +
                "HEIGHT INT NULL, " +
                "PRIMARY KEY (ID), " +
                "UNIQUE KEY uk_thumbs_variation_size (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT), " +
                "KEY idx_thumbs_variations (VARIATIONS_FK)" +
                ") " + TableOptions);

            statements.Add(
                "CREATE TABLE IF NOT EXISTS FACES (" +
                "ID BIGINT NOT NULL AUTO_INCREMENT, " +
                "VARIATIONS_FK BIGINT NOT NULL, " +
                "PREVIEW_WIDTH INT NOT NULL DEFAULT 0, " +
                "PREVIEW_HEIGHT INT NOT NULL DEFAULT 0, " +
                "X INT NOT NULL, " +
                "Y INT NOT NULL, " +
                "WIDTH INT NOT NULL, " +
                "HEIGHT INT NOT NULL, " +
                "REL_SIZE DOUBLE NOT NULL, " +
                "PRIMARY KEY (ID), " +
                "KEY idx_faces_variations (VARIATIONS_FK), " +
                "KEY idx_faces_rel_size (REL_SIZE)" +
                ") " + TableOptions);

            return statements;
        }

        private void CreateTriggers(MySqlConnection connection)
        {
            List<Tuple<string, string>> triggers = new List<Tuple<string, string>>
            {
                new Tuple<string, string>("trg_attachments_delete",
                    "CREATE TRIGGER trg_attachments_delete BEFORE DELETE ON ATTACHMENTS " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM ATTACHED WHERE ATTACHMENTS_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_mediafiles_delete",
                    "CREATE TRIGGER trg_mediafiles_delete BEFORE DELETE ON MEDIAFILES " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM VARIATIONS WHERE MEDIAFILES_FK = OLD.ID; " +
                    "DELETE FROM EXTRADATAS WHERE MEDIAFILES_FK = OLD.ID; " +
                    "DELETE FROM METADATA WHERE MEDIAFILES_FK = OLD.ID; " +
                    "DELETE FROM ATTACHED WHERE MEDIAFILES_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_variations_delete",
                    "CREATE TRIGGER trg_variations_delete BEFORE DELETE ON VARIATIONS " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM LAYERS WHERE VARIATIONS_FK = OLD.ID; " +
                    "DELETE FROM CATEGORIZE WHERE VARIATIONS_FK = OLD.ID; " +
                    "DELETE FROM THUMBS WHERE VARIATIONS_FK = OLD.ID; " +
                    "DELETE FROM FACES WHERE VARIATIONS_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_category_delete",
                    "CREATE TRIGGER trg_category_delete BEFORE DELETE ON CATEGORY " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM CATEGORIZE WHERE CATEGORY_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_description_delete",
                    "CREATE TRIGGER trg_description_delete BEFORE DELETE ON DESCRIPTION " +
                    "FOR EACH ROW BEGIN " +
                    "UPDATE MEDIAFILES SET DESCRIPTION_FK = NULL WHERE DESCRIPTION_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_metadataname_delete",
                    "CREATE TRIGGER trg_metadataname_delete BEFORE DELETE ON METADATANAME " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM METADATA WHERE METANAME_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_extradata_delete",
                    "CREATE TRIGGER trg_extradata_delete BEFORE DELETE ON EXTRADATA " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM EXTRADATAS WHERE EXTRADATA_FK = OLD.ID; " +
                    "END"),
                new Tuple<string, string>("trg_thumbs_delete",
                    "CREATE TRIGGER trg_thumbs_delete BEFORE DELETE ON THUMBS " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM FACES WHERE VARIATIONS_FK = OLD.VARIATIONS_FK " +
                    "AND PREVIEW_WIDTH = OLD.PREVIEW_WIDTH AND PREVIEW_HEIGHT = OLD.PREVIEW_HEIGHT; " +
                    "END"),
                new Tuple<string, string>("trg_folders_delete",
                    "CREATE TRIGGER trg_folders_delete BEFORE DELETE ON FOLDERS " +
                    "FOR EACH ROW BEGIN " +
                    "DELETE FROM MEDIAFILES WHERE FOLDERS_FK = OLD.ID; " +
                    "END")
            };

            int index = 0;

            foreach (Tuple<string, string> trigger in triggers)
            {
                index++;
                this.Report("  Trigger " + index + "/" + triggers.Count + ": " + trigger.Item1 + "...");
                this.Execute(connection, "DROP TRIGGER IF EXISTS " + QuoteIdentifier(trigger.Item1));
                this.Execute(connection, trigger.Item2);
            }
        }

        public void ApplyDefaults()
        {
            using (MySqlConnection connection = new MySqlConnection(this.options.BuildConnectionString(this.options.Database)))
            {
                connection.Open();
                this.Seed(connection);
            }
        }

        private void Seed(MySqlConnection connection)
        {
            this.SetProperty(connection, "DBVersion", UpdateDb.DbVersion);
            this.InsertPropertyIfMissing(connection, "HOST", Environment.MachineName);
            this.InsertPropertyIfMissing(connection, "USER", Environment.UserName);
            this.InsertPropertyIfMissing(connection, "GUID", System.Guid.NewGuid().ToString("N"));
            this.InsertPropertyIfMissing(connection, "FILENAME", this.options.Database);
            this.InsertPropertyIfMissing(connection, "CheckSumMaxLength", "52428800");
            this.InsertPropertyIfMissing(connection, "CommitInsertSeconds", "5");
            this.InsertPropertyIfMissing(connection, "DefaultRoleID", "1");
            this.InsertPropertyIfMissing(connection, "ThumbnailJPEGQuality", "80");
            this.InsertPropertyIfMissing(connection, "ThumbnailSize", "150");
            this.InsertPropertyIfMissing(connection, "InitialDBTreePath", String.Empty);
            this.InsertPropertyIfMissing(connection, "InitialCategoryTreePath", String.Empty);
            this.InsertPropertyIfMissing(connection, "DiaryCategorizeFolder", MediaBrowserContext.CategoryDiary);
            this.InsertPropertyIfMissing(connection, "TagFolder", "Verschlagwortung");
            this.InsertPropertyIfMissing(connection, "MissingFileBehavior", "show");
            this.InsertPropertyIfMissing(connection, "AutoCategorizeDate", "1");
            this.InsertPropertyIfMissing(connection, "DefaultMediaArchivFolder",
                MediaBrowserContext.DefaultMediaArchivRoot);
            this.InsertPropertyIfMissing(connection, "DefaultMediaTempFolder",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            this.Execute(connection, "INSERT IGNORE INTO ROLES (`ROLE`) VALUES ('PUBLIC')");
            this.Execute(connection, "INSERT IGNORE INTO ROLES (`ROLE`) VALUES ('PRIVATE')");

            if (GetCategoryCount(connection) == 0)
            {
                this.InsertCategoryIfMissing(connection, "Tagebuch", "Tagebuch", "00002",
                    "Für jeden Tag wird eine Kategorie angelegt", 1, 1, 0);
                this.InsertCategoryIfMissing(connection, "Orte", "Orte", "00001",
                    "Kategorien die geographisch zuordenbar sind", 1, 0, 1);
                this.InsertCategoryIfMissing(connection, "Verschlagwortung", "Verschlagwortung", "00000",
                    "Über die schnelle Verschlagwortung angelegte Kategorien", 0, 0, 0);
            }
        }

        private static long GetCategoryCount(MySqlConnection connection)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM CATEGORY";
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private void SetProperty(MySqlConnection connection, string key, string value)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO DBPROPERTIES (`KEY`, `VALUE`) VALUES (@key, @value) " +
                    "ON DUPLICATE KEY UPDATE `VALUE` = @value";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value ?? String.Empty);
                command.ExecuteNonQuery();
            }
        }

        private void InsertPropertyIfMissing(MySqlConnection connection, string key, string value)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO DBPROPERTIES (`KEY`, `VALUE`) " +
                    "SELECT @key, @value FROM DUAL " +
                    "WHERE NOT EXISTS (SELECT 1 FROM DBPROPERTIES WHERE `KEY` = @key)";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value ?? String.Empty);
                command.ExecuteNonQuery();
            }
        }

        private void InsertCategoryIfMissing(MySqlConnection connection, string name, string fullPath,
            string sortOrder, string description, int isUnique, int isDate, int isLocation)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "INSERT INTO CATEGORY (NAME, FULLPATH, SORTORDER, DESCRIPTION, GUID, `DATE`, " +
                    "ISUNIQUE, ISDATE, ISLOCATION, LATITUDE, LONGITUDE, PARENT) " +
                    "SELECT @name, @fullpath, @sortorder, @description, @guid, CURRENT_TIMESTAMP(6), " +
                    "@isunique, @isdate, @islocation, 0, 0, 0 FROM DUAL " +
                    "WHERE NOT EXISTS (SELECT 1 FROM CATEGORY WHERE FULLPATH = @fullpath)";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@fullpath", fullPath);
                command.Parameters.AddWithValue("@sortorder", sortOrder);
                command.Parameters.AddWithValue("@description", description);
                command.Parameters.AddWithValue("@guid", System.Guid.NewGuid().ToString("N"));
                command.Parameters.AddWithValue("@isunique", isUnique);
                command.Parameters.AddWithValue("@isdate", isDate);
                command.Parameters.AddWithValue("@islocation", isLocation);
                command.ExecuteNonQuery();
            }
        }

        private void Execute(MySqlConnection connection, string sql)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandTimeout = 120;
                command.ExecuteNonQuery();
            }
        }

        private void Report(string message)
        {
            if (this.progress != null)
            {
                this.progress(message);
            }
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "`" + (identifier ?? String.Empty).Replace("`", "``") + "`";
        }
    }
}
