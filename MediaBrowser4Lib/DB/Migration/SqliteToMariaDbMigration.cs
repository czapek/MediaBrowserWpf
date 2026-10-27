using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser4.DB.Maria;
using MySqlConnector;

namespace MediaBrowser4.DB.Migration
{
    public sealed class MigrationProgress
    {
        public string TableName { get; set; }
        public string Message { get; set; }
        public long RowsProcessed { get; set; }
        public long TotalRows { get; set; }
        public int TableIndex { get; set; }
        public int TableCount { get; set; }
    }

    public sealed class MigrationPreflight
    {
        public string SourceMainPath { get; internal set; }
        public string SourceThumbnailPath { get; internal set; }
        public string TargetHost { get; internal set; }
        public string TargetUser { get; internal set; }
        public string TargetDatabase { get; internal set; }
        public long TotalRows { get; internal set; }
        public int NormalizedDates { get; internal set; }
        public Dictionary<string, long> SourceCounts { get; internal set; }
        public Dictionary<string, long> TargetCounts { get; internal set; }
        public List<string> Warnings { get; internal set; }

        internal List<MigrationTable> Tables { get; set; }
        internal Dictionary<string, string> TargetProperties { get; set; }
        internal string TargetTagFolder { get; set; }
    }

    public sealed class MigrationResult
    {
        public long TotalRows { get; internal set; }
        public int NormalizedDates { get; internal set; }
        public TimeSpan Elapsed { get; internal set; }
        public Dictionary<string, long> SourceCounts { get; internal set; }
        public Dictionary<string, long> TargetCounts { get; internal set; }
        public List<string> Warnings { get; internal set; }
    }

    internal sealed class MigrationTable
    {
        public MigrationTable(string name, bool useThumbnailDatabase, string sourceSelect,
            string[] targetColumns, string[] requiredSourceColumns, bool allowExtraTargetRows = false,
            int fallbackBatchSize = 500)
        {
            this.Name = name;
            this.UseThumbnailDatabase = useThumbnailDatabase;
            this.SourceSelect = sourceSelect;
            this.TargetColumns = targetColumns;
            this.RequiredSourceColumns = requiredSourceColumns;
            this.AllowExtraTargetRows = allowExtraTargetRows;
            this.FallbackBatchSize = fallbackBatchSize;
        }

        public string Name { get; private set; }
        public bool UseThumbnailDatabase { get; private set; }
        public string SourceSelect { get; private set; }
        public string[] TargetColumns { get; private set; }
        public string[] RequiredSourceColumns { get; private set; }
        public bool AllowExtraTargetRows { get; private set; }
        public int FallbackBatchSize { get; private set; }
        public long SourceRows { get; set; }
    }

    public sealed class SqliteToMariaDbMigration
    {
        private const long BulkCopyMinPacketSize = 16L * 1024 * 1024;

        private static readonly string[] TargetTables =
        {
            "DBPROPERTIES", "ATTACHMENTS", "ATTACHED", "MEDIAFILES", "VARIATIONS", "DESCRIPTION",
            "LAYERS", "FOLDERS", "ROLES", "FAVORITES", "CATEGORY", "CATEGORIZE", "REMOTEMAPPING",
            "METADATA", "METADATANAME", "EXTRADATA", "EXTRADATAS", "USERDEFINEDREQUESTS",
            "DATALOGGER_METEOROLOGY", "DATALOGGER_GPS", "THUMBS", "FACES"
        };

        private static readonly string[] AutoIncrementTables =
        {
            "ATTACHMENTS", "CATEGORY", "DESCRIPTION", "EXTRADATA", "FOLDERS", "MEDIAFILES",
            "METADATANAME", "ROLES", "USERDEFINEDREQUESTS", "VARIATIONS"
        };

        private readonly MariaDbConnectionOptions targetOptions;
        private readonly string sourceMainPath;
        private readonly string sourceThumbnailPath;

        public SqliteToMariaDbMigration(MariaDbConnectionOptions targetOptions, string sourceMainPath)
            : this(targetOptions, sourceMainPath, FindThumbnailDatabase(sourceMainPath))
        {
        }

        public SqliteToMariaDbMigration(MariaDbConnectionOptions targetOptions, string sourceMainPath,
            string sourceThumbnailPath)
        {
            if (targetOptions == null)
                throw new ArgumentNullException("targetOptions");

            if (String.IsNullOrWhiteSpace(sourceMainPath))
                throw new ArgumentException("Der Pfad zur SQLite-Datenbank fehlt.", "sourceMainPath");

            if (String.IsNullOrWhiteSpace(sourceThumbnailPath))
                throw new ArgumentException("Der Pfad zur Thumbnail-Datenbank fehlt.", "sourceThumbnailPath");

            this.targetOptions = new MariaDbConnectionOptions
            {
                Host = targetOptions.Host,
                Port = targetOptions.Port,
                Database = targetOptions.Database,
                User = targetOptions.User,
                Password = targetOptions.Password,
                SslMode = targetOptions.SslMode,
                ConnectionTimeout = targetOptions.ConnectionTimeout,
                AllowLoadLocalInfile = true
            };

            this.sourceMainPath = Path.GetFullPath(sourceMainPath);
            this.sourceThumbnailPath = Path.GetFullPath(sourceThumbnailPath);
        }

