[CmdletBinding()]
param(
    [string]$HostName = '192.168.2.129',
    [int]$Port = 3306,
    [string]$Database = 'mediabrowser-new',
    [string]$User = 'mediabrowser-new',
    [string]$PasswordEnv = 'MEDIABROWSER_DB_PASSWORD',
    [string]$MySqlConnectorPath,
    [switch]$ListOnly
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

$connection = [MySqlConnector.MySqlConnection]::new($builder.ConnectionString)
$connection.Open()

try {
    $selectCommand = $connection.CreateCommand()
    $selectCommand.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'"
    [void]$selectCommand.Parameters.AddWithValue('@schema', $Database)

    $reader = $selectCommand.ExecuteReader()
    $tables = @()

    while ($reader.Read()) {
        $tables += $reader.GetString(0)
    }

    $reader.Close()

    if ($ListOnly) {
        foreach ($table in $tables) {
            Write-Host "Tabelle: $table"
        }
        Write-Host "Gefundene Tabellen: $($tables.Count)"
        return
    }

    $toggleCommand = $connection.CreateCommand()
    $toggleCommand.CommandText = 'SET FOREIGN_KEY_CHECKS = 0'
    [void]$toggleCommand.ExecuteNonQuery()

    foreach ($table in $tables) {
        $quotedTable = $table.Replace('`', '``')
        $dropCommand = $connection.CreateCommand()
        $dropCommand.CommandText = "DROP TABLE IF EXISTS ``$quotedTable``"
        [void]$dropCommand.ExecuteNonQuery()
        Write-Host "Geloescht: $table"
    }

    $toggleCommand.CommandText = 'SET FOREIGN_KEY_CHECKS = 1'
    [void]$toggleCommand.ExecuteNonQuery()

    Write-Host "Fertig. Geloeschte Tabellen: $($tables.Count)"
}
finally {
    $connection.Close()
}
