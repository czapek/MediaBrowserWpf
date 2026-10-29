[CmdletBinding()]
param(
    [string]$HostName = '192.168.2.129',
    [int]$Port = 3306,
    [string]$Database = 'mediabrowser-new',
    [string]$User = 'mediabrowser-new',
    [string]$PasswordEnv = 'MEDIABROWSER_DB_PASSWORD',
    [string]$MySqlConnectorPath,
    [switch]$Preview
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
$builder.Database = $Database
$builder.UserID = $User
$builder.Password = $password
$builder.ConnectionTimeout = 15

$connection = [MySqlConnector.MySqlConnection]::new($builder.ConnectionString)
$connection.Open()

$indexes = @(
    @{
        Table = 'IMAGE_ANALYSIS'
        Name = 'IDX_IMAGE_ANALYSIS_MODEL'
        Definition = 'KEY `IDX_IMAGE_ANALYSIS_MODEL` (`MODEL`)'
    },
    @{
        Table = 'IMAGE_ANALYSIS'
        Name = 'IDX_IMAGE_ANALYSIS_CREATED_AT'
        Definition = 'KEY `IDX_IMAGE_ANALYSIS_CREATED_AT` (`CREATED_AT`)'
    },
    @{
        Table = 'IMAGE_ANALYSIS'
        Name = 'FT_WEATHER_DESCRIPTION'
        Definition = 'FULLTEXT KEY `FT_WEATHER_DESCRIPTION` (`WEATHER_DESCRIPTION`)'
    },
    @{
        Table = 'IMAGE_ANALYSIS'
        Name = 'FT_LOCATION_DESCRIPTION'
        Definition = 'FULLTEXT KEY `FT_LOCATION_DESCRIPTION` (`LOCATION_DESCRIPTION`)'
    },
    @{
        Table = 'IMAGE_ANALYSIS'
        Name = 'FT_FORMAT_OF_DOCUMENT_OR_IMAGE'
        Definition = 'FULLTEXT KEY `FT_FORMAT_OF_DOCUMENT_OR_IMAGE` (`FORMAT_OF_DOCUMENT_OR_IMAGE`)'
    },
    @{
        Table = 'IMAGE_OBJECTS'
        Name = 'IDX_IMAGE_OBJECTS_ANALYSIS_NAME'
        Definition = 'KEY `IDX_IMAGE_OBJECTS_ANALYSIS_NAME` (`IMAGE_ANALYSIS_ID`, `OBJECT_NAME`(191))'
    },
    @{
        Table = 'IMAGE_OBJECTS'
        Name = 'FT_OBJECT_NAME'
        Definition = 'FULLTEXT KEY `FT_OBJECT_NAME` (`OBJECT_NAME`)'
    }
)

function Test-IndexExists([string]$tableName, [string]$indexName) {
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT COUNT(*)
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = @schema
  AND TABLE_NAME = @table
  AND INDEX_NAME = @index
"@
    [void]$command.Parameters.AddWithValue('@schema', $Database)
    [void]$command.Parameters.AddWithValue('@table', $tableName)
    [void]$command.Parameters.AddWithValue('@index', $indexName)
    $command.CommandTimeout = 0
    return ([int]$command.ExecuteScalar() -gt 0)
}

function Test-TableExists([string]$tableName) {
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table"
    [void]$command.Parameters.AddWithValue('@schema', $Database)
    [void]$command.Parameters.AddWithValue('@table', $tableName)
    $command.CommandTimeout = 0
    return ([int]$command.ExecuteScalar() -gt 0)
}

try {
    foreach ($tableName in @('IMAGE_ANALYSIS', 'IMAGE_OBJECTS')) {
        if (-not (Test-TableExists $tableName)) {
            throw "Tabelle '$tableName' existiert nicht in '$Database'."
        }
    }

    $missing = @()

    foreach ($index in $indexes) {
        if (Test-IndexExists $index.Table $index.Name) {
            Write-Host "Vorhanden: $($index.Table).$($index.Name)"
            continue
        }

        $missing += $index
        Write-Host "Fehlt:     $($index.Table).$($index.Name)"
    }

    if ($missing.Count -eq 0) {
        Write-Host 'Alle Indizes sind bereits vorhanden.'
        return
    }

    if ($Preview) {
        Write-Host ''
        Write-Host 'Vorschau:'
        foreach ($index in $missing) {
            Write-Host "ALTER TABLE ``$($index.Table)`` ADD $($index.Definition), ALGORITHM=INPLACE"
        }
        return
    }

    foreach ($index in $missing) {
        $statement = "ALTER TABLE ``$($index.Table)`` ADD $($index.Definition), ALGORITHM=INPLACE"
        Write-Host "Altere: $($index.Table).$($index.Name) ..."
        $command = $connection.CreateCommand()
        $command.CommandText = $statement
        $command.CommandTimeout = 0
        [void]$command.ExecuteNonQuery()
        Write-Host "Angelegt: $($index.Table).$($index.Name)"
    }

    Write-Host 'Indizes angelegt.'
}
finally {
    $connection.Close()
}