        public static string FindThumbnailDatabase(string sourceMainPath)
        {
            if (String.IsNullOrWhiteSpace(sourceMainPath))
                throw new ArgumentException("Der Pfad zur SQLite-Datenbank fehlt.", "sourceMainPath");

            string mainPath = Path.GetFullPath(sourceMainPath);
            string directory = Path.GetDirectoryName(mainPath);

            if (String.IsNullOrEmpty(directory))
                directory = Environment.CurrentDirectory;

            string baseName = Path.GetFileNameWithoutExtension(mainPath);
            string[] candidates = Directory.GetFiles(directory, baseName + ".*.mb4t");

            if (candidates.Length == 0)
            {
                throw new FileNotFoundException(
                    "Die Thumbnail-Datenbank zur SQLite-Datenbank wurde nicht gefunden: "
                    + Path.Combine(directory, baseName + ".150.mb4t"));
            }

            string preferred = candidates.FirstOrDefault(x =>
                String.Equals(Path.GetFileName(x), baseName + ".150.mb4t", StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                return preferred;

            if (candidates.Length == 1)
                return candidates[0];

            throw new InvalidOperationException(
                "Es wurden mehrere Thumbnail-Datenbanken gefunden. Bitte die 150er-Datenbank verwenden:"
                + Environment.NewLine + String.Join(Environment.NewLine, candidates));
        }

        public MigrationPreflight Preflight()
        {
            if (!File.Exists(this.sourceMainPath))
                throw new FileNotFoundException("SQLite-Datenbank nicht gefunden.", this.sourceMainPath);

            if (!File.Exists(this.sourceThumbnailPath))
                throw new FileNotFoundException("Thumbnail-Datenbank nicht gefunden.", this.sourceThumbnailPath);

            List<MigrationTable> tables = BuildTables();
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            Dictionary<string, long> sourceCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            using (SQLiteConnection source = OpenSource(this.sourceMainPath))
            using (SQLiteConnection thumbs = OpenSource(this.sourceThumbnailPath))
            {
                PrepareThumbnailValidation(source, thumbs);
                ValidateSourceVersion(source, errors);
                ValidateSourceSchema(source, thumbs, tables, errors);
                ValidateConstraints(source, thumbs, errors, warnings);

                foreach (MigrationTable table in tables)
                {
                    SQLiteConnection connection = table.UseThumbnailDatabase ? thumbs : source;
                    long rawRows = Count(connection, "SELECT COUNT(*) FROM " + QuoteSqlite(table.Name));
                    table.SourceRows = Count(connection,
                        "SELECT COUNT(*) FROM (" + table.SourceSelect + ") AS filtered_source");
                    sourceCounts[table.Name] = table.SourceRows;

                    if (rawRows > table.SourceRows)
                    {
                        warnings.Add(GetOrphanLabel(table.Name) + " werden nicht übernommen: "
                            + (rawRows - table.SourceRows));
                    }
                }

                sourceCounts["DBPROPERTIES"] = Count(source, "SELECT COUNT(*) FROM DBPROPERTIES");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Die Migration kann nicht gestartet werden:" + Environment.NewLine
                    + String.Join(Environment.NewLine, errors));
            }

            Dictionary<string, long> targetCounts;
            Dictionary<string, string> targetProperties;

            using (MySqlConnection target = OpenTarget())
            {
                ValidateTargetSchema(target, tables, errors);
                targetCounts = ReadTargetCounts(target);
                targetProperties = ReadTargetProperties(target);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Die Migration kann nicht gestartet werden:" + Environment.NewLine
                    + String.Join(Environment.NewLine, errors));
            }

            string targetHost = GetProperty(targetProperties, "HOST");
            string targetUser = GetProperty(targetProperties, "USER");
            string targetFile = GetProperty(targetProperties, "FILENAME");
            string targetTagFolder = GetProperty(targetProperties, "TagFolder");

            if (String.IsNullOrWhiteSpace(targetHost))
                targetHost = this.targetOptions.Host;

            if (String.IsNullOrWhiteSpace(targetUser))
                targetUser = this.targetOptions.User;

            if (String.IsNullOrWhiteSpace(targetFile))
                targetFile = this.targetOptions.Database;

            int normalizedDates = CountInvalidDates(sourceMainPath, "CATEGORY", "DATE");

            long totalRows = tables.Sum(x => x.SourceRows);

            return new MigrationPreflight
            {
                SourceMainPath = this.sourceMainPath,
                SourceThumbnailPath = this.sourceThumbnailPath,
                TargetHost = targetHost,
                TargetUser = targetUser,
                TargetDatabase = this.targetOptions.Database,
                TotalRows = totalRows,
                NormalizedDates = normalizedDates,
                SourceCounts = sourceCounts,
                TargetCounts = targetCounts,
                Warnings = warnings,
                Tables = tables,
                TargetProperties = targetProperties,
                TargetTagFolder = targetTagFolder
            };
        }

        public async Task<MigrationResult> MigrateAsync(MigrationPreflight preflight,
            IProgress<MigrationProgress> progress, CancellationToken cancellationToken)
        {
            if (preflight == null)
                throw new ArgumentNullException("preflight");

            Stopwatch stopwatch = Stopwatch.StartNew();
            MigrationResult result = new MigrationResult
            {
                SourceCounts = new Dictionary<string, long>(preflight.SourceCounts, StringComparer.OrdinalIgnoreCase),
                NormalizedDates = preflight.NormalizedDates,
                Warnings = new List<string>(preflight.Warnings)
            };

            using (MySqlConnection target = OpenTarget())
            {
                using (SQLiteConnection source = OpenSource(this.sourceMainPath))
                using (SQLiteConnection thumbs = OpenSource(this.sourceThumbnailPath))
                {
                    PrepareThumbnailValidation(source, thumbs);
                    long maxPacketBytes = ReadMaxAllowedPacket(target);

                    if (maxPacketBytes < BulkCopyMinPacketSize)
                    {
                        Report(progress,
                            "max_allowed_packet=" + (maxPacketBytes / (1024 * 1024)) + " MB: "
                            + "verwende Einzelinserts statt Bulk-Import.",
                            0, preflight.TotalRows);
                    }

                    Report(progress, "Leere Zieldatenbank...", 0, preflight.TotalRows);
                    TruncateTarget(target);

                    long completedRows = 0;
                    int tableIndex = 0;

                    foreach (MigrationTable table in preflight.Tables)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        tableIndex++;

                        Report(progress, "Migriere " + table.Name + "...", completedRows, preflight.TotalRows,
                            table.Name, tableIndex, preflight.Tables.Count);

                        SQLiteConnection sourceConnection = table.UseThumbnailDatabase ? thumbs : source;

                        List<string> tableWarnings = await MigrateTableAsync(target, sourceConnection, table,
                            completedRows, preflight.TotalRows, tableIndex, preflight.Tables.Count,
                            maxPacketBytes, progress, cancellationToken);

                        result.Warnings.AddRange(tableWarnings);
                        completedRows += table.SourceRows;

                        Report(progress, table.Name + " abgeschlossen.", completedRows, preflight.TotalRows,
                            table.Name, tableIndex, preflight.Tables.Count);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Report(progress, "Übernehme Datenbankeigenschaften...", completedRows, preflight.TotalRows);
                    MergeProperties(target, source, preflight);

                    cancellationToken.ThrowIfCancellationRequested();
                    new MariaDbAdministration(this.targetOptions).ApplyDefaults();

                    RepairAutoIncrement(target);
                    result.TargetCounts = ReadTargetCounts(target);
                    ValidateResult(preflight, result);
                }
            }

            stopwatch.Stop();
            result.Elapsed = stopwatch.Elapsed;
            return result;
        }

        private async Task<List<string>> MigrateTableAsync(MySqlConnection target, SQLiteConnection source,
            MigrationTable table, long completedRows, long totalRows, int tableIndex, int tableCount,
            long maxPacketBytes, IProgress<MigrationProgress> progress, CancellationToken cancellationToken)
        {
            if (maxPacketBytes < BulkCopyMinPacketSize)
            {
                Report(progress, table.Name + ": Einzelinserts...", completedRows, totalRows,
                    table.Name, tableIndex, tableCount);

                return FallbackLoadTable(target, source, table, completedRows, totalRows,
                    tableIndex, tableCount, maxPacketBytes, progress, cancellationToken);
            }

            try
            {
                return await BulkLoadTableAsync(target, source, table, completedRows, totalRows,
                    tableIndex, tableCount, progress, cancellationToken);
            }
            catch (MySqlException ex)
            {
                bool canFallback = IsLocalInfileError(ex)
                    || target.State != ConnectionState.Open;

                if (!canFallback)
                    throw;

                if (target.State != ConnectionState.Open)
                    target.Open();

                Report(progress, table.Name + ": Bulk-Import nicht möglich, verwende Einzelinserts...",
                    completedRows, totalRows, table.Name, tableIndex, tableCount);

                return FallbackLoadTable(target, source, table, completedRows, totalRows,
                    tableIndex, tableCount, maxPacketBytes, progress, cancellationToken);
            }
        }

