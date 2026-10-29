[CmdletBinding()]
param(
    [string]$HostName = '192.168.2.129',
    [int]$Port = 3306,
    [string]$SourceDatabase = 'mediabrowser',
    [string]$TargetDatabase = 'mediabrowser-new',
    [string]$User = 'mediabrowser-new',
    [string]$PasswordEnv = 'MEDIABROWSER_DB_PASSWORD',
    [string]$MySqlConnectorPath,
    [switch]$Execute,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    if (-not $pwsh) {
        throw 'PowerShell 7 (pwsh.exe) ist erforderlich.'
    }

    $forwardArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    foreach ($name in $PSBoundParameters.Keys) {
        $value = $PSBoundParameters[$name]
        if ($value -is [switch]) {
            if ($value.IsPresent) {
                $forwardArguments += "-$name"
            }
        }
        else {
            $forwardArguments += "-$name"
            $forwardArguments += [string]$value
        }
    }

    & $pwsh.Source @forwardArguments
    exit $LASTEXITCODE
}

if ([string]::IsNullOrEmpty($MySqlConnectorPath)) {
    $MySqlConnectorPath = Join-Path $PSScriptRoot '..\MediaBrowser4Lib\bin\Debug\MySqlConnector.dll'
}

$password = [Environment]::GetEnvironmentVariable($PasswordEnv)
if ([string]::IsNullOrEmpty($password)) {
    throw "Umgebungsvariable '$PasswordEnv' ist nicht gesetzt."
}

if (-not (Test-Path -LiteralPath $MySqlConnectorPath)) {
    throw "MySqlConnector.dll nicht gefunden: $MySqlConnectorPath"
}

Add-Type -Path $MySqlConnectorPath

$builder = [MySqlConnector.MySqlConnectionStringBuilder]::new()
$builder.Server = $HostName
$builder.Port = $Port
$builder.Database = $TargetDatabase
$builder.UserID = $User
$builder.Password = $password
$builder.ConnectionTimeout = 15

$connection = [MySqlConnector.MySqlConnection]::new($builder.ConnectionString)
$connection.Open()

$source = $SourceDatabase
$target = $TargetDatabase

function Expand-Sql([string]$sql) {
    return $sql.Replace('__SRC__', $source).Replace('__TGT__', $target)
}