        private async Task<List<string>> BulkLoadTableAsync(MySqlConnection target, SQLiteConnection source,
            MigrationTable table, long completedRows, long totalRows, int tableIndex, int tableCount,
            IProgress<MigrationProgress> progress, CancellationToken cancellationToken)
        {
            List<string> warnings = new List<string>();

            using (SQLiteCommand command = source.CreateCommand())
            {
                command.CommandText = table.SourceSelect;
                command.CommandTimeout = 0;

                using (SQLiteDataReader reader = command.ExecuteReader())
                using (MySqlTransaction transaction = target.BeginTransaction())
                {
                    try
                    {
                        MySqlBulkCopy bulkCopy = new MySqlBulkCopy(target, transaction)
                        {
                            DestinationTableName = QuoteMySql(table.Name),
                            BulkCopyTimeout = 0,
                            NotifyAfter = 10000
                        };

                        for (int i = 0; i < table.TargetColumns.Length; i++)
                        {
                            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping
                            {
                                SourceOrdinal = i,
                                DestinationColumn = table.TargetColumns[i]
                            });
                        }

                        bulkCopy.MySqlRowsCopied += (sender, args) =>
                        {
                            long rows = completedRows + Math.Min(args.RowsCopied, table.SourceRows);
                            Report(progress, "Migriere " + table.Name + "...", rows, totalRows,
                                table.Name, tableIndex, tableCount);
                        };

                        MySqlBulkCopyResult copyResult =
                            await bulkCopy.WriteToServerAsync(reader, cancellationToken).AsTask();

                        if (copyResult.Warnings != null && copyResult.Warnings.Count > 0)
                        {
                            foreach (MySqlError warning in copyResult.Warnings)
                            {
                                warnings.Add(table.Name + ": " + warning.Message);
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch
                        {
                        }

                        throw;
                    }
                }
            }

            return warnings;
        }

        private List<string> FallbackLoadTable(MySqlConnection target, SQLiteConnection source,
            MigrationTable table, long completedRows, long totalRows, int tableIndex, int tableCount,
            long maxPacketBytes, IProgress<MigrationProgress> progress, CancellationToken cancellationToken)
        {
            List<string> warnings = new List<string>();
            long processedRows = 0;
            long batchBudget = Math.Max(16 * 1024, Math.Min(maxPacketBytes / 2, 4 * 1024 * 1024));

            using (SQLiteCommand command = source.CreateCommand())
            {
                command.CommandText = table.SourceSelect;
                command.CommandTimeout = 0;

                using (SQLiteDataReader reader = command.ExecuteReader())
                using (MySqlTransaction transaction = target.BeginTransaction())
                {
                    try
                    {
                        List<object[]> batch = new List<object[]>(table.FallbackBatchSize);
                        int fieldCount = reader.FieldCount;
                        int parameterIndex = 0;
                        long batchBytes = 0;

                        while (reader.Read())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            object[] values = new object[fieldCount];

                            for (int i = 0; i < fieldCount; i++)
                            {
                                values[i] = NormalizeValue(reader.GetValue(i));
                            }

                            long rowBytes = EstimateRowSize(values);

                            if (batch.Count >= table.FallbackBatchSize
                                || (batch.Count > 0 && batchBytes + rowBytes > batchBudget))
                            {
                                parameterIndex = ExecuteBatch(target, transaction, table, batch, parameterIndex);
                                processedRows += batch.Count;
                                batch.Clear();
                                batchBytes = 0;

                                Report(progress, "Migriere " + table.Name + "...",
                                    completedRows + processedRows, totalRows, table.Name, tableIndex, tableCount);
                            }

                            batch.Add(values);
                            batchBytes += rowBytes;
                        }

                        if (batch.Count > 0)
                        {
                            ExecuteBatch(target, transaction, table, batch, parameterIndex);
                            processedRows += batch.Count;

                            Report(progress, "Migriere " + table.Name + "...",
                                completedRows + processedRows, totalRows, table.Name, tableIndex, tableCount);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch
                        {
                        }

                        throw;
                    }
                }
            }

            return warnings;
        }

        private static long EstimateRowSize(object[] values)
        {
            long size = 64;

            foreach (object value in values)
            {
                if (value == null || value == DBNull.Value)
                {
                    size += 8;
                }
                else if (value is byte[])
                {
                    size += ((byte[])value).LongLength + 16;
                }
                else if (value is string)
                {
                    size += Encoding.UTF8.GetByteCount((string)value) + 16;
                }
                else if (value is DateTime)
                {
                    size += 32;
                }
                else
                {
                    size += 24;
                }
            }

            return size;
        }

        private static int ExecuteBatch(MySqlConnection target, MySqlTransaction transaction,
            MigrationTable table, List<object[]> rows, int parameterIndex)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("INSERT INTO ");
            sql.Append(QuoteMySql(table.Name));
            sql.Append(" (");
            sql.Append(String.Join(", ", table.TargetColumns.Select(QuoteMySql)));
            sql.Append(") VALUES ");

            using (MySqlCommand command = target.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandTimeout = 0;

                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    if (rowIndex > 0)
                        sql.Append(", ");

                    sql.Append("(");

                    for (int columnIndex = 0; columnIndex < table.TargetColumns.Length; columnIndex++)
                    {
                        if (columnIndex > 0)
                            sql.Append(", ");

                        string parameterName = "@p" + parameterIndex++;
                        sql.Append(parameterName);
                        command.Parameters.AddWithValue(parameterName, rows[rowIndex][columnIndex]);
                    }

                    sql.Append(")");
                }

                command.CommandText = sql.ToString();
                command.ExecuteNonQuery();
            }

            return parameterIndex;
        }

        private static object NormalizeValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return DBNull.Value;

            if (value is DateTime)
            {
                DateTime date = (DateTime)value;

                if (date.Year < 1000)
                    return DBNull.Value;

                return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            }