function Invoke-Sql([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = Expand-Sql $sql
    $command.CommandTimeout = 0
    try {
        return $command.ExecuteNonQuery()
    }
    catch {
        throw "SQL fehlgeschlagen: $($command.CommandText) :: $($_.Exception.Message)"
    }
}

function Get-Rows([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = Expand-Sql $sql
    $command.CommandTimeout = 0
    $reader = $command.ExecuteReader()
    $rows = @()

    while ($reader.Read()) {
        $values = @()
        for ($i = 0; $i -lt $reader.FieldCount; $i++) {
            if ($reader.IsDBNull($i)) {
                $values += $null
            }
            else {
                $values += $reader.GetValue($i)
            }
        }
        $rows += ,$values
    }

    $reader.Close()
    if ($rows.Count -eq 0) {
        return
    }
    return ,$rows
}

function Get-Scalar([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = Expand-Sql $sql
    $command.CommandTimeout = 0
    try {
        $value = $command.ExecuteScalar()
    }
    catch {
        throw "SQL fehlgeschlagen: $($command.CommandText) :: $($_.Exception.Message)"
    }
    if ($null -eq $value -or $value -is [System.DBNull]) {
        return $null
    }
    return $value
}

function Table-Exists([string]$tableName) {
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table"
    [void]$command.Parameters.AddWithValue('@schema', $TargetDatabase)
    [void]$command.Parameters.AddWithValue('@table', $tableName)
    $command.CommandTimeout = 0
    return ([int]$command.ExecuteScalar() -gt 0)
}

function Get-ExistingTargetTables {
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND LOWER(TABLE_NAME) IN ('image_analysis','image_objects','image_readable_text')"
    [void]$command.Parameters.AddWithValue('@schema', $TargetDatabase)
    $command.CommandTimeout = 0
    $reader = $command.ExecuteReader()
    $tables = @()

    while ($reader.Read()) {
        $tables += $reader.GetString(0)
    }

    $reader.Close()
    if ($tables.Count -eq 0) {
        return
    }
    return ,$tables
}

function Write-Table([string]$title, $rows, [string[]]$columns) {
    Write-Host ''
    Write-Host $title

    if ($rows.Count -eq 0) {
        Write-Host '  (leer)'
        return
    }

    foreach ($row in $rows) {
        $parts = @()
        for ($i = 0; $i -lt $columns.Count; $i++) {
            $value = $row[$i]
            if ($null -eq $value) {
                $value = ''
            }
            $parts += ("{0}={1}" -f $columns[$i], $value)
        }
        Write-Host ('  ' + ($parts -join ' | '))
    }
}

try {
    $existingTargetTables = Get-ExistingTargetTables
    foreach ($table in $existingTargetTables) {
        if ($table -cnotin @('IMAGE_ANALYSIS', 'IMAGE_OBJECTS', 'IMAGE_READABLE_TEXT')) {
            throw "Tabelle '$table' existiert bereits mit abweichender Schreibweise. Abbruch."
        }
    }

    $targetHasData = $false
    foreach ($table in @('IMAGE_ANALYSIS', 'IMAGE_OBJECTS', 'IMAGE_READABLE_TEXT')) {
        if (Table-Exists $table) {
            $count = [long](Get-Scalar "SELECT COUNT(*) FROM $table")
            if ($count -gt 0) {
                $targetHasData = $true
                Write-Host "Zieltabelle ${table}: $count Zeilen"
            }
        }
    }

    if ($Execute -and $targetHasData -and -not $Force) {
        throw 'Zieltabellen enthalten bereits Daten. Mit -Force werden sie geleert.'
    }

    Invoke-Sql 'DROP TEMPORARY TABLE IF EXISTS `_IMAGE_ANALYSIS_MAP`' | Out-Null
    Invoke-Sql 'DROP TEMPORARY TABLE IF EXISTS `_IMAGE_ANALYSIS_SELECT`' | Out-Null

    Invoke-Sql @'
CREATE TEMPORARY TABLE `_IMAGE_ANALYSIS_MAP` (
  `OLD_ANALYSIS_ID` BIGINT NOT NULL,
  `NEW_MEDIA_ID` BIGINT NOT NULL,
  `MATCH_TYPE` VARCHAR(20) NOT NULL,
  PRIMARY KEY (`OLD_ANALYSIS_ID`),
  KEY `IDX_NEW_MEDIA` (`NEW_MEDIA_ID`)
) ENGINE=InnoDB
'@ | Out-Null

    Invoke-Sql @'
CREATE TEMPORARY TABLE `_IMAGE_ANALYSIS_SELECT` (
  `NEW_MEDIA_ID` BIGINT NOT NULL,
  `OLD_ANALYSIS_ID` BIGINT NOT NULL,
  PRIMARY KEY (`NEW_MEDIA_ID`),
  KEY `IDX_OLD_ANALYSIS` (`OLD_ANALYSIS_ID`)
) ENGINE=InnoDB
'@ | Out-Null

    Invoke-Sql @'
INSERT INTO `_IMAGE_ANALYSIS_MAP` (`OLD_ANALYSIS_ID`, `NEW_MEDIA_ID`, `MATCH_TYPE`)
SELECT a.id, MIN(n.ID), 'FOLDER_FILENAME'
FROM `__SRC__`.`image_analysis` a
JOIN `__SRC__`.`MEDIAFILES` o ON o.ID = a.media_id
JOIN `__SRC__`.`FOLDERS` of ON of.ID = o.FOLDERS_FK
JOIN `__TGT__`.`FOLDERS` nf ON nf.FOLDERNAME = of.FOLDERNAME
JOIN `__TGT__`.`MEDIAFILES` n ON n.FOLDERS_FK = nf.ID AND n.FILENAME = o.FILENAME
GROUP BY a.id
HAVING COUNT(DISTINCT n.ID) = 1
'@ | Out-Null

    Invoke-Sql @'
INSERT INTO `_IMAGE_ANALYSIS_MAP` (`OLD_ANALYSIS_ID`, `NEW_MEDIA_ID`, `MATCH_TYPE`)
SELECT a.id, MIN(n.ID), 'UNIQUE_FILENAME'
FROM `__SRC__`.`image_analysis` a
JOIN `__SRC__`.`MEDIAFILES` o ON o.ID = a.media_id
LEFT JOIN `_IMAGE_ANALYSIS_MAP` m ON m.OLD_ANALYSIS_ID = a.id
JOIN `__TGT__`.`MEDIAFILES` n ON n.FILENAME = o.FILENAME
WHERE m.OLD_ANALYSIS_ID IS NULL
GROUP BY a.id
HAVING COUNT(DISTINCT n.ID) = 1
'@ | Out-Null

    Invoke-Sql @'
INSERT INTO `_IMAGE_ANALYSIS_SELECT` (`NEW_MEDIA_ID`, `OLD_ANALYSIS_ID`)
SELECT `NEW_MEDIA_ID`, MAX(`OLD_ANALYSIS_ID`)
FROM `_IMAGE_ANALYSIS_MAP`
GROUP BY `NEW_MEDIA_ID`
'@ | Out-Null

    $analysisTotal = [long](Get-Scalar 'SELECT COUNT(*) FROM `__SRC__`.`image_analysis`')
    $mappedByFolder = [long](Get-Scalar "SELECT COUNT(*) FROM `_IMAGE_ANALYSIS_MAP` WHERE `MATCH_TYPE` = 'FOLDER_FILENAME'")
    $mappedByFilename = [long](Get-Scalar "SELECT COUNT(*) FROM `_IMAGE_ANALYSIS_MAP` WHERE `MATCH_TYPE` = 'UNIQUE_FILENAME'")
    $mappedTotal = [long](Get-Scalar 'SELECT COUNT(*) FROM `_IMAGE_ANALYSIS_MAP`')
    $selectedTotal = [long](Get-Scalar 'SELECT COUNT(*) FROM `_IMAGE_ANALYSIS_SELECT`')
    $skippedTotal = $analysisTotal - $mappedTotal
    $droppedTotal = $mappedTotal - $selectedTotal

    $objectTotal = [long](Get-Scalar 'SELECT COUNT(*) FROM `__SRC__`.`image_objects`')
    $objectSelected = [long](Get-Scalar 'SELECT COUNT(*) FROM `__SRC__`.`image_objects` c JOIN `_IMAGE_ANALYSIS_SELECT` s ON s.OLD_ANALYSIS_ID = c.image_analysis_id')
    $textTotal = [long](Get-Scalar 'SELECT COUNT(*) FROM `__SRC__`.`image_readable_text`')
    $textSelected = [long](Get-Scalar 'SELECT COUNT(*) FROM `__SRC__`.`image_readable_text` c JOIN `_IMAGE_ANALYSIS_SELECT` s ON s.OLD_ANALYSIS_ID = c.image_analysis_id')

    Write-Host ''
    Write-Host '=== Image-Analyse Migration ==='
    Write-Host "Quelle:            $SourceDatabase"
    Write-Host "Ziel:              $TargetDatabase"
    Write-Host "Modus:             $(if ($Execute) { 'Ausfuehren' } else { 'Dry-Run (keine Schreiboperation)' })"
    Write-Host ''
    Write-Host "Analysen gesamt:   $analysisTotal"
    Write-Host "  Ordner+Datei:    $mappedByFolder"
    Write-Host "  Dateiname:       $mappedByFilename"
    Write-Host "  ohne Zuordnung:  $skippedTotal"
    Write-Host "  Dubletten:       $droppedTotal"
    Write-Host "Ziel-Analysen:     $selectedTotal"
    Write-Host "Objekte:           $objectSelected von $objectTotal"
    Write-Host "Lesbarer Text:     $textSelected von $textTotal"

    $skippedRows = Get-Rows @'
SELECT a.id, a.media_id, o.FILENAME, of.FOLDERNAME,
       CASE
         WHEN NOT EXISTS (SELECT 1 FROM `__TGT__`.`MEDIAFILES` n WHERE n.FILENAME = o.FILENAME) THEN 'NO_FILE'
         WHEN (SELECT COUNT(*) FROM `__TGT__`.`MEDIAFILES` n WHERE n.FILENAME = o.FILENAME) > 1 THEN 'AMBIGUOUS'
         ELSE 'OTHER'
       END AS REASON
FROM `__SRC__`.`image_analysis` a
JOIN `__SRC__`.`MEDIAFILES` o ON o.ID = a.media_id
JOIN `__SRC__`.`FOLDERS` of ON of.ID = o.FOLDERS_FK
LEFT JOIN `_IMAGE_ANALYSIS_MAP` m ON m.OLD_ANALYSIS_ID = a.id
WHERE m.OLD_ANALYSIS_ID IS NULL
ORDER BY a.id
'@

    Write-Table 'Nicht zugeordnete Analysen:' $skippedRows @('ANALYSIS_ID', 'OLD_MEDIA_ID', 'FILENAME', 'OLD_FOLDER', 'REASON')

    $droppedRows = Get-Rows @'
SELECT m.OLD_ANALYSIS_ID, m.NEW_MEDIA_ID, m.MATCH_TYPE, a.media_id, o.FILENAME, of.FOLDERNAME
FROM `_IMAGE_ANALYSIS_MAP` m
JOIN `__SRC__`.`image_analysis` a ON a.id = m.OLD_ANALYSIS_ID
JOIN `__SRC__`.`MEDIAFILES` o ON o.ID = a.media_id
JOIN `__SRC__`.`FOLDERS` of ON of.ID = o.FOLDERS_FK
LEFT JOIN `_IMAGE_ANALYSIS_SELECT` s ON s.OLD_ANALYSIS_ID = m.OLD_ANALYSIS_ID
WHERE s.OLD_ANALYSIS_ID IS NULL
ORDER BY m.NEW_MEDIA_ID, m.OLD_ANALYSIS_ID
'@

    Write-Table 'Verdraengte Dubletten (pro Ziel-Medium bleibt die hoechste Analyse-ID):' $droppedRows @('ANALYSIS_ID', 'NEW_MEDIA_ID', 'MATCH_TYPE', 'OLD_MEDIA_ID', 'FILENAME', 'OLD_FOLDER')

    if (-not $Execute) {
        Write-Host ''
        Write-Host 'Dry-Run beendet. Mit -Execute werden die Tabellen angelegt und die Daten migriert.'
        return
    }

    if ($targetHasData) {
        Write-Host ''
        Write-Host '-Force: Zieltabellen werden geleert.'
        Invoke-Sql 'SET FOREIGN_KEY_CHECKS = 0' | Out-Null
        try {
            if (Table-Exists 'IMAGE_OBJECTS') {
                Invoke-Sql 'TRUNCATE TABLE `IMAGE_OBJECTS`' | Out-Null
            }
            if (Table-Exists 'IMAGE_READABLE_TEXT') {
                Invoke-Sql 'TRUNCATE TABLE `IMAGE_READABLE_TEXT`' | Out-Null
            }
            if (Table-Exists 'IMAGE_ANALYSIS') {
                Invoke-Sql 'TRUNCATE TABLE `IMAGE_ANALYSIS`' | Out-Null
            }
        }
        finally {
            Invoke-Sql 'SET FOREIGN_KEY_CHECKS = 1' | Out-Null
        }
    }

    Invoke-Sql @'
CREATE TABLE IF NOT EXISTS `IMAGE_ANALYSIS` (
  `ID` BIGINT NOT NULL AUTO_INCREMENT,
  `MEDIA_ID` BIGINT NOT NULL,
  `ANIMAL_COUNT` INT NOT NULL,
  `WEATHER_DESCRIPTION` LONGTEXT NULL,
  `OBJECT_COUNT` INT NOT NULL,
  `CREATION_DATE` DATETIME(6) NULL,
  `LOCATION_DESCRIPTION` LONGTEXT NULL,
  `FORMAT_OF_DOCUMENT_OR_IMAGE` TEXT NULL,
  `DETAILED_IMAGE_ANALYSIS` LONGTEXT NULL,
  `TEMPERATUR_MIN_CELSIUS` INT NULL,
  `TEMPERATUR_MAX_CELSIUS` INT NULL,
  `MODEL_TEMPERATURE` DOUBLE NULL,
  `CREATED_IN_MILLISECONDS` BIGINT NULL,
  `MODEL` VARCHAR(100) NULL,
  `PERSON_COUNT` INT NOT NULL,
  `CREATED_AT` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `UQ_IMAGE_ANALYSIS_MEDIA_ID` (`MEDIA_ID`),
  KEY `IDX_IMAGE_ANALYSIS_CREATION_DATE` (`CREATION_DATE`),
  KEY `IDX_IMAGE_ANALYSIS_ANIMAL_COUNT` (`ANIMAL_COUNT`),
  KEY `IDX_IMAGE_ANALYSIS_OBJECT_COUNT` (`OBJECT_COUNT`),
  KEY `IDX_IMAGE_ANALYSIS_PERSON_COUNT` (`PERSON_COUNT`),
  KEY `IDX_IMAGE_ANALYSIS_TEMPERATUR_MIN` (`TEMPERATUR_MIN_CELSIUS`),
  KEY `IDX_IMAGE_ANALYSIS_TEMPERATUR_MAX` (`TEMPERATUR_MAX_CELSIUS`),
  KEY `IDX_IMAGE_ANALYSIS_ALL_COUNTS` (`ANIMAL_COUNT`, `OBJECT_COUNT`, `PERSON_COUNT`),
  KEY `IDX_IMAGE_ANALYSIS_TEMPERATUR_RANGE` (`TEMPERATUR_MIN_CELSIUS`, `TEMPERATUR_MAX_CELSIUS`),
  KEY `IDX_IMAGE_ANALYSIS_MODEL` (`MODEL`),
  KEY `IDX_IMAGE_ANALYSIS_CREATED_AT` (`CREATED_AT`),
  FULLTEXT KEY `FT_DETAILED_IMAGE_ANALYSIS` (`DETAILED_IMAGE_ANALYSIS`),
  FULLTEXT KEY `FT_WEATHER_DESCRIPTION` (`WEATHER_DESCRIPTION`),
  FULLTEXT KEY `FT_LOCATION_DESCRIPTION` (`LOCATION_DESCRIPTION`),
  FULLTEXT KEY `FT_FORMAT_OF_DOCUMENT_OR_IMAGE` (`FORMAT_OF_DOCUMENT_OR_IMAGE`),
  CONSTRAINT `FK_IMAGE_ANALYSIS_MEDIA` FOREIGN KEY (`MEDIA_ID`) REFERENCES `MEDIAFILES` (`ID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
'@ | Out-Null

    Invoke-Sql @'
CREATE TABLE IF NOT EXISTS `IMAGE_OBJECTS` (
  `ID` BIGINT NOT NULL AUTO_INCREMENT,
  `IMAGE_ANALYSIS_ID` BIGINT NOT NULL,
  `OBJECT_NAME` VARCHAR(1024) NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `IDX_IMAGE_OBJECTS_ANALYSIS` (`IMAGE_ANALYSIS_ID`),
  KEY `IDX_IMAGE_OBJECTS_OBJECT_NAME` (`OBJECT_NAME`(768)),
  KEY `IDX_IMAGE_OBJECTS_ANALYSIS_NAME` (`IMAGE_ANALYSIS_ID`, `OBJECT_NAME`(191)),
  FULLTEXT KEY `FT_OBJECT_NAME` (`OBJECT_NAME`),
  CONSTRAINT `FK_IMAGE_OBJECTS_ANALYSIS` FOREIGN KEY (`IMAGE_ANALYSIS_ID`) REFERENCES `IMAGE_ANALYSIS` (`ID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
'@ | Out-Null

    Invoke-Sql @'
CREATE TABLE IF NOT EXISTS `IMAGE_READABLE_TEXT` (
  `ID` BIGINT NOT NULL AUTO_INCREMENT,
  `IMAGE_ANALYSIS_ID` BIGINT NOT NULL,
  `TEXT_LINE` TEXT NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `IDX_IMAGE_TEXT_ANALYSIS` (`IMAGE_ANALYSIS_ID`),
  FULLTEXT KEY `FT_IMAGE_READABLE_TEXT` (`TEXT_LINE`),
  CONSTRAINT `FK_IMAGE_TEXT_ANALYSIS` FOREIGN KEY (`IMAGE_ANALYSIS_ID`) REFERENCES `IMAGE_ANALYSIS` (`ID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
'@ | Out-Null

    $transaction = $connection.BeginTransaction()
    $migrationError = $null
    try {
        $insertAnalysis = $connection.CreateCommand()
        $insertAnalysis.Transaction = $transaction
        $insertAnalysis.CommandTimeout = 0
        $insertAnalysis.CommandText = @'
INSERT INTO `IMAGE_ANALYSIS` (
  `ID`, `MEDIA_ID`, `ANIMAL_COUNT`, `WEATHER_DESCRIPTION`, `OBJECT_COUNT`,
  `CREATION_DATE`, `LOCATION_DESCRIPTION`, `FORMAT_OF_DOCUMENT_OR_IMAGE`,
  `DETAILED_IMAGE_ANALYSIS`, `TEMPERATUR_MIN_CELSIUS`, `TEMPERATUR_MAX_CELSIUS`,
  `MODEL_TEMPERATURE`, `CREATED_IN_MILLISECONDS`, `MODEL`, `PERSON_COUNT`, `CREATED_AT`
)
SELECT a.id, s.NEW_MEDIA_ID, a.animal_count, a.weather_description, a.object_count,
       a.creation_date, a.location_description, a.format_of_document_or_image,
       a.detailed_image_analysis, a.temperatur_min_celsius, a.temperatur_max_celsius,
       a.model_temperature, a.created_in_milliseconds, a.model, a.person_count, a.created_at
FROM `__SRC__`.`image_analysis` a
JOIN `_IMAGE_ANALYSIS_SELECT` s ON s.OLD_ANALYSIS_ID = a.id
'@
        $insertAnalysis.CommandText = Expand-Sql $insertAnalysis.CommandText
        $analysisInserted = $insertAnalysis.ExecuteNonQuery()

        $insertObjects = $connection.CreateCommand()
        $insertObjects.Transaction = $transaction
        $insertObjects.CommandTimeout = 0
        $insertObjects.CommandText = @'
INSERT INTO `IMAGE_OBJECTS` (`ID`, `IMAGE_ANALYSIS_ID`, `OBJECT_NAME`)
SELECT c.id, c.image_analysis_id, c.object_name
FROM `__SRC__`.`image_objects` c
JOIN `_IMAGE_ANALYSIS_SELECT` s ON s.OLD_ANALYSIS_ID = c.image_analysis_id
'@
        $insertObjects.CommandText = Expand-Sql $insertObjects.CommandText
        $objectsInserted = $insertObjects.ExecuteNonQuery()

        $insertText = $connection.CreateCommand()
        $insertText.Transaction = $transaction
        $insertText.CommandTimeout = 0
        $insertText.CommandText = @'
INSERT INTO `IMAGE_READABLE_TEXT` (`ID`, `IMAGE_ANALYSIS_ID`, `TEXT_LINE`)
SELECT c.id, c.image_analysis_id, c.text_line
FROM `__SRC__`.`image_readable_text` c
JOIN `_IMAGE_ANALYSIS_SELECT` s ON s.OLD_ANALYSIS_ID = c.image_analysis_id
'@
        $insertText.CommandText = Expand-Sql $insertText.CommandText
        $textInserted = $insertText.ExecuteNonQuery()

        $transaction.Commit()
    }
    catch {
        $migrationError = $_
        if ($connection.State -eq [System.Data.ConnectionState]::Open) {
            try {
                $transaction.Rollback()
            }
            catch {
            }
        }
    }

    if ($connection.State -ne [System.Data.ConnectionState]::Open) {
        $connection.Open()
    }

    $analysisCount = [long](Get-Scalar 'SELECT COUNT(*) FROM `IMAGE_ANALYSIS`')
    $objectCount = [long](Get-Scalar 'SELECT COUNT(*) FROM `IMAGE_OBJECTS`')
    $textCount = [long](Get-Scalar 'SELECT COUNT(*) FROM `IMAGE_READABLE_TEXT`')

    if ($analysisCount -eq $selectedTotal -and $objectCount -eq $objectSelected -and $textCount -eq $textSelected) {
        if ($null -ne $migrationError) {
            Write-Warning "Beim Commit wurde ein Fehler gemeldet, die Daten sind aber vollstaendig vorhanden: $($migrationError.Exception.Message)"
        }
        $analysisInserted = $analysisCount
        $objectsInserted = $objectCount
        $textInserted = $textCount
    }
    else {
        if ($null -ne $migrationError) {
            throw $migrationError
        }
        throw "Verifikation fehlgeschlagen: IMAGE_ANALYSIS=$analysisCount/$selectedTotal, IMAGE_OBJECTS=$objectCount/$objectSelected, IMAGE_READABLE_TEXT=$textCount/$textSelected"
    }

    Write-Host ''
    Write-Host 'Migration abgeschlossen.'
    Write-Host "  IMAGE_ANALYSIS:        $analysisInserted"
    Write-Host "  IMAGE_OBJECTS:         $objectsInserted"
    Write-Host "  IMAGE_READABLE_TEXT:   $textInserted"
}
finally {
    $connection.Close()
}