            return value;
        }

        private void MergeProperties(MySqlConnection target, SQLiteConnection source,
            MigrationPreflight preflight)
        {
            Dictionary<string, string> sourceProperties = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            using (SQLiteCommand command = source.CreateCommand())
            {
                command.CommandText = "SELECT [KEY], [VALUE] FROM DBPROPERTIES";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sourceProperties[reader.GetString(0)] = reader.IsDBNull(1)
                            ? String.Empty
                            : reader.GetString(1);
                    }
                }
            }

            using (MySqlTransaction transaction = target.BeginTransaction())
            {
                foreach (KeyValuePair<string, string> property in sourceProperties)
                {
                    SetTargetProperty(target, transaction, property.Key, property.Value);
                }

                SetTargetProperty(target, transaction, "DBVersion", UpdateDb.DbVersion);
                SetTargetProperty(target, transaction, "HOST", preflight.TargetHost);
                SetTargetProperty(target, transaction, "USER", preflight.TargetUser);
                SetTargetProperty(target, transaction, "FILENAME", preflight.TargetDatabase);

                if (!String.IsNullOrWhiteSpace(preflight.TargetTagFolder))
                {
                    SetTargetProperty(target, transaction, "TagFolder", preflight.TargetTagFolder);
                }
                else if (!sourceProperties.ContainsKey("TagFolder"))
                {
                    SetTargetProperty(target, transaction, "TagFolder", "Verschlagwortung");
                }

                transaction.Commit();
            }
        }

        private static void SetTargetProperty(MySqlConnection target, MySqlTransaction transaction,
            string key, string value)
        {
            using (MySqlCommand command = target.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO DBPROPERTIES (`KEY`, `VALUE`) VALUES (@key, @value) " +
                    "ON DUPLICATE KEY UPDATE `VALUE` = @value";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value ?? String.Empty);
                command.ExecuteNonQuery();
            }
        }

        private static void RepairAutoIncrement(MySqlConnection target)
        {
            foreach (string table in AutoIncrementTables)
            {
                long nextId = 1;

                using (MySqlCommand command = target.CreateCommand())
                {
                    command.CommandText = "SELECT COALESCE(MAX(ID), 0) + 1 FROM " + QuoteMySql(table);
                    command.CommandTimeout = 0;
                    nextId = Convert.ToInt64(command.ExecuteScalar());
                }

                using (MySqlCommand command = target.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE " + QuoteMySql(table) + " AUTO_INCREMENT = " + nextId;
                    command.CommandTimeout = 0;
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void ValidateResult(MigrationPreflight preflight, MigrationResult result)
        {
            List<string> errors = new List<string>();

            foreach (MigrationTable table in preflight.Tables)
            {
                long targetRows;

                if (!result.TargetCounts.TryGetValue(table.Name, out targetRows))
                {
                    errors.Add("Zieltabelle fehlt: " + table.Name);
                    continue;
                }

                bool valid = table.AllowExtraTargetRows
                    ? targetRows >= table.SourceRows
                    : targetRows == table.SourceRows;

                if (!valid)
                {
                    errors.Add(table.Name + ": Quelle " + table.SourceRows + ", Ziel " + targetRows);
                }
            }

            if (!result.TargetCounts.ContainsKey("FACES") || result.TargetCounts["FACES"] != 0)
                errors.Add("FACES muss leer bleiben.");

            long sourceProperties;

            if (!preflight.SourceCounts.TryGetValue("DBPROPERTIES", out sourceProperties))
                sourceProperties = 0;

            long targetProperties;

            if (!result.TargetCounts.TryGetValue("DBPROPERTIES", out targetProperties)
                || targetProperties < sourceProperties)
            {
                errors.Add("DBPROPERTIES wurde nicht vollständig übernommen.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Die Migration wurde ausgeführt, aber die Nachprüfung ist fehlgeschlagen:"
                    + Environment.NewLine + String.Join(Environment.NewLine, errors));
            }
        }

        private static void TruncateTarget(MySqlConnection target)
        {
            ExecuteTarget(target, "SET FOREIGN_KEY_CHECKS=0");

            try
            {
                foreach (string table in TargetTables)
                {
                    ExecuteTarget(target, "TRUNCATE TABLE " + QuoteMySql(table));
                }
            }
            finally
            {
                ExecuteTarget(target, "SET FOREIGN_KEY_CHECKS=1");
            }
        }

        private static void ExecuteTarget(MySqlConnection target, string sql)
        {
            using (MySqlCommand command = target.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandTimeout = 0;
                command.ExecuteNonQuery();
            }
        }

        private static void ValidateSourceVersion(SQLiteConnection source, List<string> errors)
        {
            string version = null;

            using (SQLiteCommand command = source.CreateCommand())
            {
                command.CommandText = "SELECT [VALUE] FROM DBPROPERTIES WHERE [KEY] = 'DBVersion'";
                object value = command.ExecuteScalar();
                version = value == null || value == DBNull.Value ? null : value.ToString();
            }

            if (String.IsNullOrWhiteSpace(version))
            {
                errors.Add("Die SQLite-Datenbank enthält keine DBVersion.");
                return;
            }

            Version parsedVersion;

            if (Version.TryParse(version, out parsedVersion) && parsedVersion > new Version(5, 0))
            {
                errors.Add("Die SQLite-DBVersion " + version + " wird nicht unterstützt.");
            }
        }

        private static void ValidateSourceSchema(SQLiteConnection source, SQLiteConnection thumbs,
            List<MigrationTable> tables, List<string> errors)
        {
            HashSet<string> sourceTables = ReadSqliteTables(source);
            HashSet<string> thumbTables = ReadSqliteTables(thumbs);

            if (!sourceTables.Contains("DBPROPERTIES"))
                errors.Add("Quelltabelle fehlt: DBPROPERTIES");

            if (!thumbTables.Contains("THUMBS"))
                errors.Add("Thumbnail-Tabelle fehlt: THUMBS");

            foreach (MigrationTable table in tables)
            {
                SQLiteConnection connection = table.UseThumbnailDatabase ? thumbs : source;
                HashSet<string> tablesInDb = table.UseThumbnailDatabase ? thumbTables : sourceTables;

                if (!tablesInDb.Contains(table.Name))
                {
                    errors.Add("Quelltabelle fehlt: " + table.Name);
                    continue;
                }

                HashSet<string> columns = ReadSqliteColumns(connection, table.Name);

                foreach (string requiredColumn in table.RequiredSourceColumns)
                {
                    if (!columns.Contains(requiredColumn))
                    {
                        errors.Add("Quellspalte fehlt: " + table.Name + "." + requiredColumn);
                    }
                }
            }
        }

        private static void ValidateTargetSchema(MySqlConnection target, List<MigrationTable> tables,
            List<string> errors)
        {
            HashSet<string> targetTables = ReadTargetTables(target);
            Dictionary<string, HashSet<string>> targetColumns = ReadTargetColumns(target);

            foreach (string table in TargetTables)
            {
                if (!targetTables.Contains(table))
                    errors.Add("Zieltabelle fehlt: " + table);
            }

            foreach (MigrationTable table in tables)
            {
                HashSet<string> columns;

                if (!targetColumns.TryGetValue(table.Name, out columns))
                    continue;

                foreach (string targetColumn in table.TargetColumns)
                {
                    if (!columns.Contains(targetColumn))
                    {
                        errors.Add("Zielspalte fehlt: " + table.Name + "." + targetColumn);
                    }
                }
            }

            HashSet<string> propertyColumns;

            if (targetColumns.TryGetValue("DBPROPERTIES", out propertyColumns))
            {
                if (!propertyColumns.Contains("KEY") || !propertyColumns.Contains("VALUE"))
                    errors.Add("Zieltabelle DBPROPERTIES ist unvollständig.");
            }

            string version = ReadTargetProperty(target, "DBVersion");

            if (!String.Equals(version, UpdateDb.DbVersion, StringComparison.Ordinal))
            {
                errors.Add("Die MariaDB-DBVersion ist " + (version ?? "(leer)")
                    + ". Starten Sie MediaBrowser mit --update.");
            }

            ValidateTargetColumnCapacity(target, "LAYERS", "EDIT", 64, errors);
            ValidateTargetColumnCapacity(target, "METADATA", "VALUE", 0, errors);
        }

        private static void ValidateTargetColumnCapacity(MySqlConnection target, string tableName,
            string columnName, int minimumLength, List<string> errors)
        {
            string dataType;
            long maxLength;

            if (!TryReadTargetColumnType(target, tableName, columnName, out dataType, out maxLength))
                return;

            bool valid;

            if (IsTextType(dataType))
            {
                valid = true;
            }
            else if (String.Equals(dataType, "varchar", StringComparison.OrdinalIgnoreCase)
                || String.Equals(dataType, "char", StringComparison.OrdinalIgnoreCase))
            {
                valid = minimumLength <= 0 || maxLength >= minimumLength;
            }
            else
            {
                valid = false;
            }

            if (!valid)
            {
                errors.Add("Die Zieltabelle " + tableName + "." + columnName
                    + " hat eine zu kleine Spaltenbreite. Starten Sie MediaBrowser mit --update.");
            }
        }

        private static bool TryReadTargetColumnType(MySqlConnection target, string tableName,
            string columnName, out string dataType, out long maxLength)
        {
            dataType = null;
            maxLength = 0;

            using (MySqlCommand command = target.CreateCommand())
            {
                command.CommandText =
                    "SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column";
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

        private static void ValidateConstraints(SQLiteConnection source, SQLiteConnection thumbs,
            List<string> errors, List<string> warnings)
        {
            AddCountCheck(source, "FOLDERS.FOLDERNAME ist nicht eindeutig (ohne Groß-/Kleinschreibung).",
                "SELECT COUNT(*) FROM (SELECT LOWER(FOLDERNAME) FROM FOLDERS GROUP BY LOWER(FOLDERNAME) HAVING COUNT(*) > 1)",
                errors);
            AddCountCheck(source, "CATEGORY.GUID ist nicht eindeutig.",
                "SELECT COUNT(*) FROM (SELECT GUID FROM CATEGORY GROUP BY GUID HAVING COUNT(*) > 1)", errors);
            AddCountCheck(source, "CATEGORY.GUID ist leer oder hat nicht 32 Zeichen.",
                "SELECT COUNT(*) FROM CATEGORY WHERE GUID IS NULL OR LENGTH(GUID) <> 32", errors);
            AddCountCheck(source, "ROLES.ROLE ist nicht eindeutig.",
                "SELECT COUNT(*) FROM (SELECT ROLE FROM ROLES GROUP BY ROLE HAVING COUNT(*) > 1)", errors);
            AddCountCheck(source, "CATEGORIZE enthält doppelte Schlüssel.",
                "SELECT COUNT(*) FROM (SELECT CATEGORY_FK, VARIATIONS_FK FROM CATEGORIZE " +
                "WHERE EXISTS (SELECT 1 FROM CATEGORY c WHERE c.ID = CATEGORIZE.CATEGORY_FK) " +
                "AND EXISTS (SELECT 1 FROM VARIATIONS v WHERE v.ID = CATEGORIZE.VARIATIONS_FK " +
                "AND EXISTS (SELECT 1 FROM MEDIAFILES m WHERE m.ID = v.MEDIAFILES_FK)) " +
                "GROUP BY CATEGORY_FK, VARIATIONS_FK HAVING COUNT(*) > 1)",
                errors);
            AddCountCheck(source, "ATTACHED enthält doppelte Schlüssel.",
                "SELECT COUNT(*) FROM (SELECT MEDIAFILES_FK, ATTACHMENTS_FK FROM ATTACHED GROUP BY MEDIAFILES_FK, ATTACHMENTS_FK HAVING COUNT(*) > 1)",
                errors);
            AddCountCheck(thumbs, "THUMBS enthält mehrere Einträge pro Variation.",
                "SELECT COUNT(*) FROM (SELECT VARIATIONS_FK FROM THUMBS " +
                "WHERE EXISTS (SELECT 1 FROM _validvariations v WHERE v.ID = THUMBS.VARIATIONS_FK) " +
                "GROUP BY VARIATIONS_FK HAVING COUNT(*) > 1)",
                errors);

            AddNotNullCheck(source, "ATTACHMENTS", new[] { "PATH" }, errors);
            AddNotNullCheck(source, "CATEGORY", new[]
            {
                "NAME", "SORTORDER", "GUID", "ISUNIQUE", "ISDATE", "ISLOCATION"
            }, errors);
            AddNotNullCheck(source, "CATEGORIZE", new[] { "CATEGORY_FK", "VARIATIONS_FK", "USERDEFINED" },
                errors);
            AddNotNullCheck(source, "DATALOGGER_GPS", new[]
            {
                "FILETIME", "FILESIZE", "UTCTIME", "LOCALTIME", "LONGITUDE", "LATITUDE"
            }, errors);
            AddNotNullCheck(source, "DATALOGGER_METEOROLOGY", new[]
            {
                "LOGTIME", "TEMPERATURE", "HUMIDITY"
            }, errors);
            AddNotNullCheck(source, "FOLDERS", new[] { "FOLDERNAME" }, errors);
            AddNotNullCheck(source, "MEDIAFILES", new[]
            {
                "FILENAME", "SORTORDER", "FOLDERS_FK", "MD5VALUE", "ISDELETED", "ISBOOKMARKED",
                "CREATIONDATE", "MEDIADATE", "EDITDATE", "INSERTDATE", "ROLES_FK", "HISTORYVERSION",
                "ISDUBLICATE"
            }, errors);
            AddNotNullCheck(source, "METADATA", new[] { "MEDIAFILES_FK", "METANAME_FK" }, errors);
            AddNotNullCheck(source, "METADATANAME", new[]
            {
                "NAME", "GROUPNAME", "TYPE", "isVisible"
            }, errors);
            AddNotNullCheck(source, "REMOTEMAPPING", new[]
            {
                "DBGUID", "OBJECTGUID_LOCAL", "OBJECTGUID_REMOTE", "TYPE"
            }, errors);
            AddNotNullCheck(source, "ROLES", new[] { "ROLE" }, errors);
            AddNotNullCheck(thumbs, "THUMBS", new[] { "VARIATIONS_FK", "THUMB", "ISOUTDATED" }, errors);
            AddNotNullCheck(source, "USERDEFINEDREQUESTS", new[] { "REQUEST" }, errors);
            AddNotNullCheck(source, "VARIATIONS", new[] { "MEDIAFILES_FK", "POSITION" }, errors);
            AddNotNullCheck(source, "LAYERS", new[] { "VARIATIONS_FK", "POSITION" }, errors);
            AddNotNullCheck(source, "ATTACHED", new[] { "MEDIAFILES_FK", "ATTACHMENTS_FK" }, errors);
            AddNotNullCheck(source, "EXTRADATA", new[] { "NAME", "TYP", "CATEGORY" }, errors);
            AddNotNullCheck(source, "EXTRADATAS", new[] { "EXTRADATA_FK", "MEDIAFILES_FK" }, errors);
            AddNotNullCheck(source, "FAVORITES", new[] { "NAME", "TYPE", "ACCESS" }, errors);

            AddLengthCheck(source, "ATTACHMENTS", "PATH", 8000, errors);
            AddLengthCheck(source, "CATEGORY", "NAME", 50, errors);
            AddLengthCheck(source, "CATEGORY", "SORTORDER", 50, errors);
            AddLengthCheck(source, "CATEGORY", "DESCRIPTION", 255, errors);
            AddLengthCheck(source, "CATEGORY", "FULLPATH", 8000, errors);
            AddLengthCheck(source, "CATEGORY", "GUID", 32, errors);
            AddLengthCheck(source, "DBPROPERTIES", "KEY", 50, errors);
            AddLengthCheck(source, "DBPROPERTIES", "VALUE", 500, errors);
            AddLengthCheck(source, "EXTRADATA", "NAME", 255, errors);
            AddLengthCheck(source, "EXTRADATA", "TYP", 3, errors);
            AddLengthCheck(source, "EXTRADATA", "FOLDER", 50, errors);
            AddLengthCheck(source, "EXTRADATA", "CATEGORY", 50, errors);
            AddLengthCheck(source, "FAVORITES", "NAME", 255, errors);
            AddLengthCheck(source, "FAVORITES", "TYPE", 3, errors);
            AddLengthCheck(source, "FOLDERS", "FOLDERNAME", 500, errors);
            AddLengthCheck(source, "LAYERS", "EDIT", 64, errors);
            AddLengthCheck(source, "LAYERS", "ACTION", 512, errors);
            AddLengthCheck(source, "MEDIAFILES", "FILENAME", 255, errors);
            AddLengthCheck(source, "MEDIAFILES", "SORTORDER", 255, errors);
            AddLengthCheck(source, "MEDIAFILES", "MD5VALUE", 32, errors);
            AddLengthCheck(source, "MEDIAFILES", "TYPE", 3, errors);
            AddLengthCheck(source, "METADATANAME", "NAME", 50, errors);
            AddLengthCheck(source, "METADATANAME", "GROUPNAME", 50, errors);
            AddLengthCheck(source, "METADATANAME", "TYPE", 4, errors);
            AddLengthCheck(source, "REMOTEMAPPING", "DBGUID", 32, errors);
            AddLengthCheck(source, "REMOTEMAPPING", "OBJECTGUID_LOCAL", 32, errors);
            AddLengthCheck(source, "REMOTEMAPPING", "OBJECTGUID_REMOTE", 32, errors);
            AddLengthCheck(source, "REMOTEMAPPING", "TYPE", 3, errors);
            AddLengthCheck(source, "ROLES", "ROLE", 30, errors);
            AddLengthCheck(source, "ROLES", "PASSWORD", 32, errors);
            AddLengthCheck(source, "ROLES", "DESCRIPTION", 500, errors);
            AddLengthCheck(source, "VARIATIONS", "NAME", 50, errors);
            AddLengthCheck(source, "VARIATIONS", "DESCRIPTION", 512, errors);

            AddDateCheck(source, "CATEGORY", "DATE", false, errors, warnings);
            AddDateCheck(source, "MEDIAFILES", "CREATIONDATE", true, errors, warnings);
            AddDateCheck(source, "MEDIAFILES", "MEDIADATE", true, errors, warnings);
            AddDateCheck(source, "MEDIAFILES", "EDITDATE", true, errors, warnings);
            AddDateCheck(source, "MEDIAFILES", "INSERTDATE", true, errors, warnings);
            AddDateCheck(source, "MEDIAFILES", "DELETEDATE", false, errors, warnings);
            AddDateCheck(source, "FAVORITES", "ACCESS", true, errors, warnings);
            AddDateCheck(source, "DATALOGGER_GPS", "FILETIME", true, errors, warnings);
            AddDateCheck(source, "DATALOGGER_GPS", "UTCTIME", true, errors, warnings);
            AddDateCheck(source, "DATALOGGER_GPS", "LOCALTIME", true, errors, warnings);
            AddDateCheck(source, "DATALOGGER_METEOROLOGY", "LOGTIME", true, errors, warnings);

            long invalidCurrentVariations = Count(source,
                "SELECT COUNT(*) FROM MEDIAFILES WHERE CURRENTVARIATION IS NOT NULL " +
                "AND CURRENTVARIATION NOT IN (SELECT ID FROM VARIATIONS)");

            if (invalidCurrentVariations > 0)
            {
                warnings.Add("MEDIAFILES.CURRENTVARIATION ohne VARIATIONS werden auf NULL gesetzt: "
                    + invalidCurrentVariations);
            }
        }

        private static void AddCountCheck(SQLiteConnection connection, string message, string sql,
            List<string> errors)
        {
            long count = Count(connection, sql);

            if (count > 0)
                errors.Add(message + " (" + count + ")");
        }

        private static void AddNotNullCheck(SQLiteConnection connection, string table, string[] columns,
            List<string> errors)
        {
            string where = String.Join(" OR ", columns.Select(x => QuoteSqlite(x) + " IS NULL"));
            long count = Count(connection, "SELECT COUNT(*) FROM " + QuoteSqlite(table) + " WHERE " + where);

            if (count > 0)
                errors.Add(table + " enthält " + count + " Zeilen mit leeren Pflichtfeldern.");
        }

        private static void AddLengthCheck(SQLiteConnection connection, string table, string column,
            int maxLength, List<string> errors)
        {
            long count = Count(connection,
                "SELECT COUNT(*) FROM " + QuoteSqlite(table)
                + " WHERE LENGTH(" + QuoteSqlite(column) + ") > " + maxLength.ToString(CultureInfo.InvariantCulture));

            if (count > 0)
            {
                errors.Add(table + "." + column + " ist bei " + count + " Zeilen länger als "
                    + maxLength + " Zeichen.");
            }
        }

        private static void AddDateCheck(SQLiteConnection connection, string table, string column,
            bool required, List<string> errors, List<string> warnings)
        {
            long count = Count(connection,
                "SELECT COUNT(*) FROM " + QuoteSqlite(table)
                + " WHERE " + QuoteSqlite(column) + " IS NOT NULL AND " + QuoteSqlite(column)
                + " < '1000-01-01 00:00:00'");

            if (count == 0)
                return;

            if (required)
                errors.Add(table + "." + column + " enthält " + count + " ungültige Datumswerte.");
            else
                warnings.Add(table + "." + column + ": " + count + " ungültige Datumswerte werden auf NULL gesetzt.");
        }

        private static string GetOrphanLabel(string tableName)
        {
            switch (tableName)
            {
                case "VARIATIONS":
                    return "VARIATIONS ohne MEDIAFILES";
                case "CATEGORIZE":
                    return "CATEGORIZE ohne gültige CATEGORY/VARIATIONS";
                case "METADATA":
                    return "METADATA ohne gültige MEDIAFILES/METADATANAME";
                case "LAYERS":
                    return "LAYERS ohne gültige VARIATIONS";
                case "THUMBS":
                    return "THUMBS ohne gültige VARIATIONS";
                default:
                    return tableName + " mit ungültigen Referenzen";
            }
        }

        private static int CountInvalidDates(string sourceMainPath, string table, string column)
        {
            using (SQLiteConnection source = OpenSource(sourceMainPath))
            {
                return (int)Count(source,
                    "SELECT COUNT(*) FROM " + QuoteSqlite(table)
                    + " WHERE " + QuoteSqlite(column) + " IS NOT NULL AND " + QuoteSqlite(column)
                    + " < '1000-01-01 00:00:00'");
            }
        }

        private static long Count(SQLiteConnection connection, string sql)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static HashSet<string> ReadSqliteTables(SQLiteConnection connection)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(reader.GetString(0));
                }
            }

            return result;
        }

        private static HashSet<string> ReadSqliteColumns(SQLiteConnection connection, string table)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(" + QuoteSqlite(table) + ")";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(reader.GetString(1));
                }
            }

            return result;
        }

        private static HashSet<string> ReadTargetTables(MySqlConnection target)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (MySqlCommand command = target.CreateCommand())
            {
                command.CommandText =
                    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema";
                command.Parameters.AddWithValue("@schema", target.Database);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(reader.GetString(0));
                }
            }

            return result;
        }

        private static Dictionary<string, HashSet<string>> ReadTargetColumns(MySqlConnection target)
        {
            Dictionary<string, HashSet<string>> result =
                new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            using (MySqlCommand command = target.CreateCommand())
            {
                command.CommandText =
                    "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema";
                command.Parameters.AddWithValue("@schema", target.Database);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader.GetString(0);
                        HashSet<string> columns;

                        if (!result.TryGetValue(tableName, out columns))
                        {
                            columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            result[tableName] = columns;
                        }

                        columns.Add(reader.GetString(1));
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, long> ReadTargetCounts(MySqlConnection target)
        {
            Dictionary<string, long> result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (string table in TargetTables)
            {
                using (MySqlCommand command = target.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM " + QuoteMySql(table);
                    command.CommandTimeout = 0;
                    result[table] = Convert.ToInt64(command.ExecuteScalar());
                }
            }

            return result;
        }

        private static Dictionary<string, string> ReadTargetProperties(MySqlConnection target)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (MySqlCommand command = target.CreateCommand())
            {
                command.CommandText = "SELECT `KEY`, `VALUE` FROM DBPROPERTIES";

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result[reader.GetString(0)] = reader.IsDBNull(1)
                            ? String.Empty
                            : reader.GetString(1);
                    }
                }
            }

            return result;
        }

        private static string ReadTargetProperty(MySqlConnection target, string key)
        {
            using (MySqlCommand command = target.CreateCommand())
            {
                command.CommandText = "SELECT `VALUE` FROM DBPROPERTIES WHERE `KEY` = @key";
                command.Parameters.AddWithValue("@key", key);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : value.ToString();
            }
        }

        private static string GetProperty(Dictionary<string, string> properties, string key)
        {
            string value;

            return properties.TryGetValue(key, out value) ? value : null;
        }

        private static MySqlConnection OpenTarget(MariaDbConnectionOptions options)
        {
            MySqlConnection connection = new MySqlConnection(
                options.BuildConnectionString(options.Database));

            if (connection.State != ConnectionState.Open)
                connection.Open();

            return connection;
        }

        private MySqlConnection OpenTarget()
        {
            return OpenTarget(this.targetOptions);
        }

        private static long ReadMaxAllowedPacket(MySqlConnection target)
        {
            try
            {
                using (MySqlCommand command = target.CreateCommand())
                {
                    command.CommandText = "SELECT @@max_allowed_packet";
                    object value = command.ExecuteScalar();

                    if (value != null && value != DBNull.Value)
                        return Convert.ToInt64(value);
                }
            }
            catch
            {
            }

            return 1024 * 1024;
        }

        private static SQLiteConnection OpenSource(string path)
        {
            SQLiteConnection connection = new SQLiteConnection(
                "Data Source=" + path + ";Read Only=True;FailIfMissing=True;UTF8Encoding=True;Version=3");
            connection.Open();
            return connection;
        }

        private static void PrepareThumbnailValidation(SQLiteConnection source, SQLiteConnection thumbs)
        {
            using (SQLiteCommand command = thumbs.CreateCommand())
            {
                command.CommandText = "CREATE TEMP TABLE IF NOT EXISTS _validvariations (ID INTEGER PRIMARY KEY)";
                command.ExecuteNonQuery();
            }

            using (SQLiteTransaction transaction = thumbs.BeginTransaction())
            {
                using (SQLiteCommand command = thumbs.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM _validvariations";
                    command.ExecuteNonQuery();
                }

                using (SQLiteCommand select = source.CreateCommand())
                {
                    select.CommandText = "SELECT ID FROM VARIATIONS WHERE EXISTS " +
                        "(SELECT 1 FROM MEDIAFILES m WHERE m.ID = VARIATIONS.MEDIAFILES_FK)";

                    using (SQLiteDataReader reader = select.ExecuteReader())
                    using (SQLiteCommand insert = thumbs.CreateCommand())
                    {
                        insert.Transaction = transaction;
                        insert.CommandText = "INSERT OR IGNORE INTO _validvariations (ID) VALUES (@id)";
                        SQLiteParameter parameter = insert.Parameters.Add("@id", DbType.Int64);

                        while (reader.Read())
                        {
                            parameter.Value = reader.GetInt64(0);
                            insert.ExecuteNonQuery();
                        }
                    }
                }

                transaction.Commit();
            }
        }

        private static bool IsLocalInfileError(MySqlException exception)
        {
            string message = exception.Message ?? String.Empty;

            return message.IndexOf("local infile", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("local data", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("load data", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("local_infile", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Report(IProgress<MigrationProgress> progress, string message,
            long rowsProcessed, long totalRows, string tableName = null, int tableIndex = 0,
            int tableCount = 0)
        {
            if (progress == null)
                return;

            progress.Report(new MigrationProgress
            {
                Message = message,
                TableName = tableName,
                RowsProcessed = rowsProcessed,
                TotalRows = totalRows,
                TableIndex = tableIndex,
                TableCount = tableCount
            });
        }

        private static string QuoteMySql(string identifier)
        {
            return "`" + (identifier ?? String.Empty).Replace("`", "``") + "`";
        }

        private static string QuoteSqlite(string identifier)
        {
            return "[" + (identifier ?? String.Empty).Replace("]", "]]") + "]";
        }

        private static List<MigrationTable> BuildTables()
        {
            List<MigrationTable> tables = new List<MigrationTable>();

            tables.Add(new MigrationTable("FOLDERS", false,
                "SELECT ID, FOLDERNAME FROM FOLDERS",
                new[] { "ID", "FOLDERNAME" },
                new[] { "ID", "FOLDERNAME" }));

            tables.Add(new MigrationTable("ROLES", false,
                "SELECT ID, [ROLE], [PASSWORD], DESCRIPTION FROM ROLES",
                new[] { "ID", "ROLE", "PASSWORD", "DESCRIPTION" },
                new[] { "ID", "ROLE", "PASSWORD", "DESCRIPTION" }, true));

            tables.Add(new MigrationTable("ATTACHMENTS", false,
                "SELECT ID, PATH FROM ATTACHMENTS",
                new[] { "ID", "PATH" },
                new[] { "ID", "PATH" }));

            tables.Add(new MigrationTable("CATEGORY", false,
                "SELECT ID, PARENT, NAME, SORTORDER, DESCRIPTION, ISUNIQUE, ISDATE, ISLOCATION, " +
                "LATITUDE, LONGITUDE, " +
                "CASE WHEN [DATE] IS NULL OR [DATE] < '1000-01-01 00:00:00' THEN NULL ELSE [DATE] END, " +
                "FULLPATH, GUID FROM CATEGORY",
                new[]
                {
                    "ID", "PARENT", "NAME", "SORTORDER", "DESCRIPTION", "ISUNIQUE", "ISDATE",
                    "ISLOCATION", "LATITUDE", "LONGITUDE", "DATE", "FULLPATH", "GUID"
                },
                new[]
                {
                    "ID", "PARENT", "NAME", "SORTORDER", "DESCRIPTION", "ISUNIQUE", "ISDATE",
                    "ISLOCATION", "LATITUDE", "LONGITUDE", "DATE", "FULLPATH", "GUID"
                }, true));

            tables.Add(new MigrationTable("METADATANAME", false,
                "SELECT ID, NAME, GROUPNAME, [TYPE], isVisible FROM METADATANAME",
                new[] { "ID", "NAME", "GROUPNAME", "TYPE", "ISVISIBLE" },
                new[] { "ID", "NAME", "GROUPNAME", "TYPE", "isVisible" }));

            tables.Add(new MigrationTable("DESCRIPTION", false,
                "SELECT ID, [VALUE] FROM DESCRIPTION",
                new[] { "ID", "VALUE" },
                new[] { "ID", "VALUE" }));

            tables.Add(new MigrationTable("EXTRADATA", false,
                "SELECT ID, NAME, TYP, FOLDER, CATEGORY, DATA FROM EXTRADATA",
                new[] { "ID", "NAME", "TYP", "FOLDER", "CATEGORY", "DATA" },
                new[] { "ID", "NAME", "TYP", "FOLDER", "CATEGORY", "DATA" }));

            tables.Add(new MigrationTable("USERDEFINEDREQUESTS", false,
                "SELECT ID, REQUEST FROM USERDEFINEDREQUESTS",
                new[] { "ID", "REQUEST" },
                new[] { "ID", "REQUEST" }));

            tables.Add(new MigrationTable("DATALOGGER_METEOROLOGY", false,
                "SELECT LOGTIME, TEMPERATURE, HUMIDITY FROM DATALOGGER_METEOROLOGY",
                new[] { "LOGTIME", "TEMPERATURE", "HUMIDITY" },
                new[] { "LOGTIME", "TEMPERATURE", "HUMIDITY" }));

            tables.Add(new MigrationTable("DATALOGGER_GPS", false,
                "SELECT FILETIME, FILESIZE, UTCTIME, [LOCALTIME], LONGITUDE, LATITUDE, ALTITUDE " +
                "FROM DATALOGGER_GPS",
                new[] { "FILETIME", "FILESIZE", "UTCTIME", "LOCALTIME", "LONGITUDE", "LATITUDE", "ALTITUDE" },
                new[] { "FILETIME", "FILESIZE", "UTCTIME", "LOCALTIME", "LONGITUDE", "LATITUDE", "ALTITUDE" }));

            tables.Add(new MigrationTable("MEDIAFILES", false,
                "SELECT ID, FILENAME, SORTORDER, FOLDERS_FK, MD5VALUE, LENGTH, VIEWED, ISDELETED, " +
                "DELETEDATE, ISBOOKMARKED, CREATIONDATE, MEDIADATE, EDITDATE, INSERTDATE, ORIENTATION, " +
                "[TYPE], PRIVACY, " +
                "CASE WHEN CURRENTVARIATION IS NOT NULL AND CURRENTVARIATION NOT IN (SELECT ID FROM VARIATIONS) " +
                "THEN NULL ELSE CURRENTVARIATION END, DESCRIPTION_FK, DURATION, FRAMES, PRIORITY, ROLES_FK, " +
                "HISTORYVERSION, ISDUBLICATE, WIDTH, HEIGHT, LONGITUDE, LATITUDE FROM MEDIAFILES",
                new[]
                {
                    "ID", "FILENAME", "SORTORDER", "FOLDERS_FK", "MD5VALUE", "LENGTH", "VIEWED",
                    "ISDELETED", "DELETEDATE", "ISBOOKMARKED", "CREATIONDATE", "MEDIADATE", "EDITDATE",
                    "INSERTDATE", "ORIENTATION", "TYPE", "PRIVACY", "CURRENTVARIATION", "DESCRIPTION_FK",
                    "DURATION", "FRAMES", "PRIORITY", "ROLES_FK", "HISTORYVERSION", "ISDUBLICATE",
                    "WIDTH", "HEIGHT", "LONGITUDE", "LATITUDE"
                },
                new[]
                {
                    "ID", "FILENAME", "SORTORDER", "FOLDERS_FK", "MD5VALUE", "LENGTH", "VIEWED",
                    "ISDELETED", "DELETEDATE", "ISBOOKMARKED", "CREATIONDATE", "MEDIADATE", "EDITDATE",
                    "INSERTDATE", "ORIENTATION", "TYPE", "PRIVACY", "CURRENTVARIATION", "DESCRIPTION_FK",
                    "DURATION", "FRAMES", "PRIORITY", "ROLES_FK", "HISTORYVERSION", "ISDUBLICATE",
                    "WIDTH", "HEIGHT", "LONGITUDE", "LATITUDE"
                }));

            tables.Add(new MigrationTable("VARIATIONS", false,
                "SELECT ID, MEDIAFILES_FK, [POSITION], NAME, DESCRIPTION FROM VARIATIONS " +
                "WHERE EXISTS (SELECT 1 FROM MEDIAFILES m WHERE m.ID = VARIATIONS.MEDIAFILES_FK)",
                new[] { "ID", "MEDIAFILES_FK", "POSITION", "NAME", "DESCRIPTION" },
                new[] { "ID", "MEDIAFILES_FK", "POSITION", "NAME", "DESCRIPTION" }));

            tables.Add(new MigrationTable("ATTACHED", false,
                "SELECT MEDIAFILES_FK, ATTACHMENTS_FK FROM ATTACHED",
                new[] { "MEDIAFILES_FK", "ATTACHMENTS_FK" },
                new[] { "MEDIAFILES_FK", "ATTACHMENTS_FK" }));

            tables.Add(new MigrationTable("CATEGORIZE", false,
                "SELECT CATEGORY_FK, VARIATIONS_FK, USERDEFINED FROM CATEGORIZE " +
                "WHERE EXISTS (SELECT 1 FROM CATEGORY c WHERE c.ID = CATEGORIZE.CATEGORY_FK) " +
                "AND EXISTS (SELECT 1 FROM VARIATIONS v WHERE v.ID = CATEGORIZE.VARIATIONS_FK " +
                "AND EXISTS (SELECT 1 FROM MEDIAFILES m WHERE m.ID = v.MEDIAFILES_FK))",
                new[] { "CATEGORY_FK", "VARIATIONS_FK", "USERDEFINED" },
                new[] { "CATEGORY_FK", "VARIATIONS_FK", "USERDEFINED" }));

            tables.Add(new MigrationTable("METADATA", false,
                "SELECT MEDIAFILES_FK, METANAME_FK, [VALUE] FROM METADATA " +
                "WHERE EXISTS (SELECT 1 FROM MEDIAFILES m WHERE m.ID = METADATA.MEDIAFILES_FK) " +
                "AND EXISTS (SELECT 1 FROM METADATANAME n WHERE n.ID = METADATA.METANAME_FK)",
                new[] { "MEDIAFILES_FK", "METANAME_FK", "VALUE" },
                new[] { "MEDIAFILES_FK", "METANAME_FK", "VALUE" }, false, 1000));

            tables.Add(new MigrationTable("EXTRADATAS", false,
                "SELECT EXTRADATA_FK, MEDIAFILES_FK FROM EXTRADATAS",
                new[] { "EXTRADATA_FK", "MEDIAFILES_FK" },
                new[] { "EXTRADATA_FK", "MEDIAFILES_FK" }));

            tables.Add(new MigrationTable("FAVORITES", false,
                "SELECT NAME, [TYPE], ACCESS FROM FAVORITES",
                new[] { "NAME", "TYPE", "ACCESS" },
                new[] { "NAME", "TYPE", "ACCESS" }));

            tables.Add(new MigrationTable("LAYERS", false,
                "SELECT [POSITION], EDIT, ACTION, VARIATIONS_FK FROM LAYERS " +
                "WHERE EXISTS (SELECT 1 FROM VARIATIONS v WHERE v.ID = LAYERS.VARIATIONS_FK " +
                "AND EXISTS (SELECT 1 FROM MEDIAFILES m WHERE m.ID = v.MEDIAFILES_FK))",
                new[] { "POSITION", "EDIT", "ACTION", "VARIATIONS_FK" },
                new[] { "POSITION", "EDIT", "ACTION", "VARIATIONS_FK" }));

            tables.Add(new MigrationTable("THUMBS", true,
                "SELECT VARIATIONS_FK, 0, 0, ISOUTDATED, THUMB, 'jpg', " +
                "CAST(NULL AS INTEGER), CAST(NULL AS INTEGER) FROM THUMBS " +
                "WHERE EXISTS (SELECT 1 FROM _validvariations v WHERE v.ID = THUMBS.VARIATIONS_FK)",
                new[]
                {
                    "VARIATIONS_FK", "PREVIEW_WIDTH", "PREVIEW_HEIGHT", "ISOUTDATED", "THUMB",
                    "EXTENSION", "WIDTH", "HEIGHT"
                },
                new[] { "VARIATIONS_FK", "ISOUTDATED", "THUMB" }, false, 50));

            tables.Add(new MigrationTable("REMOTEMAPPING", false,
                "SELECT DBGUID, OBJECTGUID_LOCAL, OBJECTGUID_REMOTE, [TYPE] FROM REMOTEMAPPING",
                new[] { "DBGUID", "OBJECTGUID_LOCAL", "OBJECTGUID_REMOTE", "TYPE" },
                new[] { "DBGUID", "OBJECTGUID_LOCAL", "OBJECTGUID_REMOTE", "TYPE" }));

            return tables;
        }
    }
}
